using System.Reflection;
using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

/// <summary>T-10, T-11, T-12: segredo nunca vaza, todo evento é auditável e raciocínio privado não é registrado.</summary>
public class ObservabilityTests
{
    private const string Canary = "sk-CANARIO-0123456789abcdef";

    // T-10 — segredo-sentinela nunca aparece em log, evento, artefato nem no que o modelo vê
    [Fact]
    public async Task T10_segredo_nunca_aparece_em_eventos_artefatos_nem_na_conversa()
    {
        var rig = new Rig();
        new InMemorySecretStore().Put("api", Canary, rig.Redactor);
        rig.Probe("p", behavior: (_, _) => ToolOutcome.Ok($"o conteúdo tinha a chave {Canary} dentro"));
        rig.Provider
            .ThenToolUse("c1", "p", "{}")
            .Then(new ModelResponse([new TextBlock($"usei {Canary} e terminei")], StopReason.EndTurn, ScriptedModelProvider.Cost(1)));

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        foreach (var e in rig.Events()) Assert.DoesNotContain(Canary, EventSerializer.ToLine(e));
        foreach (var a in result.Artifacts) Assert.DoesNotContain(Canary, a.Location);
        foreach (var request in rig.Provider.Requests)
            foreach (var m in request.Messages)
                Assert.DoesNotContain(Canary, ContentSerializer.ToJson(m.Content));
        Assert.Contains(SecretRedactor.Mask, EventSerializer.ToLine(rig.Events().First(e => e.Kind == EventKind.ToolResult)));
    }

    [Fact]
    public async Task T10_segredo_na_tarefa_e_no_prompt_do_sistema_tambem_e_redigido()
    {
        var rig = new Rig();
        rig.Redactor.Register(Canary);
        rig.Provider.ThenText("pronto");
        var request = Rig.Request() with
        {
            Task = new TaskSpec("task-1", $"use a chave {Canary}", ["feito"]),
            Agent = Rig.Request().Agent with { SystemPrompt = $"segredo {Canary}" },
        };

        await rig.Runner().RunAsync(request);

        var seen = Assert.Single(rig.Provider.Requests);
        Assert.DoesNotContain(Canary, seen.System);
        Assert.DoesNotContain(Canary, ContentSerializer.ToJson(seen.Messages[0].Content));
    }

    [Fact]
    public void T10_redator_recusa_segredo_curto_demais_e_cobre_o_mais_longo_primeiro()
    {
        var redactor = new SecretRedactor();
        Assert.Throws<ArgumentException>(() => redactor.Register("curto"));
        redactor.Register("abcdefgh");
        redactor.Register("abcdefgh-estendido");
        Assert.Equal($"x {SecretRedactor.Mask} y", redactor.Redact("x abcdefgh-estendido y"));
    }

    [Fact]
    public async Task T10_segredo_so_circula_como_referencia_pelo_store()
    {
        var store = new InMemorySecretStore().Put("api", Canary);
        Assert.Equal(Canary, await store.ResolveAsync(new SecretRef("api"), CancellationToken.None));
        Assert.Null(await store.ResolveAsync(new SecretRef("outro"), CancellationToken.None));
        Assert.DoesNotContain(Canary, new SecretRef("api").ToString()); // a referência não carrega o valor
    }

