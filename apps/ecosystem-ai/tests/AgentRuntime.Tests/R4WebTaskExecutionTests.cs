using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4WebTaskExecutionTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-web-tasks-" + Guid.NewGuid().ToString("N"));

    private static int FreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        try { return ((IPEndPoint)tcp.LocalEndpoint).Port; }
        finally { tcp.Stop(); }
    }

    private static WebTaskSettings Settings(long total = 35, int runs = 2, bool history = false) =>
        new("http://127.0.0.1:59999/v1/chat/completions", "mock",
            20, 5, 1m, 3m, total, runs, history);

    private static string Token(string html) =>
        Regex.Match(html, "name='csrf' value='([0-9A-F]{64})'").Groups[1].Value;

    [Fact]
    public async Task Post_real_usa_mesmo_executor_uma_vez_reserva_budget_e_nunca_grant_de_escrita()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "private", "catalog");
        Directory.CreateDirectory(workspace);
        var important = Path.Combine(workspace, "important.md");
        File.WriteAllText(important, "não mexer");
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("Projeto", workspace);
            var session = store.CreateSession(project.Id, "Primeira sessão");
            var calls = new List<string[]>();
            var runner = new CliWebTaskRunner(catalog, null, Settings(), args =>
            {
                calls.Add(args);
                store.AppendTurn(project.Id, session.Id, "user", args[Array.IndexOf(args, "--goal") + 1]);
                store.AppendTurn(project.Id, session.Id, "assistant", "Resposta mockada");
                return Task.FromResult(0);
            });
            var port = FreePort();
            await using var app = CliWebUi.CreateApp(catalog, port, taskRunner: runner);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

                var home = await http.GetStringAsync("/");
                Assert.Contains("Nova tarefa supervisionada", home);
                Assert.Contains("action='/tasks'", home);
                Assert.Contains("Tarefas restantes", home);
                var csrf = Token(home);
                Assert.Equal(64, csrf.Length);
                Assert.Empty(calls); // GET jamais chama modelo.

                async Task<HttpResponseMessage> Submit(string goal) => await http.PostAsync("/tasks",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["csrf"] = csrf, ["projectId"] = project.Id,
                        ["sessionId"] = session.Id, ["goal"] = goal
                    }));

                using (var first = await Submit("Verifique README.md"))
                    Assert.Equal(HttpStatusCode.SeeOther, first.StatusCode);
                using (var second = await Submit("Faça outra inspeção"))
                    Assert.Equal(HttpStatusCode.SeeOther, second.StatusCode);
                using (var over = await Submit("Terceira execução"))
                    Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);

                Assert.Equal(2, calls.Count);
                Assert.Equal("20", calls[0][Array.IndexOf(calls[0], "--budget-cents") + 1]);
                Assert.Equal("15", calls[1][Array.IndexOf(calls[1], "--budget-cents") + 1]);
                Assert.Equal("5", calls[1][Array.IndexOf(calls[1], "--max-call-cents") + 1]);
                Assert.DoesNotContain("--allow-create", calls[0]);
                Assert.DoesNotContain("--allow-create", calls[1]);
                Assert.DoesNotContain("--use-history", calls[0]);
                Assert.Equal(35, runner.Board.ReservedCents);
                Assert.Equal(0, runner.Board.RemainingCents);
                Assert.Equal(0, runner.Board.RemainingRuns);
                Assert.Equal("não mexer", File.ReadAllText(important));
                var refreshed = await http.GetStringAsync("/");
                Assert.Contains("Resposta mockada", refreshed);
                Assert.Contains("disabled", refreshed);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Post_nao_esta_disponivel_sem_optin_e_rejeita_origin_csrf_campos_e_ids()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var count = 0;
            var runner = new CliWebTaskRunner(catalog, null, Settings(),
                _ => { count++; return Task.FromResult(0); });
            var port = FreePort();
            await using (var without = CliWebUi.CreateApp(catalog, port))
            {
                await without.StartAsync();
                try
                {
                    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                    { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                    var home = await http.GetStringAsync("/");
                    Assert.DoesNotContain("action='/tasks'", home);
                    using var response = await http.PostAsync("/tasks",
                        new FormUrlEncodedContent(new Dictionary<string, string>
                        { ["goal"] = "teste" }));
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }
                finally { await without.StopAsync(); }
            }

            var port2 = FreePort();
            await using var app = CliWebUi.CreateApp(catalog, port2, taskRunner: runner);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{port2}") };
                var csrf = Token(await http.GetStringAsync("/"));
                async Task<HttpResponseMessage> Post(Dictionary<string, string> data, bool foreign = false)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/tasks");
                    request.Content = new FormUrlEncodedContent(data);
                    if (foreign) request.Headers.TryAddWithoutValidation("Origin", "https://site.invalid");
                    return await http.SendAsync(request);
                }
                var valid = new Dictionary<string, string>
                {
                    ["csrf"] = csrf, ["goal"] = "Tarefa",
                    ["projectId"] = project.Id, ["sessionId"] = session.Id
                };
                using (var foreign = await Post(valid, true))
                    Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
                valid["csrf"] = "invalido";
                using (var wrong = await Post(valid))
                    Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
                valid["csrf"] = csrf;
                valid["allow-create"] = "true";
                using (var extra = await Post(valid))
                    Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
                valid.Remove("allow-create");
                valid["projectId"] = Guid.NewGuid().ToString("N");
                using (var missing = await Post(valid))
                    Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
                Assert.Equal(0, count);
                Assert.Equal(0, runner.Board.ReservedCents);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Concorrencia_nao_gera_fila_repeticao_ou_duas_chamadas_ao_modelo()
    {
        var root = Root();
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        try
        {
            var store = new LocalProjectStore(catalog);
            var project = store.CreateProject("P", workspace);
            var session = store.CreateSession(project.Id, "S");
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runs = 0;
            var runner = new CliWebTaskRunner(catalog, null, Settings(), async _ =>
            {
                Interlocked.Increment(ref runs);
                started.SetResult();
                return await finish.Task;
            });
            var task = runner.SubmitAsync(project.Id, session.Id, "Primeira tarefa");
            await started.Task;
            Assert.Equal(WebTaskState.Busy,
                (await runner.SubmitAsync(project.Id, session.Id, "Segunda tarefa")).State);
            finish.SetResult(1);
            Assert.Equal(WebTaskState.Failed, (await task).State);
            Assert.Equal(1, runs);
            Assert.Equal(20, runner.Board.ReservedCents);
            Assert.Equal(1, runner.Board.RemainingRuns);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Configuracao_invalida_e_tentativa_de_grant_por_cli_sao_recusadas()
    {
        Assert.Throws<ArgumentException>(() =>
            Settings(10).Validate());
        Assert.Throws<ArgumentException>(() =>
            new WebTaskSettings("http://provider.exemplo/v1/chat/completions", "x",
                10, 5, 1, 1, 20).Validate());

        var root = Root();
        Directory.CreateDirectory(root);
        try
        {
            var cat = Path.Combine(root, "catalog");
            Assert.Equal(2, await EcosystemAiCli.RunAsync(["--web-tasks", "--catalog", cat]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync(["--web-ui", "--web-tasks",
                "--catalog", cat, "--allow-create"]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync(["--web-ui", "--web-tasks",
                "--catalog", cat, "--budget-cents", "5"]));
            Assert.False(File.Exists(Path.Combine(cat, "catalog.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}
