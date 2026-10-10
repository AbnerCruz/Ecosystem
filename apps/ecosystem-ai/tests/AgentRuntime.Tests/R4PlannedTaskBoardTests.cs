using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4PlannedTaskBoardTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-planned-" + Guid.NewGuid().ToString("N"));

    private static int Port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static (LocalProjectStore Store, ProjectEntry Project, SessionEntry Session,
        LocalAgentProfile Agent) Seed(string root)
    {
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);
        var store = new LocalProjectStore(Path.Combine(root, "catalog"));
        var project = store.CreateProject("P", workspace);
        var session = store.CreateSession(project.Id, "S");
        var agent = new LocalAgentRosterStore(Path.Combine(root, "catalog"))
            .CreateAgent(project.Id, "<script>Eng</script>", "Revisar C#");
        return (store, project, session, agent);
    }

    private static WebTaskSettings Settings() => new(
        "http://127.0.0.1:59999/v1/chat/completions", "mock", 20, 5, 1, 2,
        80, 4);

    [Fact]
    public async Task HTTP_planeja_sem_custo_e_executa_por_gesto_com_recibo_do_store()
    {
        var root = Root();
        try
        {
            var (store, project, session, agent) = Seed(root);
            var cat = Path.Combine(root, "catalog");
            var original = Path.Combine(root, "workspace", "important.md");
            File.WriteAllText(original, "inalterado");
            var calls = new List<string[]>();
            var runner = new CliWebTaskRunner(cat, null, Settings(), args =>
            {
                calls.Add(args);
                var goal = args[Array.IndexOf(args, "--goal") + 1];
                store.AppendTurn(project.Id, session.Id, "user", goal);
                store.CompleteRun(project.Id, session.Id,
                    new RunReceipt("planned-run-" + calls.Count,
                        "succeeded", 3, "USD", true, "resposta presente", DateTimeOffset.UtcNow),
                    "Resposta real simulada.");
                return Task.FromResult(0);
            });
            var port = Port();
            await using var app = CliWebUi.CreateApp(cat, port, taskRunner: runner);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var html = await http.GetStringAsync("/");
                Assert.Contains("action='/planned-tasks'", html);
                Assert.Contains("Critério de aceite", html);
                var csrf = Regex.Match(html, "name='csrf' value='([0-9A-F]{64})'")
                    .Groups[1].Value;
                Assert.Equal(64, csrf.Length);
                using (var created = await http.PostAsync("/planned-tasks",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["csrf"] = csrf, ["projectId"] = project.Id,
                        ["sessionId"] = session.Id, ["title"] = "<b>Auditar</b>",
                        ["goal"] = "Audite o arquivo important.md",
                        ["acceptance"] = "Citar evidências em texto",
                        ["assignee"] = "agent:" + agent.Id
                    })))
                    Assert.Equal(HttpStatusCode.SeeOther, created.StatusCode);
                Assert.Empty(calls);
                Assert.Empty(store.Read().Projects.Single().Sessions.Single().Runs);
                var boardStore = new LocalPlannedTaskStore(cat);
                var planned = Assert.Single(boardStore.Read().Tasks);
                Assert.Empty(planned.Attempts);
                Assert.False(planned.Cancelled);
                Assert.Equal("Citar evidências em texto", planned.Acceptance);
                var page = await http.GetStringAsync("/");
                Assert.Contains("&lt;b&gt;Auditar&lt;/b&gt;", page);
                Assert.DoesNotContain("<b>Auditar</b>", page);
                Assert.Contains("Planejada, não executada", page);
                Assert.Contains("action='/task-run'", page);

                using (var run = await http.PostAsync("/task-run",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    { ["csrf"] = csrf, ["taskId"] = planned.Id })))
                    Assert.Equal(HttpStatusCode.SeeOther, run.StatusCode);
                Assert.Single(calls);
                Assert.Contains("--agent-profile-id", calls[0]);
                Assert.DoesNotContain("--allow-create", calls[0]);
                var updated = Assert.Single(new LocalPlannedTaskStore(cat).Read().Tasks);
                var attempt = Assert.Single(updated.Attempts);
                Assert.Equal("response_verified", attempt.Outcome);
                Assert.Equal("planned-run-1", Assert.Single(attempt.RunIds));
                Assert.Equal(20, runner.Board.ReservedCents);
                Assert.Equal("inalterado", File.ReadAllText(original));
                Assert.Single(store.Read().Projects.Single().Sessions.Single().Runs);
                var updatedHtml = await http.GetStringAsync("/");
                Assert.Contains("Resposta presente e verificada", updatedHtml);
                Assert.Contains("Critério: ", updatedHtml);
                Assert.Contains(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(
                    "Citar evidências em texto"), updatedHtml);

                using (var cancel = await http.PostAsync("/task-cancel",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    { ["csrf"] = csrf, ["taskId"] = planned.Id })))
                    Assert.Equal(HttpStatusCode.SeeOther, cancel.StatusCode);
                Assert.True(new LocalPlannedTaskStore(cat).Read().Tasks.Single().Cancelled);
                using (var again = await http.PostAsync("/task-run",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    { ["csrf"] = csrf, ["taskId"] = planned.Id })))
                    Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
                Assert.Single(calls);
                Assert.Equal(20, runner.Board.ReservedCents);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Persistencia_recusa_ids_cruzados_duplicacoes_e_corrupcao()
    {
        var root = Root();
        try
        {
            var (store, project, session, agent) = Seed(root);
            var cat = Path.Combine(root, "catalog");
            var second = Path.Combine(root, "other");
            Directory.CreateDirectory(second);
            var p2 = store.CreateProject("Outro", second);
            var s2 = store.CreateSession(p2.Id, "S2");
            var revision = store.Read().Revision;
            var tasks = new LocalPlannedTaskStore(cat);
            Assert.Throws<KeyNotFoundException>(() =>
                tasks.Create(p2.Id, session.Id, "Inválida", "Objetivo", "Critério", "default"));
            Assert.Throws<ArgumentException>(() =>
                tasks.Create(p2.Id, s2.Id, "Inválida", "Objetivo", "Critério",
                    "agent:" + agent.Id));
            var created = tasks.Create(project.Id, session.Id, "Revisão",
                "Analise", "Resposta presente", "default");
            Assert.Equal(revision, store.Read().Revision);
            Assert.Empty(created.Attempts);
            Assert.Throws<ArgumentException>(() => tasks.RecordAttempt(
                created.Id, "integrated", []));
            Assert.Throws<InvalidOperationException>(() => tasks.RecordAttempt(
                created.Id, "response_verified", ["fake"]));
            var cancelled = tasks.Cancel(created.Id);
            Assert.True(cancelled.Cancelled);
            Assert.Throws<InvalidOperationException>(() => tasks.Cancel(created.Id));
            Assert.Throws<InvalidOperationException>(() =>
                tasks.RecordAttempt(created.Id, "failed", []));
            var file = Path.Combine(cat, "planned-tasks.json");
            var raw = File.ReadAllText(file);
            Assert.Contains("\"default\"", raw);
            File.WriteAllText(file, raw.Replace("\"default\"", "\"other\""));
            Assert.Throws<InvalidDataException>(() => new LocalPlannedTaskStore(cat).Read());
            Assert.Equal(revision, store.Read().Revision);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CSRF_formulario_estrito_e_modo_sem_execucao_nao_chamam_modelo()
    {
        var root = Root();
        try
        {
            var (_, project, session, _) = Seed(root);
            var cat = Path.Combine(root, "catalog");
            var port = Port();
            await using var app = CliWebUi.CreateApp(cat, port);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var page = await http.GetStringAsync("/");
                var csrf = Regex.Match(page, "name='csrf' value='([0-9A-F]{64})'")
                    .Groups[1].Value;
                Assert.Contains("action='/planned-tasks'", page);
                Assert.DoesNotContain("action='/task-run'", page);
                using (var absent = await http.PostAsync("/task-run",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    { ["csrf"] = csrf, ["taskId"] = Guid.NewGuid().ToString("N") })))
                    Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
                var data = new Dictionary<string, string>
                {
                    ["csrf"] = "no", ["projectId"] = project.Id, ["sessionId"] = session.Id,
                    ["title"] = "Tarefa", ["goal"] = "Teste", ["acceptance"] = "Verificar",
                    ["assignee"] = "default"
                };
                using (var bad = await http.PostAsync("/planned-tasks", new FormUrlEncodedContent(data)))
                    Assert.Equal(HttpStatusCode.Forbidden, bad.StatusCode);
                data["csrf"] = csrf;
                data["fs.write"] = "true";
                using (var extra = await http.PostAsync("/planned-tasks", new FormUrlEncodedContent(data)))
                    Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
                Assert.Empty(new LocalPlannedTaskStore(cat).Read().Tasks);
                Assert.Empty(new LocalProjectStore(cat).Read().Projects.Single().Sessions.Single().Runs);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }
}
