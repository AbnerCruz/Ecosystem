using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AgentRuntime;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class R4WebAuditTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-web-audit-" + Guid.NewGuid().ToString("N"));

    private static int Port()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        try { return ((IPEndPoint)socket.LocalEndpoint).Port; }
        finally { socket.Stop(); }
    }

    private static async Task Seed(string journal, string id, string filename)
    {
        var agent = new AgentIdentity("<svg onload=1>", "Agente", "executor");
        var now = DateTimeOffset.UtcNow;
        var log = new LocalRunEventLog(journal);
        var items = JsonSerializer.Serialize(new[] {
            new ArtifactRef("file:report", "file", "private/" + filename)
        });
        var events = new[]
        {
            new RuntimeEvent(id, 1, now, EventKind.RunCreated, agent, "<img onerror=1>", "project"),
            new RuntimeEvent(id, 2, now, EventKind.ModelResponded, agent, "<img onerror=1>", "project",
                Model: "model<script>", Cost: new Money(8, "USD"), Payload: "NEVER-EXPOSE-SECRET"),
            new RuntimeEvent(id, 3, now, EventKind.ToolCalled, agent, "<img onerror=1>", "project",
                Tool: "files.write", ApprovedBy: "policy"),
            new RuntimeEvent(id, 4, now, EventKind.ToolResult, agent, "<img onerror=1>", "project",
                Tool: "files.write", Result: "NEVER-EXPOSE-TOOL",
                Data: new Dictionary<string, string> { ["artifacts"] = items }),
            new RuntimeEvent(id, 5, now, EventKind.VerificationPassed, agent, "<img onerror=1>", "project",
                Verification: "PRIVATE-VERIFICATION"),
            new RuntimeEvent(id, 6, now, EventKind.RunSucceeded, agent, "<img onerror=1>", "project", Result: "succeeded")
        };
        foreach (var item in events) await log.AppendAsync(item, CancellationToken.None);
    }

    [Fact]
    public async Task Painel_consulta_journal_replay_e_exibe_agentes_tarefas_sem_payload()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var id = "run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt(id, "succeeded", 8, "USD", true, "valid", DateTimeOffset.UtcNow));
            await Seed(journal, id, "<script>.md");
            var revision = store.Read().Revision;
            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port, journal);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var html = await http.GetStringAsync("/");
                Assert.Contains("Atividade verificada de agentes", html);
                Assert.Contains("Tarefas auditadas", html);
                Assert.Contains("Ver auditoria da tarefa", html);
                Assert.Contains("&lt;svg onload=1&gt;", html);
                Assert.Contains("&lt;img onerror=1&gt;", html);
                Assert.Contains("model&lt;script&gt;", html);
                Assert.Contains("&lt;script&gt;.md", html);
                Assert.Contains("Verificação:", html);
                Assert.Contains("8 USD", html);
                Assert.DoesNotContain("NEVER-EXPOSE-SECRET", html);
                Assert.DoesNotContain("NEVER-EXPOSE-TOOL", html);
                Assert.DoesNotContain("PRIVATE-VERIFICATION", html);
                Assert.DoesNotContain(workspace, html);
                Assert.DoesNotContain("private/", html);
                Assert.DoesNotContain("<script>.md", html);
                Assert.Contains($"id='r-{id}'", html);
                Assert.Equal(revision, store.Read().Revision);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Sem_journal_nao_inventa_evidencia_e_journal_corrupto_falha_fechado()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        var journal = Path.Combine(root, "journal");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(journal);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var id = "run-" + Guid.NewGuid().ToString("N");
            store.AppendRun(project.Id, session.Id,
                new RunReceipt(id, "succeeded", 8, "USD", true, "receipt", DateTimeOffset.UtcNow));
            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port, journal);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var html = await http.GetStringAsync("/");
                Assert.Contains("Sem evidência de journal para este run", html);
                Assert.Contains("Sem evidência no journal", html);
                Assert.DoesNotContain("Ver auditoria da tarefa", html);

                await Seed(journal, id, "resultado.md");
                var path = Directory.GetFiles(journal, "*.json").Single();
                File.WriteAllText(path, "{broken");
                var error = await http.GetAsync("/");
                Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
                var body = await error.Content.ReadAsStringAsync();
                Assert.Contains("Catálogo indisponível", body);
                Assert.DoesNotContain("PRIVATE-", body);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Journal_dentro_do_workspace_ou_modo_de_execucao_nao_habilitam_painel()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            store.CreateProject("P", workspace);
            var inside = Path.Combine(workspace, "runs");
            Directory.CreateDirectory(inside);
            Assert.Throws<ArgumentException>(() => CliWebUi.CreateApp(catalog, Port(), inside));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--web-ui", "--catalog", catalog, "--journal", inside, "--model", "modelo"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync([
                "--journal", inside, "--catalog", catalog, "--port", "8765"]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Relatorio_sem_journal_mantem_comportamento_original()
    {
        var model = new ProjectCatalog(1, []);
        var original = CliWebUiHtml.Render(model, new string('A', 64));
        Assert.DoesNotContain("Auditoria ativa", original);
        Assert.DoesNotContain("Atividade verificada de agentes", original);
        Assert.Contains("Novo projeto", original);
        Assert.Contains("name='csrf'", original);
    }
}
