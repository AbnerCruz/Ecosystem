namespace TabletopRpg.Core;

/// <summary>
/// RPG-004: a host-controlled combat encounter. It is deliberately transient:
/// campaign HP and audit events persist, turn order/initiative do not until RPG-032.
/// This is a ruleset-neutral engine; an AI can only submit GameIntent.
/// </summary>
public sealed class CombatEncounter
{
    public const int MaxCombatants = 32;
    private readonly object _gate = new();
    private long _version;
    private readonly Campaign _campaign;
    private readonly ICombatRules _rules;
    private readonly CharacterId[] _turnOrder;
    private int _turnIndex;
    private int _round = 1;
    private bool _actionSpent;
    private bool _finished;

    public CombatEncounter(Campaign campaign, ICombatRules rules, IEnumerable<CharacterId> orderedCombatants)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        ArgumentNullException.ThrowIfNull(orderedCombatants);
        if (_campaign.ActiveSessionId is null)
            throw new InvalidOperationException("Combat requires an active campaign session.");

        // Materialize once: caller cannot reorder the encounter after validation.
        _turnOrder = orderedCombatants.Take(MaxCombatants + 1).ToArray();
        if (_turnOrder.Length < 2 || _turnOrder.Length > MaxCombatants ||
            _turnOrder.Distinct().Count() != _turnOrder.Length)
            throw new ArgumentException("Combat must have 2..32 distinct characters.", nameof(orderedCombatants));

        foreach (var id in _turnOrder)
        {
            if (!_campaign.TryGetCharacter(id, out _) || HitPoints(id) is not > 0)
                throw new ArgumentException("All combatants must exist and have positive hp.", nameof(orderedCombatants));
        }
        _campaign.RecordEvent("combat-start", null, null, _turnOrder.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    internal Campaign Campaign => _campaign;
    public IReadOnlyList<CharacterId> TurnOrder => Array.AsReadOnly(_turnOrder);
    public CharacterId ActiveCharacter => _turnOrder[_turnIndex];
    public int Round => _round;
    public bool ActionSpent => _actionSpent;
    public bool IsFinished => _finished;

    public ActionResolution Attack(AttackIntent intent, IDiceRoller dice)
    {
        lock (_gate) return AttackCore(intent, dice);
    }

    private ActionResolution AttackCore(AttackIntent intent, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(dice);
        var expectedVersion = _version;
        if (!CanAct(intent.CharacterId, out var reason)) return ActionResolution.Reject(reason);
        if (_actionSpent) return ActionResolution.Reject("Combat action already spent in this turn.");
        if (intent.TargetId == intent.CharacterId || !_turnOrder.Contains(intent.TargetId))
            return ActionResolution.Reject("Attack target must be a different active combatant.");
        if (!_campaign.TryGetCharacter(intent.CharacterId, out var attacker) ||
            !_campaign.TryGetCharacter(intent.TargetId, out var defender) ||
            HitPoints(intent.TargetId) is not > 0)
            return ActionResolution.Reject("Combat target is not able to fight.");
        if (!ValidAttackKey(intent.AttackKey))
            return ActionResolution.Reject("Attack key must be a lowercase slug.");

        // Rules are trusted application code; no AI-provided damage or die rolls.
        // Rejections are fail-closed: invalid rule output cannot mutate HP/turns.
        CombatStrike strike;
        try
        {
            strike = _rules.ResolveAttack(attacker, defender, intent.AttackKey, dice);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException)
        {
            return ActionResolution.Reject("Combat rules could not resolve this attack.");
        }
        // A ruleset callback may reenter this encounter through its trusted
        // host. Do not let the outer action overwrite a committed nested turn.
        if (_version != expectedVersion || !CanAct(intent.CharacterId, out _) || _actionSpent)
            return ActionResolution.Reject("Combat turn changed while resolving rules.");
        if (strike is null || strike.Roll < 1 || strike.Roll > 1000000 ||
            strike.Damage is < 0 or > 1000000 || (!strike.Hit && strike.Damage != 0))
            return ActionResolution.Reject("Combat rules returned an invalid result.");
        var hp = HitPoints(intent.TargetId)!.Value;
        var damage = strike.Hit ? Math.Min(strike.Damage, hp) : 0;

        if (damage > 0) _campaign.ChangeResource(intent.TargetId, "hp", -damage);
        _campaign.RecordEvent(
            "combat-attack", intent.ParticipantId, intent.CharacterId,
            $"{intent.CharacterId.Value:N}>{intent.TargetId.Value:N}:{intent.AttackKey}:{strike.Roll}:{(strike.Hit ? "hit" : "miss")}:{damage}");
        _actionSpent = true;
        _version++;

        if (_turnOrder.Count(x => HitPoints(x) is > 0) <= 1)
        {
            _finished = true;
            _campaign.RecordEvent("combat-finished", null, null, "last-combatant-standing");
        }
        return new ActionResolution(true, strike.Hit, strike.Hit ? "hit" : "miss",
            Combat: new CombatStrike(strike.Hit, strike.Roll, damage));
    }

    public ActionResolution EndTurn(EndCombatTurnIntent intent)
    {
        lock (_gate) return EndTurnCore(intent);
    }

    private ActionResolution EndTurnCore(EndCombatTurnIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CanAct(intent.CharacterId, out var reason)) return ActionResolution.Reject(reason);

        // Advance only to a living combatant. No background clock or LLM polling.
        for (var offset = 1; offset <= _turnOrder.Length; offset++)
        {
            var next = (_turnIndex + offset) % _turnOrder.Length;
            if (HitPoints(_turnOrder[next]) is not > 0) continue;
            if (next <= _turnIndex)
            {
                if (_round == int.MaxValue) return ActionResolution.Reject("Maximum round reached.");
                _round++;
            }
            _turnIndex = next;
            _actionSpent = false;
            _version++;
            _campaign.RecordEvent("combat-next-turn", intent.ParticipantId, intent.CharacterId,
                $"{_round}:{ActiveCharacter.Value:N}");
            return new ActionResolution(true, true, "next-turn");
        }
        return ActionResolution.Reject("No living combatant.");
    }

