using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

/// <summary>T-15 e aprovações: o run é reconstruído do log, inclusive depois de "queda" em qualquer ponto.</summary>
public class ResumeTests
{
    private static ModelResponse Respond(ModelRequest request)
    {
        // Reativo: decide só pela conversa (sobrevive a reinícios). Sem resultado de ferramenta ainda → chama; com → conclui.
        var hasToolResult = request.Messages.Any(m => m.Content.OfType<ToolResultBlock>().Any());
        return hasToolResult
            ? new ModelResponse([new TextBlock("pronto")], StopReason.EndTurn, ScriptedModelProvider.Cost(1))
            : new ModelResponse([new ToolUseBlock("c1", "w", ScriptedModelProvider.Json("{\"a\":1}"))], StopReason.ToolUse, ScriptedModelProvider.Cost(1));
    }

    private static (Rig rig, ProbeTool write, ReactiveProvider provider) NewWorld()
    {
        var rig = new Rig();
        var write = rig.Probe("w", RiskClass.Write, permissions: [Permissions.FsWrite], idempotent: false);
        return (rig, write, new ReactiveProvider("reactive", Respond));
    }

    private static RunRequest Req() => Rig.Request(grant: Rig.Grant(["w"]), providerId: "reactive");

    // T-15 — retomável: queda em QUALQUER ponto, estado reconstruído do log
    [Fact]
    public async Task T15_queda_em_qualquer_ponto_do_run_e_recuperavel_sem_repetir_efeito_nao_idempotente()
    {
        // run sem queda, só para saber quantos eventos ele tem
        var baseline = NewWorld();
        await baseline.rig.Runner(extraProvider: baseline.provider).RunAsync(Req());
        var total = baseline.rig.Events().Count;
        Assert.True(total >= 8);

        for (var survives = 1; survives < total; survives++)
        {
            var (rig, write, provider) = NewWorld();
            var crashing = new FaultInjectingEventLog(rig.Log, survives);

            await Assert.ThrowsAsync<InjectedLogFailure>(() => rig.Runner(crashing, extraProvider: provider).RunAsync(Req()));

            // "reinício do processo": runner novo, ledger novo, mesmo log durável
            var ledger = new InMemoryLedger(rig.Clock, Rig.Currency);
            ledger.SetLimits(Rig.AgentBudget, new BudgetLimits(Total: new Money(1_000, Rig.Currency)));
            var resumed = await rig.Runner(rig.Log, ledger, provider).ResumeAsync(Req());

            Assert.True(resumed.State.Status == RunStatus.Succeeded, $"queda após {survives} evento(s): status {resumed.State.Status}");
            Assert.True(resumed.State.Verified);
            Assert.Equal(1, write.Invocations); // efeito não idempotente nunca é repetido, em nenhum ponto de queda
            var fromLog = RunState.Replay(rig.Events()); // o estado devolvido É o do log
            Assert.Equal(fromLog.Status, resumed.State.Status);
            Assert.Equal(fromLog.LastSequence, resumed.State.LastSequence);
        }
    }

    [Fact]
    public async Task T15_chamada_interrompida_de_ferramenta_idempotente_e_refeita()
    {
        var rig = new Rig();
        var read = rig.Probe("w", idempotent: true);
        var provider = new ReactiveProvider("reactive", Respond);
        // morre logo depois de ToolCalled ter sido gravado e antes do ToolResult
        var crashing = new FaultInjectingEventLog(rig.Log, failAfterAppends: 5);
        await Assert.ThrowsAsync<InjectedLogFailure>(() => rig.Runner(crashing, extraProvider: provider).RunAsync(Req()));
        Assert.Equal(EventKind.ToolCalled, rig.Events()[^1].Kind);

        var resumed = await rig.Runner(extraProvider: provider).ResumeAsync(Req());

        Assert.Equal(RunStatus.Succeeded, resumed.State.Status);
        Assert.Equal(2, read.Invocations); // idempotente: pode e deve ser refeita
    }

