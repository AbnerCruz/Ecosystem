using TabletopRpg.Core;
using TabletopRpg.ModelGateway;
using TabletopRpg.PlayerAgent;

namespace TabletopRpg.Core.Tests;

public sealed class PlayerAgentTests
{
    [Fact]
    public async Task Agent_speaks_only_after_wake_and_sees_own_character_knowledge()
    {
        var fixture = TestTable.Create();
        var secret = fixture.Campaign.AddWorldFact("THE-DUKE-IS-A-VAMPIRE");
        fixture.Campaign.RevealFactTo(fixture.Hero.Id, secret.Id, "private conversation");
        fixture.Campaign.AddBelief(fixture.Companion.Id, "The bridge seems safe.", "scout rumor", 0.35);
        fixture.Campaign.Remember(fixture.Companion.Id, "I owe the smith a favor.");

        var gateway = new FakeGateway("{\"kind\":\"speak\",\"text\":\"Stay behind me.\"}");
        var runner = fixture.CreateRunner(gateway);

        Assert.Equal(0, gateway.Calls);
        Assert.Empty(fixture.Campaign.Events);

        var result = await runner.ReactAsync(
            fixture.Agent.Id, new AgentWakeSignal("turn-started", "A bandit approaches."),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result!.Accepted);
        Assert.Equal(1, gateway.Calls);
        var request = Assert.IsType<ModelTurnRequest>(gateway.LastRequest);
        Assert.Equal("game-intent.v1", request.ResponseContract);
        var prompt = request.Messages.Single(x => x.Role == ModelMessageRole.User).Content;
        Assert.Contains("cautious scout", prompt);
        Assert.Contains("protect the traveler", prompt);
        Assert.Contains("trusted companion", prompt);
        Assert.Contains("The bridge seems safe.", prompt);
        Assert.Contains("I owe the smith a favor.", prompt);
        Assert.DoesNotContain("THE-DUKE-IS-A-VAMPIRE", prompt);
        Assert.DoesNotContain(fixture.Hero.Name, prompt);
        Assert.DoesNotContain("WorldFacts", prompt);
        Assert.Equal("speech", Assert.Single(fixture.Campaign.Events).Kind);
        Assert.Contains("Stay behind me.", fixture.Campaign.Events.Single().Text);
    }

