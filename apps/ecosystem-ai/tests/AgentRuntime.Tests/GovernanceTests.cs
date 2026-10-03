using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

/// <summary>T-1, T-2, T-3 (parte de Context), T-6: permissão, capability, escopo e provedor — tudo deny-by-default.</summary>
public class GovernanceTests
{
    // T-1 — o agente não usa capability sem permissão
    [Fact]
    public async Task T1_capability_fora_da_concessao_do_agente_e_negada_e_nao_executa()
    {
        var rig = new Rig();
        var read = rig.Probe("probe.read");
        var write = rig.Probe("probe.write", RiskClass.Write, permissions: [Permissions.FsWrite], idempotent: false);
        rig.Provider.ThenToolUse("c1", "probe.write", "{}").ThenText("pronto");

        // organização e projeto permitem as duas; o AGENTE só recebeu probe.read
        var all = Rig.Grant(["probe.read", "probe.write"]);
        var request = Rig.Request(grant: Rig.Grant(["probe.read"]), organization: all, project: all);
        var result = await rig.Runner().RunAsync(request);

        Assert.Equal(0, write.Invocations);
        Assert.Equal(0, read.Invocations);
        var denied = Assert.Single(rig.Events(), e => e.Kind == EventKind.ToolDenied);
        Assert.Equal("probe.write", denied.Tool);
        Assert.Contains("agente", denied.Result);
        Assert.Equal(RunStatus.Succeeded, result.State.Status); // o modelo ainda concluiu; a ferramenta é que foi negada
    }

    [Fact]
    public async Task T1_sem_agent_act_nenhuma_ferramenta_mesmo_com_tudo_concedido()
    {
        var rig = new Rig();
        var read = rig.Probe("probe.read");
        rig.Provider.ThenToolUse("c1", "probe.read", "{}").ThenText("pronto");

        var grant = Rig.Grant(["probe.read"], [Permissions.FsRead]); // falta agent.act: agir nunca é herdado
        await rig.Runner().RunAsync(Rig.Request(grant: grant));

        Assert.Equal(0, read.Invocations);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.ToolDenied && e.Result!.Contains(Permissions.AgentAct));
    }

    [Fact]
    public async Task T1_ferramenta_inexistente_e_negada_como_desconhecida()
    {
        var rig = new Rig();
        rig.Provider.ThenToolUse("c1", "inventada.tool", "{}").ThenText("pronto");
        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant([])));
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.ToolDenied && e.Tool == "inventada.tool");
    }

    // T-2 — ferramentas = interseção Host ∩ organização ∩ projeto ∩ agente
    [Fact]
    public void T2_ferramentas_sao_a_intersecao_dos_quatro_niveis()
    {
        var rig = new Rig();
        rig.Probe("a"); rig.Probe("b"); rig.Probe("c"); rig.Probe("d");
        var org = Rig.Grant(["a", "b", "c"]);
        var project = Rig.Grant(["b", "c", "d"]);
        var agent = Rig.Grant(["b", "d", "c"]);

        var resolved = ToolResolver.Resolve(rig.Host, org, project, agent, Rig.Context);

        Assert.Equal(["b", "c"], resolved.Tools.Select(t => t.Descriptor.CapabilityId));
        Assert.Contains(resolved.Denied, d => d.CapabilityId == "a" && d.Reason.Contains("projeto"));
        Assert.Contains(resolved.Denied, d => d.CapabilityId == "d" && d.Reason.Contains("organização"));
    }

    [Fact]
    public void T2_permissoes_efetivas_sao_a_intersecao_e_a_ferramenta_exige_as_suas()
    {
        var rig = new Rig();
        rig.Probe("w", RiskClass.Write, permissions: [Permissions.FsWrite], idempotent: false);
        var caps = new[] { "w" };
        var org = Rig.Grant(caps, [Permissions.AgentAct, Permissions.FsWrite]);
        var project = Rig.Grant(caps, [Permissions.AgentAct, Permissions.FsWrite]);
        var agentSemEscrita = Rig.Grant(caps, [Permissions.AgentAct, Permissions.FsRead]);

        Assert.Empty(ToolResolver.Resolve(rig.Host, org, project, agentSemEscrita, Rig.Context).Tools);
        Assert.Single(ToolResolver.Resolve(rig.Host, org, project, org, Rig.Context).Tools);
    }

    [Fact]
    public void T2_concessao_vazia_nao_permite_nada()
    {
        var rig = new Rig();
        rig.Probe("a");
        Assert.Empty(ToolResolver.Resolve(rig.Host, ToolGrant.None, ToolGrant.None, ToolGrant.None, Rig.Context).Tools);
    }

    // T-3 — o Run não acessa Context fora do escopo
    [Fact]
    public async Task T3_ferramenta_de_outro_workspace_nao_e_resolvida_nem_executada()
    {
        var rig = new Rig();
        var tool = rig.Probe("probe.other", scope: Rig.OtherWorkspace);
        rig.Provider.ThenToolUse("c1", "probe.other", "{}").ThenText("pronto");

        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["probe.other"])));

        Assert.Equal(0, tool.Invocations);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.ToolDenied && e.Result!.Contains("escopo"));
    }

    [Fact]
    public void T3_context_exige_niveis_em_ordem_estrita_e_contem_so_descendentes()
    {
        Assert.Throws<ArgumentException>(() => ContextPath.Create(
            new ContextStep(ContextLevel.Project, "p"), new ContextStep(ContextLevel.Product, "x")));
        Assert.Throws<ArgumentException>(() => ContextPath.Create(
            new ContextStep(ContextLevel.Product, "p"), new ContextStep(ContextLevel.Product, "q")));
        Assert.True(Rig.ProjectScope.Contains(Rig.Context));
        Assert.False(Rig.Context.Contains(Rig.ProjectScope));
        Assert.False(Rig.Context.Contains(Rig.OtherWorkspace));
    }

    // T-6 — provedor desconhecido falha (falha fechada)
    [Fact]
    public async Task T6_provedor_desconhecido_falha_o_run_sem_chamar_ninguem()
    {
        var rig = new Rig();
        rig.Provider.ThenText("nunca deveria ser chamado");

        var result = await rig.Runner().RunAsync(Rig.Request(providerId: "nao-existe"));

        Assert.Equal(RunStatus.Failed, result.State.Status);
        Assert.Equal(0, rig.Provider.Calls);
        Assert.Contains(rig.Events(), e => e.Kind == EventKind.RunFailed && e.Result!.Contains("nao-existe"));
    }

    [Fact]
    public void T6_registro_recusa_id_desconhecido_e_duplicado()
    {
        var registry = new ProviderRegistry().Register(new ScriptedModelProvider("p1"));
        Assert.Throws<UnknownProviderException>(() => registry.Resolve("p2"));
        Assert.Throws<InvalidOperationException>(() => registry.Register(new ScriptedModelProvider("p1")));
        Assert.Throws<ArgumentException>(() => registry.Register(new ScriptedModelProvider(" ")));
    }
}
