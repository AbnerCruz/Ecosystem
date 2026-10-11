using TabletopRpg.Core;
using TabletopRpg.Director;
using TabletopRpg.Npc;
using TabletopRpg.PlayerAgent;

namespace TabletopRpg.QuickStart;

public enum QuickStartTheme
{
    Fantasy,
    Mystery,
    Frontier
}

/// <summary>
/// Choice-free startup uses Fantasy and default player identity.
/// These are nonsecret choices, not a model or provider selection.
/// </summary>
public sealed record QuickStartOptions(
    QuickStartTheme Theme = QuickStartTheme.Fantasy,
    string HeroName = "Viajante",
    string? CampaignName = null);

/// <summary>Safe menu projection. No secret facts, cues or model prompts.</summary>
public sealed record QuickStartChoice(
    QuickStartTheme Theme,
    string Name,
    string PublicPitch);

/// <summary>
/// Newly built, ready-to-play solo campaign plus transient behavior profiles.
/// The canonical Campaign alone is what the v2 persistence codec saves.
/// </summary>
public sealed record QuickStartResult(
    Campaign Campaign,
    ParticipantId HumanParticipantId,
    CharacterId HeroCharacterId,
    ParticipantId CompanionParticipantId,
    CharacterId CompanionCharacterId,
    ParticipantId GameMasterParticipantId,
    CharacterId NpcCharacterId,
    SceneId StartingSceneId,
    QuestId StartingQuestId,
    PlayerAgentProfile CompanionProfile,
    NpcProfile NpcProfile,
    IReadOnlyList<DirectorThread> DirectorThreads,
    string OpeningText);
