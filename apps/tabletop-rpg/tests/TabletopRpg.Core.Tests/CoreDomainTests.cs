using TabletopRpg.Core;

namespace TabletopRpg.Core.Tests;

public sealed class CoreDomainTests
{
    [Fact]
    public void World_fact_does_not_become_character_knowledge_until_revealed()
    {
        var fixture = Fixture.Create();
        var fact = fixture.Campaign.AddWorldFact("There is a secret passage behind the library.");

        Assert.Empty(fixture.Campaign.CreatePlayerView(fixture.Human.Id).Characters.Single().Knowledge);

        fixture.Campaign.RevealFactTo(fixture.Hero.Id, fact.Id, "personal observation");

        var view = fixture.Campaign.CreatePlayerView(fixture.Human.Id);
        var claim = Assert.Single(view.Characters.Single().Knowledge);

        Assert.Equal(fact.Id, claim.FactId);
        Assert.Equal(fact.Statement, claim.Statement);
    }

    [Fact]
    public void Knowledge_does_not_leak_between_characters()
    {
        var fixture = Fixture.Create();
        var fact = fixture.Campaign.AddWorldFact("The duke is a vampire.");

        fixture.Campaign.RevealFactTo(fixture.Hero.Id, fact.Id, "witnessed transformation");

        var humanView = fixture.Campaign.CreatePlayerView(fixture.Human.Id);
        var agentView = fixture.Campaign.CreatePlayerView(fixture.Agent.Id);

        Assert.Single(humanView.Characters.Single().Knowledge);
        Assert.Empty(agentView.Characters.Single().Knowledge);
    }

    [Fact]
    public void Player_view_has_no_world_state_surface()
    {
        var fixture = Fixture.Create();
        fixture.Campaign.AddWorldFact("The sealed chest contains a crown.");

        var view = fixture.Campaign.CreatePlayerView(fixture.Agent.Id);

        Assert.Null(typeof(PlayerView).GetProperty("WorldFacts"));
        Assert.Empty(view.Characters.Single().Knowledge);
    }

    [Fact]
    public void Subjective_memory_is_not_promoted_to_world_truth_or_knowledge()
    {
        var fixture = Fixture.Create();

        fixture.Campaign.Remember(fixture.Hero.Id, "I am certain the innkeeper poisoned the wine.");

        var view = fixture.Campaign.CreatePlayerView(fixture.Human.Id);
        var character = Assert.Single(view.Characters);

        Assert.Single(character.Memories);
        Assert.Empty(character.Knowledge);
    }

    [Fact]
    public void Human_and_agent_intents_use_the_same_session_engine()
    {
        var fixture = Fixture.Create();
        var engine = fixture.CreateEngine(10);

        var humanResult = engine.Execute(
            new SpeakIntent(fixture.Human.Id, fixture.Hero.Id, "We go left."));
        var agentResult = engine.Execute(
            new SpeakIntent(fixture.Agent.Id, fixture.Companion.Id, "I will cover the rear."));

        Assert.True(humanResult.Accepted);
        Assert.True(agentResult.Accepted);
        Assert.Equal(2, fixture.Campaign.Events.Count);
        Assert.All(fixture.Campaign.Events, e => Assert.Equal("speech", e.Kind));
    }

    [Fact]
    public void Participant_cannot_act_for_a_character_it_does_not_control()
    {
        var fixture = Fixture.Create();
        var engine = fixture.CreateEngine(10);

        var result = engine.Execute(
            new SpeakIntent(fixture.Agent.Id, fixture.Hero.Id, "I hijacked the hero."));

        Assert.False(result.Accepted);
        Assert.Empty(fixture.Campaign.Events);
    }

    [Fact]
    public void Skill_check_is_resolved_by_rules_and_dice_not_by_the_caller()
    {
        var fixture = Fixture.Create(heroStrength: 3);
        var engine = fixture.CreateEngine(14);

        var result = engine.Execute(new SkillCheckIntent(
            fixture.Human.Id,
            fixture.Hero.Id,
            "strength",
            15,
            "Force the iron door"));

        Assert.True(result.Accepted);
        Assert.True(result.Success);
        Assert.NotNull(result.Check);
        Assert.Equal(14, result.Check!.NaturalRoll);
        Assert.Equal(3, result.Check.Modifier);
        Assert.Equal(17, result.Check.Total);
        Assert.True(result.Check.Success);
        Assert.Contains("d20 14 + 3 = 17 vs 15: success", fixture.Campaign.Events.Single().Text);
    }

