using System.Text.Json;
using AgentRuntime;
using AgentRuntime.Testing;
using AgentWorkspace;
using Lunet.Core;
using Lunet.Core.Capabilities;

// P6-4 / R3: a sessão abaixo é o Host REAL do Lunet (ProjectStore -> OpenForProject).
// Só o composition root de TESTE conhece os dois Products; nenhum assembly distribuído os liga.
var temp = Path.Combine(Path.GetTempPath(), "p6-lunet-host-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var store = new ProjectStore(temp);
    var first = store.Create("PilotOne");
    var second = store.Create("PilotTwo");
    Ensure(first.Manifest.GameId != second.Manifest.GameId, "projetos precisam de identidades distintas");
    await RunVerifiedAsync(first, "p6-first");
    await RunVerifiedAsync(second, "p6-second");
    await DenyWithoutAgentActAsync(first);
    await CheckHostGrantsRevocationAndCancelAsync(first);
    Console.WriteLine("P6-4 Lunet Host: PASS — 2 projetos reais, resultado verificado, deny-by-default, revogacao e cancelamento.");
}
finally
{
    Directory.Delete(temp, recursive: true);
}

static async Task RunVerifiedAsync(LunetProject project, string runId)
{
    using var hostSession = LunetCapabilityHost.CreateDefault().OpenForProject(project);
    var path = BoundContext(hostSession);
    Ensure(path.Steps.Any(s => s.Level == ContextLevel.Project && s.Id == project.Manifest.GameId), "Context nao usa GameId do Host");
    Ensure(hostSession.Identity == "lunet-local-user", "identidade Host-owned nao foi preservada");

    var bridge = new LunetInspectionTool(hostSession, path);
    var tools = new ToolHost().Register(bridge);
    var provider = new ScriptedModelProvider("scripted")
        .ThenToolUse("inspect-1", LunetTextInspectionCapability.CapabilityId, """{"text":"olá mundo"}""")
        .ThenText("a verificacao independente decide o resultado");
    var providers = new ProviderRegistry();
    providers.Register(provider);

    var clock = new FakeClock();
    var ledger = new InMemoryLedger(clock, "BRL");
    var budget = new BudgetScope(BudgetScopeKind.Agent, "p6-local-agent");
    ledger.SetLimits(budget, new BudgetLimits(Total: new Money(100, "BRL")));
    var log = new InMemoryEventLog();
    var verifier = new DelegateVerifier((task, _) =>
        bridge.SuccessfulResponse is { } output
            && output.GetProperty("characters").GetInt32() == 9
            && output.GetProperty("words").GetInt32() == 2
            && output.GetProperty("lines").GetInt32() == 1
                ? VerificationResult.Pass(task.Id, "text.inspect validado no Host real do Lunet")
                : VerificationResult.Fail(task.Id, "Host nao produziu resultado comprovado"));

    var runner = new AgentRunner(providers, tools, ledger, log, clock, verifier);
    var grant = ToolGrant.Of([LunetTextInspectionCapability.CapabilityId], [Permissions.AgentAct]);
    var workspace = new WorkspaceSession("p6:" + project.Manifest.GameId, runner, path, grant, grant, [budget]);
    var agent = new AgentDefinition(
        new AgentIdentity("p6-agent", "Pilot", "test"),
        new ModelProfile("scripted", "test-no-network", new ModelCapabilities(true, false, true, false, false, 8_000), new Money(5, "BRL")),
        "Use apenas a capability do Host.", 4, grant);
    var result = await workspace.ExecuteAsync(runId, new TaskSpec(runId, "Inspecione texto", ["contagem correta"]), agent);
    Ensure(result.State.Status == RunStatus.Succeeded, "Run nao passou na verificacao independente");
    Ensure(bridge.Invocations == 1, "o Host real nao foi chamado exatamente uma vez");
    Ensure(provider.Calls == 2, "o script executou chamadas inesperadas");
    var events = await log.ReadRunAsync(runId, CancellationToken.None);
    Ensure(events.Any(e => e.Kind == EventKind.ToolCalled && e.Tool == LunetTextInspectionCapability.CapabilityId), "evento de chamada ausente");
    Ensure(events.Any(e => e.Kind == EventKind.VerificationPassed), "verificacao ausente");
    Ensure(events.All(e => e.ContextRef == path.ToString()), "log perdeu isolamento do projeto");
    Ensure(events.All(e => EventContract.Validate(e).Count == 0), "evento incompleto");
    Console.WriteLine("PASS " + runId + " " + project.Manifest.GameId);
}

static async Task DenyWithoutAgentActAsync(LunetProject project)
{
    using var host = LunetCapabilityHost.CreateDefault().OpenForProject(project);
    var path = BoundContext(host);
    var bridge = new LunetInspectionTool(host, path);
    var provider = new ScriptedModelProvider("scripted")
        .ThenToolUse("unauthorized", LunetTextInspectionCapability.CapabilityId, """{"text":"segredo"}""")
        .ThenText("tentativa encerrada");
    var providers = new ProviderRegistry();
    providers.Register(provider);
    var clock = new FakeClock();
    var ledger = new InMemoryLedger(clock, "BRL");
    var budget = new BudgetScope(BudgetScopeKind.Agent, "deny");
    ledger.SetLimits(budget, new BudgetLimits(Total: new Money(100, "BRL")));
    var log = new InMemoryEventLog();
    var runner = new AgentRunner(providers, new ToolHost().Register(bridge), ledger, log, clock,
        DelegateVerifier.AlwaysFail("sem chamada real"));
    var denied = ToolGrant.Of([LunetTextInspectionCapability.CapabilityId], []); // sem agent.act
    var workspace = new WorkspaceSession("denied", runner, path, denied, denied, [budget]);
    var agent = new AgentDefinition(new AgentIdentity("p6-denied", "Denied", "test"),
        new ModelProfile("scripted", "test-no-network", new ModelCapabilities(true, false, true, false, false, 8_000), new Money(5, "BRL")),
        "", 3, denied);
    var result = await workspace.ExecuteAsync("p6-denied", new TaskSpec("deny", "Tente acessar", []), agent);
    Ensure(result.State.Status != RunStatus.Succeeded, "sem permissao nao pode completar");
    Ensure(bridge.Invocations == 0, "Runtime chamou Host mesmo sem agent.act");
    Ensure(log.All.Any(e => e.Kind == EventKind.ToolDenied), "negacao nao foi registrada");
    Console.WriteLine("PASS deny-by-default do Agent Runtime");
}

