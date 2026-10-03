using System.Collections.Frozen;
using AgentRuntime;

namespace AgentWorkspace;

/// <summary>
/// Sessão local experimental do Workspace, não o laço do agente nem uma Host API pública.
/// O Host confiável fornece o runner e fixa Context, concessões e orçamento ao abrir a sessão.
/// Trocar de projeto ou concessão exige uma sessão nova; nenhum nome de Product é interpretado.
/// </summary>
public sealed class WorkspaceSession
{
    private readonly AgentRunner _runner;
    private readonly ToolGrant _organization;
    private readonly ToolGrant _project;
    private readonly IReadOnlyList<BudgetScope> _budgets;
    private readonly ContextPath _context;
    private int _running;

    public string Id { get; }
    public ContextPath Context => ContextPath.Create(_context.Steps.ToArray());

    public WorkspaceSession(string id, AgentRunner runner, ContextPath context,
        ToolGrant organization, ToolGrant project, IReadOnlyList<BudgetScope> budgets)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Sessão precisa de identidade.", nameof(id));
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(budgets);
        Id = id;
        _runner = runner;
        // ContextPath tem semântica imutável, mas sua coleção pode ter sido obtida como array.
        _context = ContextPath.Create(context.Steps.ToArray());
        _organization = Freeze(organization);
        _project = Freeze(project);
        _budgets = Array.AsReadOnly(budgets.ToArray());
    }

    /// <summary>
    /// Uma tentativa por sessão de cada vez. O resultado e seus eventos pertencem ao Runtime;
    /// o Workspace não mantém ledger, log, provedor ou máquina de estados paralelos.
    /// </summary>
    public async Task<RunResult> ExecuteAsync(string runId, TaskSpec task, AgentDefinition agent,
        RunPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Run precisa de identidade.", nameof(runId));
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(agent);
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Uma execução já está ativa nesta sessão.");
        try
        {
            var request = new RunRequest(runId, task with { Acceptance = Array.AsReadOnly(task.Acceptance.ToArray()) },
                agent with { Grant = Freeze(agent.Grant) }, Context,
                _organization, _project, _budgets, policy ?? RunPolicy.Default);
            return await _runner.RunAsync(request, cancellationToken);
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private static ToolGrant Freeze(ToolGrant grant) => new(
        grant.Capabilities.ToFrozenSet(StringComparer.Ordinal), grant.Permissions.ToFrozenSet(StringComparer.Ordinal));
}