    private bool CanAct(CharacterId actor, out string reason)
    {
        if (_finished)
        {
            reason = "Combat encounter has finished.";
            return false;
        }
        if (_campaign.ActiveSessionId is null)
        {
            reason = "Combat requires an active campaign session.";
            return false;
        }
        if (actor != ActiveCharacter)
        {
            reason = "It is not this character's combat turn.";
            return false;
        }
        if (HitPoints(actor) is not > 0)
        {
            reason = "Character is not able to fight.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private int? HitPoints(CharacterId id)
    {
        foreach (var resource in _campaign.Resources)
            if (resource.CharacterId == id && resource.Key == "hp") return resource.Current;
        return null;
    }

    private static bool ValidAttackKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= 128 &&
        key.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');
}

public sealed record CombatStrike(bool Hit, int Roll, int Damage);

/// <summary>
/// A trusted ruleset supplies the interpretation, not the authoritative state.
/// Dice must come from the supplied runtime IDiceRoller.
/// </summary>
public interface ICombatRules
{
    CombatStrike ResolveAttack(Character attacker, Character defender, string attackKey, IDiceRoller dice);
}

/// <summary>A minimal optional d20 example, not a required or universal RPG ruleset.</summary>
public sealed class BasicD20CombatRules : ICombatRules
{
    public CombatStrike ResolveAttack(Character attacker, Character defender, string attackKey, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(dice);
        if (attackKey != "strike") throw new ArgumentException("Unknown attack key.", nameof(attackKey));
        var roll = dice.Roll(20);
        var hit = checked(roll + attacker.AttributeModifier("strength")) >=
            checked(10 + defender.AttributeModifier("defense"));
        var damage = hit ? Math.Clamp(checked(dice.Roll(6) + attacker.AttributeModifier("strength")), 1, 1000000) : 0;
        return new CombatStrike(hit, roll, damage);
    }
}
