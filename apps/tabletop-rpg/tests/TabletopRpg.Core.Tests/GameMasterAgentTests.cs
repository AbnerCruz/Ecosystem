using TabletopRpg.Core;
using TabletopRpg.GameMaster;
using TabletopRpg.ModelGateway;

namespace TabletopRpg.Core.Tests;

public sealed class GameMasterAgentTests
{
    [Fact]
    public async Task Gm_receives_private_world_truth_without_leaking_it_into_player_view()
    {
        var t = Table.Create();
        var hidden = t.Campaign.AddWorldFact("The mayor is an impostor.");
        var sceneId = SceneId.New();
        t.Campaign.AddScene(sceneId, "Market", "Quiet streets");
        t.Campaign.EnterScene(sceneId);
        t.Campaign.AddQuest(QuestId.New(), "Find the mayor", sceneId);

        var gateway = new FakeGateway("{\"kind\":\"pass\"}");
        var gm = new GameMasterAgent(t.Campaign, gateway);

        Assert.Equal(0, gateway.Calls);
        Assert.Empty(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);

        var proposal = await gm.ProposeAsync(t.GameMaster.Id,
            new AgentWakeSignal("scene-changed", "The group enters the market."),
            TestContext.Current.CancellationToken);

        Assert.Null(proposal);
        Assert.Equal(1, gateway.Calls);
        var prompt = gateway.LastRequest!.Messages.Single(m => m.Role == ModelMessageRole.User).Content;
        Assert.Contains("The mayor is an impostor.", prompt);
        Assert.Contains("Find the mayor", prompt);
        Assert.Contains("Market", prompt);
        Assert.Equal("gm-proposal.v1", gateway.LastRequest.ResponseContract);
        Assert.Empty(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);
        Assert.DoesNotContain("The mayor is an impostor.",
            string.Join("|", t.Campaign.Events.Select(e => e.Text)));
    }

    [Fact]
    public async Task Narration_is_only_a_draft_until_explicitly_approved()
    {
        var t = Table.Create();
        var gateway = new FakeGateway("{\"kind\":\"narrate\",\"text\":\"A distant bell rings.\"}");
        var agent = new GameMasterAgent(t.Campaign, gateway);
        var oldRevision = t.Campaign.CreateGameMasterView(t.GameMaster.Id).Revision;

        var draft = Assert.IsType<NarrationDraft>(await agent.ProposeAsync(
            t.GameMaster.Id, new AgentWakeSignal("player-action"),
            TestContext.Current.CancellationToken));

        Assert.Equal(oldRevision, t.Campaign.CreateGameMasterView(t.GameMaster.Id).Revision);
        Assert.Empty(t.Campaign.Events);
        Assert.Equal(t.GameMaster.Id, draft.GameMasterId);

        new ApprovedGameMasterTools(t.Campaign).ApplyApproved(draft);

        Assert.Equal("gm-narration", Assert.Single(t.Campaign.Events).Kind);
        Assert.Equal("A distant bell rings.", t.Campaign.Events[0].Text);
    }

    [Fact]
    public async Task Reveal_fact_requires_host_approval_and_targets_exactly_one_character()
    {
        var t = Table.Create();
        var fact = t.Campaign.AddWorldFact("The ring is cursed.");
        var output = "{\"kind\":\"reveal_fact\",\"factId\":\"" +
            fact.Id.Value.ToString("N") + "\",\"targetCharacterId\":\"" +
            t.Hero.Id.Value.ToString("N") + "\"}";
        var gateway = new FakeGateway(output);

        var draft = Assert.IsType<RevealFactDraft>(await new GameMasterAgent(t.Campaign, gateway)
            .ProposeAsync(t.GameMaster.Id, new AgentWakeSignal("addressed"),
                TestContext.Current.CancellationToken));

        Assert.Empty(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);
        new ApprovedGameMasterTools(t.Campaign).ApplyApproved(draft);
        var claim = Assert.Single(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);
        Assert.Equal(fact.Id, claim.FactId);
        Assert.Equal("The ring is cursed.", claim.Statement);
    }

