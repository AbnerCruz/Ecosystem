using System.Collections.ObjectModel;

namespace TabletopRpg.Core;

public sealed record GameEvent(
    long Sequence,
    string Kind,
    ParticipantId? ParticipantId,
    CharacterId? CharacterId,
    string Text);

public sealed partial class Campaign
{
    private readonly Dictionary<CharacterId, Character> _characters = [];
    private readonly Dictionary<ParticipantId, Participant> _participants = [];
    private readonly Dictionary<FactId, WorldFact> _worldFacts = [];
    private readonly Dictionary<CharacterId, CharacterPerspective> _perspectives = [];
    private readonly List<GameEvent> _events = [];
    private readonly List<CampaignSession> _sessions = [];
    private SessionId? _activeSessionId;
    private long _clock;

    public Campaign(CampaignId id, string name)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Campaign id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Campaign name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
    }

    public CampaignId Id { get; }
    public string Name { get; }
    public long Clock => _clock;
    public IReadOnlyList<GameEvent> Events => _events.AsReadOnly();
    public IReadOnlyList<CampaignSession> Sessions => _sessions.AsReadOnly();
    public SessionId? ActiveSessionId => _activeSessionId;

    public void AddCharacter(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!_characters.TryAdd(character.Id, character))
            throw new InvalidOperationException($"Character {character.Id} already exists.");

        _perspectives.Add(character.Id, new CharacterPerspective(character.Id));
    }

    public void AddParticipant(Participant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);

        foreach (var characterId in participant.ControlledCharacterIds)
        {
            if (!_characters.ContainsKey(characterId))
                throw new InvalidOperationException(
                    $"Participant {participant.Id} cannot control unknown character {characterId}.");
        }

        if (!_participants.TryAdd(participant.Id, participant))
            throw new InvalidOperationException($"Participant {participant.Id} already exists.");
    }

    public WorldFact AddWorldFact(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            throw new ArgumentException("World fact statement is required.", nameof(statement));

        var fact = new WorldFact(FactId.New(), statement.Trim());
        _worldFacts.Add(fact.Id, fact);
        return fact;
    }

    public void RevealFactTo(CharacterId characterId, FactId factId, string source, double confidence = 1.0)
    {
        var perspective = GetPerspective(characterId);
        if (!_worldFacts.TryGetValue(factId, out var fact))
            throw new KeyNotFoundException($"World fact {factId} does not exist.");

        perspective.Learn(new KnowledgeClaim(
            fact.Id,
            fact.Statement,
            NormalizeSource(source),
            NormalizeConfidence(confidence),
            NextClock()));
    }

    public void AddBelief(CharacterId characterId, string statement, string source, double confidence)
    {
        if (string.IsNullOrWhiteSpace(statement))
            throw new ArgumentException("Belief statement is required.", nameof(statement));

        GetPerspective(characterId).Learn(new KnowledgeClaim(
            null,
            statement.Trim(),
            NormalizeSource(source),
            NormalizeConfidence(confidence),
            NextClock()));
    }

    public void Remember(CharacterId characterId, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Memory summary is required.", nameof(summary));

        GetPerspective(characterId).Remember(new MemoryEntry(summary.Trim(), NextClock()));
    }

    public CampaignSession StartSession(SessionId id, string name)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Session id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Session name is required.", nameof(name));
        if (_activeSessionId is not null) throw new InvalidOperationException("A campaign session is already active.");
        if (_sessions.Any(session => session.Id == id))
            throw new InvalidOperationException($"Session {id} already exists.");

        var session = new CampaignSession(id, name.Trim(), NextClock(), null);
        _sessions.Add(session);
        _activeSessionId = id;
        return session;
    }

    public CampaignSession EndActiveSession()
    {
        if (_activeSessionId is not { } activeId)
            throw new InvalidOperationException("There is no active campaign session.");

        var index = _sessions.FindIndex(session => session.Id == activeId);
        if (index < 0)
            throw new InvalidOperationException("Active session state is inconsistent.");

        var ended = _sessions[index] with { EndedAt = NextClock() };
        _sessions[index] = ended;
        _activeSessionId = null;
        return ended;
    }

    public bool TryGetCharacter(CharacterId id, out Character character) =>
        _characters.TryGetValue(id, out character!);

    public bool TryGetParticipant(ParticipantId id, out Participant participant) =>
        _participants.TryGetValue(id, out participant!);

    public PlayerView CreatePlayerView(ParticipantId participantId)
    {
        if (!_participants.TryGetValue(participantId, out var participant))
            throw new KeyNotFoundException($"Participant {participantId} does not exist.");

        var characters = participant.ControlledCharacterIds
            .Select(id =>
            {
                var character = _characters[id];
                var perspective = _perspectives[id];
                return new CharacterView(
                    character.Id,
                    character.Name,
                    new ReadOnlyDictionary<string, int>(
                        character.Attributes.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value,
                            StringComparer.OrdinalIgnoreCase)),
                    perspective.Knowledge.ToArray(),
                    perspective.Memories.ToArray());
            })
            .ToArray();

        return new PlayerView(Id, participant.Id, participant.Name, participant.Role, characters);
    }

    internal long RecordEvent(
        string kind,
        ParticipantId? participantId,
        CharacterId? characterId,
        string text)
    {
        var sequence = NextClock();
        _events.Add(new GameEvent(sequence, kind, participantId, characterId, text));
        return sequence;
    }

    private CharacterPerspective GetPerspective(CharacterId characterId)
    {
        if (!_perspectives.TryGetValue(characterId, out var perspective))
            throw new KeyNotFoundException($"Character {characterId} does not exist.");

        return perspective;
    }

    private long NextClock() => ++_clock;

    private static string NormalizeSource(string source) =>
        string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();

    private static double NormalizeConfidence(double confidence)
    {
        if (double.IsNaN(confidence) || double.IsInfinity(confidence))
            throw new ArgumentOutOfRangeException(nameof(confidence));

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
