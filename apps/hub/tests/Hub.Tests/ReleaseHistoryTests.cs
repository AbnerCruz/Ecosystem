using Hub.Core;

namespace Hub.Tests;

public sealed class ReleaseHistoryTests
{
    static readonly ReleaseChannel Channel = new(new("acme", "alpha"), "https://github.com/acme/alpha/releases", "alpha-v");
    static ReleaseInfo Release(string tag, string? date = null, string? body = "Notas") => new(tag, "Release " + tag, tag.Contains('-'), date,
        "https://example.invalid/" + tag, 0, [], BodyMarkdown: body);
    static HubSnapshot Snapshot(Datum<IReadOnlyList<ReleaseInfo>> data, ReleaseChannel? channel = null) => new(
        [new("alpha", Datum<string>.From("Alpha", "eco"), Datum<string>.From("product", "eco"), Datum<string>.From("active", "eco"),
            Datum<string>.From("9.0.0", "apps/alpha/VERSION"), Datum<string>.From("repo", "eco"), Datum<string>.Missing("eco"))],
        new([], [], [], []), new Dictionary<string, Datum<IReadOnlyList<ReleaseInfo>>> { ["alpha"] = data }, false,
        new Dictionary<string, Datum<ReleaseChannel>> { ["alpha"] = Datum<ReleaseChannel>.From(channel ?? Channel, "profile") });
    static Datum<IReadOnlyList<ReleaseInfo>> Data(params ReleaseInfo[] values) => Datum<IReadOnlyList<ReleaseInfo>>.From(values, "provider://releases/alpha");

    [Fact]
    public void OrderIsPublicationDateThenSemanticVersionWithPrefixAndStableTie()
    {
        var history = ReleaseHistoryBuilder.Build(Snapshot(Data(Release("alpha-v1.10.0"), Release("alpha-v1.2.0"),
            Release("alpha-v1.0.0", "2026-10-03T00:00:00Z"), Release("alpha-v1.9.0", "2026-10-02T00:00:00Z"))), "alpha");
        Assert.Equal(["1.0.0", "1.9.0", "1.10.0", "1.2.0"], history.Releases.Select(r => r.Version));
        Assert.Equal("9.0.0", history.SourceVersion.Value); Assert.Null(history.InstalledVersion.Value);
        Assert.False(history.Changes.Comparable); Assert.Equal("provider://releases/alpha", history.Releases[0].Source);
    }

    [Fact]
    public void UnknownVersionsRemainVisibleAndHistoryIsBounded()
    {
        var data = Enumerable.Range(0, 110).Select(i => Release("nightly-" + i)).ToArray();
        var history = ReleaseHistoryBuilder.Build(Snapshot(Data(data)), "alpha");
        Assert.Equal(100, history.Releases.Count); Assert.Contains(history.Releases, r => r.Version == "nightly-0");
    }

    [Fact]
    public void SinceVersionIsDeterministicIncludesPrereleasesAndWarnsAboutUnknownTags()
    {
        var history = ReleaseHistoryBuilder.Build(Snapshot(Data(Release("alpha-v1.4.0"), Release("alpha-v1.2.0"), Release("alpha-v1.3.1"),
            Release("alpha-v1.3.0-beta.10"), Release("alpha-v1.3.0-beta.2"), Release("alpha-vnightly"), Release("alpha-v1.2.1"))), "alpha",
            Datum<string>.From("1.2.0", "os"));
        Assert.True(history.Changes.Comparable); Assert.True(history.Changes.BaseFound);
        Assert.Equal(["1.2.1", "1.3.0-beta.2", "1.3.0-beta.10", "1.3.1", "1.4.0"], history.Changes.Releases.Select(r => r.Version));
        Assert.Contains("1 tag(s)", history.Changes.Note);
        Assert.Equal("Notas", history.Changes.Releases[0].Release.BodyMarkdown);
    }

    [Fact]
    public void MissingBaseOrUnparseableInstalledVersionDoesNotClaimCompleteInterval()
    {
        var snapshot = Snapshot(Data(Release("alpha-v1.4.0")));
        var missing = ReleaseHistoryBuilder.Build(snapshot, "alpha", Datum<string>.From("1.2.0", "os"));
        Assert.False(missing.Changes.BaseFound); Assert.Contains("incompleto", missing.Changes.Note);
        Assert.False(ReleaseHistoryBuilder.Build(snapshot, "alpha", Datum<string>.From("nightly", "os")).Changes.Comparable);
        Assert.Empty(ReleaseHistoryBuilder.Build(snapshot, "alpha", Datum<string>.From("1.5.0", "os")).Changes.Releases);
    }