    // T-11 — todo evento responde quem/por quê/ferramenta/modelo/custo/contexto/resultado/verificação/aprovação
    [Fact]
    public async Task T11_todo_evento_de_um_run_completo_e_auditavel()
    {
        var rig = new Rig { Approver = new Testing.FixedApprover(true, "human:abner") };
        rig.Probe("r");
        rig.Probe("d", RiskClass.Destructive, permissions: [Permissions.FsWrite]);
        rig.Provider.ThenToolUse("c1", "r", "{}").ThenToolUse("c2", "d", "{}").ThenText("pronto");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["r", "d"])));

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        var events = rig.Events();
        foreach (var e in events)
        {
            Assert.Empty(EventContract.Validate(e));
            Assert.Equal("a1", e.Agent.Id);                    // quem
            Assert.Equal("task-1", e.ReasonRef);               // por quê
            Assert.Equal(Rig.Context.ToString(), e.ContextRef); // contexto
        }
        Assert.All(events.Where(e => e.Kind == EventKind.ModelResponded), e => { Assert.NotNull(e.Model); Assert.NotNull(e.Cost); });
        Assert.All(events.Where(e => e.Kind == EventKind.ToolCalled), e => { Assert.NotNull(e.Tool); Assert.NotNull(e.ApprovedBy); });
        Assert.Contains(events, e => e.Kind == EventKind.ToolCalled && e.Tool == "r" && e.ApprovedBy == "policy");
        Assert.Contains(events, e => e.Kind == EventKind.ToolCalled && e.Tool == "d" && e.ApprovedBy == "human:abner");
        Assert.Contains(events, e => e.Kind == EventKind.ApprovalRequired && e.ApprovedBy == "pending");
        Assert.All(events.Where(e => e.Kind == EventKind.ToolResult), e => Assert.NotNull(e.Result));
        Assert.All(events.Where(e => e.Kind == EventKind.VerificationPassed), e => Assert.NotNull(e.Verification));
    }

    [Fact]
    public void T11_contrato_de_evento_recusa_o_que_nao_responde_as_perguntas()
    {
        RuntimeEvent Base(EventKind kind) => new("r", 1, DateTimeOffset.UnixEpoch, kind, Rig.Agent, "t", "product:x");

        Assert.NotEmpty(EventContract.Validate(Base(EventKind.ToolCalled)));                                   // sem ferramenta nem aprovação
        Assert.NotEmpty(EventContract.Validate(Base(EventKind.ModelResponded) with { Model = "m" }));          // sem custo
        Assert.NotEmpty(EventContract.Validate(Base(EventKind.VerificationPassed)));                           // sem verificação
        Assert.NotEmpty(EventContract.Validate(Base(EventKind.RunBlocked)));                                   // sem resultado
        Assert.NotEmpty(EventContract.Validate(Base(EventKind.RunCreated) with { ReasonRef = "" }));           // sem por quê
        Assert.NotEmpty(EventContract.Validate(Base(EventKind.RunCreated) with { ContextRef = " " }));         // sem contexto
        Assert.Empty(EventContract.Validate(Base(EventKind.RunCreated)));
    }

    [Fact]
    public void T11_evento_sobrevive_a_ida_e_volta_pela_serializacao_estavel()
    {
        var e = new RuntimeEvent("r", 7, new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), EventKind.ToolCalled, Rig.Agent, "t", "product:x",
            Tool: "files.read", ApprovedBy: "policy", Payload: "{}", Data: new Dictionary<string, string> { ["callId"] = "c1" });
        var line = EventSerializer.ToLine(e);
        Assert.DoesNotContain('\n', line);
        Assert.Contains("\"ToolCalled\"", line); // enum como texto: estável entre versões
        Assert.Equal(e.EventId, EventSerializer.FromLine(line).EventId);
        Assert.Equal("policy", EventSerializer.FromLine(line).ApprovedBy);
    }

    // T-12 — nenhum campo de raciocínio livre; o que o provedor mandar de raciocínio é descartado
    [Fact]
    public void T12_evento_nao_tem_campo_de_raciocinio_livre()
    {
        var expected = new[]
        {
            "RunId", "Sequence", "At", "Kind", "Agent", "ReasonRef", "ContextRef", "Tool", "Model", "Cost", "Result",
            "Verification", "ApprovedBy", "Payload", "Data", "EventId",
        }.Order().ToArray();
        var actual = typeof(RuntimeEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Order().ToArray();
        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual, n => n.Contains("Thought", StringComparison.OrdinalIgnoreCase) || n.Contains("Think", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Chain", StringComparison.OrdinalIgnoreCase) || n.Equals("Reasoning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task T12_raciocinio_devolvido_pelo_provedor_e_descartado_do_log_e_da_conversa()
    {
        const string privado = "PENSAMENTO-PRIVADO-DO-MODELO";
        var rig = new Rig();
        rig.Probe("p");
        rig.Provider
            .Then(new ModelResponse([new ReasoningBlock(privado), new ToolUseBlock("c1", "p", ScriptedModelProvider.Json("{}"))], StopReason.ToolUse, ScriptedModelProvider.Cost(1)))
            .Then(new ModelResponse([new ReasoningBlock(privado), new TextBlock("pronto")], StopReason.EndTurn, ScriptedModelProvider.Cost(1)));

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        foreach (var e in rig.Events()) Assert.DoesNotContain(privado, EventSerializer.ToLine(e));
        foreach (var request in rig.Provider.Requests)
            foreach (var m in request.Messages)
                Assert.DoesNotContain(m.Content, b => b is ReasoningBlock);
    }

    [Fact]
    public void T12_serializador_de_conteudo_recusa_raciocinio()
    {
        Assert.Throws<InvalidOperationException>(() => ContentSerializer.ToJson([new ReasoningBlock("x")]));
    }
}