    [Fact]
    public async Task Agent_with_two_controlled_characters_only_sends_target_character_perspective()
    {
        var fixture = TestTable.Create(includeSecondAgentCharacter: true);
        var fact = fixture.Campaign.AddWorldFact("SECOND-AGENT-CHARACTER-SECRET");
        fixture.Campaign.RevealFactTo(fixture.SecondCompanion!.Id, fact.Id, "seen alone");
        var gateway = new FakeGateway("{\"kind\":\"pass\"}");
        var runner = fixture.CreateRunner(gateway);

        var result = await runner.ReactAsync(
            fixture.Agent.Id, new AgentWakeSignal("addressed"),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(1, gateway.Calls);
        var text = gateway.LastRequest!.Messages.Last().Content;
        Assert.DoesNotContain("SECOND-AGENT-CHARACTER-SECRET", text);
        Assert.DoesNotContain(fixture.SecondCompanion.Name, text);
    }

    [Theory]
    [InlineData("{\"kind\":\"speak\",\"text\":\"Hi\",\"participantId\":\"another\"}")]
    [InlineData("{\"kind\":\"speak\",\"text\":\"Hi\",\"characterId\":\"another\"}")]
    [InlineData("{\"kind\":\"skill_check\",\"difficulty\":1,\"attribute\":\"strength\"}")]
    [InlineData("{\"kind\":\"speak\",\"text\":\"Hi\",\"text\":\"Override\"}")]
    [InlineData("{\"kind\":\"end_turn\",\"text\":\"unneeded\"}")]
    [InlineData("{\"kind\":\"attack\",\"targetId\":\"any\"}")]
    [InlineData("{\"kind\":\"speak\",\"text\":\"\"}")]
    [InlineData("{\"kind\":\"SPEAK\",\"text\":\"Hi\"}")]
    [InlineData("Here is my action: {\"kind\":\"speak\",\"text\":\"Hi\"}")]
    [InlineData("{\"kind\":\"pass\",}")]
    public void Untrusted_or_malformed_actions_fail_closed(string response)
    {
        var participant = ParticipantId.New();
        var character = CharacterId.New();
        Assert.Null(PlayerIntentDecoder.TryDecode(response, participant, character));
    }

    [Fact]
    public void Structured_intents_are_bound_to_runtime_identity_not_model_supplied_ids()
    {
        var participant = ParticipantId.New();
        var character = CharacterId.New();

        var speak = Assert.IsType<SpeakIntent>(
            PlayerIntentDecoder.TryDecode("{\"kind\":\"speak\",\"text\":\"  Halt!  \"}", participant, character));
        Assert.Equal(participant, speak.ParticipantId);
        Assert.Equal(character, speak.CharacterId);
        Assert.Equal("Halt!", speak.Text);

        var end = Assert.IsType<EndCombatTurnIntent>(
            PlayerIntentDecoder.TryDecode("{\"kind\":\"end_turn\"}", participant, character));
        Assert.Equal(participant, end.ParticipantId);
        Assert.Equal(character, end.CharacterId);
    }

    [Fact]
    public async Task Invalid_model_output_never_mutates_campaign()
    {
        var fixture = TestTable.Create();
        var gateway = new FakeGateway("{\"kind\":\"skill_check\",\"difficulty\":-100}");
        var runner = fixture.CreateRunner(gateway);

        var result = await runner.ReactAsync(
            fixture.Agent.Id, new AgentWakeSignal("turn-started"),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(fixture.Campaign.Events);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task End_turn_uses_session_engine_and_cannot_execute_without_combat()
    {
        var fixture = TestTable.Create();
        var gateway = new FakeGateway("{\"kind\":\"end_turn\"}");

        var result = await fixture.CreateRunner(gateway).ReactAsync(
            fixture.Agent.Id, new AgentWakeSignal("turn-started"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.False(result!.Accepted);
        Assert.Empty(fixture.Campaign.Events);
    }

    [Fact]
    public async Task Identity_mismatch_and_bad_signal_are_rejected_before_gateway_call()
    {
        var fixture = TestTable.Create();
        var gateway = new FakeGateway("{\"kind\":\"pass\"}");
        var source = fixture.CreateSource(gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.DecideAsync(fixture.Campaign.CreatePlayerView(fixture.Human.Id),
                new AgentWakeSignal("turn-started")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.DecideAsync(fixture.Campaign.CreatePlayerView(fixture.Agent.Id),
                new AgentWakeSignal("idle-poll")));
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Cancellation_prevents_call_and_propagates_into_gateway()
    {
        var fixture = TestTable.Create();
        var gateway = new FakeGateway("{\"kind\":\"pass\"}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var source = fixture.CreateSource(gateway);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.DecideAsync(fixture.Campaign.CreatePlayerView(fixture.Agent.Id),
                new AgentWakeSignal("turn-started"), cts.Token));
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public void Profile_is_snapshot_and_never_stores_knowledge_or_memory()
    {
        var goals = new[] { "protect the traveler" };
        var profile = new PlayerAgentProfile(ParticipantId.New(), CharacterId.New(),
            "cautious scout", goals, [new("Traveler", "trusted companion")]);

        goals[0] = "betray the traveler";
        Assert.Equal("protect the traveler", Assert.Single(profile.Goals));
        Assert.Null(typeof(PlayerAgentProfile).GetProperty("WorldFacts"));
        Assert.Null(typeof(PlayerAgentProfile).GetProperty("Memories"));
        Assert.Throws<ArgumentException>(() =>
            new PlayerAgentProfile(ParticipantId.New(), CharacterId.New(), " "));
    }

    private sealed class FakeGateway : IModelGateway
    {
        private readonly string _response;

        public FakeGateway(string response) => _response = response;

        public int Calls { get; private set; }
        public ModelTurnRequest? LastRequest { get; private set; }

        public Task<ModelTurnResponse> GenerateAsync(
            ModelTurnRequest turn,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastRequest = turn;
            return Task.FromResult(new ModelTurnResponse(_response));
        }
    }

    private sealed class TestTable
    {
        private TestTable(Campaign campaign, Participant human, Participant agent,
            Character hero, Character companion, Character? secondCompanion)
        {
            Campaign = campaign;
            Human = human;
            Agent = agent;
            Hero = hero;
            Companion = companion;
            SecondCompanion = secondCompanion;
        }

        public Campaign Campaign { get; }
        public Participant Human { get; }
        public Participant Agent { get; }
        public Character Hero { get; }
        public Character Companion { get; }
        public Character? SecondCompanion { get; }

        public static TestTable Create(bool includeSecondAgentCharacter = false)
        {
            var campaign = new Campaign(CampaignId.New(), "Solo session");
            var hero = new Character(CharacterId.New(), "Hero");
            var companion = new Character(CharacterId.New(), "Marcus");
            campaign.AddCharacter(hero);
            campaign.AddCharacter(companion);
            Character? second = null;
            if (includeSecondAgentCharacter)
            {
                second = new Character(CharacterId.New(), "SecretKeeper");
                campaign.AddCharacter(second);
            }

            var human = new Participant(ParticipantId.New(), "Human",
                ControllerKind.Human, SeatRole.Player, [hero.Id]);
            var ids = second is null ? new[] { companion.Id } : new[] { companion.Id, second.Id };
            var agent = new Participant(ParticipantId.New(), "Agent",
                ControllerKind.Agent, SeatRole.Player, ids);
            campaign.AddParticipant(human);
            campaign.AddParticipant(agent);
            return new TestTable(campaign, human, agent, hero, companion, second);
        }

        public PlayerAgentDecisionSource CreateSource(FakeGateway gateway) =>
            new(new PlayerAgentProfile(Agent.Id, Companion.Id, "cautious scout",
                ["protect the traveler"], [new("Traveler", "trusted companion")]), gateway);

        public AgentTurnRunner CreateRunner(FakeGateway gateway) =>
            new(Campaign,
                new SessionEngine(Campaign, new BasicD20RuleSystem(),
                    new DiceRoller(new SystemRandomSource())),
                CreateSource(gateway));
    }
}
