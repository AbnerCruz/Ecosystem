using System.Security.Cryptography;
using System.Text;
using TabletopRpg.Core;
using TabletopRpg.GameMaster;
using TabletopRpg.ModelGateway;

namespace TabletopRpg.Orchestration;

public enum OrchestratedTurnStatus
{
    Applied,
    Rejected,
    NoAction,
    Proposal,
    Duplicate,
    Busy,
    Failed,
    NoProgress
}

/// <summary>
/// An immutable audit receipt. Never contains prompts, secrets, private world
/// facts, model answers or GM draft text.
/// </summary>
public sealed record OrchestratedTurnReceipt(
    string EventKey,
    ParticipantId ParticipantId,
    OrchestratedTurnStatus Status,
    int Attempts,
    string? Reason = null,
    ActionResolution? Action = null,
    GameMasterProposal? Proposal = null);

/// <summary>
/// An explicit provider failure is safe for immediate retry only when the
/// adapter can guarantee no charge/effect (e.g. rejected before submission).
/// Unknown remote outcomes are never retried automatically.
/// </summary>
public sealed class NoChargeTransientModelException : Exception
{
    public NoChargeTransientModelException(string reason = "Provider rejected before submission.")
        : base(reason) { }
}

/// <summary>
/// Event-only coordinator of the existing Player Agent runner and GM proposals.
/// No timers, background wake, provider selection, auto tool approval or limits
/// on money/tokens. Guards are about duplicated work, not spending quotas.
/// </summary>
public sealed class RpgEventOrchestrator
{
    private const int MaxTrackedEvents = 16_384;
    private readonly Campaign _campaign;
    private readonly SessionEngine _session;
    private readonly object _gate = new();
    private readonly Dictionary<ParticipantId, IAgentDecisionSource> _players = [];
    private readonly Dictionary<ParticipantId, GameMasterAgent> _gameMasters = [];
    private readonly HashSet<(ParticipantId, string)> _seen = [];
    private readonly HashSet<ParticipantId> _busy = [];
    private readonly Dictionary<ParticipantId, (string Trigger, string Revision)> _noProgress = [];
    private readonly List<OrchestratedTurnReceipt> _receipts = [];

    public RpgEventOrchestrator(Campaign campaign, SessionEngine session)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public IReadOnlyList<OrchestratedTurnReceipt> Receipts
    {
        get { lock (_gate) return Array.AsReadOnly(_receipts.ToArray()); }
    }

    public void RegisterPlayer(ParticipantId id, IAgentDecisionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireSeat(id, SeatRole.Player);
        lock (_gate)
        {
            if (_players.ContainsKey(id) || _gameMasters.ContainsKey(id))
                throw new InvalidOperationException("Seat already configured.");
            _players.Add(id, source);
        }
    }

    public void RegisterGameMaster(ParticipantId id, GameMasterAgent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireSeat(id, SeatRole.GameMaster);
        lock (_gate)
        {
            if (_gameMasters.ContainsKey(id) || _players.ContainsKey(id))
                throw new InvalidOperationException("Seat already configured.");
            _gameMasters.Add(id, source);
        }
    }

    public async Task<OrchestratedTurnReceipt> DispatchAsync(
        string eventKey, ParticipantId participantId, AgentWakeSignal signal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || eventKey.Length > 128)
            throw new ArgumentException("Event key must contain 1..128 characters.", nameof(eventKey));
        ArgumentNullException.ThrowIfNull(signal);
        cancellationToken.ThrowIfCancellationRequested();

