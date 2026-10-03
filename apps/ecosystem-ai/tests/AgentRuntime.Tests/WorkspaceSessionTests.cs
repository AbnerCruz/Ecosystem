using AgentWorkspace;
using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

public class WorkspaceSessionTests
{
    private static ContextPath Scope(string project) => ContextPath.Create(
        new(ContextLevel.Product, "ecosystem-ai"), new(ContextLevel.Project, project), new(ContextLevel.Workspace, "editor"));

    private static WorkspaceSession Session(Rig rig, ContextPath? context = null, ToolGrant? grant = null,
        AgentRunner? runner = null, IReadOnlyList<BudgetScope>? budgets = null) => new("session", runner ?? rig.Runner(),
        context ?? Rig.Context, grant ?? Rig.Grant(["notes.write", "code.write"]),
        grant ?? Rig.Grant(["notes.write", "code.write"]), budgets ?? [Rig.AgentBudget]);

    [Fact]
    public async Task Mesmo_workspace_e_runner_operam_em_dois_contextos_sem_misturar_capabilities()
    {
        var notesContext = Scope("notes"); var codeContext = Scope("code");
        var rig = new Rig(); var artifacts = new Dictionary<string, string>();
        var notes = rig.Probe("notes.write", RiskClass.Write, notesContext, permissions: [Permissions.FsWrite],
            behavior: (_, _) => { artifacts["note"] = "texto"; return ToolOutcome.Ok("salvo", new ArtifactRef("note", "text", "note.md")); });
        var code = rig.Probe("code.write", RiskClass.Write, codeContext, permissions: [Permissions.FsWrite],
            behavior: (_, _) => { artifacts["code"] = "codigo"; return ToolOutcome.Ok("salvo", new ArtifactRef("code", "text", "game.cs")); });
        var advertised = new List<string[]>();
        var provider = new ReactiveProvider("reactive", request =>
        {
            advertised.Add(request.Tools.Select(t => t.Name).ToArray());
            if (request.Messages.Any(m => m.Content.OfType<ToolResultBlock>().Any()))
                return new([new TextBlock("feito")], StopReason.EndTurn, ScriptedModelProvider.Cost(1));
            return new([new ToolUseBlock("write", Assert.Single(request.Tools).Name, ScriptedModelProvider.Json("{}"))],
                StopReason.ToolUse, ScriptedModelProvider.Cost(1));
        });
        rig.Verifier = new DelegateVerifier((task, _) => artifacts.ContainsKey(task.Id)
            ? VerificationResult.Pass(task.Id, "artefato conferido") : VerificationResult.Fail(task.Id, "ausente"));
        var runner = rig.Runner(extraProvider: provider);
        var agent = Rig.Request(Rig.Grant(["notes.write", "code.write"]), providerId: "reactive").Agent;
        var first = await Session(rig, notesContext, runner: runner).ExecuteAsync("notes-run", new("note", "escrever", ["texto"]), agent);
        var second = await Session(rig, codeContext, runner: runner).ExecuteAsync("code-run", new("code", "programar", ["codigo"]), agent);
        Assert.Equal(RunStatus.Succeeded, first.State.Status); Assert.Equal(RunStatus.Succeeded, second.State.Status);
        Assert.Equal(1, notes.Invocations); Assert.Equal(1, code.Invocations);
        Assert.Equal(new[] { "notes.write" }, advertised[0]); Assert.Equal(new[] { "code.write" }, advertised[2]);
        Assert.All(rig.Events("notes-run"), e => Assert.Equal(notesContext.ToString(), e.ContextRef));
        Assert.All(rig.Events("code-run"), e => Assert.Equal(codeContext.ToString(), e.ContextRef));
    }