    [Fact]
    public async Task Agent_decision_source_runs_only_after_an_explicit_wake_signal()
    {
        var fixture = Fixture.Create();
        var source = new RecordingAgentSource();
        var engine = fixture.CreateEngine(7);
        var runner = new AgentTurnRunner(fixture.Campaign, engine, source);

        Assert.Equal(0, source.Calls);

        var result = await runner.ReactAsync(
            fixture.Agent.Id,
            new AgentWakeSignal("turn-started", "agent turn"));

        Assert.Equal(1, source.Calls);
        Assert.NotNull(source.LastView);
        Assert.Equal(fixture.Agent.Id, source.LastView!.ParticipantId);
        Assert.Single(source.LastView.Characters);
        Assert.Equal(fixture.Companion.Id, source.LastView.Characters.Single().CharacterId);
        Assert.True(result!.Accepted);
    }

    [Fact]
    public async Task Agent_cannot_return_an_intent_for_another_participant()
    {
        var fixture = Fixture.Create();
        var source = new CrossParticipantAgentSource(fixture.Human.Id, fixture.Hero.Id);
        var runner = new AgentTurnRunner(
            fixture.Campaign,
            fixture.CreateEngine(11),
            source);

        var result = await runner.ReactAsync(
            fixture.Agent.Id,
            new AgentWakeSignal("turn-started"));

        Assert.NotNull(result);
        Assert.False(result!.Accepted);
        Assert.Empty(fixture.Campaign.Events);
    }

    private sealed class SequenceRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public SequenceRandomSource(params int[] values)
        {
            _values = new Queue<int>(values);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_values.Count == 0)
                throw new InvalidOperationException("No deterministic random values remain.");

            var value = _values.Dequeue();
            if (value < minInclusive || value >= maxExclusive)
                throw new InvalidOperationException(
                    $"Deterministic value {value} is outside [{minInclusive}, {maxExclusive}).");

            return value;
        }
    }

    private sealed class RecordingAgentSource : IAgentDecisionSource
    {
        public int Calls { get; private set; }
        public PlayerView? LastView { get; private set; }

        public Task<GameIntent?> DecideAsync(
            PlayerView view,
            AgentWakeSignal signal,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastView = view;
            var character = view.Characters.Single();

            return Task.FromResult<GameIntent?>(
                new SpeakIntent(view.ParticipantId, character.CharacterId, "Ready."));
        }
    }

    private sealed class CrossParticipantAgentSource : IAgentDecisionSource
    {
        private readonly ParticipantId _otherParticipant;
        private readonly CharacterId _otherCharacter;

        public CrossParticipantAgentSource(ParticipantId otherParticipant, CharacterId otherCharacter)
        {
            _otherParticipant = otherParticipant;
            _otherCharacter = otherCharacter;
        }

        public Task<GameIntent?> DecideAsync(
            PlayerView view,
            AgentWakeSignal signal,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameIntent?>(
                new SpeakIntent(_otherParticipant, _otherCharacter, "Unauthorized."));
    }

    private sealed class Fixture
    {
        private Fixture(
            Campaign campaign,
            Character hero,
            Character companion,
            Participant human,
            Participant agent)
        {
            Campaign = campaign;
            Hero = hero;
            Companion = companion;
            Human = human;
            Agent = agent;
        }

        public Campaign Campaign { get; }
        public Character Hero { get; }
        public Character Companion { get; }
        public Participant Human { get; }
        public Participant Agent { get; }

        public static Fixture Create(int heroStrength = 0)
        {
            var campaign = new Campaign(CampaignId.New(), "Test Campaign");
            var hero = new Character(
                CharacterId.New(),
                "Hero",
                new Dictionary<string, int> { ["strength"] = heroStrength });
            var companion = new Character(
                CharacterId.New(),
                "Companion",
                new Dictionary<string, int> { ["strength"] = 1 });

            campaign.AddCharacter(hero);
            campaign.AddCharacter(companion);

            var human = new Participant(
                ParticipantId.New(),
                "Human",
                ControllerKind.Human,
                SeatRole.Player,
                [hero.Id]);
            var agent = new Participant(
                ParticipantId.New(),
                "Agent",
                ControllerKind.Agent,
                SeatRole.Player,
                [companion.Id]);

            campaign.AddParticipant(human);
            campaign.AddParticipant(agent);

            return new Fixture(campaign, hero, companion, human, agent);
        }

        public SessionEngine CreateEngine(params int[] rolls) =>
            new(
                Campaign,
                new BasicD20RuleSystem(),
                new DiceRoller(new SequenceRandomSource(rolls)));
    }
}
