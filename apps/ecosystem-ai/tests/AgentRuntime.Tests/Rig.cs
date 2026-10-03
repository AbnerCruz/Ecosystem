using System.Text.Json;
using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

/// <summary>Ferramenta de teste: conta invocações e devolve o que o teste mandar. Permite provar que algo NÃO foi executado.</summary>
public sealed class ProbeTool(string id, RiskClass risk, ContextPath scope, IEnumerable<string> permissions, bool idempotent, Func<JsonElement, CancellationToken, ToolOutcome>? behavior = null) : ITool
{
    private int _invocations;

    public int Invocations => Volatile.Read(ref _invocations);

    public ToolDescriptor Descriptor { get; } = new(id, "1.0.0", $"probe {id}", """{"type":"object"}""",
        new HashSet<string>(permissions, StringComparer.Ordinal), risk, scope, idempotent);

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocations);
        return ValueTask.FromResult(behavior is null ? ToolOutcome.Ok($"ok:{id}") : behavior(invocation.Arguments, cancellationToken));
    }
}

/// <summary>Provedor reativo: a resposta depende só da conversa, então sobrevive a "reinícios" (T-15).</summary>
public sealed class ReactiveProvider(string providerId, Func<ModelRequest, ModelResponse> respond) : IModelProvider
{
    private int _calls;

    public string ProviderId { get; } = providerId;

    public int Calls => Volatile.Read(ref _calls);

    public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _calls);
        return ValueTask.FromResult(respond(request));
    }
}

/// <summary>Monta um mundo completo e determinístico em volta de um <see cref="AgentRunner"/>.</summary>
public sealed class Rig
{
    public static readonly ContextPath Context = ContextPath.Create(
        new ContextStep(ContextLevel.Product, "ecosystem-ai"),
        new ContextStep(ContextLevel.Project, "demo"),
        new ContextStep(ContextLevel.Workspace, "w1"));

    public static readonly ContextPath OtherWorkspace = ContextPath.Create(
        new ContextStep(ContextLevel.Product, "ecosystem-ai"),
        new ContextStep(ContextLevel.Project, "demo"),
        new ContextStep(ContextLevel.Workspace, "w2"));

    public static readonly ContextPath ProjectScope = ContextPath.Create(
        new ContextStep(ContextLevel.Product, "ecosystem-ai"),
        new ContextStep(ContextLevel.Project, "demo"));

    public const string Currency = "BRL";
    public static readonly BudgetScope AgentBudget = new(BudgetScopeKind.Agent, "a1");

    public FakeClock Clock { get; } = new();
    public InMemoryEventLog Log { get; } = new();
    public InMemoryLedger Ledger { get; }
    public SecretRedactor Redactor { get; } = new();
    public ToolHost Host { get; } = new();
    public ProviderRegistry Providers { get; } = new();
    public ScriptedModelProvider Provider { get; } = new("scripted");
    public IVerifier Verifier { get; set; } = DelegateVerifier.AlwaysPass();
    public IApprover? Approver { get; set; }

    public static readonly AgentIdentity Agent = new("a1", "Agente de teste", "executor");

    public Rig(long totalBudget = 1_000)
    {
        Ledger = new InMemoryLedger(Clock, Currency);
        if (totalBudget > 0) Ledger.SetLimits(AgentBudget, new BudgetLimits(Total: new Money(totalBudget, Currency)));
        Providers.Register(Provider);
    }

    public static readonly string[] AllPermissions = [Permissions.AgentAct, Permissions.FsRead, Permissions.FsWrite];

    public ProbeTool Probe(string id, RiskClass risk = RiskClass.Read, ContextPath? scope = null, bool idempotent = true,
        IEnumerable<string>? permissions = null, Func<JsonElement, CancellationToken, ToolOutcome>? behavior = null)
    {
        var tool = new ProbeTool(id, risk, scope ?? ProjectScope, permissions ?? [Permissions.FsRead], idempotent, behavior);
        Host.Register(tool);
        return tool;
    }

    public AgentRunner Runner(IEventLog? log = null, ILedger? ledger = null, IModelProvider? extraProvider = null)
    {
        var providers = Providers;
        if (extraProvider is not null)
        {
            providers = new ProviderRegistry();
            providers.Register(extraProvider);
        }
        return new AgentRunner(providers, Host, ledger ?? Ledger, log ?? Log, Clock, Verifier, Approver, Redactor);
    }

    public static ToolGrant Grant(IEnumerable<string> capabilities, IEnumerable<string>? permissions = null) =>
        ToolGrant.Of(capabilities, permissions ?? AllPermissions);

    public static RunRequest Request(
        ToolGrant? grant = null,
        string runId = "run-1",
        string providerId = "scripted",
        long maxCostPerCall = 5,
        int maxSteps = 20,
        RunPolicy? policy = null,
        IReadOnlyList<string>? acceptance = null,
        ContextPath? context = null,
        ToolGrant? organization = null,
        ToolGrant? project = null)
    {
        grant ??= ToolGrant.None;
        var model = new ModelProfile(providerId, "modelo-teste", new ModelCapabilities(true, false, true, false, false, 8_000), new Money(maxCostPerCall, Currency));
        var agent = new AgentDefinition(Agent, model, "Você é um agente de teste.", maxSteps, grant);
        return new RunRequest(runId, new TaskSpec("task-1", "Fazer a tarefa", acceptance ?? ["feito"]), agent, context ?? Context,
            organization ?? grant, project ?? grant, [AgentBudget], policy ?? RunPolicy.Default);
    }

    public IReadOnlyList<RuntimeEvent> Events(string runId = "run-1") => Log.ReadRunAsync(runId, CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public static IEnumerable<EventKind> Kinds(IEnumerable<RuntimeEvent> events) => events.Select(e => e.Kind);
}
