using TabletopRpg.Persistence;

namespace TabletopRpg.Core.Tests;

public sealed class CombatRuntimeTests
{
    private sealed record Fixture(
        Campaign Campaign,
        CharacterId Hero,
        CharacterId Enemy,
        ParticipantId Human,
        ParticipantId Agent,
        CombatEncounter Encounter,
        SessionEngine Engine);

    private sealed class FixedDice(params int[] sequence) : IDiceRoller
    {
        private readonly Queue<int> _values = new(sequence);
        public int Calls { get; private set; }
        public int Roll(int sides)
        {
            Calls++;
            var value = _values.Dequeue();
            if (value < 1 || value > sides) throw new InvalidOperationException("Invalid dice sample.");
            return value;
        }
    }

    private sealed class FixedRules(CombatStrike outcome) : ICombatRules
    {
        public int Calls { get; private set; }
        public CombatStrike ResolveAttack(Character attacker, Character defender, string attackKey, IDiceRoller dice)
        {
            Calls++;
            return outcome;
        }
    }

    private static Fixture Create(ICombatRules? rules = null, IDiceRoller? dice = null, int hp = 10)
    {
        var campaign = new Campaign(CampaignId.New(), "Encounter");
        var hero = CharacterId.New();
        var enemy = CharacterId.New();
        var human = ParticipantId.New();
        var agent = ParticipantId.New();
        campaign.AddCharacter(new Character(hero, "Hero", new Dictionary<string, int> { ["strength"] = 2 }));
        campaign.AddCharacter(new Character(enemy, "Enemy"));
        campaign.AddParticipant(new Participant(human, "Human", ControllerKind.Human, SeatRole.Player, [hero]));
        campaign.AddParticipant(new Participant(agent, "Agent", ControllerKind.Agent, SeatRole.Player, [enemy]));
        campaign.SetResource(hero, "hp", hp, hp);
        campaign.SetResource(enemy, "hp", hp, hp);
        campaign.StartSession(SessionId.New(), "Battle");
        var encounter = new CombatEncounter(campaign, rules ?? new BasicD20CombatRules(), [hero, enemy]);
        var engine = new SessionEngine(campaign, new BasicD20RuleSystem(), dice ?? new FixedDice(15, 3), encounter);
        return new Fixture(campaign, hero, enemy, human, agent, encounter, engine);
    }

    private static int Hp(Campaign campaign, CharacterId id) =>
        campaign.Resources.Single(x => x.CharacterId == id && x.Key == "hp").Current;

    [Fact]
    public void Human_and_agent_obey_identical_turn_authorization_and_die_rolls()
    {
        var dice = new FixedDice(15, 3, 16, 4);
        var f = Create(dice: dice);
        var first = f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike"));
        Assert.True(first.Accepted);
        Assert.True(first.Success);
        Assert.NotNull(first.Combat);
        Assert.Equal(15, first.Combat.Roll);
        Assert.Equal(5, first.Combat.Damage);
        Assert.Equal(5, Hp(f.Campaign, f.Enemy));
        Assert.True(f.Encounter.ActionSpent);
        Assert.Equal(2, dice.Calls);

        var repeat = f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike"));
        var illegal = f.Engine.Execute(new AttackIntent(f.Agent, f.Enemy, f.Hero, "strike"));
        Assert.False(repeat.Accepted);
        Assert.False(illegal.Accepted);
        Assert.Equal(2, dice.Calls);

        Assert.True(f.Engine.Execute(new EndCombatTurnIntent(f.Human, f.Hero)).Accepted);
        Assert.Equal(f.Enemy, f.Encounter.ActiveCharacter);
        var second = f.Engine.Execute(new AttackIntent(f.Agent, f.Enemy, f.Hero, "strike"));
        Assert.True(second.Accepted);
        Assert.Equal(6, Hp(f.Campaign, f.Hero));
        Assert.Equal(4, dice.Calls);
        Assert.True(f.Engine.Execute(new EndCombatTurnIntent(f.Agent, f.Enemy)).Accepted);
        Assert.Equal(2, f.Encounter.Round);
        Assert.Equal(f.Hero, f.Encounter.ActiveCharacter);
    }