static async Task CheckHostGrantsRevocationAndCancelAsync(LunetProject project)
{
    var root = LunetHostContext.ForProject(project);
    var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var slow = new LunetHostCapability(
        "pilot.read", "lunet2d", new Version(1, 0, 0), root,
        ["fs.read"], "session",
        v => v.ValueKind == JsonValueKind.Object,
        v => v.ValueKind == JsonValueKind.Object,
        async (_, token) =>
        {
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return JsonSerializer.SerializeToElement(new { ok = true });
        });
    var realHost = new LunetCapabilityHost([slow], ["fs.read"]);
    using (var noGrant = realHost.Open("local-user", root, []))
    {
        Ensure(noGrant.Discover().Count == 0, "Host anunciou capability sem grant");
        var unavailable = await noGrant.InvokeAsync("pilot.read", new Version(1, 0, 0), JsonSerializer.SerializeToElement(new { }));
        Ensure(unavailable.HostError == "CAPABILITY_UNAVAILABLE", "Host nao falhou fechado");
    }
    using (var granted = realHost.Open("local-user", root, ["fs.read"]))
    {
        Ensure(granted.Discover().Count == 1, "grant explicito nao habilitou capability");
        var pending = granted.InvokeAsync("pilot.read", new Version(1, 0, 0), JsonSerializer.SerializeToElement(new { }));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Ensure(granted.Revoke(["fs.read"]), "revogacao nao reconhecida");
        var revoked = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Ensure(revoked.HostError == "REVOKED", "operacao revogada produziu sucesso");
        Ensure(granted.Discover().Count == 0, "capability revogada continua anunciada");
    }
    started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using (var granted = realHost.Open("local-user", root, ["fs.read"]))
    {
        var pending = granted.InvokeAsync("pilot.read", new Version(1, 0, 0), JsonSerializer.SerializeToElement(new { }));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Ensure(granted.CancelActive(), "Host nao cancelou operacao ativa");
        var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Ensure(cancelled.HostError == "CANCELLED", "cancelamento retornou sucesso");
        granted.Dispose();
        var closed = await granted.InvokeAsync("pilot.read", new Version(1, 0, 0), JsonSerializer.SerializeToElement(new { }));
        Ensure(closed.HostError == "SESSION_CLOSED", "Host fechado aceitou nova chamada");
    }
    Console.WriteLine("PASS grants, revoke, cancel, close do Host real");
}

static ContextPath BoundContext(LunetHostSession session) => ContextPath.Create(
    session.Context.Path.Select(step => new ContextStep(
        Enum.Parse<ContextLevel>(step.Level, ignoreCase: true), step.Id)).ToArray());

static void Ensure(bool check, string message)
{
    if (!check) throw new InvalidOperationException("P6-4: " + message);
}

/// <summary>Adapter apenas no teste de integração: nenhuma dependencia Lunet → Agent Runtime nos assemblies de produto.</summary>
sealed class LunetInspectionTool : ITool
{
    private readonly LunetHostSession _host;
    private readonly ContextPath _scope;
    private int _invocations;

    public ToolDescriptor Descriptor { get; }
    public int Invocations => Volatile.Read(ref _invocations);
    public JsonElement? SuccessfulResponse { get; private set; }

    public LunetInspectionTool(LunetHostSession host, ContextPath scope)
    {
        _host = host;
        _scope = scope;
        var definition = host.Discover().Single(d => d.Capability == LunetTextInspectionCapability.CapabilityId);
        Descriptor = new ToolDescriptor(
            definition.Capability, definition.Version.ToString(), "Contagem de caracteres, palavras e linhas",
            """{"type":"object","required":["text"],"properties":{"text":{"type":"string"}}}""",
            definition.RequiredPermissions, RiskClass.Read, scope, true);
    }

    public async ValueTask<ToolOutcome> InvokeAsync(ToolInvocation call, ToolContext context, CancellationToken cancellationToken)
    {
        // Defender tambem no adapter: um ToolContext forjado nao pode cruzar projeto.
        if (!_scope.Equals(context.Context)) return ToolOutcome.Error("CONTEXT_MISMATCH");
        Interlocked.Increment(ref _invocations);
        var result = await _host.InvokeAsync(Descriptor.CapabilityId, new Version(1, 0, 0),
            call.Arguments, cancellationToken);
        if (!result.Succeeded || result.Output is not { } output)
            return ToolOutcome.Error(result.Error ?? "HOST_NO_OUTPUT");
        SuccessfulResponse = output.Clone();
        return ToolOutcome.Ok(output.GetRawText());
    }
}
