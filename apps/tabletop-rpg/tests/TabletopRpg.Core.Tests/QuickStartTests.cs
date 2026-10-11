using TabletopRpg.Core;
using TabletopRpg.Director;
using TabletopRpg.Npc;
using TabletopRpg.Persistence;
using TabletopRpg.QuickStart;

namespace TabletopRpg.Core.Tests;

public sealed class QuickStartTests
{
    [Theory]
    [InlineData(QuickStartTheme.Fantasy)]
    [InlineData(QuickStartTheme.Mystery)]
    [InlineData(QuickStartTheme.Frontier)]
    public void Every_theme_opens_a_playable_session_without_manual_preparation(QuickStartTheme theme)
    {
        var start = QuickStartFactory.Create(new QuickStartOptions(theme, "Alex", "Minha campanha"));
        var campaign = start.Campaign;

        Assert.Equal("Minha campanha", campaign.Name);
        Assert.NotNull(campaign.ActiveSessionId);
        Assert.Equal(start.StartingSceneId, campaign.ActiveSceneId);
        Assert.Single(campaign.Sessions);
        Assert.Equal("Primeira sessão", campaign.Sessions.Single().Name);
        Assert.Equal(start.StartingQuestId, Assert.Single(campaign.Quests).Id);
        Assert.Equal(QuestStatus.Open, campaign.Quests.Single().Status);
        Assert.Equal(start.StartingSceneId, campaign.Quests.Single().SceneId);
        Assert.False(string.IsNullOrWhiteSpace(start.OpeningText));
        Assert.Equal(3, campaign.Resources.Count);
        Assert.Equal(1, campaign.Inventory.Count);
        Assert.Equal(start.HeroCharacterId, campaign.Inventory.Single().Owner);

        Assert.True(campaign.TryGetParticipant(start.HumanParticipantId, out var human));
        Assert.Equal(ControllerKind.Human, human.Controller);
        Assert.Equal(SeatRole.Player, human.Role);
        Assert.True(human.Controls(start.HeroCharacterId));
        Assert.False(human.Controls(start.NpcCharacterId));
        Assert.True(campaign.TryGetCharacter(start.HeroCharacterId, out var hero));
        Assert.Equal("Alex", hero.Name);

        Assert.True(campaign.TryGetParticipant(start.CompanionParticipantId, out var companion));
        Assert.Equal(ControllerKind.Agent, companion.Controller);
        Assert.Equal(SeatRole.Player, companion.Role);
        Assert.True(companion.Controls(start.CompanionCharacterId));
        Assert.False(companion.Controls(start.NpcCharacterId));

        Assert.True(campaign.TryGetParticipant(start.GameMasterParticipantId, out var gm));
        Assert.Equal(ControllerKind.Agent, gm.Controller);
        Assert.Equal(SeatRole.GameMaster, gm.Role);
        Assert.True(gm.Controls(start.NpcCharacterId));
        Assert.False(gm.Controls(start.HeroCharacterId));
        Assert.False(gm.Controls(start.CompanionCharacterId));

        Assert.Equal(start.CompanionCharacterId, start.CompanionProfile.CharacterId);
        Assert.Equal(start.CompanionParticipantId, start.CompanionProfile.ParticipantId);
        Assert.NotEmpty(start.CompanionProfile.Goals);

        Assert.Equal(start.NpcCharacterId, start.NpcProfile.CharacterId);
        Assert.Equal(start.GameMasterParticipantId, start.NpcProfile.GameMasterId);
        Assert.Equal(start.StartingSceneId, start.NpcProfile.SceneId);

        Assert.Equal(3, start.DirectorThreads.Count);
        Assert.Equal(new[]
        {
            DirectorThreadKind.Threat, DirectorThreadKind.Clue, DirectorThreadKind.Promise
        }, start.DirectorThreads.Select(t => t.Kind));
    }

    [Theory]
    [InlineData(QuickStartTheme.Fantasy)]
    [InlineData(QuickStartTheme.Mystery)]
    [InlineData(QuickStartTheme.Frontier)]
    public void Every_theme_preserves_hidden_truth_and_subjective_player_knowledge(QuickStartTheme theme)
    {
        var start = QuickStartFactory.Create(new QuickStartOptions(theme));
        var hero = start.Campaign.CreatePlayerView(start.HumanParticipantId);
        var companion = start.Campaign.CreatePlayerView(start.CompanionParticipantId);
        var gm = start.Campaign.CreateGameMasterView(start.GameMasterParticipantId);

        Assert.NotEmpty(gm.WorldFacts);
        var secret = Assert.Single(gm.WorldFacts).Statement;
        Assert.DoesNotContain(secret, start.OpeningText);
        Assert.DoesNotContain(secret, QuickStartFactory.Choices.Single(c => c.Theme == theme).PublicPitch);
        Assert.DoesNotContain(secret, string.Join("|", start.Campaign.Events.Select(e => e.Text)));

        var heroView = Assert.Single(hero.Characters);
        var rumor = Assert.Single(heroView.Knowledge);
        Assert.Null(rumor.FactId);
        Assert.Equal("rumor", rumor.Source);
        Assert.Equal(0.55, rumor.Confidence, 2);
        Assert.DoesNotContain(secret, rumor.Statement);
        Assert.Single(heroView.Memories);

        var companionView = Assert.Single(companion.Characters);
        Assert.Empty(companionView.Knowledge);
        Assert.Empty(companionView.Memories);
        Assert.Null(typeof(PlayerView).GetProperty("WorldFacts"));
    }

