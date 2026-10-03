using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

/// <summary>T-4, T-5, T-7, T-8: orçamento, verificação, cancelamento e limites de laço.</summary>
public class LimitsTests
{
    // T-4 — orçamento nunca é ultrapassado em silêncio
    [Fact]
    public async Task T4_reserva_negada_bloqueia_sem_chamar_o_provedor()
    {
        var rig = new Rig(totalBudget: 5);
        rig.Provider.ThenToolUse("c1", "p", "{}", cost: 3).ThenToolUse("c2", "p", "{\"x\":1}", cost: 3).ThenText("pronto");
        rig.Probe("p");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"]), maxCostPerCall: 3));

        Assert.Equal(RunStatus.Blocked, result.State.Status);
        Assert.Equal(BlockReason.Budget, result.State.Block);
        Assert.Equal(1, rig.Provider.Calls);        // a segunda chamada nem aconteceu
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.BudgetExceeded);
    }

    [Fact]
    public async Task T4_sem_orcamento_configurado_nao_gasta_nada()
    {
        var rig = new Rig(totalBudget: 0); // nenhum limite: deny-by-default
        rig.Provider.ThenText("pronto");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.Equal(BlockReason.Budget, result.State.Block);
        Assert.Equal(0, rig.Provider.Calls);
    }

    [Fact]
    public async Task T4_gasto_real_acima_do_reservado_e_registrado_e_bloqueia()
    {
        var rig = new Rig(totalBudget: 10);
        rig.Provider.ThenText("pronto", cost: 50); // o provedor cobrou mais que o teto da chamada

        var result = await rig.Runner().RunAsync(Rig.Request(maxCostPerCall: 5));

        Assert.Equal(RunStatus.Blocked, result.State.Status);
        Assert.Equal(BlockReason.Budget, result.State.Block);
        Assert.Equal(new Money(50, Rig.Currency), result.State.Cost);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.BudgetExceeded && e.Result!.Contains("passou"));
    }

    [Fact]
    public void T4_ledger_reserva_tudo_ou_nada_respeita_dia_mes_e_operacao()
    {
        var clock = new FakeClock();
        var ledger = new InMemoryLedger(clock, "BRL");
        var a = new BudgetScope(BudgetScopeKind.Agent, "a");
        var p = new BudgetScope(BudgetScopeKind.Project, "p");
        ledger.SetLimits(a, new BudgetLimits(Daily: new Money(10, "BRL"), PerOperation: new Money(6, "BRL")));
        ledger.SetLimits(p, new BudgetLimits(Total: new Money(8, "BRL")));

        Assert.False(ledger.TryReserve([a], new Money(7, "BRL"), out _, out var reason)); // acima do limite por operação
        Assert.Contains("operação", reason);
        Assert.True(ledger.TryReserve([a, p], new Money(6, "BRL"), out var r1, out _));
        Assert.False(ledger.TryReserve([a, p], new Money(3, "BRL"), out _, out _));        // estoura p (total 8): nada é reservado em a
        Assert.Equal(new Money(4, "BRL"), ledger.Remaining(a));                              // a só tem os 6 do primeiro
        ledger.Release(r1!);
        Assert.Equal(new Money(10, "BRL"), ledger.Remaining(a));

        Assert.True(ledger.TryReserve([a], new Money(6, "BRL"), out _, out _));
        clock.Advance(TimeSpan.FromDays(1));                                                 // dia seguinte: o limite diário recomeça
        Assert.True(ledger.TryReserve([a], new Money(6, "BRL"), out _, out _));
    }

    [Fact]
    public void T4_dinheiro_e_inteiro_checado_e_nao_mistura_moedas()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(1, "BRL") + new Money(1, "USD"));
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue, "BRL") + new Money(1, "BRL"));
        Assert.Equal(new Money(3, "BRL"), new Money(1, "BRL") + new Money(2, "BRL"));
    }

    // T-5 — Completed exige saída E verificação aprovada
    [Fact]
    public async Task T5_modelo_dizer_terminei_nao_conclui_sem_verificacao_aprovada()
    {
        var rig = new Rig { Verifier = DelegateVerifier.AlwaysFail("arquivo não existe") };
        for (var i = 0; i < 5; i++) rig.Provider.ThenText("terminei");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.NotEqual(RunStatus.Succeeded, result.State.Status);
        Assert.False(result.State.Verified);
        Assert.DoesNotContain(rig.Events(), e => e.Kind == EventKind.RunSucceeded);
    }

    [Fact]
    public async Task T5_verificacao_aprovada_sem_evidencia_nao_conta()
    {
        var rig = new Rig { Verifier = new DelegateVerifier((t, _) => new VerificationResult(t.Id, true, "  ", [])) };
        for (var i = 0; i < 5; i++) rig.Provider.ThenText("terminei");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.NotEqual(RunStatus.Succeeded, result.State.Status);
    }

    [Fact]
    public async Task T5_sucesso_exige_verificacao_aprovada_e_deixa_evidencia_no_log()
    {
        var rig = new Rig { Verifier = DelegateVerifier.AlwaysPass("3 critérios no disco") };
        rig.Provider.ThenText("terminei");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.True(result.State.Verified);
        Assert.Equal("3 critérios no disco", result.Verification!.Evidence);
        var kinds = Rig.Kinds(rig.Events()).ToList();
        Assert.True(kinds.IndexOf(EventKind.VerificationPassed) < kinds.IndexOf(EventKind.RunSucceeded));
    }

    [Fact]
    public void T5_tarefa_nao_chega_a_completed_sem_verificacao_aprovada_da_propria_tarefa()
    {
        TaskRecord NewRunning() { var t = new TaskRecord(new TaskSpec("t1", "g", ["a"])); t.Start(); return t; }

        var direct = NewRunning();
        Assert.Throws<InvalidOperationException>(() => direct.Complete(VerificationResult.Pass("t1", "e"))); // não passou por Verifying

        var failed = NewRunning(); failed.BeginVerification();
        Assert.Throws<InvalidOperationException>(() => failed.Complete(VerificationResult.Fail("t1", "e")));

        var foreign = NewRunning(); foreign.BeginVerification();
        Assert.Throws<InvalidOperationException>(() => foreign.Complete(VerificationResult.Pass("outra", "e")));

        var noEvidence = NewRunning(); noEvidence.BeginVerification();
        Assert.Throws<InvalidOperationException>(() => noEvidence.Complete(VerificationResult.Pass("t1", "")));

        var ok = NewRunning(); ok.BeginVerification(); ok.Complete(VerificationResult.Pass("t1", "evidência"));
        Assert.Equal(TaskState.Completed, ok.State);
    }

    [Fact]
    public void T5_log_com_sucesso_sem_verificacao_e_recusado_na_reconstrucao()
    {
        var events = new[]
        {
            Event(1, EventKind.RunCreated, "created"),
            Event(2, EventKind.RunSucceeded, "succeeded"),
        };
        Assert.Throws<InvalidEventLogException>(() => RunState.Replay(events));
    }

    [Fact]
    public void T5_reconstrucao_recusa_sequencia_quebrada_e_eventos_depois_do_fim()
    {
        Assert.Throws<InvalidEventLogException>(() => RunState.Replay([Event(1, EventKind.RunCreated, "c"), Event(3, EventKind.RunCancelled, "x")]));
        Assert.Throws<InvalidEventLogException>(() => RunState.Replay([Event(1, EventKind.RunCreated, "c"), Event(2, EventKind.RunCancelled, "x"), Event(3, EventKind.RunFailed, "y")]));
        Assert.Throws<InvalidEventLogException>(() => RunState.Replay([]));
    }

    // T-7 — cancelamento interrompe a execução em um passo
    [Fact]
    public async Task T7_cancelar_durante_a_chamada_ao_provedor_cancela_o_run()
    {
        var rig = new Rig();
        using var cts = new CancellationTokenSource();
        rig.Provider.Then((_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return null!; })
            .ThenText("não deveria chegar aqui");

        var result = await rig.Runner().RunAsync(Rig.Request(), cts.Token);

        Assert.Equal(RunStatus.Cancelled, result.State.Status);
        Assert.Equal(1, rig.Provider.Calls);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.RunCancelled);
        Assert.Equal(new Money(1_000, Rig.Currency), rig.Ledger.Remaining(Rig.AgentBudget)); // a reserva foi devolvida
    }

    [Fact]
    public async Task T7_cancelar_durante_uma_ferramenta_para_antes_do_proximo_passo()
    {
        var rig = new Rig();
        using var cts = new CancellationTokenSource();
        rig.Probe("p", behavior: (_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return ToolOutcome.Ok("x"); });
        rig.Provider.ThenToolUse("c1", "p", "{}").ThenText("não deveria chegar aqui");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])), cts.Token);

        Assert.Equal(RunStatus.Cancelled, result.State.Status);
        Assert.Equal(1, rig.Provider.Calls);
    }

    [Fact]
    public async Task T7_token_ja_cancelado_nao_chama_o_provedor()
    {
        var rig = new Rig();
        rig.Provider.ThenText("x");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await rig.Runner().RunAsync(Rig.Request(), cts.Token);

        Assert.Equal(RunStatus.Cancelled, result.State.Status);
        Assert.Equal(0, rig.Provider.Calls);
    }

    // T-8 — laços de retry têm limite
    [Fact]
    public async Task T8_falhas_equivalentes_de_verificacao_escalam_depois_de_N_tentativas()
    {
        var rig = new Rig { Verifier = DelegateVerifier.AlwaysFail("Arquivo   NÃO existe") };
        for (var i = 0; i < 10; i++) rig.Provider.ThenText("terminei");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.Equal(RunStatus.Blocked, result.State.Status);
        Assert.Equal(BlockReason.Escalation, result.State.Block);
        Assert.Equal(3, rig.Provider.Calls);
        Assert.Equal(3, ((DelegateVerifier)rig.Verifier).Calls);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.EscalationRaised);
    }

    [Fact]
    public async Task T8_falha_de_verificacao_diferente_a_cada_vez_ainda_tem_teto_de_tentativas()
    {
        var n = 0;
        var rig = new Rig { Verifier = new DelegateVerifier((t, _) => VerificationResult.Fail(t.Id, $"erro {++n}")) };
        for (var i = 0; i < 10; i++) rig.Provider.ThenText("terminei");

        var result = await rig.Runner().RunAsync(Rig.Request(policy: new RunPolicy(MaxEquivalentFailures: 99, MaxVerificationAttempts: 4)));

        Assert.Equal(BlockReason.Escalation, result.State.Block);
        Assert.Equal(4, rig.Provider.Calls);
    }

    [Fact]
    public async Task T8_falha_transitoria_do_provedor_tenta_de_novo_com_espera_e_depois_bloqueia()
    {
        var rig = new Rig();
        for (var i = 0; i < 5; i++) rig.Provider.ThenFail("timeout", transient: true);

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.Equal(BlockReason.Provider, result.State.Block);
        Assert.Equal(3, rig.Provider.Calls);
        Assert.Equal(2, rig.Clock.DelayCount);   // espera entre as tentativas, nenhuma depois da última
        Assert.Equal(3, rig.Events().Count(e => e.Kind == EventKind.ProviderFailed));
        Assert.Equal(new Money(1_000, Rig.Currency), rig.Ledger.Remaining(Rig.AgentBudget)); // falha não gasta orçamento
    }

    [Fact]
    public async Task T8_falha_transitoria_que_se_resolve_segue_o_run()
    {
        var rig = new Rig();
        rig.Provider.ThenFail("rate-limit", transient: true).ThenText("pronto");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
    }

    [Fact]
    public async Task T8_falha_permanente_do_provedor_bloqueia_na_hora()
    {
        var rig = new Rig();
        rig.Provider.ThenFail("auth", transient: false).ThenText("não deveria chegar aqui");

        var result = await rig.Runner().RunAsync(Rig.Request());

        Assert.Equal(BlockReason.Provider, result.State.Block);
        Assert.Equal(1, rig.Provider.Calls);
        Assert.Equal(0, rig.Clock.DelayCount);
    }

    [Fact]
    public async Task T8_mesma_chamada_com_mesmo_resultado_e_sem_progresso_e_bloqueia()
    {
        var rig = new Rig();
        rig.Probe("p");
        for (var i = 0; i < 10; i++) rig.Provider.ThenToolUse($"c{i}", "p", "{\"a\":1}");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));

        Assert.Equal(BlockReason.NoProgress, result.State.Block);
        Assert.Equal(3, rig.Provider.Calls);
    }

    [Fact]
    public async Task T8_limite_de_passos_bloqueia()
    {
        var rig = new Rig();
        rig.Probe("p");
        for (var i = 0; i < 10; i++) rig.Provider.ThenToolUse($"c{i}", "p", $"{{\"i\":{i}}}");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"]), maxSteps: 2));

        Assert.Equal(BlockReason.StepLimit, result.State.Block);
        Assert.Equal(2, rig.Provider.Calls);
    }

    internal static RuntimeEvent Event(long sequence, EventKind kind, string result) =>
        new("r", sequence, DateTimeOffset.UnixEpoch, kind, Rig.Agent, "t", "product:x", Result: result);
}