    [Fact]
    public void StatesDistinguishEmptyUnavailableLoadingAndStale()
    {
        Assert.Equal(ReleaseHistoryState.Empty, ReleaseHistoryBuilder.Build(Snapshot(Data()), "alpha").State);
        var missing = Snapshot(Datum<IReadOnlyList<ReleaseInfo>>.Missing("provider", "HTTP 503"));
        Assert.Equal(ReleaseHistoryState.Unavailable, ReleaseHistoryBuilder.Build(missing, "alpha").State);
        Assert.Equal(ReleaseHistoryState.Loading, ReleaseHistoryBuilder.Build(missing, "alpha", loading: true).State);
        var stale = Snapshot(Data(Release("alpha-v1.0.0")).AsStale());
        Assert.Equal(ReleaseHistoryState.Stale, ReleaseHistoryBuilder.Build(stale, "alpha").State);
        Assert.True(ReleaseHistoryBuilder.Build(stale, "alpha", loading: true).Refreshing);
        Assert.Equal(ReleaseHistoryState.Unavailable, ReleaseHistoryBuilder.Build(null, "missing").State);
    }

    [Fact]
    public void PartialFailurePreservesNotesFromSameChannelAndPersistsStaleProvenance()
    {
        var cached = SnapshotCache.Serialize(Snapshot(Data(Release("alpha-v1.0.0", body: "**Body**"))));
        var fresh = Snapshot(Datum<IReadOnlyList<ReleaseInfo>>.Missing("provider", "HTTP 503"));
        var choice = SnapshotPolicy.Choose(fresh, cached);
        var history = ReleaseHistoryBuilder.Build(choice.Show, "alpha");
        Assert.False(choice.Show!.Stale); Assert.Equal(ReleaseHistoryState.Stale, history.State);
        Assert.Equal("**Body**", Assert.Single(history.Releases).Release.BodyMarkdown);
        Assert.Contains("503", history.Note);
        Assert.Equal("**Body**", SnapshotCache.Load(choice.ToCache)!.ProductReleases["alpha"].Value![0].BodyMarkdown);
    }

    [Fact]
    public void ChangedChannelOrSuccessfulEmptyNeverUsesOldReleaseCache()
    {
        var cached = SnapshotCache.Serialize(Snapshot(Data(Release("alpha-v1.0.0"))));
        var changed = Snapshot(Datum<IReadOnlyList<ReleaseInfo>>.Missing("new-provider", "error"), Channel with { Repository = new("acme", "new") });
        Assert.Equal(ReleaseHistoryState.Unavailable, ReleaseHistoryBuilder.Build(SnapshotPolicy.Choose(changed, cached).Show, "alpha").State);
        Assert.Empty(SnapshotPolicy.Choose(Snapshot(Data()), cached).Show!.ProductReleases["alpha"].Value!);
    }

    [Fact]
    public void OldCacheWithoutBodyIsCompatibleAndDoesNotInventNotes()
    {
        var json = SnapshotCache.Serialize(Snapshot(Data(Release("alpha-v1.0.0", body: null)))).Replace(",\"BodyMarkdown\":null", "");
        var loaded = SnapshotCache.Load(json); Assert.NotNull(loaded);
        Assert.Null(ReleaseHistoryBuilder.Build(loaded, "alpha").Releases[0].Release.BodyMarkdown);
        Assert.Equal(ReleaseHistoryState.Stale, ReleaseHistoryBuilder.Build(loaded, "alpha").State);
    }

    [Fact]
    public async Task ProviderBoundaryUsesExistingReleaseModelWithoutGitHubDomain()
    { IReleaseProvider provider = new FixtureProvider(); Assert.Equal("1.0.0", (await provider.ReadReleasesAsync(TestContext.Current.CancellationToken)).Value![0].Tag); }
    sealed class FixtureProvider : IReleaseProvider
    { public Task<Datum<IReadOnlyList<ReleaseInfo>>> ReadReleasesAsync(CancellationToken ct = default) => Task.FromResult(Data(Release("1.0.0"))); }
}
