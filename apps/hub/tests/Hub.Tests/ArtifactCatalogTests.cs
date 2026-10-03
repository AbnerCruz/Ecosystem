using Hub.Core;

namespace Hub.Tests;

public class ArtifactCatalogTests
{
    static HubSnapshot Snapshot(bool stale = false)
    {
        var product = new ProductSummary("alpha", Datum<string>.From("Alpha", "manifest"),
            Datum<string>.From("product", "manifest"), Datum<string>.From("active", "manifest"),
            Datum<string>.From("1.0.0", "version"), Datum<string>.From("https://github.com/acme/alpha", "manifest"),
            Datum<string>.Missing("manifest"));
        var channel = Datum<ReleaseChannel>.From(new(new("acme", "alpha"), "https://github.com/acme/alpha/releases", null), "profile");
        var apks = Enumerable.Range(1, 6).Select(i => new ReleaseAssetInfo($"alpha-{i}.apk", $"https://github.com/acme/alpha/releases/download/v1/alpha-{i}.apk", 3, new string('a', 64))).ToArray();
        var releases = Enumerable.Range(1, 5).Select(i => new ReleaseInfo($"v{i}", null, false, null, "release", 7,
            [new("checksums.txt", "https://example.invalid/checksums", 3, null), .. apks])).ToArray();
        return new([product], new([], [], [], []),
            new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>> { ["alpha"] = Datum<IReadOnlyList<ReleaseInfo>>.From(releases, "api") }, stale,
            new Dictionary<string, Datum<ReleaseChannel>> { ["alpha"] = channel });
    }

    [Fact]
    public void OffersOnlyApksOfTheDisplayedProductsAndDeclaredChannels()
    {
        var choices = ArtifactCatalog.Choices(Snapshot());
        Assert.Equal(12, choices.Count); // três releases × quatro APKs
        Assert.All(choices, c => { Assert.Equal("alpha", c.ProductId); Assert.True(c.Asset.IsAndroidApk); Assert.False(c.Stale); });
        Assert.Equal(["v1", "v2", "v3"], choices.Select(c => c.Tag).Distinct());
        Assert.Equal(["alpha-1.apk", "alpha-2.apk", "alpha-3.apk", "alpha-4.apk"], choices.Take(4).Select(c => c.Asset.Name));
    }

    [Fact]
    public void DoesNotInventDownloadChoicesWhenCachePredatesCatalogOrChannelIsMissing()
    {
        Assert.Empty(ArtifactCatalog.Choices(null));
        Assert.Empty(ArtifactCatalog.Choices(Snapshot() with { ReleaseChannels = null }));
        Assert.Empty(ArtifactCatalog.Choices(Snapshot() with { ReleaseChannels = new Dictionary<string, Datum<ReleaseChannel>>() }));
        Assert.Empty(ArtifactCatalog.Choices(Snapshot() with { ProductReleases = new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>() }));
    }

    [Fact]
    public void PreservesTheFactThatSelectedMetadataIsStale()
    {
        Assert.All(ArtifactCatalog.Choices(Snapshot(stale: true)), c => Assert.True(c.Stale));
        var snapshot = Snapshot();
        snapshot = snapshot with { ProductReleases = new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>
            { ["alpha"] = snapshot.ProductReleases["alpha"].AsStale() } };
        Assert.All(ArtifactCatalog.Choices(snapshot), c => Assert.True(c.Stale));
    }

    [Fact]
    public void UnavailableDataNeverBecomesADownloadChoiceEvenIfValueIsPresent()
    {
        var snapshot = Snapshot();
        snapshot = snapshot with { ProductReleases = new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>>
            { ["alpha"] = snapshot.ProductReleases["alpha"] with { Availability = Availability.NotAvailable } } };
        Assert.Empty(ArtifactCatalog.Choices(snapshot));
    }
}
