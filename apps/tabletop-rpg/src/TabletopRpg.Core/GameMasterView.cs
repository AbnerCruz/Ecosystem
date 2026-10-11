namespace TabletopRpg.Core;

/// <summary>
/// Snapshot visible only to a GameMaster seat. PlayerView is a separate
/// projection and never inherits the world's private facts from this type.
/// </summary>
public sealed record GameMasterCharacter(CharacterId Id, string Name);

public sealed record GameMasterView(
    CampaignId CampaignId,
    ParticipantId ParticipantId,
    long Revision,
    string CampaignName,
    long FictionMinutes,
    SceneId? ActiveSceneId,
    IReadOnlyList<WorldFact> WorldFacts,
    IReadOnlyList<CampaignScene> Scenes,
    IReadOnlyList<CampaignQuest> Quests,
    IReadOnlyList<GameMasterCharacter> Characters,
    IReadOnlyList<GameEvent> RecentEvents);

public sealed partial class Campaign
{
    public GameMasterView CreateGameMasterView(ParticipantId participantId)
    {
        if (!_participants.TryGetValue(participantId, out var participant) ||
            participant.Role != SeatRole.GameMaster)
            throw new InvalidOperationException("A GameMaster seat is required.");

        return new GameMasterView(
            Id,
            participant.Id,
            Clock,
            Name,
            FictionMinutes,
            ActiveSceneId,
            Array.AsReadOnly(_worldFacts.Values.OrderBy(f => f.Id.Value).ToArray()),
            Array.AsReadOnly(Scenes.ToArray()),
            Array.AsReadOnly(Quests.ToArray()),
            Array.AsReadOnly(_characters.Values
                .OrderBy(c => c.Id.Value)
                .Select(c => new GameMasterCharacter(c.Id, c.Name)).ToArray()),
            Array.AsReadOnly(_events.TakeLast(24).ToArray()));
    }

    /// <summary>
    /// Host-approved GM actions only. The model returns proposals but never has
    /// a reference to Campaign; the trusted caller must invoke this separately.
    /// </summary>
    public void PublishGameMasterNarration(
        ParticipantId participantId,
        long expectedRevision,
        string narration)
    {
        RequireGameMasterAuthorization(participantId, expectedRevision);
        if (string.IsNullOrWhiteSpace(narration) || narration.Length > 1800)
            throw new ArgumentException("Narration must contain 1..1800 characters.", nameof(narration));
        RecordEvent("gm-narration", participantId, null, narration.Trim());
    }

    public void RevealGameMasterFact(
        ParticipantId participantId,
        long expectedRevision,
        CharacterId targetCharacter,
        FactId factId)
    {
        RequireGameMasterAuthorization(participantId, expectedRevision);
        if (!_characters.ContainsKey(targetCharacter))
            throw new KeyNotFoundException("Reveal target is not a campaign character.");
        if (!_worldFacts.ContainsKey(factId))
            throw new KeyNotFoundException("Reveal fact is not in the campaign.");
        RevealFactTo(targetCharacter, factId, "game-master");
    }

    private void RequireGameMasterAuthorization(ParticipantId participantId, long expectedRevision)
    {
        if (!_participants.TryGetValue(participantId, out var participant) ||
            participant.Role != SeatRole.GameMaster)
            throw new InvalidOperationException("GameMaster authorization is required.");
        if (expectedRevision != Clock)
            throw new InvalidOperationException("Stale GameMaster proposal; refresh campaign state.");
    }
}
