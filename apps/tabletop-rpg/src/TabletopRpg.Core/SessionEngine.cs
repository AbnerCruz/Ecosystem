namespace TabletopRpg.Core;

public sealed class SessionEngine
{
    private readonly Campaign _campaign;
    private readonly IRuleSystem _rules;
    private readonly IDiceRoller _dice;
    private readonly CombatEncounter? _combat;

    public SessionEngine(Campaign campaign, IRuleSystem rules, IDiceRoller dice, CombatEncounter? combat = null)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _dice = dice ?? throw new ArgumentNullException(nameof(dice));
        if (combat is not null && !ReferenceEquals(combat.Campaign, _campaign))
            throw new ArgumentException("Combat must belong to this campaign.", nameof(combat));
        _combat = combat;
    }

    public ActionResolution Execute(GameIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!_campaign.TryGetParticipant(intent.ParticipantId, out var participant))
            return ActionResolution.Reject("Unknown participant.");

        if (!_campaign.TryGetCharacter(intent.CharacterId, out var character))
            return ActionResolution.Reject("Unknown character.");

        if (!participant.Controls(character.Id))
            return ActionResolution.Reject("Participant does not control this character.");

        return intent switch
        {
            SpeakIntent speak => ResolveSpeak(participant, character, speak),
            SkillCheckIntent check => ResolveSkillCheck(participant, character, check),
            AttackIntent attack => _combat is null
                ? ActionResolution.Reject("No combat encounter is active.")
                : _combat.Attack(attack, _dice),
            EndCombatTurnIntent endTurn => _combat is null
                ? ActionResolution.Reject("No combat encounter is active.")
                : _combat.EndTurn(endTurn),
            _ => ActionResolution.Reject($"Unsupported intent type: {intent.GetType().Name}.")
        };
    }

    private ActionResolution ResolveSpeak(Participant participant, Character character, SpeakIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.Text))
            return ActionResolution.Reject("Speech cannot be empty.");

        var text = intent.Text.Trim();
        _campaign.RecordEvent("speech", participant.Id, character.Id, $"{character.Name}: {text}");
        return new ActionResolution(true, true, text);
    }

    private ActionResolution ResolveSkillCheck(
        Participant participant,
        Character character,
        SkillCheckIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.Attribute))
            return ActionResolution.Reject("Skill check attribute is required.");

        if (intent.Difficulty < 1)
            return ActionResolution.Reject("Skill check difficulty must be positive.");

        if (string.IsNullOrWhiteSpace(intent.Description))
            return ActionResolution.Reject("Skill check description is required.");

        var result = _rules.ResolveSkillCheck(character, intent, _dice);
        var outcome = result.Success ? "success" : "failure";

        _campaign.RecordEvent(
            "skill-check",
            participant.Id,
            character.Id,
            $"{character.Name}: {intent.Description.Trim()} => d20 {result.NaturalRoll} + {result.Modifier} = {result.Total} vs {result.Difficulty}: {outcome}.");

        return new ActionResolution(true, result.Success, outcome, result);
    }
}
