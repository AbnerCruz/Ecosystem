namespace TabletopRpg.Core;

public sealed record AgentWakeSignal(
    string Kind,
    string? Detail = null);

public interface IAgentDecisionSource
{
    Task<GameIntent?> DecideAsync(
        PlayerView view,
        AgentWakeSignal signal,
        CancellationToken cancellationToken = default);
}

public sealed class AgentTurnRunner
{
    private readonly Campaign _campaign;
    private readonly SessionEngine _session;
    private readonly IAgentDecisionSource _source;

    public AgentTurnRunner(Campaign campaign, SessionEngine session, IAgentDecisionSource source)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async Task<ActionResolution?> ReactAsync(
        ParticipantId participantId,
        AgentWakeSignal signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (!_campaign.TryGetParticipant(participantId, out var participant))
            throw new KeyNotFoundException($"Participant {participantId} does not exist.");

        if (participant.Controller != ControllerKind.Agent)
            throw new InvalidOperationException("Only agent-controlled participants can use AgentTurnRunner.");

        var view = _campaign.CreatePlayerView(participant.Id);
        var intent = await _source.DecideAsync(view, signal, cancellationToken).ConfigureAwait(false);

        if (intent is null) return null;

        if (intent.ParticipantId != participant.Id)
            return ActionResolution.Reject("Agent returned an intent for another participant.");

        return _session.Execute(intent);
    }
}
