using System.Text.Json;
using AgentRuntime;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class R4JournalVisualReportTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "r4-journal-html-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Exportacao_opcional_usa_replay_valido_sem_expor_payload_nem_caminho_de_arquivos()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        var export = Path.Combine(root, "exports");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(export);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var runId = "cli-run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(project.Id, session.Id, new RunReceipt(
                runId, "succeeded", 20, "USD", true, "check", DateTimeOffset.UtcNow));
            var savedRevision = store.Read().Revision;
            var actor = new AgentIdentity("<img src=x onerror=1>", "Assistente", "executor");
            var at = DateTimeOffset.UtcNow;
            var log = new LocalRunEventLog(journal);
            var artifacts = JsonSerializer.Serialize(new[]
            {
                new ArtifactRef("file:docs/report", "file", "segredos/<svg onload=1>.md")
            });
            var events = new[]
            {
                new RuntimeEvent(runId, 1, at, EventKind.RunCreated, actor, "cli-task", "project"),
                new RuntimeEvent(runId, 2, at, EventKind.ModelResponded, actor, "cli-task", "project",
                    Model: "modelo<t>", Cost: new Money(20, "USD"), Payload: "private-message-DO-NOT-EXPORT"),
                new RuntimeEvent(runId, 3, at, EventKind.ToolCalled, actor, "cli-task", "project",
                    Tool: "files.write", ApprovedBy: "policy"),
                new RuntimeEvent(runId, 4, at, EventKind.ToolResult, actor, "cli-task", "project",
                    Tool: "files.write", Result: "SECRET-TOOL-PAYLOAD",
                    Data: new Dictionary<string, string> { ["artifacts"] = artifacts }),
                new RuntimeEvent(runId, 5, at, EventKind.VerificationPassed, actor, "cli-task", "project",
                    Verification: "verified-internal-secret"),
                new RuntimeEvent(runId, 6, at, EventKind.RunSucceeded, actor, "cli-task", "project", Result: "succeeded")
            };
            foreach (var item in events) await log.AppendAsync(item, CancellationToken.None);

            var output = Path.Combine(export, "auditoria.html");
            await CliHtmlSnapshot.ExportAsync(catalog, output, journal);
            var html = File.ReadAllText(output);
            Assert.Contains("Auditoria do Runtime", html);
            Assert.Contains("passos: 1", html);
            Assert.Contains("chamadas de ferramenta: 1", html);
            Assert.Contains("Artefatos referenciados", html);
            Assert.Contains("&lt;svg onload=1&gt;.md", html);
            Assert.Contains("&lt;img src=x onerror=1&gt;", html);
            Assert.DoesNotContain("private-message-DO-NOT-EXPORT", html);
            Assert.DoesNotContain("SECRET-TOOL-PAYLOAD", html);
            Assert.DoesNotContain("verified-internal-secret", html);
            Assert.DoesNotContain("segredos/", html);
            Assert.DoesNotContain(workspace, html);
            Assert.DoesNotContain("<svg", html);
            Assert.Equal(savedRevision, store.Read().Revision);

            var noJournal = Path.Combine(export, "normal.html");
            CliHtmlSnapshot.Export(catalog, noJournal);
            Assert.DoesNotContain("Auditoria do Runtime", File.ReadAllText(noJournal));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Exportacao_rejeita_journal_no_workspace_e_arquivo_corrompido()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        var export = Path.Combine(root, "exports");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(export);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var runId = "cli-run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt(runId, "failed", 0, "USD", false, null, DateTimeOffset.UtcNow));
            var inside = Path.Combine(workspace, "runs");
            Directory.CreateDirectory(inside);
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await CliHtmlSnapshot.ExportAsync(catalog, Path.Combine(export, "inside.html"), inside));
            Assert.False(File.Exists(Path.Combine(export, "inside.html")));

            var log = new LocalRunEventLog(journal);
            var agent = new AgentIdentity("agent", "Agent", "worker");
            var at = DateTimeOffset.UtcNow;
            await log.AppendAsync(new RuntimeEvent(
                runId, 1, at, EventKind.RunCreated, agent, "cli-task", "project"), CancellationToken.None);
            var path = Directory.GetFiles(journal, "*.json").Single();
            File.WriteAllText(path, "{bad-json");
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await CliHtmlSnapshot.ExportAsync(catalog, Path.Combine(export, "corrupt.html"), journal));
            Assert.False(File.Exists(Path.Combine(export, "corrupt.html")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Journal_ausente_nao_inventa_evidencia_e_modo_cli_valida_opcoes()
    {
        var root = NewRoot();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        var export = Path.Combine(root, "exports");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(journal);
        Directory.CreateDirectory(export);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt("missing-run", "failed", 0, "USD", false, null, DateTimeOffset.UtcNow));
            var output = Path.Combine(export, "report.html");
            Assert.Equal(0, await EcosystemAiCli.RunAsync([
                "--export-html", "--catalog", catalog, "--output", output, "--journal", journal]));
            var html = File.ReadAllText(output);
            Assert.Contains("Sem journal disponível", html);
            Assert.DoesNotContain("Auditoria do Runtime — ", html);

            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--export-html", "--catalog", catalog, "--output", Path.Combine(export, "invalid.html"),
                "--journal", journal, "--allow-create"]));
        }
        finally { Directory.Delete(root, true); }
    }
}