    [Fact]
    public void Untrusted_output_does_not_control_identity_or_create_arbitrary_actions()
    {
        var id = ParticipantId.New();
        var revision = new GameMasterRevision(10, 0, 0, 0);
        foreach (var response in new[]
        {
            "{\"kind\":\"narrate\",\"text\":\"Hi\",\"participantId\":\"forged\"}",
            "{\"kind\":\"narrate\",\"text\":\"Hi\",\"text\":\"Override\"}",
            "{\"kind\":\"narrate\",\"text\":\" \"}",
            "{\"kind\":\"change_hp\",\"value\":100}",
            "{\"kind\":\"reveal_fact\",\"factId\":\"bad\",\"targetCharacterId\":\"bad\"}",
            "{\"kind\":\"reveal_fact\",\"factId\":\"00000000000000000000000000000000\",\"targetCharacterId\":\"00000000000000000000000000000000\"}",
            "{\"kind\":\"narrate\",}",
            "Model: {\"kind\":\"narrate\",\"text\":\"Hi\"}"
        })
        {
            Assert.Null(GameMasterProposalDecoder.TryDecode(response, id, revision));
        }

        var parsed = Assert.IsType<NarrationDraft>(
            GameMasterProposalDecoder.TryDecode(
                "{\"kind\":\"narrate\",\"text\":\"Stand clear.\"}", id, revision));
        Assert.Equal(id, parsed.GameMasterId);
        Assert.Equal(revision, parsed.ExpectedRevision);
    }

    [Fact]
    public void Unapproved_or_stale_proposals_cannot_change_world()
    {
        var t = Table.Create();
        var draft = new NarrationDraft(t.GameMaster.Id, t.Campaign.CreateGameMasterView(t.GameMaster.Id).Revision, "A drum sounds.");
        t.Campaign.AddWorldFact("New fact makes prior view stale.");
        var eventsBefore = t.Campaign.Events.Count;

        Assert.Throws<InvalidOperationException>(() =>
            new ApprovedGameMasterTools(t.Campaign).ApplyApproved(draft));
        Assert.Equal(eventsBefore, t.Campaign.Events.Count);
    }

    [Fact]
    public void Fake_player_and_nonexistent_world_references_fail_closed()
    {
        var t = Table.Create();
        var tools = new ApprovedGameMasterTools(t.Campaign);
        Assert.Throws<InvalidOperationException>(() =>
            tools.ApplyApproved(new NarrationDraft(t.Player.Id, t.Campaign.CreateGameMasterView(t.GameMaster.Id).Revision, "Fake GM")));
        Assert.Throws<KeyNotFoundException>(() =>
            tools.ApplyApproved(new RevealFactDraft(
                t.GameMaster.Id, t.Campaign.CreateGameMasterView(t.GameMaster.Id).Revision, t.Hero.Id, FactId.New())));
        Assert.Throws<KeyNotFoundException>(() =>
            tools.ApplyApproved(new RevealFactDraft(
                t.GameMaster.Id, t.Campaign.CreateGameMasterView(t.GameMaster.Id).Revision, CharacterId.New(), FactId.New())));
        Assert.Empty(t.Campaign.Events);
    }

    [Fact]
    public async Task Unauthorized_or_idle_seat_never_calls_gateway()
    {
        var t = Table.Create();
        var gateway = new FakeGateway("{\"kind\":\"pass\"}");
        var agent = new GameMasterAgent(t.Campaign, gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.ProposeAsync(t.Player.Id, new AgentWakeSignal("session-started")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            agent.ProposeAsync(t.GameMaster.Id, new AgentWakeSignal("idle-poll")));
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Cancellation_is_checked_before_model_invocation()
    {
        var t = Table.Create();
        var gateway = new FakeGateway("{\"kind\":\"pass\"}");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GameMasterAgent(t.Campaign, gateway)
                .ProposeAsync(t.GameMaster.Id, new AgentWakeSignal("player-action"),
                    cancelled.Token));
        Assert.Equal(0, gateway.Calls);
    }

    private sealed class FakeGateway : IModelGateway
    {
        private readonly string _answer;
        public FakeGateway(string answer) => _answer = answer;
        public int Calls { get; private set; }
        public ModelTurnRequest? LastRequest { get; private set; }

        public Task<ModelTurnResponse> GenerateAsync(
            ModelTurnRequest turn, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = turn;
            return Task.FromResult(new ModelTurnResponse(_answer));
        }
    }

    private sealed class Table
    {
        private Table(Campaign campaign, Participant gameMaster, Participant player, Character hero)
        {
            Campaign = campaign;
            GameMaster = gameMaster;
            Player = player;
            Hero = hero;
        }
        public Campaign Campaign { get; }
        public Participant GameMaster { get; }
        public Participant Player { get; }
        public Character Hero { get; }

        public static Table Create()
        {
            var campaign = new Campaign(CampaignId.New(), "Test GM");
            var hero = new Character(CharacterId.New(), "Explorer");
            campaign.AddCharacter(hero);
            var player = new Participant(ParticipantId.New(), "Human",
                ControllerKind.Human, SeatRole.Player, [hero.Id]);
            var gm = new Participant(ParticipantId.New(), "Narrator",
                ControllerKind.Agent, SeatRole.GameMaster);
            campaign.AddParticipant(player);
            campaign.AddParticipant(gm);
            return new Table(campaign, gm, player, hero);
        }
    }
}
