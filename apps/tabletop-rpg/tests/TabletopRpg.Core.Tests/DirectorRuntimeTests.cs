using TabletopRpg.Core;
using TabletopRpg.Director;

namespace TabletopRpg.Core.Tests;

public sealed class DirectorRuntimeTests
{
    [Fact]
    public void Scenes_checks_and_quest_completion_create_unscripted_private_beats()
    {
        var t = Table.Create();
        var secret = t.Campaign.AddWorldFact("The guard captain secretly serves the cult.");
        var scene = t.Campaign.AddScene(SceneId.New(), "Ruined port");
        var quest = t.Campaign.AddQuest(QuestId.New(), "Find the lost ship", scene.Id);
        var director = t.CreateDirector();

        // Nothing runs in the background: no world changes or cues until Observe.
        Assert.Equal(2, t.Campaign.Events.Count);
        Assert.Empty(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);

        t.Campaign.EnterScene(scene.Id);
        var before = t.Campaign.Events.Count;
        var first = director.Observe(t.View());

        Assert.Equal(before, t.Campaign.Events.Count);
        Assert.Equal(1, first.Tension);
        var foreshadow = Assert.Single(first.Cues);
        Assert.Equal(DirectorBeatKind.Foreshadow, foreshadow.Beat);
        Assert.Equal("clue", foreshadow.ThreadKey);
        Assert.Contains("hidden cargo", foreshadow.Guidance);
        Assert.Empty(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);
        Assert.DoesNotContain(secret.Id, t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single()
            .Knowledge.Where(k => k.FactId.HasValue).Select(k => k.FactId!.Value));

        var repeat = director.Observe(t.View());
        Assert.Empty(repeat.Cues);
        Assert.Equal(1, repeat.Tension);

        var engine = new SessionEngine(t.Campaign, new BasicD20RuleSystem(),
            new DiceRoller(new FixedRoll()));
        Assert.True(engine.Execute(new SkillCheckIntent(t.Player.Id, t.Hero.Id,
            "wisdom", 12, "Search for evidence")).Accepted);
        var second = director.Observe(t.View());
        Assert.Equal(2, second.Tension);
        Assert.Equal(DirectorBeatKind.Discovery, Assert.Single(second.Cues).Beat);

        t.Campaign.ResolveQuest(quest.Id, QuestStatus.Completed);
        var third = director.Observe(t.View());
        Assert.Equal(0, third.Tension);
        Assert.Equal(DirectorBeatKind.Consequence, Assert.Single(third.Cues).Beat);
        Assert.Equal("promise", third.Cues.Single().ThreadKey);
        Assert.Empty(t.Campaign.CreatePlayerView(t.Player.Id).Characters.Single().Knowledge);
    }

    [Fact]
    public void Combat_pressure_prefers_threats_and_never_forces_outcomes()
    {
        var t = Table.Create();
        var director = t.CreateDirector();
        var template = t.View();
        var synthetic = template with
        {
            Revision = template.Revision with { Clock = 4 },
            RecentEvents = Array.AsReadOnly(new[]
            {
                new GameEvent(1, "combat-start", null, null, "4"),
                new GameEvent(2, "combat-attack", t.Player.Id, t.Hero.Id, "hit"),
                new GameEvent(3, "combat-finished", null, null, "last-combatant-standing")
            })
        };

        var result = director.Observe(synthetic);
        Assert.Equal(3, result.Tension);
        Assert.Equal(new[] {
            DirectorBeatKind.Escalation,
            DirectorBeatKind.Escalation,
            DirectorBeatKind.Consequence
        }, result.Cues.Select(x => x.Beat));
        Assert.Equal("threat", result.Cues[0].ThreadKey);
        Assert.Equal("threat", result.Cues[1].ThreadKey);
        Assert.Contains("player agency", result.Cues[0].Guidance);
        Assert.Empty(t.Campaign.Events);
    }

    [Fact]
    public void Two_directors_produce_identical_results_for_same_events_and_threads()
    {
        var t = Table.Create();
        t.Campaign.EnterScene(t.Campaign.AddScene(SceneId.New(), "Crossroads").Id);
        var a = t.CreateDirector();
        var b = t.CreateDirector();

        var first = a.Observe(t.View());
        var second = b.Observe(t.View());

        Assert.Equal(first.Tension, second.Tension);
        Assert.Equal(first.Threads, second.Threads);
        Assert.Equal(first.Cues, second.Cues);
        Assert.Equal(first.LastProcessedEventSequence, second.LastProcessedEventSequence);
    }