    [Fact]
    public void Unauthorized_agents_and_wrong_targets_cannot_mutate_hp_or_turns()
    {
        var dice = new FixedDice(17, 6);
        var f = Create(dice: dice);
        var before = f.Campaign.Events.Count;
        Assert.False(f.Engine.Execute(new AttackIntent(f.Agent, f.Hero, f.Enemy, "strike")).Accepted);
        Assert.False(f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Hero, "strike")).Accepted);
        Assert.False(f.Engine.Execute(new AttackIntent(f.Human, f.Hero, CharacterId.New(), "strike")).Accepted);
        Assert.False(f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "../inject")).Accepted);
        Assert.False(f.Engine.Execute(new EndCombatTurnIntent(f.Agent, f.Enemy)).Accepted);
        Assert.Equal(before, f.Campaign.Events.Count);
        Assert.Equal(10, Hp(f.Campaign, f.Hero));
        Assert.Equal(10, Hp(f.Campaign, f.Enemy));
        Assert.Equal(0, dice.Calls);
        Assert.False(f.Encounter.ActionSpent);
    }

    [Fact]
    public void Ruleset_is_interchangeable_and_cannot_inject_damage_as_user_input()
    {
        var rules = new FixedRules(new CombatStrike(true, 1, 7));
        var f = Create(rules);
        var outcome = f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "custom-weapon"));
        Assert.True(outcome.Accepted);
        Assert.Equal(3, Hp(f.Campaign, f.Enemy));
        Assert.Equal(1, rules.Calls);
        Assert.Equal(2, f.Campaign.Events.Count(e => e.Kind is "combat-start" or "combat-attack"));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, -1)]
    [InlineData(true, 1000001)]
    public void Invalid_rules_output_is_rejected_without_spending_turn(bool hit, int damage)
    {
        var f = Create(new FixedRules(new CombatStrike(hit, 1, damage)));
        var before = f.Campaign.Events.Count;
        var result = f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "attack"));
        Assert.False(result.Accepted);
        Assert.Equal(before, f.Campaign.Events.Count);
        Assert.False(f.Encounter.ActionSpent);
        Assert.Equal(10, Hp(f.Campaign, f.Enemy));
    }

    [Fact]
    public void Miss_has_no_damage_but_consumes_one_authorized_action()
    {
        var f = Create(new FixedRules(new CombatStrike(false, 2, 0)));
        var attack = f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike"));
        Assert.True(attack.Accepted);
        Assert.False(attack.Success);
        Assert.True(f.Encounter.ActionSpent);
        Assert.Equal(10, Hp(f.Campaign, f.Enemy));
        Assert.Equal("combat-attack", f.Campaign.Events[^1].Kind);
    }

    [Fact]
    public void Lethal_damage_is_clamped_and_finishes_encounter()
    {
        var f = Create(new FixedRules(new CombatStrike(true, 20, 1000000)), hp: 6);
        var result = f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike"));
        Assert.True(result.Accepted);
        Assert.Equal(0, Hp(f.Campaign, f.Enemy));
        Assert.Equal(6, result.Combat!.Damage);
        Assert.True(f.Encounter.IsFinished);
        Assert.Contains(f.Campaign.Events, e => e.Kind == "combat-finished");
        Assert.False(f.Engine.Execute(new EndCombatTurnIntent(f.Human, f.Hero)).Accepted);
        Assert.False(f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike")).Accepted);
    }

    [Fact]
    public void Active_encounter_can_only_exist_during_session_and_has_unique_living_combatants()
    {
        var f = Create();
        Assert.Throws<ArgumentException>(() => new CombatEncounter(f.Campaign, new BasicD20CombatRules(), [f.Hero, f.Hero]));
        Assert.Throws<ArgumentException>(() => new CombatEncounter(f.Campaign, new BasicD20CombatRules(), [f.Hero, CharacterId.New()]));

        var unrelated = new Campaign(CampaignId.New(), "Other");
        Assert.Throws<ArgumentException>(() =>
            new SessionEngine(unrelated, new BasicD20RuleSystem(), new FixedDice(), f.Encounter));
        f.Campaign.EndActiveSession();
        Assert.False(f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike")).Accepted);
        Assert.Throws<InvalidOperationException>(() =>
            new CombatEncounter(f.Campaign, new BasicD20CombatRules(), [f.Hero, f.Enemy]));
    }

    [Fact]
    public void Combat_persists_hp_and_event_history_but_not_transient_turn_cursor()
    {
        var f = Create(new FixedRules(new CombatStrike(true, 4, 2)));
        f.Engine.Execute(new AttackIntent(f.Human, f.Hero, f.Enemy, "strike"));
        f.Engine.Execute(new EndCombatTurnIntent(f.Human, f.Hero));
        var encoded = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider()).Encode(f.Campaign);
        var restored = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider()).Decode(encoded);
        Assert.Equal(8, Hp(restored, f.Enemy));
        Assert.Equal(f.Campaign.Events.Select(x => x.Kind), restored.Events.Select(x => x.Kind));
        Assert.Equal(f.Campaign.ActiveSessionId, restored.ActiveSessionId);
        Assert.Null(typeof(Campaign).GetProperty("CombatEncounter"));
        Assert.Null(typeof(PlayerView).GetProperty("CombatEncounter"));
    }
}
