using System.Text.Json;

namespace AgentRuntime;

public sealed class InvalidEventLogException(string message) : Exception(message);

/// <summary>
/// Estado de um run, RECONSTRUÍDO do log (nunca guardado ao lado): é o que torna o run retomável e auditável. A reconstrução valida
/// as invariantes — um log adulterado (por exemplo, "sucesso" sem verificação aprovada) é recusado, não aceito.
/// </summary>
public sealed record RunState(
    RunStatus Status,
    BlockReason? Block,
    int Steps,
    int ToolCalls,
    Money? Cost,
    long LastSequence,
    bool Verified,
    IReadOnlyList<ArtifactRef> Artifacts)
{
    public bool IsTerminal => Status is RunStatus.Succeeded or RunStatus.Failed or RunStatus.Cancelled;

    public static RunState Replay(IEnumerable<RuntimeEvent> events)
    {
        var list = events.ToList();
        if (list.Count == 0) throw new InvalidEventLogException("Log vazio: não há run.");
        if (list[0].Kind != EventKind.RunCreated) throw new InvalidEventLogException("O log não começa com RunCreated.");

        var runId = list[0].RunId;
        var status = RunStatus.Created;
        BlockReason? block = null;
        var steps = 0;
        var toolCalls = 0;
        Money? cost = null;
        var verifiedNow = false;
        var artifacts = new List<ArtifactRef>();

        for (var i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.RunId != runId) throw new InvalidEventLogException($"Evento {e.EventId} é de outro run.");
            if (e.Sequence != i + 1) throw new InvalidEventLogException($"Sequência quebrada: esperado {i + 1}, veio {e.Sequence}.");
            if (status is RunStatus.Succeeded or RunStatus.Failed or RunStatus.Cancelled)
                throw new InvalidEventLogException($"Evento {e.EventId} depois de um estado terminal ({status}).");
            if (status == RunStatus.Blocked && e.Kind != EventKind.RunResumed)
                throw new InvalidEventLogException($"Evento {e.EventId} ({e.Kind}) em run bloqueado sem RunResumed.");

            switch (e.Kind)
            {
                case EventKind.RunCreated:
                    if (i != 0) throw new InvalidEventLogException("RunCreated só abre o log.");
                    break;
                case EventKind.ModelResponded:
                    steps++;
                    if (e.Cost is { } c) cost = cost is null ? c : cost.Value + c;
                    status = RunStatus.Running;
                    break;
                case EventKind.ToolCalled:
                    toolCalls++;
                    status = RunStatus.Running;
                    break;
                case EventKind.ToolResult:
                    if (e.Data is { } d && d.TryGetValue("artifacts", out var json))
                        artifacts.AddRange(JsonSerializer.Deserialize<List<ArtifactRef>>(json) ?? []);
                    status = RunStatus.Running;
                    break;
                case EventKind.VerificationPassed:
                    verifiedNow = true;
                    break;
                case EventKind.VerificationFailed:
                    verifiedNow = false;
                    break;
                case EventKind.RunSucceeded:
                    if (!verifiedNow) throw new InvalidEventLogException("RunSucceeded sem VerificationPassed vigente: saída não é sucesso.");
                    status = RunStatus.Succeeded;
                    break;
                case EventKind.RunBlocked:
                    status = RunStatus.Blocked;
                    block = e.Data is { } bd && bd.TryGetValue("reason", out var r) && Enum.TryParse<BlockReason>(r, out var br)
                        ? br
                        : throw new InvalidEventLogException($"RunBlocked {e.EventId} sem motivo válido.");
                    break;
                case EventKind.RunResumed:
                    if (status != RunStatus.Blocked && status != RunStatus.Running && status != RunStatus.Created)
                        throw new InvalidEventLogException($"RunResumed em estado '{status}'.");
                    status = RunStatus.Running;
                    block = null;
                    break;
                case EventKind.RunCancelled:
                    status = RunStatus.Cancelled;
                    break;
                case EventKind.RunFailed:
                    status = RunStatus.Failed;
                    break;
                default:
                    if (status == RunStatus.Created) status = RunStatus.Running;
                    break;
            }
        }
        return new RunState(status, block, steps, toolCalls, cost, list[^1].Sequence, verifiedNow && status == RunStatus.Succeeded, artifacts);
    }
}
