using System.Globalization;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Superfície opt-in do Product. Submete UMA tarefa explícita ao mesmo
/// EcosystemAiCli.RunAsync → WorkspaceSession → AgentRunner/ledger.
/// Não executa em GET, não cria motor/provedor/ledger paralelo.
/// </summary>
public sealed record WebTaskSettings(
    string Endpoint, string Model, long BudgetCents, long MaxCallCents,
    decimal InputUsdPerMillion, decimal OutputUsdPerMillion,
    long ProcessBudgetCents, int ProcessMaxRuns = 8, bool UseHistory = false)
{
    public void Validate()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || (uri.Scheme == "http" && !uri.IsLoopback)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Endpoint precisa ser HTTPS ou HTTP loopback sem credenciais ou fragmento.");
        if (string.IsNullOrWhiteSpace(Model) || Model.Length > 256)
            throw new ArgumentException("Modelo inválido.");
        if (BudgetCents < 1 || MaxCallCents < 1 || MaxCallCents > BudgetCents
            || ProcessBudgetCents < BudgetCents || ProcessMaxRuns is < 1 or > 100)
            throw new ArgumentException("Limites por run, sessão do servidor ou número de execuções inválidos.");
        if (InputUsdPerMillion < 0 || OutputUsdPerMillion < 0)
            throw new ArgumentException("Preços não podem ser negativos.");
    }
}

public enum WebTaskState
{
    Succeeded,
    Failed,
    Invalid,
    Busy,
    QuotaExceeded
}

public sealed record WebTaskResult(WebTaskState State, int ExitCode = 0);

public sealed record WebTaskBoard(long ReservedCents, long RemainingCents, int RemainingRuns,
    long RunBudgetCents, bool UseHistory);

public sealed class CliWebTaskRunner
{
    public const int MaxGoalLength = 16 * 1024;
    private readonly string _catalog;
    private readonly string? _journal;
    private readonly WebTaskSettings _settings;
    private readonly Func<string[], Task<int>> _execute;
    private long _reserved;
    private int _runs;
    private int _busy;

    public CliWebTaskRunner(string catalogDirectory, string? journalDirectory,
        WebTaskSettings settings, Func<string[], Task<int>>? execute = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _catalog = Path.GetFullPath(catalogDirectory);
        _journal = journalDirectory is null ? null : Path.GetFullPath(journalDirectory);
        _settings = settings;
        _execute = execute ?? EcosystemAiCli.RunAsync;
    }

    public WebTaskBoard Board => new(
        Interlocked.Read(ref _reserved),
        Math.Max(0, _settings.ProcessBudgetCents - Interlocked.Read(ref _reserved)),
        Math.Max(0, _settings.ProcessMaxRuns - Volatile.Read(ref _runs)),
        _settings.BudgetCents,
        _settings.UseHistory);

    public async Task<WebTaskResult> SubmitAsync(string projectId, string sessionId, string goal)
    {
        // Não manter fila invisível. Uma tentativa já rodando devolve Busy.
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return new WebTaskResult(WebTaskState.Busy);
        try
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(sessionId)
                || string.IsNullOrWhiteSpace(goal) || goal.Length > MaxGoalLength)
                return new WebTaskResult(WebTaskState.Invalid);

            // Relê antes de reservar o orçamento: projeto/sessão não são
            // confiados ao form. O workspace precisa ser o vinculado ao catálogo.
            var projects = new LocalProjectStore(_catalog).Read().Projects;
            var project = projects.FirstOrDefault(p => p.Id == projectId);
            if (project is null || !project.Sessions.Any(s => s.Id == sessionId)
                || !Directory.Exists(project.WorkspaceDirectory))
                return new WebTaskResult(WebTaskState.Invalid);
            CliRunJournalCommands.RequireOutsideWorkspace(_catalog, project.WorkspaceDirectory);
            if (_journal is not null)
                CliRunJournalCommands.RequireOutsideWorkspace(_journal, project.WorkspaceDirectory);

            if (_runs >= _settings.ProcessMaxRuns
                || _reserved >= _settings.ProcessBudgetCents)
                return new WebTaskResult(WebTaskState.QuotaExceeded);

            // Reservar limite máximo antes de chamar o provedor e NUNCA
            // liberar a reserva após falha, cancelamento ou receita estimada.
            var allocation = Math.Min(_settings.BudgetCents, _settings.ProcessBudgetCents - _reserved);
            if (allocation <= 0) return new WebTaskResult(WebTaskState.QuotaExceeded);
            _reserved += allocation;
            _runs++;

            var args = new List<string>
            {
                "--catalog", _catalog,
                "--project", project.WorkspaceDirectory,
                "--project-id", projectId,
                "--session-id", sessionId,
                "--goal", goal,
                "--endpoint", _settings.Endpoint,
                "--model", _settings.Model,
                "--budget-cents", allocation.ToString(CultureInfo.InvariantCulture),
                "--max-call-cents", Math.Min(_settings.MaxCallCents, allocation)
                    .ToString(CultureInfo.InvariantCulture),
                "--input-usd-per-million", _settings.InputUsdPerMillion.ToString(CultureInfo.InvariantCulture),
                "--output-usd-per-million", _settings.OutputUsdPerMillion.ToString(CultureInfo.InvariantCulture)
            };
            if (_journal is not null) { args.Add("--journal"); args.Add(_journal); }
            if (_settings.UseHistory) args.Add("--use-history");
            // Intencional: NUNCA passar --allow-create ao executor web.
            // O usuário pode liberar fs.write apenas na CLI separada.
            var code = await _execute(args.ToArray());
            return new WebTaskResult(code switch
            {
                0 => WebTaskState.Succeeded,
                2 => WebTaskState.Invalid,
                _ => WebTaskState.Failed
            }, code);
        }
        finally { Volatile.Write(ref _busy, 0); }
    }
}
