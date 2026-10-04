using TabletopRpg.Persistence;

namespace TabletopRpg.Core.Tests;

public sealed class PersistenceRecoveryTests
{
    [Fact]
    public async Task Second_save_keeps_previous_valid_snapshot_as_backup_and_corrupt_primary_recovers()
    {
        using var temp = new PersistenceTestFixture.TemporaryDirectory();
        var fixture = PersistenceTestFixture.CreateCampaign();
        var store = new FileCampaignStore(temp.Path);

        await store.SaveAsync(fixture.Campaign, TestContext.Current.CancellationToken);
        var firstClock = fixture.Campaign.Clock;

        fixture.Campaign.Remember(fixture.HeroId, "This exists only in the newer primary.");
        await store.SaveAsync(fixture.Campaign, TestContext.Current.CancellationToken);

        var primary = PersistenceTestFixture.PrimaryPath(temp.Path, fixture.Campaign.Id);
        var backup = primary + ".bak";
        Assert.True(File.Exists(primary));
        Assert.True(File.Exists(backup));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));

        await File.WriteAllTextAsync(
            primary,
            "{ this is corrupt",
            TestContext.Current.CancellationToken);

        var recovered = await store.LoadAsync(
            fixture.Campaign.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(CampaignLoadSource.Backup, recovered.Source);
        Assert.True(recovered.RepairedPrimary);
        Assert.Equal(firstClock, recovered.Campaign.Clock);
        Assert.DoesNotContain(
            recovered.Campaign.CreatePlayerView(fixture.HumanId).Characters.Single().Memories,
            memory => memory.Summary.Contains("newer primary", StringComparison.Ordinal));

        var reloaded = await store.LoadAsync(
            fixture.Campaign.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(CampaignLoadSource.Primary, reloaded.Source);
        Assert.False(reloaded.RepairedPrimary);
        Assert.Equal(firstClock, reloaded.Campaign.Clock);
    }

    [Fact]
    public async Task Invalid_primary_and_invalid_backup_fail_with_recovery_error()
    {
        using var temp = new PersistenceTestFixture.TemporaryDirectory();
        var fixture = PersistenceTestFixture.CreateCampaign();
        var store = new FileCampaignStore(temp.Path);

        await store.SaveAsync(fixture.Campaign, TestContext.Current.CancellationToken);
        fixture.Campaign.Remember(fixture.HeroId, "newer");
        await store.SaveAsync(fixture.Campaign, TestContext.Current.CancellationToken);

        var primary = PersistenceTestFixture.PrimaryPath(temp.Path, fixture.Campaign.Id);
        await File.WriteAllTextAsync(primary, "bad primary", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            primary + ".bak",
            "bad backup",
            TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<CampaignRecoveryException>(
            () => store.LoadAsync(
                fixture.Campaign.Id,
                TestContext.Current.CancellationToken));

        Assert.NotNull(error.PrimaryError);
        Assert.NotNull(error.BackupError);
    }
    [Fact]
    public async Task Concurrent_store_instances_do_not_corrupt_the_same_campaign_file()
    {
        using var temp = new PersistenceTestFixture.TemporaryDirectory();
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var snapshot = codec.Encode(fixture.Campaign);
        var firstCampaign = codec.Decode(snapshot);
        var secondCampaign = codec.Decode(snapshot);
        firstCampaign.Remember(fixture.HeroId, "save from first store");
        secondCampaign.Remember(fixture.HeroId, "save from second store");

        var firstStore = new FileCampaignStore(temp.Path, codec);
        var secondStore = new FileCampaignStore(temp.Path, codec);

        await Task.WhenAll(
            firstStore.SaveAsync(firstCampaign, TestContext.Current.CancellationToken),
            secondStore.SaveAsync(secondCampaign, TestContext.Current.CancellationToken));

        var loaded = await firstStore.LoadAsync(
            fixture.Campaign.Id,
            TestContext.Current.CancellationToken);
        var memories = loaded.Campaign
            .CreatePlayerView(fixture.HumanId)
            .Characters.Single()
            .Memories;

        Assert.Contains(
            memories,
            memory => memory.Summary is "save from first store" or "save from second store");
        Assert.True(File.Exists(
            PersistenceTestFixture.PrimaryPath(temp.Path, fixture.Campaign.Id) + ".bak"));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

}
