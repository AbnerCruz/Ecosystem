using AgentRuntime;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace EcosystemAi.Product;

public enum RunAuditAvailability { Available, Missing, Unavailable, ReceiptMismatch }
public sealed record RunAuditEvent(long Sequence, DateTimeOffset At, EventKind Kind);
public sealed record ProductRunAuditResult(RunAuditAvailability Availability, RunStatus? Status = null,
    bool Verified = false, int Steps = 0, int ToolCalls = 0, Money? RecordedCost = null,
    bool CostEstimated = false, int TotalEvents = 0, IReadOnlyList<RunAuditEvent>? Events = null, RunTokenUsage? Tokens = null);

/// <summary>
/// Projeção somente leitura de um recibo da conversa sobre o journal canônico.
/// Estado e verificação são derivados de RunState.Replay, nunca do texto do modelo.
/// Não entrega payload, resultado, verificação livre, IDs de ferramentas ou caminhos.
/// </summary>
public static class ProductRunAudit
{
    public const int MaxVisibleEvents = 100;

    public static async Task<ProductRunAuditResult> ReadAsync(ProjectCatalog catalog,
        string projectId, string sessionId, string runId, string journalDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();
        var project = catalog.Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto inexistente.");
        var session = project.Sessions.SingleOrDefault(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Conversa não pertence ao projeto.");
        var receipt = session.Runs.SingleOrDefault(r => r.RunId == runId)
            ?? throw new KeyNotFoundException("Execução não pertence à conversa.");
        // Associação ambígua não autoriza escolher dados de outra conversa.
        if (catalog.Projects.SelectMany(p => p.Sessions).SelectMany(s => s.Runs)
            .Count(r => r.RunId == runId) != 1)
            return new(RunAuditAvailability.Unavailable);
        try
        {
            ProductExecutionHost.RequireOutside(journalDirectory, project.WorkspaceDirectory);
            if (!Directory.Exists(journalDirectory)) return new(RunAuditAvailability.Missing);
            var events = await new LocalRunEventLog(journalDirectory).ReadRunAsync(runId, cancellationToken);
            if (events.Count == 0) return new(RunAuditAvailability.Missing);
            var context = ContextPath.Create(new(ContextLevel.Ecosystem, "ecosystem"),
                new(ContextLevel.Product, "ecosystem-ai"), new(ContextLevel.Project, project.Id)).ToString();
            if (events.Any(e => e.ContextRef != context)) return new(RunAuditAvailability.Unavailable);
            var state = RunState.Replay(events);
            var matches = string.Equals(receipt.Status, state.Status.ToString(), StringComparison.OrdinalIgnoreCase)
                && receipt.Verified == state.Verified
                && receipt.CostMinor == (state.Cost?.Minor ?? 0)
                && (state.Cost is null || receipt.Currency == state.Cost.Value.Currency);
            return new(matches ? RunAuditAvailability.Available : RunAuditAvailability.ReceiptMismatch,
                state.Status, state.Verified, state.Steps, state.ToolCalls, state.Cost,
                receipt.CostEstimated, events.Count, events.TakeLast(MaxVisibleEvents)
                    .Select(e => new RunAuditEvent(e.Sequence, e.At, e.Kind)).ToArray(), RunTokenUsage.Read(events));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or InvalidEventLogException or System.Text.Json.JsonException or OverflowException)
        {
            // Não levar mensagens de disco, JSON ou caminhos privados para a UI.
            return new(RunAuditAvailability.Unavailable);
        }
    }
}
