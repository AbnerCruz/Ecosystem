using TabletopRpg.Core;
using TabletopRpg.Npc;

namespace TabletopRpg.Core.Tests;

public sealed class NpcRuntimeTests
{
    [Fact]
    public void Npc_is_not_a_player_agent_and_requires_explicit_gm_control()
    {
        var table = Table.Create();
        var runtime = table.Runtime();

        runtime.Register(table.Profile());
        Assert.Equal(0, table.Campaign.Events.Count(e => e.Kind == "speech"));
        Assert.Equal(7, runtime.Snapshot(table.Npc.Id).Trust);
        Assert.False(typeof(IAgentDecisionSource).IsAssignableFrom(typeof(NpcRuntime)));

        Assert.Throws<InvalidOperationException>(() => runtime.Register(table.Profile()));
        Assert.Throws<InvalidOperationException>(() =>
            table.Runtime().Register(new NpcProfile(
                table.Npc.Id, table.Human.Id, table.Town.Id, "merchant",
                "sell goods", NpcDisposition.Friendly)));
    }

    [Fact]
    public void NPC_dialogue_is_a_proposal_until_session_engine_approves_it()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());
        var start = table.Campaign.Events.Count;

        var dialogue = runtime.Propose(table.Interaction(NpcInteractionKind.Greet));

        Assert.Equal(NpcReactionKind.Welcome, dialogue.Kind);
        Assert.Equal("Welcome. What brings you here?", dialogue.Text);
        Assert.Equal(8, dialogue.NextTrust);
        Assert.Equal(7, runtime.Snapshot(table.Npc.Id).Trust);
        Assert.Equal(start, table.Campaign.Events.Count);

        var result = runtime.ApplyApproved(dialogue);
        Assert.True(result.Accepted);
        Assert.Equal(8, runtime.Snapshot(table.Npc.Id).Trust);
        Assert.Equal("speech", table.Campaign.Events.Last().Kind);
        Assert.Equal(table.GameMaster.Id, table.Campaign.Events.Last().ParticipantId);
        Assert.Equal(table.Npc.Id, table.Campaign.Events.Last().CharacterId);

        Assert.False(runtime.ApplyApproved(dialogue).Accepted);
        Assert.Single(table.Campaign.Events.Where(e => e.Kind == "speech"));
    }

    [Fact]
    public void Host_cannot_forge_npc_proposals_or_replay_superseded_dialogue()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());

        var original = runtime.Propose(table.Interaction(NpcInteractionKind.Greet));
        var forged = original with { Text = "The king secretly serves the cult." };

        Assert.False(runtime.ApplyApproved(forged).Accepted);
        var newer = runtime.Propose(table.Interaction(NpcInteractionKind.Ask));

        Assert.False(runtime.ApplyApproved(original).Accepted);
        Assert.True(runtime.ApplyApproved(newer).Accepted);
        Assert.Single(table.Campaign.Events.Where(e => e.Kind == "speech"));
    }

    [Fact]
    public void Stale_proposals_do_not_mutate_npc_or_campaign()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());
        var draft = runtime.Propose(table.Interaction(NpcInteractionKind.Threaten));
        var before = runtime.Snapshot(table.Npc.Id);

        table.Campaign.AddWorldFact("A secret only the GM knows.");
        var result = runtime.ApplyApproved(draft);

        Assert.False(result.Accepted);
        Assert.Equal(before.Trust, runtime.Snapshot(table.Npc.Id).Trust);
        Assert.Equal(before.Alertness, runtime.Snapshot(table.Npc.Id).Alertness);
        Assert.Empty(table.Campaign.Events.Where(e => e.Kind == "speech"));
    }

    [Fact]
    public void Threats_change_attitude_only_after_approval()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());

        var threatened = runtime.Propose(table.Interaction(NpcInteractionKind.Threaten));
        Assert.Equal(5, threatened.NextTrust);
        Assert.Equal(3, threatened.NextAlertness);
        Assert.True(runtime.ApplyApproved(threatened).Accepted);

        var ask = runtime.Propose(table.Interaction(NpcInteractionKind.Ask));
        Assert.Equal(NpcReactionKind.Listen, ask.Kind);
        Assert.True(runtime.ApplyApproved(ask).Accepted);

        var help = runtime.Propose(table.Interaction(NpcInteractionKind.RequestHelp));
        Assert.Equal(NpcReactionKind.Refuse, help.Kind);
        Assert.True(runtime.ApplyApproved(help).Accepted);
    }

    [Fact]
    public void Only_a_player_in_the_npcs_active_scene_may_interact()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());

        Assert.Throws<InvalidOperationException>(() =>
            runtime.Propose(new NpcInteraction(table.Npc.Id, table.GameMaster.Id,
                table.Npc.Id, NpcInteractionKind.Greet)));

        Assert.Throws<InvalidOperationException>(() =>
            runtime.Propose(new NpcInteraction(table.Npc.Id, table.Human.Id,
                table.Npc.Id, NpcInteractionKind.Greet)));

        table.Campaign.EnterScene(table.Forest.Id);
        Assert.Throws<InvalidOperationException>(() =>
            runtime.Propose(table.Interaction(NpcInteractionKind.Greet)));
        Assert.Empty(table.Campaign.Events.Where(e => e.Kind == "speech"));
    }

    [Fact]
    public void NPC_does_not_react_to_offscreen_combat_but_remembers_local_combat()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());

        table.Campaign.EnterScene(table.Forest.Id);
        table.Campaign.StartSession(SessionId.New(), "Exploration");
        table.Campaign.SetResource(table.Hero.Id, "hp", 8, 8);
        table.Campaign.SetResource(table.Npc.Id, "hp", 8, 8);

        _ = new CombatEncounter(table.Campaign, new BasicD20CombatRules(),
            [table.Hero.Id, table.Npc.Id]);

        runtime.Observe(table.Npc.Id);
        Assert.Equal(0, runtime.Snapshot(table.Npc.Id).Alertness);

        table.Campaign.EnterScene(table.Town.Id);
        // The NPC is present for a new encounter in the town.
        _ = new CombatEncounter(table.Campaign, new BasicD20CombatRules(),
            [table.Hero.Id, table.Npc.Id]);
        var after = runtime.Observe(table.Npc.Id);

        Assert.Equal(3, after.Alertness);
        Assert.Equal(table.Campaign.Events.Last().Sequence, after.LastEventSequence);

        var repeat = runtime.Observe(table.Npc.Id);
        Assert.Equal(after, repeat);
    }

    [Fact]
    public void Rejected_dialogue_does_not_change_npc_mood()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());
        var proposal = runtime.Propose(table.Interaction(NpcInteractionKind.Greet));
        var before = runtime.Snapshot(table.Npc.Id);

        table.Campaign.EnterScene(table.Forest.Id);
        Assert.False(runtime.ApplyApproved(proposal).Accepted);
        var after = runtime.Snapshot(table.Npc.Id);
        Assert.Equal(before.Trust, after.Trust);
        Assert.Equal(before.Alertness, after.Alertness);
        Assert.Empty(table.Campaign.Events.Where(e => e.Kind == "speech"));
    }

    [Fact]
    public void Missing_npc_event_history_fails_closed()
    {
        var table = Table.Create();
        var runtime = table.Runtime();
        runtime.Register(table.Profile());
        table.Campaign.AdvanceTime(1);
        runtime.Observe(table.Npc.Id);

        for (var i = 0; i < 30; i++)
            table.Campaign.AdvanceTime(1);

        Assert.Throws<InvalidOperationException>(() => runtime.Observe(table.Npc.Id));
    }

    [Fact]
    public void Invalid_profiles_and_unknown_entities_are_refused()
    {
        var table = Table.Create();

        Assert.Throws<ArgumentException>(() =>
            new NpcProfile(table.Npc.Id, table.GameMaster.Id, table.Town.Id,
                "", "do business", NpcDisposition.Friendly));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NpcProfile(table.Npc.Id, table.GameMaster.Id, table.Town.Id,
                "merchant", "do business", (NpcDisposition)99));

        Assert.Throws<KeyNotFoundException>(() =>
            table.Runtime().Register(new NpcProfile(CharacterId.New(), table.GameMaster.Id,
                table.Town.Id, "merchant", "trade", NpcDisposition.Cautious)));
        Assert.Throws<KeyNotFoundException>(() =>
            table.Runtime().Register(new NpcProfile(table.Npc.Id, table.GameMaster.Id,
                SceneId.New(), "merchant", "trade", NpcDisposition.Cautious)));
    }

    private sealed class Table
    {
        private Table(Campaign campaign, Character npc, Character hero, Participant human,
            Participant gameMaster, CampaignScene town, CampaignScene forest)
        {
            Campaign = campaign;
            Npc = npc;
            Hero = hero;
            Human = human;
            GameMaster = gameMaster;
            Town = town;
            Forest = forest;
        }

        public Campaign Campaign { get; }
        public Character Npc { get; }
        public Character Hero { get; }
        public Participant Human { get; }
        public Participant GameMaster { get; }
        public CampaignScene Town { get; }
        public CampaignScene Forest { get; }

        public static Table Create()
        {
            var campaign = new Campaign(CampaignId.New(), "Test NPC campaign");
            var npc = new Character(CharacterId.New(), "Alda");
            var hero = new Character(CharacterId.New(), "Explorer");
            campaign.AddCharacter(npc);
            campaign.AddCharacter(hero);
            var human = new Participant(ParticipantId.New(), "Human", ControllerKind.Human,
                SeatRole.Player, [hero.Id]);
            var gm = new Participant(ParticipantId.New(), "GM", ControllerKind.Agent,
                SeatRole.GameMaster, [npc.Id]);
            campaign.AddParticipant(human);
            campaign.AddParticipant(gm);
            var town = campaign.AddScene(SceneId.New(), "Town market");
            var forest = campaign.AddScene(SceneId.New(), "Forest");
            campaign.EnterScene(town.Id);
            return new Table(campaign, npc, hero, human, gm, town, forest);
        }

        public NpcProfile Profile() => new(Npc.Id, GameMaster.Id, Town.Id,
            "merchant", "protect the market", NpcDisposition.Friendly);

        public NpcInteraction Interaction(NpcInteractionKind kind) =>
            new(Npc.Id, Human.Id, Hero.Id, kind);

        public NpcRuntime Runtime() =>
            new(Campaign, new SessionEngine(Campaign, new BasicD20RuleSystem(),
                new DiceRoller(new SystemRandomSource())));
    }
}