    [Fact]
    public async Task T15_chamada_interrompida_de_ferramenta_nao_idempotente_nao_e_refeita()
    {
        var (rig, write, provider) = NewWorld();
        var crashing = new FaultInjectingEventLog(rig.Log, failAfterAppends: 5);
        await Assert.ThrowsAsync<InjectedLogFailure>(() => rig.Runner(crashing, extraProvider: provider).RunAsync(Req()));
        Assert.Equal(EventKind.ToolCalled, rig.Events()[^1].Kind);

        var resumed = await rig.Runner(extraProvider: provider).ResumeAsync(Req());

        Assert.Equal(RunStatus.Succeeded, resumed.State.Status);
        Assert.Equal(1, write.Invocations);
        var result = rig.Events().Last(e => e.Kind == EventKind.ToolResult);
        Assert.Equal("error", result.Result);
        Assert.Contains("não foi reexecutada", Assert.IsType<ToolResultBlock>(Assert.Single(ContentSerializer.FromJson(result.Payload!))).Content);
    }

    [Fact]
    public async Task T15_run_terminal_nao_gera_eventos_novos_ao_retomar()
    {
        var (rig, _, provider) = NewWorld();
        await rig.Runner(extraProvider: provider).RunAsync(Req());
        var before = rig.Events().Count;

        var again = await rig.Runner(extraProvider: provider).ResumeAsync(Req());

        Assert.Equal(RunStatus.Succeeded, again.State.Status);
        Assert.Equal(before, rig.Events().Count);
    }

    [Fact]
    public async Task T15_run_novo_com_id_existente_e_recusado()
    {
        var (rig, _, provider) = NewWorld();
        await rig.Runner(extraProvider: provider).RunAsync(Req());
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Runner(extraProvider: provider).RunAsync(Req()));
    }

    [Fact]
    public async Task T15_retomar_run_inexistente_falha()
    {
        var (rig, _, provider) = NewWorld();
        await Assert.ThrowsAsync<InvalidEventLogException>(() => rig.Runner(extraProvider: provider).ResumeAsync(Req()));
    }

    // Aprovação: destrutivo só age com aprovação; sem aprovador, escala e fica retomável
    [Fact]
    public async Task Destrutivo_sem_aprovador_escala_e_nao_executa()
    {
        var rig = new Rig();
        var del = rig.Probe("d", RiskClass.Destructive, permissions: [Permissions.FsWrite]);
        rig.Provider.ThenToolUse("c1", "d", "{}");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["d"])));

        Assert.Equal(BlockReason.Escalation, result.State.Block);
        Assert.Equal(0, del.Invocations);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.EscalationRaised);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.ApprovalRequired);
    }

    [Fact]
    public async Task Destrutivo_bloqueado_por_aprovacao_retoma_e_executa_quando_aprovado()
    {
        var rig = new Rig();
        var del = rig.Probe("d", RiskClass.Destructive, permissions: [Permissions.FsWrite]);
        var provider = new ReactiveProvider("reactive", request =>
            request.Messages.Any(m => m.Content.OfType<ToolResultBlock>().Any())
                ? new ModelResponse([new TextBlock("pronto")], StopReason.EndTurn, ScriptedModelProvider.Cost(1))
                : new ModelResponse([new ToolUseBlock("c1", "d", ScriptedModelProvider.Json("{}"))], StopReason.ToolUse, ScriptedModelProvider.Cost(1)));
        var request = Rig.Request(grant: Rig.Grant(["d"]), providerId: "reactive");

        var blocked = await rig.Runner(extraProvider: provider).RunAsync(request);
        Assert.Equal(BlockReason.Escalation, blocked.State.Block);

        rig.Approver = new FixedApprover(true, "human:abner"); // o aprovador aparece depois (o proprietário respondeu)
        var resumed = await rig.Runner(extraProvider: provider).ResumeAsync(request);

        Assert.Equal(RunStatus.Succeeded, resumed.State.Status);
        Assert.Equal(1, del.Invocations);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.ToolCalled && e.ApprovedBy == "human:abner");
    }

    [Fact]
    public async Task Destrutivo_com_aprovacao_negada_nao_executa_e_o_run_segue()
    {
        var rig = new Rig { Approver = new FixedApprover(false) };
        var del = rig.Probe("d", RiskClass.Destructive, permissions: [Permissions.FsWrite]);
        rig.Provider.ThenToolUse("c1", "d", "{}").ThenText("não consegui apagar");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["d"])));

        Assert.Equal(0, del.Invocations);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.ToolDenied && e.Result == "aprovação negada");
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
    }
}
