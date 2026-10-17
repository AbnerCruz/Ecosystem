using System.Text.Json;
using AgentRuntime;
using EcosystemAi.Product;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class ProductRunAuditTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "product-audit-" + Guid.NewGuid().ToString("N"));
        public string Journal => Path.Combine(Root, "runs");
        public LocalProjectStore Store { get; }
        public ProjectEntry Project { get; }
        public SessionEntry Session { get; }
        public const string RunId = "audit-run";
        public Fixture()
        {
            var workspace = Path.Combine(Root, "workspace"); Directory.CreateDirectory(workspace);
            Store = new LocalProjectStore(Path.Combine(Root, "catalog"));
            Project = Store.CreateProject("P", workspace); Session = Store.CreateSession(Project.Id, "S");
            Store.AppendRun(Project.Id, Session.Id, new RunReceipt(RunId, "succeeded", 3, "USD", true,
                "PRIVATE-CATALOG-VERIFICATION", DateTimeOffset.UtcNow));
        }
        public async Task Seed(bool otherContext = false, int extraEvents = 0)
        {
            var context = ContextPath.Create(new(ContextLevel.Ecosystem, "ecosystem"),
                new(ContextLevel.Product, "ecosystem-ai"), new(ContextLevel.Project, otherContext ? "other" : Project.Id)).ToString();
            var log = new LocalRunEventLog(Journal); long sequence = 0;
            async Task Add(EventKind kind, Money? cost = null) => await log.AppendAsync(new RuntimeEvent(
                RunId, ++sequence, DateTimeOffset.UtcNow, kind, new AgentIdentity("PRIVATE-AGENT", "PRIVATE-NAME", "executor"),
                "PRIVATE-TASK", context, Tool: "PRIVATE-TOOL", Model: "PRIVATE-MODEL", Cost: cost,
                Result: "PRIVATE-RESULT", Verification: "PRIVATE-VERIFICATION", ApprovedBy: "PRIVATE-APPROVAL",
                Payload: "PRIVATE-PAYLOAD", Data: new Dictionary<string, string> { ["private"] = "PRIVATE-DATA" }), CancellationToken.None);
            await Add(EventKind.RunCreated);
            for (var i = 0; i < extraEvents; i++) await Add(EventKind.ModelRequested);
            await Add(EventKind.ModelResponded, new Money(3, "USD")); await Add(EventKind.ToolCalled);
            await Add(EventKind.ToolResult); await Add(EventKind.VerificationPassed); await Add(EventKind.RunSucceeded);
        }
        public Task<ProductRunAuditResult> Read(CancellationToken token = default) => ProductRunAudit.ReadAsync(
            Store.Read(), Project.Id, Session.Id, RunId, Journal, token);
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact]
    public async Task Replay_reads_canonical_events_without_exposing_free_text_or_writing_files()
    {
        using var f = new Fixture(); await f.Seed();
        var files = Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        var audit = await f.Read();
        Assert.Equal(RunAuditAvailability.Available, audit.Availability); Assert.Equal(RunStatus.Succeeded, audit.Status);
        Assert.True(audit.Verified); Assert.Equal(1, audit.Steps); Assert.Equal(1, audit.ToolCalls);
        Assert.Equal(new Money(3, "USD"), audit.RecordedCost); Assert.Equal(6, audit.TotalEvents);
        Assert.Equal(EventKind.RunSucceeded, audit.Events![^1].Kind);
        Assert.DoesNotContain("PRIVATE-", JsonSerializer.Serialize(audit));
        Assert.Equal(files.Count, Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).Length);
        foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }
    [Fact]
    public async Task Missing_journal_does_not_create_a_directory_or_invent_verified_state()
    {
        using var f = new Fixture(); var audit = await f.Read();
        Assert.Equal(RunAuditAvailability.Missing, audit.Availability); Assert.False(Directory.Exists(f.Journal));
        Assert.Null(audit.Status); Assert.False(audit.Verified);
        Directory.CreateDirectory(f.Journal);
        Assert.Equal(RunAuditAvailability.Missing, (await f.Read()).Availability); Assert.Empty(Directory.GetFiles(f.Journal));
    }
    [Fact]
    public async Task Corrupt_journal_fails_closed_without_exposing_contents_or_repairing()
    {
        using var f = new Fixture(); await f.Seed(); var path = Directory.GetFiles(f.Journal, "*.json").Single();
        File.WriteAllText(path, "PRIVATE-CORRUPTION"); var audit = await f.Read();
        Assert.Equal(RunAuditAvailability.Unavailable, audit.Availability);
        Assert.DoesNotContain("PRIVATE-", JsonSerializer.Serialize(audit)); Assert.Equal("PRIVATE-CORRUPTION", File.ReadAllText(path));
    }
    [Theory]
    [InlineData("status")]
    [InlineData("verified")]
    [InlineData("cost")]
    [InlineData("currency")]
    public async Task Receipt_divergence_is_signaled_while_state_comes_from_replay(string field)
    {
        using var f = new Fixture(); await f.Seed(); var catalog = f.Store.Read();
        var project = catalog.Projects.Single(); var session = project.Sessions.Single(); var old = session.Runs.Single();
        var changed = field switch { "status" => old with { Status = "cancelled" }, "verified" => old with { Verified = false },
            "cost" => old with { CostMinor = 4 }, _ => old with { Currency = "BRL" } };
        var snapshot = catalog with { Projects = [project with { Sessions = [session with { Runs = [changed] }] }] };
        var audit = await ProductRunAudit.ReadAsync(snapshot, project.Id, session.Id, Fixture.RunId, f.Journal);
        Assert.Equal(RunAuditAvailability.ReceiptMismatch, audit.Availability); Assert.Equal(RunStatus.Succeeded, audit.Status);
        Assert.Equal(new Money(3, "USD"), audit.RecordedCost);
    }
    [Fact]
    public async Task Another_session_cannot_read_a_run_by_guessing_its_id()
    {
        using var f = new Fixture(); await f.Seed(); var other = f.Store.CreateSession(f.Project.Id, "Other");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ProductRunAudit.ReadAsync(f.Store.Read(), f.Project.Id, other.Id, Fixture.RunId, f.Journal));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ProductRunAudit.ReadAsync(f.Store.Read(), "unknown-project", f.Session.Id, Fixture.RunId, f.Journal));
    }
    [Fact]
    public async Task Ambiguous_receipt_association_is_refused()
    {
        using var f = new Fixture(); await f.Seed(); var other = f.Store.CreateSession(f.Project.Id, "Other");
        f.Store.AppendRun(f.Project.Id, other.Id, f.Store.Read().Projects.Single().Sessions.First().Runs.Single());
        Assert.Equal(RunAuditAvailability.Unavailable, (await f.Read()).Availability);
    }
    [Fact]
    public async Task Events_from_another_project_are_refused()
    {
        using var f = new Fixture(); await f.Seed(otherContext: true);
        Assert.Equal(RunAuditAvailability.Unavailable, (await f.Read()).Availability);
    }
    [Fact]
    public async Task Journal_inside_agent_workspace_is_not_opened()
    {
        using var f = new Fixture(); var inside = Path.Combine(f.Project.WorkspaceDirectory, "runs");
        var audit = await ProductRunAudit.ReadAsync(f.Store.Read(), f.Project.Id, f.Session.Id, Fixture.RunId, inside);
        Assert.Equal(RunAuditAvailability.Unavailable, audit.Availability); Assert.False(Directory.Exists(inside));
    }
    [Fact]
    public async Task Cancellation_is_propagated_without_reading_or_creating_data()
    {
        using var f = new Fixture(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Read(cancellation.Token)); Assert.False(Directory.Exists(f.Journal));
    }
    [Fact]
    public async Task Visible_events_are_bounded_without_changing_total_or_order()
    {
        using var f = new Fixture(); await f.Seed(extraEvents: 105); var audit = await f.Read();
        Assert.Equal(111, audit.TotalEvents); Assert.Equal(ProductRunAudit.MaxVisibleEvents, audit.Events!.Count);
        Assert.Equal(12, audit.Events[0].Sequence); Assert.Equal(111, audit.Events[^1].Sequence);
    }
}