        IAgentDecisionSource? player;
        GameMasterAgent? master;
        var fingerprint = TriggerFingerprint(signal);
        string initialRevision;
        lock (_gate)
        {
            _players.TryGetValue(participantId, out player);
            _gameMasters.TryGetValue(participantId, out master);
            if (player is null && master is null)
                throw new InvalidOperationException("Participant has no registered agent.");

            var key = (participantId, eventKey);
            if (_seen.Contains(key))
                return Record(new(eventKey, participantId, OrchestratedTurnStatus.Duplicate, 0));
            if (_busy.Contains(participantId))
                return Record(new(eventKey, participantId, OrchestratedTurnStatus.Busy, 0));
            initialRevision = RevisionFor(participantId, master is not null);
            if (_noProgress.TryGetValue(participantId, out var previous) &&
                previous.Trigger == fingerprint && previous.Revision == initialRevision)
                return Record(new(eventKey, participantId, OrchestratedTurnStatus.NoProgress, 0));
            if (_seen.Count >= MaxTrackedEvents)
                throw new InvalidOperationException("Event checkpoint required before accepting more work.");

            // Reserve ID *before* invoking any external model, even if its outcome
            // is unknown or cancellation interrupts the request.
            _seen.Add(key);
            _busy.Add(participantId);
        }

        var attempts = 0;
        try
        {
            while (true)
            {
                attempts++;
                try
                {
                    if (player is not null)
                    {
                        var runner = new AgentTurnRunner(_campaign, _session, player);
                        var action = await runner.ReactAsync(participantId, signal, cancellationToken)
                            .ConfigureAwait(false);
                        return RecordProgress(new(eventKey, participantId, action is null
                            ? OrchestratedTurnStatus.NoAction
                            : action.Accepted ? OrchestratedTurnStatus.Applied
                                : OrchestratedTurnStatus.Rejected,
                            attempts, Action: action), fingerprint, initialRevision);
                    }

                    var proposal = await master!.ProposeAsync(
                        participantId, signal, cancellationToken).ConfigureAwait(false);
                    return RecordProgress(new(eventKey, participantId,
                        proposal is null ? OrchestratedTurnStatus.NoAction : OrchestratedTurnStatus.Proposal,
                        attempts, Proposal: proposal), fingerprint, initialRevision);
                }
                catch (NoChargeTransientModelException) when (attempts == 1 &&
                    !cancellationToken.IsCancellationRequested)
                {
                    // Exactly one extra try, but only for a provider-confirmed
                    // no-charge/no-effect attempt, never for timeout/unknown billing.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Record(new(eventKey, participantId, OrchestratedTurnStatus.Failed, attempts, "cancelled"));
            throw;
        }
        catch
        {
            return RecordProgress(new(eventKey, participantId, OrchestratedTurnStatus.Failed, attempts,
                "provider-or-agent-failure"), fingerprint, initialRevision);
        }
        finally
        {
            lock (_gate) _busy.Remove(participantId);
        }
    }

    private static string TriggerFingerprint(AgentWakeSignal signal)
    {
        var payload = (signal.Kind ?? "") + "\\n" + (signal.Detail ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private string RevisionFor(ParticipantId id, bool isMaster) => isMaster
        ? _campaign.CreateGameMasterView(id).Revision.ToString()
        : _campaign.Clock.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private OrchestratedTurnReceipt RecordProgress(OrchestratedTurnReceipt receipt,
        string fingerprint, string before)
    {
        var after = RevisionFor(receipt.ParticipantId, _gameMasters.ContainsKey(receipt.ParticipantId));
        lock (_gate)
        {
            if (before == after && receipt.Status is
                (OrchestratedTurnStatus.NoAction or OrchestratedTurnStatus.Proposal or
                 OrchestratedTurnStatus.Rejected or OrchestratedTurnStatus.Failed))
                _noProgress[receipt.ParticipantId] = (fingerprint, after);
            else
                _noProgress.Remove(receipt.ParticipantId);
        }
        return Record(receipt);
    }

    private OrchestratedTurnReceipt Record(OrchestratedTurnReceipt receipt)
    {
        lock (_gate)
        {
            _receipts.Add(receipt with { Proposal = null, Action = null });
            return receipt;
        }
    }

    private void RequireSeat(ParticipantId id, SeatRole role)
    {
        if (!_campaign.TryGetParticipant(id, out var participant) ||
            participant.Controller != ControllerKind.Agent || participant.Role != role)
            throw new InvalidOperationException("Registered seat must be an agent of the requested role.");
    }
}