    [Fact]
    public void Failed_quest_increases_pressure_while_completed_quest_releases_it()
    {
        var failed = Table.Create();
        var failedQuest = failed.Campaign.AddQuest(QuestId.New(), "Save the hostages");
        failed.Campaign.ResolveQuest(failedQuest.Id, QuestStatus.Failed);
        var failedFrame = failed.CreateDirector().Observe(failed.View());
        Assert.Equal(3, failedFrame.Tension);
        Assert.Equal(DirectorBeatKind.Consequence, Assert.Single(failedFrame.Cues).Beat);

        var completed = Table.Create();
        var completedQuest = completed.Campaign.AddQuest(QuestId.New(), "Save the hostages");
        completed.Campaign.ResolveQuest(completedQuest.Id, QuestStatus.Completed);
        var completedFrame = completed.CreateDirector().Observe(completed.View());
        Assert.Equal(0, completedFrame.Tension);
        Assert.Equal(DirectorBeatKind.Consequence, Assert.Single(completedFrame.Cues).Beat);
    }

    [Fact]
    public void Threads_can_be_resolved_without_changing_campaign()
    {
        var t = Table.Create();
        var director = t.CreateDirector();
        director.Resolve("clue");
        var scene = t.Campaign.AddScene(SceneId.New(), "Village");
        t.Campaign.EnterScene(scene.Id);
        var result = director.Observe(t.View());

        Assert.True(result.Threads.Single(x => x.Key == "clue").Resolved);
        Assert.NotEqual("clue", Assert.Single(result.Cues).ThreadKey);
        Assert.All(result.Cues, c => Assert.DoesNotContain("hidden cargo", c.Guidance));
    }

    [Fact]
    public void Snapshot_must_belong_to_bound_gm_and_cannot_regress()
    {
        var t = Table.Create();
        var director = t.CreateDirector();
        var first = t.View();
        var wrongGm = first with { ParticipantId = t.Player.Id };

        Assert.Throws<InvalidOperationException>(() => director.Observe(wrongGm));

        director.Observe(first);
        t.Campaign.EnterScene(t.Campaign.AddScene(SceneId.New(), "Cliffs").Id);
        director.Observe(t.View());

        Assert.Throws<InvalidOperationException>(() => director.Observe(first));
    }

    [Fact]
    public void Missing_more_than_24_events_is_detected_not_silently_skipped()
    {
        var t = Table.Create();
        var director = t.CreateDirector();
        t.Campaign.EnterScene(t.Campaign.AddScene(SceneId.New(), "Valley").Id);
        director.Observe(t.View());

        for (var i = 0; i < 30; i++) t.Campaign.AdvanceTime(1);

        Assert.Throws<InvalidOperationException>(() => director.Observe(t.View()));
    }

    [Fact]
    public void Thread_identity_and_pressure_are_bounded_and_snapshot_is_readonly()
    {
        var t = Table.Create();
        var director = new DirectorRuntime(t.Campaign.Id, t.GameMaster.Id);
        Assert.Throws<ArgumentException>(() =>
            director.Track(new DirectorThread("INVALID NAME", DirectorThreadKind.Threat, "bad")));
        Assert.Throws<ArgumentException>(() =>
            director.Track(new DirectorThread("bad", DirectorThreadKind.Clue, "", 0)));
        Assert.Throws<ArgumentException>(() =>
            director.Track(new DirectorThread("bad", DirectorThreadKind.Promise, "text", 11)));
        director.Track(new DirectorThread("danger", DirectorThreadKind.Threat, "Danger looms"));
        Assert.Throws<InvalidOperationException>(() =>
            director.Track(new DirectorThread("danger", DirectorThreadKind.Threat, "duplicate")));

        var frame = director.Observe(t.View());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<DirectorThread>)frame.Threads)[0] =
                new DirectorThread("injected", DirectorThreadKind.Clue, "injected"));
        Assert.Empty(t.Campaign.Events);
    }

    private sealed class FixedRoll : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    private sealed class Table
    {
        private Table(Campaign campaign, Participant player, Participant gm, Character hero)
        {
            Campaign = campaign;
            Player = player;
            GameMaster = gm;
            Hero = hero;
        }

        public Campaign Campaign { get; }
        public Participant Player { get; }
        public Participant GameMaster { get; }
        public Character Hero { get; }
        public GameMasterView View() => Campaign.CreateGameMasterView(GameMaster.Id);

        public DirectorRuntime CreateDirector()
        {
            var director = new DirectorRuntime(Campaign.Id, GameMaster.Id);
            director.Track(new DirectorThread("threat", DirectorThreadKind.Threat,
                "The raiders close in."));
            director.Track(new DirectorThread("clue", DirectorThreadKind.Clue,
                "Traces of hidden cargo."));
            director.Track(new DirectorThread("promise", DirectorThreadKind.Promise,
                "The village expects a safe return."));
            return director;
        }

        public static Table Create()
        {
            var campaign = new Campaign(CampaignId.New(), "Campaign");
            var hero = new Character(CharacterId.New(), "Hero");
            campaign.AddCharacter(hero);
            var player = new Participant(ParticipantId.New(), "Player",
                ControllerKind.Human, SeatRole.Player, [hero.Id]);
            var gm = new Participant(ParticipantId.New(), "GM",
                ControllerKind.Agent, SeatRole.GameMaster);
            campaign.AddParticipant(player);
            campaign.AddParticipant(gm);
            return new Table(campaign, player, gm, hero);
        }
    }
}
