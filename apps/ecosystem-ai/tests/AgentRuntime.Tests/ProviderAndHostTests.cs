using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

/// <summary>T-14 (contrato de provedor em duas implementações) e T-9 (host ocioso custa zero).</summary>
public class ProviderAndHostTests
{
    private static ModelResponse Canned(string text = "olá") => new([new TextBlock(text)], StopReason.EndTurn, ScriptedModelProvider.Cost(7));

    private static ModelRequest SampleRequest(string providerId) => new(
        new ModelProfile(providerId, "m", new ModelCapabilities(true, false, true, false, false, 1000), new Money(10, "BRL")),
        "sistema", [Message.User("oi")], []);

    /// <summary>Cada fábrica devolve um provedor novo que responderá <paramref name="response"/> uma vez. É a MESMA suíte para todas.</summary>
    public static TheoryData<string> Implementations => ["scripted", "recorded"];

    private static IModelProvider Build(string implementation, ModelResponse response) => implementation switch
    {
        "scripted" => new ScriptedModelProvider("p").Then(response),
        "recorded" => new RecordedModelProvider("p", [RecordedModelProvider.Record(response)]),
        _ => throw new ArgumentOutOfRangeException(nameof(implementation)),
    };

    // T-14 — a suíte de contrato roda em ≥ 2 implementações
    [Theory, MemberData(nameof(Implementations))]
    public async Task T14_devolve_resposta_normalizada_com_custo_na_moeda(string implementation)
    {
        var provider = Build(implementation, Canned("olá"));
        Assert.False(string.IsNullOrWhiteSpace(provider.ProviderId));

        var response = await provider.CompleteAsync(SampleRequest("p"), CancellationToken.None);

        Assert.Equal("olá", Assert.IsType<TextBlock>(Assert.Single(response.Content)).Text);
        Assert.Equal(StopReason.EndTurn, response.Stop);
        Assert.Equal(new Money(7, "BRL"), response.Usage.Cost);
    }

    [Theory, MemberData(nameof(Implementations))]
    public async Task T14_respeita_cancelamento(string implementation)
    {
        var provider = Build(implementation, Canned());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await provider.CompleteAsync(SampleRequest("p"), cts.Token));
    }

    [Theory, MemberData(nameof(Implementations))]
    public async Task T14_falha_e_sempre_ProviderException_nunca_excecao_de_fornecedor(string implementation)
    {
        var provider = Build(implementation, Canned());
        await provider.CompleteAsync(SampleRequest("p"), CancellationToken.None); // consome a única resposta
        var failure = await Assert.ThrowsAsync<ProviderException>(async () => await provider.CompleteAsync(SampleRequest("p"), CancellationToken.None));
        Assert.False(string.IsNullOrWhiteSpace(failure.Kind));
        Assert.False(failure.IsTransient);
    }

    [Theory, MemberData(nameof(Implementations))]
    public async Task T14_transporta_uso_de_ferramenta_com_argumentos_intactos(string implementation)
    {
        var use = new ModelResponse([new ToolUseBlock("c1", "files.read", ScriptedModelProvider.Json("{\"path\":\"a/b.txt\"}"))], StopReason.ToolUse, ScriptedModelProvider.Cost(1));
        var provider = Build(implementation, use);

        var response = await provider.CompleteAsync(SampleRequest("p"), CancellationToken.None);

        var block = Assert.IsType<ToolUseBlock>(Assert.Single(response.Content));
        Assert.Equal("c1", block.Id);
        Assert.Equal("files.read", block.Name);
        Assert.Equal("a/b.txt", block.Input.GetProperty("path").GetString());
        Assert.Equal(StopReason.ToolUse, response.Stop);
    }

    [Theory, MemberData(nameof(Implementations))]
    public async Task T14_roda_um_run_completo_pelo_AgentRunner(string implementation)
    {
        var rig = new Rig();
        var provider = Build(implementation, Canned("terminei"));
        var registry = new ProviderRegistry().Register(provider);
        var runner = new AgentRunner(registry, rig.Host, rig.Ledger, rig.Log, rig.Clock, rig.Verifier, null, rig.Redactor);

        var result = await runner.RunAsync(Rig.Request(providerId: "p"));

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal(new Money(7, Rig.Currency), result.State.Cost);
    }

    // T-9 — ocioso custa zero: sem eventos, nenhuma chamada a provedor nem timer
    [Fact]
    public async Task T9_host_ocioso_nao_chama_provedor_nem_espera_nem_gasta()
    {
        var rig = new Rig();
        rig.Provider.ThenText("pronto");
        var host = new RuntimeHost(rig.Runner());
        using var cts = new CancellationTokenSource();
        var loop = host.RunAsync(cts.Token);

        await Task.Delay(200); // tempo real: se houvesse busy loop ou timer, apareceria aqui

        Assert.Equal(0, rig.Provider.Calls);
        Assert.Equal(0, rig.Clock.DelayCount);
        Assert.Empty(rig.Log.All);
        Assert.Equal(new Money(1_000, Rig.Currency), rig.Ledger.Remaining(Rig.AgentBudget));
        Assert.False(loop.IsCompleted); // suspenso aguardando a fila, não terminado nem girando

        var submitted = host.SubmitAsync(Rig.Request());   // um evento acorda o runtime
        var result = await submitted.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal(1, rig.Provider.Calls);

        host.Complete();
        await loop.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task T9_host_processa_em_ordem_e_encerra_ao_completar()
    {
        var rig = new Rig();
        rig.Provider.ThenText("um").ThenText("dois");
        var host = new RuntimeHost(rig.Runner());
        var a = host.SubmitAsync(Rig.Request(runId: "run-a"));
        var b = host.SubmitAsync(Rig.Request(runId: "run-b"));
        host.Complete();

        await host.RunAsync(CancellationToken.None);

        Assert.Equal(RunStatus.Succeeded, (await a).State.Status);
        Assert.Equal(RunStatus.Succeeded, (await b).State.Status);
        Assert.Throws<InvalidOperationException>(() => { _ = host.SubmitAsync(Rig.Request(runId: "run-c")); });
    }
}
