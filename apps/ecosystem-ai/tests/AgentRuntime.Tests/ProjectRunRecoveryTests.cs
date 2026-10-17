using AgentRuntime;
using EcosystemAi.Product;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class ProjectRunRecoveryTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "run-recovery-" + Guid.NewGuid().ToString("N"));
        public string Journal => Path.Combine(Root, "runs");
        public LocalProjectStore Store { get; }
        public ProjectEntry Project { get; }
        public SessionEntry Session { get; }
        public Fixture()
        {
            var workspace = Path.Combine(Root, "workspace"); Directory.CreateDirectory(workspace);
            Store = new LocalProjectStore(Path.Combine(Root, "catalog")); Project = Store.CreateProject("P", workspace);
            Session = Store.CreateSession(Project.Id, "S");
        }
        public async Task Seed(string id, string? projectId = null, bool succeeded = false)
        {
            var context = ContextPath.Create(new(ContextLevel.Ecosystem, "ecosystem"),
                new(ContextLevel.Product, "ecosystem-ai"), new(ContextLevel.Project, projectId ?? Project.Id)).ToString();
            var log = new LocalRunEventLog(Journal);
            long sequence = 0;
            async Task Add(EventKind kind, Money? cost = null) => await log.AppendAsync(new RuntimeEvent(id, ++sequence,
                DateTimeOffset.UnixEpoch.AddSeconds(sequence), kind, Rig.Agent, "PRIVATE-TASK", context,
                Model: "PRIVATE-MODEL", Cost: cost, Payload: "PRIVATE-PROMPT", Verification: "PRIVATE-VERIFICATION",
                Result: "PRIVATE-RESULT"), CancellationToken.None);
            await Add(EventKind.RunCreated); await Add(EventKind.ModelRequested);
            if (succeeded)
            {
                await Add(EventKind.ModelResponded, new Money(2, "USD"));
                await Add(EventKind.VerificationPassed); await Add(EventKind.RunSucceeded);
            }
        }
        public ProjectRunRecoveryResult Read() => ProductRunRecovery.Read(Store.Read(), Project.Id, Journal);
        public void Dispose() => Directory.Delete(Root, true);
    }

    [Fact]
    public async Task Orphaned_run_is_discovered_after_reopen_without_guessing_session_or_writing_data()
    {
        using var f = new Fixture(); await f.Seed("incomplete"); await f.Seed("finished", succeeded: true);
        var files = Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        var snapshot = new LocalProjectStore(Path.Combine(f.Root, "catalog")).Read();
        var recovery = ProductRunRecovery.Read(snapshot, f.Project.Id, f.Journal);
        Assert.Equal(2, recovery.Runs.Count); Assert.False(recovery.Truncated); Assert.False(recovery.JournalUnavailable);
        var unfinished = Assert.Single(recovery.Runs, r => r.RunId == "incomplete");
        Assert.Equal(RunStatus.Running, unfinished.Status); Assert.False(unfinished.Verified); Assert.Null(unfinished.RecordedCost);
        var finished = Assert.Single(recovery.Runs, r => r.RunId == "finished");
        Assert.Equal(RunStatus.Succeeded, finished.Status); Assert.True(finished.Verified); Assert.Equal(new Money(2, "USD"), finished.RecordedCost);
        Assert.DoesNotContain("PRIVATE-", System.Text.Json.JsonSerializer.Serialize(recovery));
        Assert.Empty(snapshot.Projects.Single().Sessions.Single().Runs);
        Assert.Equal(files.Count, Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).Length);
        foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }
    [Fact]
    public async Task Project_isolation_and_receipts_in_any_session_exclude_runs()
    {
        using var f = new Fixture(); await f.Seed("visible"); await f.Seed("other", "another-project"); await f.Seed("receipted");
        var second = f.Store.CreateSession(f.Project.Id, "Second");
        f.Store.AppendRun(f.Project.Id, second.Id, new RunReceipt("receipted", "cancelled", 0, "USD", false, null, DateTimeOffset.UtcNow));
        Assert.Equal("visible", Assert.Single(f.Read().Runs).RunId);
        Assert.Throws<KeyNotFoundException>(() => ProductRunRecovery.Read(f.Store.Read(), "unknown", f.Journal));
    }
    [Fact]
    public void Missing_journal_remains_missing()
    {
        using var f = new Fixture(); Assert.Empty(f.Read().Runs); Assert.False(Directory.Exists(f.Journal));
    }
    [Fact]
    public async Task Corrupt_or_renamed_file_is_reported_without_hiding_healthy_runs_or_repairing()
    {
        using var f = new Fixture(); await f.Seed("valid");
        File.WriteAllText(Path.Combine(f.Journal, "broken.json"), "PRIVATE-CORRUPTION");
        File.Copy(Directory.GetFiles(f.Journal, "*.json").Single(p => !p.EndsWith("broken.json", StringComparison.Ordinal)), Path.Combine(f.Journal, "wrong-name.json"));
        var recovery = f.Read(); Assert.Equal("valid", Assert.Single(recovery.Runs).RunId); Assert.Equal(2, recovery.UnavailableFiles);
        Assert.Equal("PRIVATE-CORRUPTION", File.ReadAllText(Path.Combine(f.Journal, "broken.json")));
    }
    [Fact]
    public async Task File_and_byte_limits_are_explicit_and_do_not_claim_a_complete_inventory()
    {
        using var f = new Fixture(); await f.Seed("one"); await f.Seed("two");
        var journal = new LocalRunEventLog(f.Journal);
        var files = journal.InspectRuns(maxFiles: 1); Assert.True(files.Truncated); Assert.Single(files.Runs);
        var bytes = journal.InspectRuns(maxTotalBytes: 1); Assert.True(bytes.Truncated); Assert.Empty(bytes.Runs);
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.InspectRuns(maxFiles: 0));
    }
    [Fact]
    public async Task Symlink_files_are_not_followed()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f = new Fixture(); await f.Seed("valid");
        var original = Directory.GetFiles(f.Journal, "*.json").Single();
        var outside = Path.Combine(f.Root, "outside.json"); File.Move(original, outside); File.CreateSymbolicLink(original, outside);
        var recovery = f.Read(); Assert.Empty(recovery.Runs); Assert.Equal(1, recovery.UnavailableFiles);
        Assert.True(File.Exists(outside));
    }
    [Fact]
    public void Journal_inside_workspace_is_not_created_or_opened()
    {
        using var f = new Fixture(); var inside = Path.Combine(f.Project.WorkspaceDirectory, "journal");
        var recovery = ProductRunRecovery.Read(f.Store.Read(), f.Project.Id, inside);
        Assert.True(recovery.JournalUnavailable); Assert.False(Directory.Exists(inside));
    }
    [Fact]
    public async Task Cancelled_inventory_propagates_cancellation_without_mutation()
    {
        using var f = new Fixture(); await f.Seed("valid"); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new LocalRunEventLog(f.Journal).InspectRuns(cancellationToken: cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => ProductRunRecovery.Read(f.Store.Read(), f.Project.Id, f.Journal, cancellation.Token));
    }
}
