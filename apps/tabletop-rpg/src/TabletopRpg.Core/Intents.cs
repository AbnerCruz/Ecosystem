namespace TabletopRpg.Core;

public abstract record GameIntent(
    ParticipantId ParticipantId,
    CharacterId CharacterId);

public sealed record SpeakIntent(
    ParticipantId ParticipantId,
    CharacterId CharacterId,
    string Text)
    : GameIntent(ParticipantId, CharacterId);

public sealed record SkillCheckIntent(
    ParticipantId ParticipantId,
    CharacterId CharacterId,
    string Attribute,
    int Difficulty,
    string Description)
    : GameIntent(ParticipantId, CharacterId);

public sealed record ActionResolution(
    bool Accepted,
    bool? Success,
    string Message,
    SkillCheckResult? Check = null)
{
    public static ActionResolution Reject(string message) =>
        new(false, null, message);
}
