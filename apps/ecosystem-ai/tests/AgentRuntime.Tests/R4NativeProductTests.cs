using System.Net;
using System.Text;
using System.Text.Json;
using AgentRuntime;
using AgentRuntime.Providers.ChatCompletions;
using AgentRuntime.Tools.Files;
using EcosystemAi.Product;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class R4NativeProductTests
{
    private sealed class Handler(Func<int, string, CancellationToken, Task<string>> reply) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = await request.Content!.ReadAsStringAsync(token);
            Requests.Add(body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(await reply(Requests.Count, body, token), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "r4-native-" + Guid.NewGuid().ToString("N"));
        public string Catalog => Path.Combine(Root, "catalog");
        public string Journal => Path.Combine(Root, "runs");
        public string Workspace => Path.Combine(Root, "project");
        public LocalProjectStore Store { get; }
        public ProjectEntry Project { get; }
        public SessionEntry Session { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Workspace);
            Store = new LocalProjectStore(Catalog);
            Project = Store.CreateProject("Projeto Android", Workspace);
            Session = Store.CreateSession(Project.Id, "Conversa");
        }
        public ProductExecutionHost Host(Handler handler, long budget = 100, int runs = 8) =>
            new(Catalog, Journal, budget, runs, () => new HttpClient(handler, disposeHandler: false));
        public void Dispose() => Directory.Delete(Root, true);
    }
    private static ProductModelSettings Settings(bool history = false) => new()
    {
        Endpoint = "https://provider.example.test/v1/chat/completions", Model = "configured-model",
        ApiKey = "sentinel-native-secret", RunBudgetCents = 20, MaxCallCents = 5,
        InputUsdPerMillion = 1, OutputUsdPerMillion = 3, UseHistory = history
    };
    private static string Answer(string value) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { content = value }, finish_reason = "stop" } },
        usage = new { cost = 0.01, prompt_tokens = 20, completion_tokens = 10 }
    });

    [Fact]
    public async Task Android_adapter_usa_arquivo_workspace_runner_journal_e_verificador_reais()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Workspace, "README.md"), "fonte real");
        using var handler = new Handler((number, _, _) => Task.FromResult(number == 1
            ? """
            {"choices":[{"message":{"tool_calls":[{"id":"read1","type":"function","function":{"name":"WIRE","arguments":"{\"path\":\"README.md\"}"}}]},"finish_reason":"tool_calls"}],"usage":{"cost":0.01,"prompt_tokens":20,"completion_tokens":10}}
            """.Replace("WIRE", ChatCompletionsProvider.WireName(FileToolIds.Read))
            : Answer("fonte real; resultado correto; sentinel-native-secret")));
        var host = f.Host(handler);
        var result = await host.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Leia README.md", "resultado correto");
        Assert.True(result.Receipt.Verified);
        Assert.Equal("succeeded", result.Receipt.Status);
        Assert.Equal(2, handler.Requests.Count);
        using var request = JsonDocument.Parse(handler.Requests[1]);
        Assert.Contains("fonte real", request.RootElement.GetProperty("messages")[3].GetProperty("content").GetString());
        Assert.DoesNotContain("sentinel-native-secret", string.Join("", handler.Requests));
        Assert.DoesNotContain("sentinel-native-secret", File.ReadAllText(Path.Combine(f.Catalog, "catalog.json")));
        var events = await new LocalRunEventLog(f.Journal).ReadRunAsync(result.Receipt.RunId, CancellationToken.None);
        Assert.Contains(events, e => e.Kind == EventKind.ToolCalled && e.Tool == FileToolIds.Read);
        Assert.Contains(events, e => e.Kind == EventKind.VerificationPassed);
        Assert.DoesNotContain("sentinel-native-secret", string.Join("", events.Select(e => e.Payload)));
        var audit = await ProductRunAudit.ReadAsync(f.Store.Read(), f.Project.Id, f.Session.Id,
            result.Receipt.RunId, f.Journal);
        Assert.Equal(RunAuditAvailability.Available, audit.Availability);
        Assert.True(audit.Verified);
        Assert.Equal(RunStatus.Succeeded, audit.Status);
        Assert.Contains(audit.Events!, e => e.Kind == EventKind.ToolCalled);
        Assert.Equal(2, result.Receipt.CostMinor);
        Assert.Equal(20, host.ReservedCents);
        Assert.Equal(80, host.RemainingCents);
        Assert.Equal("fonte real", File.ReadAllText(Path.Combine(f.Workspace, "README.md")));
        Assert.Equal(result.Receipt.RunId, Assert.Single(new LocalProjectStore(f.Catalog).Read().Projects.Single().Sessions.Single().Runs).RunId);
    }

    [Fact]
    public async Task Modelo_diz_que_passou_mas_trecho_ausente_nao_e_aceito()
    {
        using var f = new Fixture();
        using var handler = new Handler((_, _, _) => Task.FromResult(Answer("Todos os testes passaram, confie em mim.")));
        var host = f.Host(handler);
        var result = await host.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Produza a resposta", "EVIDÊNCIA_LITERAL");
        Assert.False(result.Receipt.Verified);
        Assert.NotEqual("succeeded", result.Receipt.Status);
        Assert.InRange(handler.Requests.Count, 1, 8);
        Assert.InRange(result.Receipt.CostMinor, 1, 20);
        var session = new LocalProjectStore(f.Catalog).Read().Projects.Single().Sessions.Single();
        Assert.False(Assert.Single(session.Runs).Verified);
    }

    [Fact]
    public async Task Cancelamento_e_concorrencia_geram_recibo_sem_fila_nem_segunda_chamada()
    {
        using var f = new Fixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, _, token) =>
        { started.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Answer("never"); });
        var host = f.Host(handler);
        using var cancel = new CancellationTokenSource();
        var run = host.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Trabalhe", cancellationToken: cancel.Token);
        await started.Task;
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Outra tarefa"));
        cancel.Cancel();
        var result = await run;
        Assert.Equal("cancelled", result.Receipt.Status);
        Assert.False(result.Receipt.Verified);
        Assert.Single(handler.Requests);
        Assert.Single(f.Store.Read().Projects.Single().Sessions.Single().Runs);
        Assert.True(result.Receipt.CostEstimated); // Ausência de receipt do provider não vira custo real zero.
        Assert.False(host.Busy);
    }

    [Fact]
    public async Task Equipe_usa_dois_agentes_e_parecer_existente_antes_de_decisao_manual()
    {
        using var f = new Fixture();
        var roster = new LocalAgentRosterStore(f.Catalog);
        var producer = roster.CreateAgent(f.Project.Id, "Produtor", "PRODUCER_ROLE");
        var reviewer = roster.CreateAgent(f.Project.Id, "Revisor", "REVIEWER_ROLE");
        var team = roster.CreateTeam(f.Project.Id, "Equipe", producer.Id, reviewer.Id);
        using var handler = new Handler((n, _, _) => Task.FromResult(Answer(n == 1 ? "Resultado com evidência." : "Parecer independente: revisar limites.")));
        var host = f.Host(handler);
        var result = await host.ExecuteTeamAsync(Settings(), f.Project.Id, f.Session.Id, "Solicitação completa", team.Id, "evidência");
        Assert.True(result.Producer.Receipt.Verified);
        Assert.True(result.Reviewer!.Receipt.Verified);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("PRODUCER_ROLE", handler.Requests[0]);
        Assert.Contains("REVIEWER_ROLE", handler.Requests[1]);
        using var reviewerRequest = JsonDocument.Parse(handler.Requests[1]);
        var reviewerGoal = reviewerRequest.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains("Solicitação completa", reviewerGoal);
        Assert.Contains("Resultado com evidência.", reviewerGoal);
        Assert.Contains("CRITÉRIO EXATO DE ACEITE DA RESPOSTA:\n" + "evidência", reviewerGoal);
        Assert.Equal(40, host.ReservedCents);
        var reviews = new LocalTeamReviewStore(f.Catalog);
        var review = Assert.Single(reviews.Read().Reviews);
        Assert.Equal("pending_owner", review.Decision);
        Assert.Equal("owner_accepted", reviews.Decide(review.Id, "owner_accepted", "Conferi as evidências.").Decision);
        Assert.Equal(2, f.Store.Read().Projects.Single().Sessions.Single().Runs.Count);
    }

    [Fact]
    public async Task Isolamento_quota_e_configuracao_invalida_falham_antes_de_gastar()
    {
        using var f = new Fixture();
        using var handler = new Handler((_, _, _) => Task.FromResult(Answer("Resposta")));
        var host = f.Host(handler, budget: 10);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Tarefa"));
        var funded = f.Host(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => funded.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Tarefa", agentId: "inexistente"));
        await Assert.ThrowsAsync<ArgumentException>(() => funded.ExecuteAsync(Settings(), f.Project.Id, f.Session.Id, "Tarefa", "sentinel-native-secret"));
        var unsafeHost = new ProductExecutionHost(Path.Combine(f.Workspace, "catalog"), f.Journal, 100,
            httpFactory: () => new HttpClient(handler, false));
        var unsafeStore = new LocalProjectStore(Path.Combine(f.Workspace, "catalog"));
        var p = unsafeStore.CreateProject("Inseguro", f.Workspace); var s = unsafeStore.CreateSession(p.Id, "S");
        await Assert.ThrowsAsync<ArgumentException>(() => unsafeHost.ExecuteAsync(Settings(), p.Id, s.Id, "Tarefa"));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, funded.ReservedCents);
        Assert.Empty(f.Store.Read().Projects.Single().Sessions.Single().Runs);
    }

    [Fact]
    public async Task Verificador_recusa_criterio_desconhecido_ausente_e_texto_excessivo()
    {
        var verifier = new ProductResponseVerifier();
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "Goal", ["model-says-ok"]), new RunOutput("ok", []), CancellationToken.None)).Passed);
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "Goal", []), new RunOutput("ok", []), CancellationToken.None)).Passed);
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "Goal", ["response-present"]), new RunOutput(new string('x', 16385), []), CancellationToken.None)).Passed);
        Assert.DoesNotContain("sentinel-native-secret", Settings().ToString());
        Assert.DoesNotContain("sentinel-native-secret", JsonSerializer.Serialize(Settings()));
        Assert.DoesNotContain(typeof(ProductExecutionHost).Assembly.GetReferencedAssemblies(), a =>
            a.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || a.Name == "EcosystemAi.Cli" || a.Name == "Mono.Android");
    }
}
