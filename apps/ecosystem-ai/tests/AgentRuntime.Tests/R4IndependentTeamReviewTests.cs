using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using EcosystemAi.Cli;
using EcosystemAi.ProjectStore;

namespace AgentRuntime.Tests;

public sealed class R4IndependentTeamReviewTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(),
        "r4-team-review-" + Guid.NewGuid().ToString("N"));

    private static WebTaskSettings Settings(long budget = 100, int runs = 6,
        bool review = true) => new(
        "http://127.0.0.1:59999/v1/chat/completions", "mock",
        20, 5, 1m, 3m, budget, runs, ReviewTeams: review);

    private static (LocalProjectStore Store, LocalAgentTeam Team,
        LocalAgentProfile Producer, LocalAgentProfile Reviewer,
        ProjectEntry Project, SessionEntry Session) Seed(string root)
    {
        var workspace = Path.Combine(root, "workspace");
        var catalog = Path.Combine(root, "catalog");
        Directory.CreateDirectory(workspace);
        var store = new LocalProjectStore(catalog);
        var project = store.CreateProject("Projeto", workspace);
        var session = store.CreateSession(project.Id, "Sessão");
        var roster = new LocalAgentRosterStore(catalog);
        var producer = roster.CreateAgent(project.Id, "Produtor", "Planejar C#");
        var reviewer = roster.CreateAgent(project.Id, "Revisor", "Criticar soluções");
        var team = roster.CreateTeam(project.Id, "Equipe", producer.Id, reviewer.Id);
        return (store, team, producer, reviewer, project, session);
    }

    private static int Port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task Equipe_executa_produtor_depois_revisor_com_recibos_distintos_e_sem_integracao_falsa()
    {
        var root = Root();
        try
        {
            var (store, team, producer, reviewer, project, session) = Seed(root);
            var catalog = Path.Combine(root, "catalog");
            var workspaceFile = Path.Combine(root, "workspace", "intocado.md");
            File.WriteAllText(workspaceFile, "original");
            var calls = new List<string[]>();
            var runner = new CliWebTaskRunner(catalog, null, Settings(), args =>
            {
                calls.Add(args);
                var currentId = args[Array.IndexOf(args, "--agent-profile-id") + 1];
                var goal = args[Array.IndexOf(args, "--goal") + 1];
                var runId = "mock-run-" + calls.Count;
                store.AppendTurn(project.Id, session.Id, "user", goal);
                store.CompleteRun(project.Id, session.Id,
                    new RunReceipt(runId, "succeeded", 3, "USD",
                        true, "resposta presente", DateTimeOffset.UtcNow),
                    currentId == producer.Id ? "Resposta do produtor: plano de teste C#" :
                        "Parecer do revisor: falta verificar execução real.");
                return Task.FromResult(0);
            });

            var result = await runner.SubmitAsync(project.Id, session.Id,
                "Inspecione o código C#", "team:" + team.Id);
            Assert.Equal(WebTaskState.Reviewed, result.State);
            Assert.Equal(2, calls.Count);
            Assert.Equal(producer.Id,
                calls[0][Array.IndexOf(calls[0], "--agent-profile-id") + 1]);
            Assert.Equal(reviewer.Id,
                calls[1][Array.IndexOf(calls[1], "--agent-profile-id") + 1]);
            Assert.Contains("Resposta do produtor: plano de teste C#",
                calls[1][Array.IndexOf(calls[1], "--goal") + 1]);
            Assert.Contains("Inspecione o código C#",
                calls[1][Array.IndexOf(calls[1], "--goal") + 1]);
            Assert.DoesNotContain("--allow-create", calls[0]);
            Assert.DoesNotContain("--allow-create", calls[1]);
            Assert.DoesNotContain("--use-history", calls[1]);
            Assert.Equal(40, runner.Board.ReservedCents);
            Assert.Equal(4, runner.Board.RemainingRuns);
            Assert.Equal(2, store.Read().Projects.Single().Sessions.Single().Runs.Count);
            Assert.Equal("original", File.ReadAllText(workspaceFile));
            Assert.False(File.Exists(Path.Combine(root, "catalog", "integration-receipt.json")));

            var port = Port();
            await using var app = CliWebUi.CreateApp(catalog, port, taskRunner: runner);
            await app.StartAsync();
            try
            {
                using var http = new HttpClient
                { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                var html = await http.GetStringAsync("/");
                Assert.Contains("produtor + revisor independente", html);
                Assert.Contains("parecer não aprova nem integra arquivos", html);
                Assert.Contains("Parecer do revisor:", html);
            }
            finally { await app.StopAsync(); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Sem_recibo_verificado_revisor_nao_e_chamado_nem_recebe_budget()
    {
        var root = Root();
        try
        {
            var (store, team, producer, _, project, session) = Seed(root);
            var runs = 0;
            var runner = new CliWebTaskRunner(Path.Combine(root, "catalog"), null,
                Settings(), args =>
                {
                    runs++;
                    // exit 0 sozinho não comprova execução; nenhum receipt/turn.
                    return Task.FromResult(0);
                });
            var response = await runner.SubmitAsync(project.Id, session.Id,
                "Teste", "team:" + team.Id);
            Assert.Equal(WebTaskState.ReviewIncomplete, response.State);
            Assert.Equal(1, runs);
            Assert.Equal(20, runner.Board.ReservedCents);
            Assert.Equal(5, runner.Board.RemainingRuns);
            Assert.Empty(store.Read().Projects.Single().Sessions.Single().Runs);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Orçamento_ou_cota_insuficiente_impede_producao_sem_revisao()
    {
        var root = Root();
        try
        {
            var (_, team, _, _, project, session) = Seed(root);
            var calls = 0;
            var littleBudget = new CliWebTaskRunner(Path.Combine(root, "catalog"), null,
                Settings(budget: 35), _ => { calls++; return Task.FromResult(0); });
            var noRuns = new CliWebTaskRunner(Path.Combine(root, "catalog"), null,
                Settings(runs: 1), _ => { calls++; return Task.FromResult(0); });
            Assert.Equal(WebTaskState.QuotaExceeded,
                (await littleBudget.SubmitAsync(project.Id, session.Id, "Tarefa",
                    "team:" + team.Id)).State);
            Assert.Equal(WebTaskState.QuotaExceeded,
                (await noRuns.SubmitAsync(project.Id, session.Id, "Tarefa",
                    "team:" + team.Id)).State);
            Assert.Equal(0, calls);
            Assert.Equal(0, littleBudget.Board.ReservedCents);
            Assert.Equal(0, noRuns.Board.ReservedCents);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Por_padrao_equipe_continua_executando_so_produtor()
    {
        var root = Root();
        try
        {
            var (_, team, producer, _, project, session) = Seed(root);
            var calls = new List<string[]>();
            var runner = new CliWebTaskRunner(Path.Combine(root, "catalog"), null,
                Settings(review: false), args =>
                {
                    calls.Add(args);
                    return Task.FromResult(0);
                });
            Assert.Equal(WebTaskState.Succeeded,
                (await runner.SubmitAsync(project.Id, session.Id, "Teste",
                    "team:" + team.Id)).State);
            var only = Assert.Single(calls);
            Assert.Equal(producer.Id, only[Array.IndexOf(only, "--agent-profile-id") + 1]);
            Assert.Equal(20, runner.Board.ReservedCents);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Flag_exige_modo_web_com_execucao_explicita()
    {
        var root = Root();
        Directory.CreateDirectory(root);
        try
        {
            var catalog = Path.Combine(root, "catalog");
            Assert.Equal(2, await EcosystemAiCli.RunAsync(
                ["--web-review-teams", "--catalog", catalog]));
            Assert.Equal(2, await EcosystemAiCli.RunAsync(
                ["--web-ui", "--catalog", catalog, "--web-review-teams"]));
            Assert.False(File.Exists(Path.Combine(catalog, "catalog.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
