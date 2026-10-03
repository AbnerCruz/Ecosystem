using System.Net;
using Hub.Core;

namespace Hub.Tests;

public class SnapshotPolicyTests
{
    const string Eco = """{ "components": { "alpha": { "name": "Alpha", "type": "product", "status": "active" } } }""";

    sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    static readonly HubOptions Options = new(new RepositoryRef("acme", "hubrepo"));
    static CancellationToken T => TestContext.Current.CancellationToken;

    static Task<HubSnapshot> Load(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new HubLoader(new HttpClient(new Fake(respond)), Options).LoadAsync(T);

    static Task<HubSnapshot> Live() => Load(r =>
        r.RequestUri!.AbsolutePath == "/acme/hubrepo/main/ecosystem.json"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Eco) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

    static Task<HubSnapshot> Down() => Load(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

    [Fact]
    public async Task LiveReadingIsShownAndSavedToTheCache()
    {
        var live = await Live();
        var choice = SnapshotPolicy.Choose(live, cachedJson: null);

        Assert.Same(live, choice.Show);
        Assert.False(choice.Show!.Stale);
        Assert.NotNull(choice.ToCache);
        Assert.Equal(["alpha"], SnapshotCache.Load(choice.ToCache)!.Products.Select(p => p.Id));
    }

    [Fact]
    public async Task FailedReadingShowsTheLastGoodStateMarkedAsStaleAndLeavesTheCacheAlone()
    {
        var cached = SnapshotCache.Serialize(await Live());
        var choice = SnapshotPolicy.Choose(await Down(), cached);

        Assert.True(choice.Show!.Stale);
        Assert.Equal(["alpha"], choice.Show.Products.Select(p => p.Id));
        Assert.Null(choice.ToCache); // a leitura ruim nunca sobrescreve o último estado bom
    }

    [Fact]
    public async Task FailedReadingWithoutPreviousStateStillShowsTheNotesInsteadOfHidingTheFailure()
    {
        var down = await Down();
        var choice = SnapshotPolicy.Choose(down, cachedJson: null);

        Assert.Same(down, choice.Show);
        Assert.Null(choice.ToCache);
        Assert.NotEmpty(choice.Show!.Timeline.Notes);
        Assert.Contains(HubScreenBuilder.Build(choice.Show).Sections, s => s.Title == "Avisos");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ quebrado")]
    public async Task BrokenCacheIsTreatedAsNoPreviousState(string? cached)
    {
        var down = await Down();
        var choice = SnapshotPolicy.Choose(down, cached);

        Assert.Same(down, choice.Show);
        Assert.Null(choice.ToCache);
    }

    [Fact]
    public void NothingAtAllMeansNothingToShow()
    {
        var choice = SnapshotPolicy.Choose(null, cachedJson: null);

        Assert.Null(choice.Show);
        Assert.Null(choice.ToCache);
        Assert.Equal(HubScreenBuilder.NoDataBanner, HubScreenBuilder.Build(choice.Show).Banner);
    }
}
