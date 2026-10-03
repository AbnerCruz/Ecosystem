using TabletopRpg.Persistence;

namespace TabletopRpg.Core.Tests;

public sealed class PersistenceRoundTripTests
{
    [Fact]
    public async Task Save_and_load_round_trip_preserves_campaign_state_and_private_perspectives()
    {
        using var temp = new PersistenceTestFixture.TemporaryDirectory();
        var fixture = PersistenceTestFixture.CreateCampaign();
        var store = new FileCampaignStore(temp.Path);
        var beforeClock = fixture.Campaign.Clock;

        await store.SaveAsync(fixture.Campaign, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(fixture.Campaign.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CampaignLoadSource.Primary, loaded.Source);
        Assert.False(loaded.RepairedPrimary);
        Assert.Equal(fixture.Campaign.Id, loaded.Campaign.Id);
        Assert.Equal(fixture.Campaign.Name, loaded.Campaign.Name);
        Assert.Equal(beforeClock, loaded.Campaign.Clock);

        Assert.True(loaded.Campaign.TryGetCharacter(fixture.HeroId, out var hero));
        Assert.Equal(3, hero.AttributeModifier("strength"));
        Assert.True(loaded.Campaign.TryGetParticipant(fixture.HumanId, out var human));
        Assert.True(human.Controls(fixture.HeroId));

        var session = Assert.Single(loaded.Campaign.Sessions);
        Assert.Equal(fixture.SessionId, session.Id);
        Assert.Null(session.EndedAt);
        Assert.Equal(fixture.SessionId, loaded.Campaign.ActiveSessionId);

        var gameEvent = Assert.Single(loaded.Campaign.Events);
        Assert.Equal("speech", gameEvent.Kind);
        Assert.Equal(fixture.HumanId, gameEvent.ParticipantId);
        Assert.Equal(fixture.HeroId, gameEvent.CharacterId);

        var view = loaded.Campaign.CreatePlayerView(fixture.HumanId);
        var characterView = Assert.Single(view.Characters);
        Assert.Equal(2, characterView.Knowledge.Count);
        Assert.Contains(characterView.Knowledge, claim => claim.FactId == fixture.KnownFactId);
        Assert.Contains(characterView.Knowledge, claim =>
            claim.FactId is null && claim.Statement == "The guard looks nervous.");
        Assert.Single(characterView.Memories);
        Assert.DoesNotContain(characterView.Knowledge, claim => claim.FactId == fixture.SecretFactId);

        loaded.Campaign.RevealFactTo(
            fixture.HeroId,
            fixture.SecretFactId,
            "opened the sealed letter");

        Assert.Contains(
            loaded.Campaign.CreatePlayerView(fixture.HumanId).Characters.Single().Knowledge,
            claim => claim.FactId == fixture.SecretFactId);
    }

    [Fact]
    public async Task Export_and_import_are_portable_without_a_campaign_store()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        await using var stream = new MemoryStream();

        await codec.ExportAsync(
            fixture.Campaign,
            stream,
            TestContext.Current.CancellationToken);

        stream.Position = 0;
        var imported = await codec.ImportAsync(
            stream,
            TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Campaign.Id, imported.Id);
        Assert.Equal(fixture.Campaign.Clock, imported.Clock);
        Assert.Equal(fixture.SessionId, imported.ActiveSessionId);
        Assert.Single(imported.Events);
        Assert.Equal(
            fixture.Campaign.CreatePlayerView(fixture.HumanId).Characters.Single().Knowledge,
            imported.CreatePlayerView(fixture.HumanId).Characters.Single().Knowledge);
    }

    [Fact]
    public async Task Import_can_persist_the_portable_campaign_into_a_new_store()
    {
        using var sourceDir = new PersistenceTestFixture.TemporaryDirectory();
        using var targetDir = new PersistenceTestFixture.TemporaryDirectory();
        var fixture = PersistenceTestFixture.CreateCampaign();
        var sourceStore = new FileCampaignStore(
            sourceDir.Path,
            new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider()));
        var targetStore = new FileCampaignStore(
            targetDir.Path,
            new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider()));
        await using var stream = new MemoryStream();

        await sourceStore.ExportAsync(
            fixture.Campaign,
            stream,
            TestContext.Current.CancellationToken);
        stream.Position = 0;

        var imported = await targetStore.ImportAsync(
            stream,
            saveToStore: true,
            TestContext.Current.CancellationToken);
        var loaded = await targetStore.LoadAsync(
            imported.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Campaign.Id, loaded.Campaign.Id);
        Assert.Equal(CampaignLoadSource.Primary, loaded.Source);
        Assert.True(File.Exists(
            PersistenceTestFixture.PrimaryPath(targetDir.Path, fixture.Campaign.Id)));
    }
}
