using TabletopRpg.Core;

namespace TabletopRpg.PlayerAgent;

/// <summary>
/// Character-authored behavior, not world truth. Knowledge and memories remain
/// canonical in Campaign and must be projected from the current PlayerView.
/// This profile is transient in RPG-011: no campaign schema change.
/// </summary>
public sealed class PlayerAgentProfile
{
    public PlayerAgentProfile(
        ParticipantId participantId,
        CharacterId characterId,
        string persona,
        IEnumerable<string>? goals = null,
        IEnumerable<PlayerRelationship>? relationships = null)
    {
        if (participantId.Value == Guid.Empty)
            throw new ArgumentException("Participant id is required.", nameof(participantId));
        if (characterId.Value == Guid.Empty)
            throw new ArgumentException("Character id is required.", nameof(characterId));
        ParticipantId = participantId;
        CharacterId = characterId;
        Persona = RequireText(persona, nameof(persona), 2_000);

        var goalsCopy = (goals ?? []).Select(x => RequireText(x, nameof(goals), 300)).ToArray();
        if (goalsCopy.Length > 8)
            throw new ArgumentException("Maximum eight goals.", nameof(goals));
        Goals = Array.AsReadOnly(goalsCopy);

        var relationshipsCopy = (relationships ?? []).ToArray();
        if (relationshipsCopy.Length > 16 || relationshipsCopy.Any(r => r is null))
            throw new ArgumentException("Invalid relationship list.", nameof(relationships));
        Relationships = Array.AsReadOnly(relationshipsCopy);
    }

    public ParticipantId ParticipantId { get; }
    public CharacterId CharacterId { get; }
    public string Persona { get; }
    public IReadOnlyList<string> Goals { get; }
    public IReadOnlyList<PlayerRelationship> Relationships { get; }

    internal static string RequireText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException("Nonempty bounded text is required.", name);
        return value.Trim();
    }
}

public sealed record PlayerRelationship
{
    public PlayerRelationship(string person, string attitude)
    {
        Person = PlayerAgentProfile.RequireText(person, nameof(person), 100);
        Attitude = PlayerAgentProfile.RequireText(attitude, nameof(attitude), 300);
    }

    public string Person { get; }
    public string Attitude { get; }
}
