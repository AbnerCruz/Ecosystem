using System.Collections.ObjectModel;

namespace TabletopRpg.Core;

public sealed record CampaignSession(
    SessionId Id,
    string Name,
    long StartedAt,
    long? EndedAt);

internal sealed record CharacterState(
    CharacterId Id,
    string Name,
    IReadOnlyDictionary<string, int> Attributes);

internal sealed record ParticipantState(
    ParticipantId Id,
    string Name,
    ControllerKind Controller,
    SeatRole Role,
    IReadOnlyList<CharacterId> ControlledCharacterIds);

internal sealed record PerspectiveState(
    CharacterId CharacterId,
    IReadOnlyList<KnowledgeClaim> Knowledge,
    IReadOnlyList<MemoryEntry> Memories);

internal sealed record CampaignState(
    CampaignId Id,
    string Name,
    long Clock,
    IReadOnlyList<CharacterState> Characters,
    IReadOnlyList<ParticipantState> Participants,
    IReadOnlyList<WorldFact> WorldFacts,
    IReadOnlyList<PerspectiveState> Perspectives,
    IReadOnlyList<GameEvent> Events,
    IReadOnlyList<CampaignSession> Sessions,
    SessionId? ActiveSessionId)
{
    internal static IReadOnlyDictionary<string, int> ReadOnlyAttributes(
        IEnumerable<KeyValuePair<string, int>> attributes) =>
        new ReadOnlyDictionary<string, int>(
            attributes.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase));
}

internal sealed class CampaignStateException : Exception
{
    public CampaignStateException(string message) : base(message) { }
}
