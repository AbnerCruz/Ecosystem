using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentRuntime;

public enum EventKind
{
    RunCreated,
    ToolsResolved,
    ModelRequested,
    ModelResponded,
    ProviderFailed,
    BudgetExceeded,
    ToolCalled,
    ToolDenied,
    ToolResult,
    ApprovalRequired,
    ApprovalGranted,
    EscalationRaised,
    VerificationPassed,
    VerificationFailed,
    RunResumed,
    RunSucceeded,
    RunBlocked,
    RunCancelled,
    RunFailed,
}

/// <summary>
/// Evento de auditoria (plano §8.3). Responde: quem fez (<see cref="Agent"/>), por quê (<see cref="ReasonRef"/>: a tarefa),
/// com que ferramenta, qual modelo, qual custo, qual contexto, qual resultado, qual verificação e quem aprovou.
/// NÃO existe campo de raciocínio livre: o raciocínio privado do modelo não é registrado nem exigido. <see cref="Payload"/> guarda só
/// entradas e saídas permitidas (mensagens, argumentos, resultados), já sem segredos.
/// </summary>
public sealed record RuntimeEvent(
    string RunId,
    long Sequence,
    DateTimeOffset At,
    EventKind Kind,
    AgentIdentity Agent,
    string ReasonRef,
    string ContextRef,
    string? Tool = null,
    string? Model = null,
    Money? Cost = null,
    string? Result = null,
    string? Verification = null,
    string? ApprovedBy = null,
    string? Payload = null,
    IReadOnlyDictionary<string, string>? Data = null)
{
    public string EventId => $"{RunId}-{Sequence:D6}";
}

/// <summary>Log append-only por run. É a fonte do estado: o run é reconstruído dele (retomável, auditável).</summary>
public interface IEventLog
{
    ValueTask AppendAsync(RuntimeEvent @event, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<RuntimeEvent>> ReadRunAsync(string runId, CancellationToken cancellationToken);
}

/// <summary>Regras de completude de um evento: o que cada tipo precisa responder.</summary>
public static class EventContract
{
    public static IReadOnlyList<string> Validate(RuntimeEvent e)
    {
        var problems = new List<string>();
        void Need(bool ok, string what) { if (!ok) problems.Add($"{e.EventId} ({e.Kind}): falta {what}"); }

        Need(!string.IsNullOrWhiteSpace(e.RunId), "RunId");
        Need(e.Sequence > 0, "Sequence positivo");
        Need(!string.IsNullOrWhiteSpace(e.Agent?.Id), "quem fez (Agent)");
        Need(!string.IsNullOrWhiteSpace(e.ReasonRef), "por quê (ReasonRef)");
        Need(!string.IsNullOrWhiteSpace(e.ContextRef), "contexto (ContextRef)");

        switch (e.Kind)
        {
            case EventKind.ModelRequested:
            case EventKind.ModelResponded:
            case EventKind.ProviderFailed:
                Need(!string.IsNullOrWhiteSpace(e.Model), "qual modelo");
                if (e.Kind == EventKind.ModelResponded) Need(e.Cost is not null, "qual custo");
                break;
            case EventKind.ToolCalled:
                Need(!string.IsNullOrWhiteSpace(e.Tool), "com qual ferramenta");
                Need(!string.IsNullOrWhiteSpace(e.ApprovedBy), "quem aprovou (política ou humano)");
                break;
            case EventKind.ToolDenied:
            case EventKind.ToolResult:
                Need(!string.IsNullOrWhiteSpace(e.Tool), "com qual ferramenta");
                break;
            case EventKind.ApprovalRequired:
            case EventKind.ApprovalGranted:
                Need(!string.IsNullOrWhiteSpace(e.Tool), "com qual ferramenta");
                Need(!string.IsNullOrWhiteSpace(e.ApprovedBy), "quem aprovou (\"pending\" enquanto ninguém aprovou)");
                break;
            case EventKind.VerificationPassed:
            case EventKind.VerificationFailed:
                Need(!string.IsNullOrWhiteSpace(e.Verification), "qual verificação");
                break;
            case EventKind.BudgetExceeded:
            case EventKind.EscalationRaised:
            case EventKind.RunBlocked:
            case EventKind.RunFailed:
            case EventKind.RunCancelled:
            case EventKind.RunSucceeded:
                Need(!string.IsNullOrWhiteSpace(e.Result), "qual resultado");
                break;
        }
        return problems;
    }
}

/// <summary>Serialização estável de eventos, uma linha JSON por evento: é o que adapters de armazenamento persistem.</summary>
public static class EventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string ToLine(RuntimeEvent e) => JsonSerializer.Serialize(e, Options);

    public static RuntimeEvent FromLine(string line) =>
        JsonSerializer.Deserialize<RuntimeEvent>(line, Options) ?? throw new JsonException("Linha de evento vazia.");
}
