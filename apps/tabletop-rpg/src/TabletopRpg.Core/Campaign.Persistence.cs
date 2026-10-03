namespace TabletopRpg.Core;

public sealed partial class Campaign
{
    internal CampaignState CaptureState()
    {
        var characters = _characters.Values
            .OrderBy(character => character.Id.Value)
            .Select(character => new CharacterState(
                character.Id,
                character.Name,
                CampaignState.ReadOnlyAttributes(
                    character.Attributes.OrderBy(
                        pair => pair.Key,
                        StringComparer.OrdinalIgnoreCase))))
            .ToArray();

        var participants = _participants.Values
            .OrderBy(participant => participant.Id.Value)
            .Select(participant => new ParticipantState(
                participant.Id,
                participant.Name,
                participant.Controller,
                participant.Role,
                participant.ControlledCharacterIds
                    .OrderBy(id => id.Value)
                    .ToArray()))
            .ToArray();

        var worldFacts = _worldFacts.Values
            .OrderBy(fact => fact.Id.Value)
            .ToArray();

        var perspectives = _perspectives.Values
            .OrderBy(perspective => perspective.CharacterId.Value)
            .Select(perspective => new PerspectiveState(
                perspective.CharacterId,
                perspective.Knowledge.ToArray(),
                perspective.Memories.ToArray()))
            .ToArray();

        return new CampaignState(
            Id,
            Name,
            _clock,
            characters,
            participants,
            worldFacts,
            perspectives,
            _events.ToArray(),
            _sessions.ToArray(),
            _activeSessionId);
    }

    internal static Campaign Restore(CampaignState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Clock < 0) throw new CampaignStateException("Campaign clock cannot be negative.");

        Campaign campaign;
        try
        {
            campaign = new Campaign(state.Id, state.Name);
        }
        catch (Exception exception) when (exception is ArgumentException)
        {
            throw new CampaignStateException(exception.Message);
        }

        foreach (var characterState in state.Characters)
        {
            try
            {
                campaign.AddCharacter(new Character(
                    characterState.Id,
                    characterState.Name,
                    characterState.Attributes));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw new CampaignStateException(exception.Message);
            }
        }

        foreach (var participantState in state.Participants)
        {
            if (participantState.ControlledCharacterIds.Count
                != participantState.ControlledCharacterIds.Distinct().Count())
            {
                throw new CampaignStateException(
                    $"Participant {participantState.Id} contains duplicate controlled character ids.");
            }

            try
            {
                campaign.AddParticipant(new Participant(
                    participantState.Id,
                    participantState.Name,
                    participantState.Controller,
                    participantState.Role,
                    participantState.ControlledCharacterIds));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw new CampaignStateException(exception.Message);
            }
        }

        foreach (var fact in state.WorldFacts)
        {
            if (fact.Id.Value == Guid.Empty)
                throw new CampaignStateException("World fact id cannot be empty.");
            if (string.IsNullOrWhiteSpace(fact.Statement))
                throw new CampaignStateException("World fact statement cannot be empty.");
            if (!campaign._worldFacts.TryAdd(fact.Id, new WorldFact(fact.Id, fact.Statement.Trim())))
                throw new CampaignStateException($"Duplicate world fact {fact.Id}.");
        }

        var seenPerspectives = new HashSet<CharacterId>();
        foreach (var perspectiveState in state.Perspectives)
        {
            if (!seenPerspectives.Add(perspectiveState.CharacterId))
                throw new CampaignStateException($"Duplicate perspective {perspectiveState.CharacterId}.");
            if (!campaign._perspectives.TryGetValue(perspectiveState.CharacterId, out var perspective))
                throw new CampaignStateException(
                    $"Perspective references unknown character {perspectiveState.CharacterId}.");

            foreach (var claim in perspectiveState.Knowledge)
            {
                ValidateKnowledgeClaim(campaign, state.Clock, claim);
                perspective.Learn(claim);
            }

            foreach (var memory in perspectiveState.Memories)
            {
                if (string.IsNullOrWhiteSpace(memory.Summary))
                    throw new CampaignStateException("Memory summary cannot be empty.");
                ValidateLogicalTime(state.Clock, memory.RememberedAt, "Memory");
                perspective.Remember(memory);
            }
        }