    [Theory]
    [InlineData("missing-grant")][InlineData("scope")][InlineData("permission")]
    public async Task Host_recusa_tool_fora_de_concessao_escopo_ou_permissao(string reason)
    {
        var rig = new Rig(); var tool = rig.Probe("notes.write", scope: reason == "scope" ? Rig.OtherWorkspace : Rig.Context);
        var grant = reason == "missing-grant" ? ToolGrant.None : Rig.Grant(["notes.write"],
            reason == "permission" ? [Permissions.AgentAct] : Rig.AllPermissions);
        rig.Provider.ThenToolUse("attempt", "notes.write", "{}").ThenText("fim");
        var result = await Session(rig, grant: grant).ExecuteAsync("denied", Rig.Request().Task, Rig.Request(Rig.Grant(["notes.write"])).Agent);
        Assert.Equal(0, tool.Invocations);
        Assert.Contains(rig.Events("denied"), e => e.Kind == EventKind.ToolDenied);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Mutacao_do_contexto_e_concessao_originais_nao_amplia_sessao()
    {
        var rig = new Rig(); var context = Scope("original");
        var capabilities = new HashSet<string>(); var permissions = new HashSet<string>(Rig.AllPermissions);
        var grant = new ToolGrant(capabilities, permissions);
        var session = Session(rig, context, grant);
        capabilities.Add("notes.write");
        ((ContextStep[])context.Steps)[1] = new(ContextLevel.Project, "outro");
        ((ContextStep[])session.Context.Steps)[1] = new(ContextLevel.Project, "adulterado");
        var tool = rig.Probe("notes.write", scope: Scope("original"));
        rig.Provider.ThenToolUse("attempt", "notes.write", "{}").ThenText("fim");
        await session.ExecuteAsync("mutated", Rig.Request().Task, Rig.Request(Rig.Grant(["notes.write"])).Agent);
        Assert.Equal(Scope("original"), session.Context); Assert.Equal(0, tool.Invocations);
    }

    [Fact]
    public async Task Cancelamento_antes_da_submissao_nao_cria_evento_nem_gasta()
    {
        var rig = new Rig(); using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Session(rig).ExecuteAsync("cancelled", Rig.Request().Task,
            Rig.Request().Agent, cancellationToken: ct.Token));
        Assert.Empty(rig.Events("cancelled")); Assert.Equal(0, rig.Provider.Calls);
    }

    [Fact]
    public async Task Workspace_usa_ledger_do_runtime_e_nao_inventa_orcamento()
    {
        var rig = new Rig(totalBudget: 0); rig.Provider.ThenText("fim");
        var result = await Session(rig).ExecuteAsync("no-budget", Rig.Request().Task, Rig.Request().Agent);
        Assert.Equal(RunStatus.Blocked, result.State.Status); Assert.Equal(0, rig.Provider.Calls);
    }

    [Fact]
    public async Task Excecao_do_runtime_libera_sessao_para_outra_tentativa()
    {
        var rig = new Rig(); rig.Provider.ThenText("primeiro").ThenText("segundo");
        var session = Session(rig); var request = Rig.Request();
        await session.ExecuteAsync("run", request.Task, request.Agent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("run", request.Task, request.Agent));
        Assert.Equal(RunStatus.Succeeded, (await session.ExecuteAsync("next", request.Task, request.Agent)).State.Status);
    }

    private sealed class WaitingProvider : IModelProvider
    {
        public string ProviderId => "waiting";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new([new TextBlock("fim")], StopReason.EndTurn, ScriptedModelProvider.Cost(1));
        }
    }

    [Fact]
    public async Task Sessao_recusa_execucao_sobreposta_e_cancelamento_libera_proxima_tentativa()
    {
        var rig = new Rig(); var provider = new WaitingProvider(); using var ct = new CancellationTokenSource();
        var session = Session(rig, runner: rig.Runner(extraProvider: provider));
        var request = Rig.Request(providerId: provider.ProviderId);
        var first = session.ExecuteAsync("first", request.Task, request.Agent, cancellationToken: ct.Token);
        await provider.Entered.Task;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("overlap", request.Task, request.Agent));
        Assert.Empty(rig.Events("overlap"));
        ct.Cancel();
        Assert.Equal(RunStatus.Cancelled, (await first).State.Status);
        provider.Release.TrySetResult();
        Assert.Equal(RunStatus.Succeeded, (await session.ExecuteAsync("retry", request.Task, request.Agent)).State.Status);
    }
}