    [Fact]
    public void Default_creation_is_ready_without_choices_and_without_provider()
    {
        var start = QuickStartFactory.Create();
        Assert.Equal(3, QuickStartFactory.Choices.Count);
        Assert.Equal("Viajante",
            start.Campaign.CreatePlayerView(start.HumanParticipantId).Characters.Single().Name);
        Assert.Equal("Ecos da Ponte Velha", start.Campaign.Name);
        Assert.NotNull(start.Campaign.ActiveSessionId);
        Assert.NotEmpty(start.OpeningText);
    }

    [Fact]
    public void User_can_speak_and_companion_uses_same_session_engine_immediately()
    {
        var start = QuickStartFactory.Create();
        var engine = new SessionEngine(start.Campaign, new BasicD20RuleSystem(),
            new DiceRoller(new SystemRandomSource()));

        var human = engine.Execute(new SpeakIntent(start.HumanParticipantId,
            start.HeroCharacterId, "Vou investigar."));
        var agent = engine.Execute(new SpeakIntent(start.CompanionParticipantId,
            start.CompanionCharacterId, "Eu vou junto."));
        var unauthorized = engine.Execute(new SpeakIntent(start.CompanionParticipantId,
            start.HeroCharacterId, "Não tenho permissão."));

        Assert.True(human.Accepted);
        Assert.True(agent.Accepted);
        Assert.False(unauthorized.Accepted);
        Assert.Equal(2, start.Campaign.Events.Count(e => e.Kind == "speech"));
    }

    [Theory]
    [InlineData(QuickStartTheme.Fantasy)]
    [InlineData(QuickStartTheme.Mystery)]
    [InlineData(QuickStartTheme.Frontier)]
    public void Existing_v2_codec_saves_and_restores_immediate_game_state(QuickStartTheme theme)
    {
        var start = QuickStartFactory.Create(new QuickStartOptions(theme));
        var codec = new CampaignCodec();

        var restored = codec.Decode(codec.Encode(start.Campaign));

        Assert.Equal(start.Campaign.Id, restored.Id);
        Assert.Equal(start.Campaign.Name, restored.Name);
        Assert.Equal(start.Campaign.ActiveSceneId, restored.ActiveSceneId);
        Assert.Equal(start.Campaign.ActiveSessionId, restored.ActiveSessionId);
        Assert.Equal(start.Campaign.Events.Count, restored.Events.Count);
        Assert.Equal(start.StartingQuestId, Assert.Single(restored.Quests).Id);
        Assert.Equal(start.Campaign.Resources.Count, restored.Resources.Count);
        Assert.Equal(start.Campaign.Inventory.Count, restored.Inventory.Count);
        Assert.Single(restored.CreatePlayerView(start.HumanParticipantId).Characters.Single().Knowledge);
        Assert.Empty(restored.CreatePlayerView(start.CompanionParticipantId).Characters.Single().Knowledge);
        Assert.Single(restored.CreateGameMasterView(start.GameMasterParticipantId).WorldFacts);
    }

    [Fact]
    public void Quickstart_profiles_are_transient_but_npc_and_director_can_initialize_without_new_persistence()
    {
        var start = QuickStartFactory.Create();
        var engine = new SessionEngine(start.Campaign, new BasicD20RuleSystem(),
            new DiceRoller(new SystemRandomSource()));
        var npcs = new NpcRuntime(start.Campaign, engine);
        npcs.Register(start.NpcProfile);
        Assert.Equal(start.NpcCharacterId, npcs.Snapshot(start.NpcCharacterId).CharacterId);

        var director = new DirectorRuntime(start.Campaign.Id, start.GameMasterParticipantId);
        foreach (var thread in start.DirectorThreads) director.Track(thread);
        var frame = director.Observe(start.Campaign.CreateGameMasterView(start.GameMasterParticipantId));
        Assert.Equal(3, frame.Threads.Count);
        Assert.Contains(frame.Cues, cue => cue.Beat == DirectorBeatKind.Foreshadow);
        Assert.Equal(0, start.Campaign.Events.Count(e => e.Kind == "speech"));
    }

    [Fact]
    public void Every_new_quickstart_has_distinct_campaign_and_character_identity()
    {
        var a = QuickStartFactory.Create();
        var b = QuickStartFactory.Create();

        Assert.NotEqual(a.Campaign.Id, b.Campaign.Id);
        Assert.NotEqual(a.HeroCharacterId, b.HeroCharacterId);
        Assert.NotEqual(a.GameMasterParticipantId, b.GameMasterParticipantId);
        Assert.NotEqual(a.StartingSceneId, b.StartingSceneId);
        Assert.NotEqual(a.StartingQuestId, b.StartingQuestId);
    }

    [Fact]
    public void Invalid_options_fail_before_creating_a_campaign()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            QuickStartFactory.Create(new QuickStartOptions((QuickStartTheme)99)));
        Assert.Throws<ArgumentException>(() =>
            QuickStartFactory.Create(new QuickStartOptions(HeroName: " ")));
        Assert.Throws<ArgumentException>(() =>
            QuickStartFactory.Create(new QuickStartOptions(CampaignName: "")));
        Assert.Throws<ArgumentException>(() =>
            QuickStartFactory.Create(new QuickStartOptions(HeroName: new string('x', 81))));
    }

    [Fact]
    public void Public_choice_menu_is_immutable_and_does_not_allow_injection()
    {
        Assert.Equal(3, QuickStartFactory.Choices.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<QuickStartChoice>)QuickStartFactory.Choices)[0] =
                new QuickStartChoice(QuickStartTheme.Fantasy, "Override", "Override"));

        var result = QuickStartFactory.Create();
        Assert.Throws<NotSupportedException>(() =>
            ((IList<DirectorThread>)result.DirectorThreads)[0] =
                new DirectorThread("override", DirectorThreadKind.Clue, "Override"));
    }
}