        if (!seenPerspectives.SetEquals(campaign._characters.Keys))
            throw new CampaignStateException("Every character must have exactly one perspective.");

        long previousEvent = 0;
        foreach (var gameEvent in state.Events)
        {
            ValidateLogicalTime(state.Clock, gameEvent.Sequence, "Game event");
            if (gameEvent.Sequence <= previousEvent)
                throw new CampaignStateException("Game events must be strictly ordered by sequence.");
            if (string.IsNullOrWhiteSpace(gameEvent.Kind) || string.IsNullOrWhiteSpace(gameEvent.Text))
                throw new CampaignStateException("Game event kind and text are required.");
            if (gameEvent.ParticipantId is { } participantId && !campaign._participants.ContainsKey(participantId))
                throw new CampaignStateException($"Game event references unknown participant {participantId}.");
            if (gameEvent.CharacterId is { } characterId && !campaign._characters.ContainsKey(characterId))
                throw new CampaignStateException($"Game event references unknown character {characterId}.");

            campaign._events.Add(gameEvent);
            previousEvent = gameEvent.Sequence;
        }

        var openSessions = new List<SessionId>();
        long? previousSessionEnd = null;
        var sawOpenSession = false;
        foreach (var session in state.Sessions)
        {
            if (session.Id.Value == Guid.Empty)
                throw new CampaignStateException("Session id cannot be empty.");
            if (string.IsNullOrWhiteSpace(session.Name))
                throw new CampaignStateException("Session name cannot be empty.");
            if (campaign._sessions.Any(existing => existing.Id == session.Id))
                throw new CampaignStateException($"Duplicate session {session.Id}.");

            ValidateLogicalTime(state.Clock, session.StartedAt, "Session start");
            if (sawOpenSession)
                throw new CampaignStateException("An open session must be the final session.");
            if (previousSessionEnd is { } previousEnd && session.StartedAt <= previousEnd)
                throw new CampaignStateException("Campaign sessions cannot overlap or be out of order.");

            if (session.EndedAt is { } endedAt)
            {
                ValidateLogicalTime(state.Clock, endedAt, "Session end");
                if (endedAt < session.StartedAt)
                    throw new CampaignStateException("Session cannot end before it starts.");
                previousSessionEnd = endedAt;
            }
            else
            {
                openSessions.Add(session.Id);
                sawOpenSession = true;
            }

            campaign._sessions.Add(session with { Name = session.Name.Trim() });
        }

        if (state.ActiveSessionId is { } activeSessionId)
        {
            if (openSessions.Count != 1 || openSessions[0] != activeSessionId)
                throw new CampaignStateException("Active session does not match the single open session.");
            campaign._activeSessionId = activeSessionId;
        }
        else if (openSessions.Count != 0)
        {
            throw new CampaignStateException("An open session requires ActiveSessionId.");
        }

        campaign._clock = state.Clock;
        return campaign;
    }

    private static void ValidateKnowledgeClaim(Campaign campaign, long clock, KnowledgeClaim claim)
    {
        if (string.IsNullOrWhiteSpace(claim.Statement))
            throw new CampaignStateException("Knowledge statement cannot be empty.");
        if (string.IsNullOrWhiteSpace(claim.Source))
            throw new CampaignStateException("Knowledge source cannot be empty.");
        if (double.IsNaN(claim.Confidence) || double.IsInfinity(claim.Confidence)
            || claim.Confidence is < 0.0 or > 1.0)
            throw new CampaignStateException("Knowledge confidence must be between 0 and 1.");
        ValidateLogicalTime(clock, claim.LearnedAt, "Knowledge");

        if (claim.FactId is { } factId && !campaign._worldFacts.ContainsKey(factId))
            throw new CampaignStateException($"Knowledge references unknown world fact {factId}.");
    }

    private static void ValidateLogicalTime(long clock, long value, string label)
    {
        if (value <= 0 || value > clock)
            throw new CampaignStateException($"{label} logical time {value} is outside campaign clock {clock}.");
    }
}
