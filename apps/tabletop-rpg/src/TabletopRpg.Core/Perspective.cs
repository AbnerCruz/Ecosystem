namespace TabletopRpg.Core;

public sealed record WorldFact(FactId Id, string Statement);

public sealed record KnowledgeClaim(
    FactId? FactId,
    string Statement,
    string Source,
    double Confidence,
    long LearnedAt);

public sealed record MemoryEntry(
    string Summary,
    long RememberedAt);

public sealed class CharacterPerspective
{
    private readonly List<KnowledgeClaim> _knowledge = [];
    private readonly List<MemoryEntry> _memories = [];

    internal CharacterPerspective(CharacterId characterId)
    {
        CharacterId = characterId;
    }

    public CharacterId CharacterId { get; }
    public IReadOnlyList<KnowledgeClaim> Knowledge => _knowledge.AsReadOnly();
    public IReadOnlyList<MemoryEntry> Memories => _memories.AsReadOnly();

    internal void Learn(KnowledgeClaim claim) => _knowledge.Add(claim);
    internal void Remember(MemoryEntry memory) => _memories.Add(memory);
}

public sealed record CharacterView(
    CharacterId CharacterId,
    string Name,
    IReadOnlyDictionary<string, int> Attributes,
    IReadOnlyList<KnowledgeClaim> Knowledge,
    IReadOnlyList<MemoryEntry> Memories);

public sealed record PlayerView(
    CampaignId CampaignId,
    ParticipantId ParticipantId,
    string ParticipantName,
    SeatRole Role,
    IReadOnlyList<CharacterView> Characters);
