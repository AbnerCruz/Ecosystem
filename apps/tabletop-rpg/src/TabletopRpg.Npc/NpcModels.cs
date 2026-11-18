using TabletopRpg.Core;

namespace TabletopRpg.Npc;

public enum NpcDisposition
{
    Friendly,
    Cautious,
    Hostile
}

public enum NpcInteractionKind
{
    Greet,
    Ask,
    RequestHelp,
    Threaten
}

public enum NpcReactionKind
{
    Welcome,
    Listen,
    Help,
    Refuse,
    Warn
}

/// <summary>
/// NPCs are campaign characters, not participants or Player Agents.
/// Their explicit GM controller must also control the CharacterId in Core.
/// Private NPC behavior is currently transient: no persistence/schema change.
/// </summary>
public sealed class NpcProfile
{
    public NpcProfile(CharacterId characterId, ParticipantId gameMasterId,
        SceneId sceneId, string role, string motivation, NpcDisposition disposition)
    {
        if (characterId.Value == Guid.Empty || gameMasterId.Value == Guid.Empty ||
            sceneId.Value == Guid.Empty)
            throw new ArgumentException("NPC identities and scene must be nonempty.");
        if (!Enum.IsDefined(disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition));

        CharacterId = characterId;
        GameMasterId = gameMasterId;
        SceneId = sceneId;
        Role = RequireText(role, nameof(role), 100);
        Motivation = RequireText(motivation, nameof(motivation), 300);
        Disposition = disposition;
    }

    public CharacterId CharacterId { get; }
    public ParticipantId GameMasterId { get; }
    public SceneId SceneId { get; }
    public string Role { get; }
    public string Motivation { get; }
    public NpcDisposition Disposition { get; }

    private static string RequireText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException("A nonempty bounded value is required.", name);
        return value.Trim();
    }
}

public sealed record NpcSnapshot(
    CharacterId CharacterId,
    SceneId SceneId,
    NpcDisposition Disposition,
    string Role,
    string Motivation,
    int Trust,
    int Alertness,
    long LastEventSequence,
    long Version);

public sealed record NpcInteraction(
    CharacterId NpcCharacterId,
    ParticipantId ActorParticipantId,
    CharacterId ActorCharacterId,
    NpcInteractionKind Kind);

/// <summary>
/// Private, one-use dialogue suggestion. Applying this is a separate, explicit
/// host action; only SessionEngine is allowed to publish the speech event.
/// </summary>
public sealed record NpcReaction(
    Guid ProposalId,
    CharacterId NpcCharacterId,
    ParticipantId GameMasterId,
    GameMasterRevision CampaignRevision,
    long StateVersion,
    NpcReactionKind Kind,
    string Text,
    int NextTrust,
    int NextAlertness);
