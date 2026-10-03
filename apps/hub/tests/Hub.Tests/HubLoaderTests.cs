using System.Net;
using Hub.Core;

namespace Hub.Tests;

public class HubLoaderTests
{
    // Repositório e Products fictícios e neutros: o Hub descobre tudo nos dados (NN-002).
    const string Eco = """
    { "components": {
        "alpha": { "name": "Alpha", "type": "product", "status": "active",
                   "version": { "authority": "version-file", "file": "apps/alpha/VERSION" },
                   "source": { "repository": "https://github.com/acme/alpha-origin" } },
        "beta":  { "name": "Beta", "type": "product", "status": "active",
                   "version": { "authority": "version-file", "file": "apps/beta/package.json" } },
        "hub":   { "name": "Hub", "type": "product", "status": "active" } } }
    """;

    const string Roadmap = "## Fase 1 — Base\n\n- [x] P1-1 — **Feito**.\n  - Depende de: —.\n- [ ] P1-2 — **Falta**.\n  - Depende de: P1-1.\n\n**Gate:** ok.\n*Estado do gate:* **aguardando**\n";
    const string Profile = """
    {"status":"current","entries":[
      {"component":"alpha","channels":[{"kind":"github-release","role":"primary","locationFrom":"source.repository"}]},
      {"component":"beta","channels":[{"kind":"github-pages","role":"primary","locationFrom":"publicUrl"}]}]}
    """;
    const string Decisions = """{ "decisions": [ { "id": "DEC-1", "status": "pending", "title": "Pendente", "blocking": true } ] }""";
    const string HandoffDone = """{ "message_id": "HO-1", "task_id": "P1-1", "state": "done" }""";
    const string HandoffReview = """{ "message_id": "HO-2", "task_id": "P1-2", "state": "review", "verification": [ { "check": "Abrir no aparelho", "kind": "human", "result": "pending" } ] }""";

    sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Urls) Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(respond(request));
        }
    }

    static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    static HttpResponseMessage Status(HttpStatusCode c) => new(c);

    static HttpResponseMessage Route(HttpRequestMessage r)
    {
        var u = r.RequestUri!;
        if (u.Host == "raw.githubusercontent.com")
            return u.AbsolutePath switch
            {
                "/acme/hubrepo/main/ecosystem.json" => Ok(Eco),
                "/acme/hubrepo/main/docs/distribution/current.profile.json" => Ok(Profile),
                "/acme/hubrepo/main/ROADMAP.md" => Ok(Roadmap),
                "/acme/hubrepo/main/docs/governance/decisions.json" => Ok(Decisions),
                "/acme/hubrepo/main/apps/alpha/VERSION" => Ok("1.2.3\n"),
                "/acme/hubrepo/main/apps/beta/package.json" => Ok("""{ "version": "4.5.6" }"""),
                "/acme/hubrepo/main/docs/governance/handoffs/HO-1.json" => Ok(HandoffDone),
                "/acme/hubrepo/main/docs/governance/handoffs/HO-2.json" => Ok(HandoffReview),
                _ => Status(HttpStatusCode.NotFound),
            };
        return u.AbsolutePath switch
        {
            "/repos/acme/hubrepo/contents/docs/governance/handoffs" => Ok("""[ { "name": "HO-1.json", "type": "file" }, { "name": "HO-2.json", "type": "file" }, { "name": "pasta", "type": "dir" }, { "name": "LEIA.md", "type": "file" } ]"""),
            "/repos/acme/hubrepo/pulls" => Ok("""[ { "number": 5, "title": "PR", "draft": false, "html_url": "u", "head": { "ref": "x" } } ]"""),
            "/repos/acme/hubrepo/branches" => Ok("""[ { "name": "main" } ]"""),
            "/repos/acme/hubrepo/releases" => Ok("[]"),
            "/repos/acme/hubrepo/issues" => Ok("[]"),
            "/repos/acme/hubrepo/actions/runs" => Ok("""{ "workflow_runs": [] }"""),
            "/repos/acme/alpha-origin/releases" => Ok("""[ { "tag_name": "v1.2.3", "html_url": "r" } ]"""),
            _ => Status(HttpStatusCode.NotFound),
        };
    }

    static readonly HubOptions Options = new(new RepositoryRef("acme", "hubrepo"));
    static CancellationToken T => TestContext.Current.CancellationToken;

    static (HubLoader, Fake) Make(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var f = new Fake(respond ?? Route);
        return (new HubLoader(new HttpClient(f), Options), f);
    }

    [Fact]
    public async Task InjectedReleaseProviderReplacesTransportWithoutChangingDomainOrUi()
    {
        var transport = new Fake(Route);
        var provider = new ReleaseFixture();
        var snapshot = await new HubLoader(new HttpClient(transport), Options, _ => provider).LoadAsync(T);
        var history = ReleaseHistoryBuilder.Build(snapshot, "alpha");
        Assert.Equal("source://alpha/releases", history.Source);
        Assert.Equal("## Added\n- Editor", Assert.Single(history.Releases).Release.BodyMarkdown);
        Assert.DoesNotContain(transport.Urls, u => u.Contains("alpha-origin/releases"));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ProviderExceptionIsIsolatedAndNeverBreaksProductsOrTimeline()
    {
        var snapshot = await new HubLoader(new HttpClient(new Fake(Route)), Options, _ => new FailedReleaseProvider()).LoadAsync(T);
        Assert.Equal(2, snapshot.Products.Count);
        Assert.Contains(snapshot.Timeline.Past, entry => entry.Id == "P1-1");
        Assert.Equal(Availability.NotAvailable, snapshot.ProductReleases["alpha"].Availability);
        Assert.Contains("provider", snapshot.ProductReleases["alpha"].Note);
    }
    sealed class FailedReleaseProvider : IReleaseProvider
    { public Task<Datum<IReadOnlyList<ReleaseInfo>>> ReadReleasesAsync(CancellationToken ct = default) => throw new HttpRequestException("offline"); }

    sealed class ReleaseFixture : IReleaseProvider
    {
        public int Calls;
        public Task<Datum<IReadOnlyList<ReleaseInfo>>> ReadReleasesAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); Calls++;
            return Task.FromResult(Datum<IReadOnlyList<ReleaseInfo>>.From(
                [new("1.0.0", "Release", false, "2026-10-03", "https://example.invalid/release", 0, [], BodyMarkdown: "## Added\n- Editor")],
                "source://alpha/releases"));
        }
    }

    [Fact]
    public async Task LoadsProductsVersionsTimelineAndReleasesFromTheRepository()
    {
        var s = await Make().Item1.LoadAsync(T);

        Assert.False(s.Stale);
        Assert.Equal(["alpha", "beta"], s.Products.Select(p => p.Id));                       // o Hub não se lista
        Assert.Equal("1.2.3", s.Products[0].Version.Value);                                  // versão pela autoridade, lida do arquivo remoto
        Assert.Equal("4.5.6", s.Products[1].Version.Value);

        Assert.Contains(s.Timeline.Past, e => e.Id == "P1-1");
        Assert.Contains(s.Timeline.Now, e => e.Id == "DEC-1");
        Assert.Contains(s.Timeline.Now, e => e.Kind == EntryKind.HumanValidation);           // vem dos handoffs remotos
        Assert.Contains(s.Timeline.Now, e => e.Id == "PR #5");                               // vem do GitHub
        Assert.Contains(s.Timeline.Next, e => e.Id == "P1-2" && e.Detail == "pronta");
        Assert.Empty(s.Timeline.Notes);

        Assert.Equal("v1.2.3", s.ProductReleases["alpha"].Value!.Single().Tag);              // repositório de origem declarado em ecosystem.json
        Assert.Equal(Availability.NotAvailable, s.ProductReleases["beta"].Availability);      // Web não vira GitHub Releases
        Assert.Contains("primário não declarado", s.ProductReleases["beta"].Note);
        Assert.Equal("acme/alpha-origin", s.ReleaseChannels!["alpha"].Value!.Repository.FullName);
    }

    [Fact]
    public async Task ReadsOnlyJsonFilesOfTheHandoffsFolderAndNeverWrites()
    {
        var (loader, fake) = Make();
        await loader.LoadAsync(T);
        Assert.Contains(fake.Urls, u => u.EndsWith("/handoffs/HO-1.json"));
        Assert.DoesNotContain(fake.Urls, u => u.Contains("LEIA.md") || u.Contains("pasta"));
        Assert.All(fake.Urls, u => Assert.True(u.StartsWith("https://raw.githubusercontent.com/") || u.StartsWith("https://api.github.com/")));
    }

    [Fact]
    public async Task EverythingDownDegradesToNotesNotExceptions()
    {
        var s = await Make(_ => throw new HttpRequestException("sem rede")).Item1.LoadAsync(T);
        Assert.Empty(s.Products);
        Assert.Empty(s.Timeline.Past); Assert.Empty(s.Timeline.Next);
        Assert.Contains(s.Timeline.Notes, n => n.Contains("ecosystem.json indisponível") && n.Contains("falha de rede"));
        Assert.Contains(s.Timeline.Notes, n => n.Contains("ROADMAP.md indisponível"));
        Assert.Contains(s.Timeline.Notes, n => n.Contains("decisions.json indisponível"));
        Assert.Contains(s.Timeline.Notes, n => n.Contains("PRs indisponível"));
    }

    [Fact]
    public async Task ApiRateLimitOnlyHidesWhatNeedsTheApi()
    {
        // Sem token a API pública responde 403 ao exceder o limite; os arquivos (raw) continuam acessíveis.
        var s = await Make(r => r.RequestUri!.Host == "api.github.com" ? Status(HttpStatusCode.Forbidden) : Route(r)).Item1.LoadAsync(T);
        Assert.Equal(["alpha", "beta"], s.Products.Select(p => p.Id));
        Assert.Contains(s.Timeline.Past, e => e.Id == "P1-1");
        Assert.DoesNotContain(s.Timeline.Now, e => e.Kind == EntryKind.HumanValidation);                       // sem a lista de handoffs
        Assert.Contains(s.Timeline.Notes, n => n.Contains("lista de handoffs indisponível") && n.Contains("HTTP 403"));
        Assert.Contains(s.Timeline.Notes, n => n.Contains("PRs indisponível") && n.Contains("HTTP 403"));
    }

    [Fact]
    public async Task AMissingHandoffIsReportedNotHidden()
    {
        var s = await Make(r => r.RequestUri!.AbsolutePath.EndsWith("HO-2.json") ? Status(HttpStatusCode.InternalServerError) : Route(r)).Item1.LoadAsync(T);
        Assert.Contains(s.Timeline.Notes, n => n.Contains("1 handoff(s) indisponível"));
        Assert.Contains(s.Timeline.Past, e => e.Id == "HO-1");
    }

    [Fact]
    public async Task CallerCancellationIsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Make().Item1.LoadAsync(cts.Token));
    }

    [Fact]
    public async Task DeclaredPublicUrlChannelUsesMonorepoTagsAndReusesItsRead()
    {
        var eco = Eco.Replace("\"name\": \"Alpha\"", "\"publicUrl\":\"https://github.com/acme/hubrepo/releases\",\"name\": \"Alpha\"");
        var (loader, fake) = Make(r => r.RequestUri!.AbsolutePath switch
        {
            "/acme/hubrepo/main/ecosystem.json" => Ok(eco),
            "/acme/hubrepo/main/docs/distribution/current.profile.json" => Ok(Profile.Replace("source.repository", "publicUrl")),
            "/repos/acme/hubrepo/releases" => Ok("""
            [{"tag_name":"beta-v99","published_at":"2026-10-03T12:00:00Z"},
             {"tag_name":"alpha-v1","published_at":"2026-10-01T12:00:00Z"},
             {"tag_name":"alpha-v3","draft":true,"published_at":"2026-10-04T12:00:00Z"},
             {"tag_name":"alpha-v2","prerelease":true,"published_at":"2026-10-02T12:00:00Z"}]
            """),
            _ => Route(r),
        });
        var s = await loader.LoadAsync(T);
        Assert.Equal(["alpha-v2", "alpha-v1"], s.ProductReleases["alpha"].Value!.Select(x => x.Tag));
        Assert.Equal("alpha-v", s.ReleaseChannels!["alpha"].Value!.TagPrefix);
        Assert.Single(fake.Urls, u => new Uri(u).AbsolutePath == "/repos/acme/hubrepo/releases");
        Assert.DoesNotContain(fake.Urls, u => u.Contains("/repos/acme/alpha-origin/releases"));
    }

    [Fact]
    public async Task MissingProfileDoesNotGuessAChannelFromTheOrigin()
    {
        var (loader, fake) = Make(r => r.RequestUri!.AbsolutePath.EndsWith(DistributionReader.ProfilePath)
            ? Status(HttpStatusCode.NotFound) : Route(r));
        var s = await loader.LoadAsync(T);
        Assert.Equal(Availability.NotAvailable, s.ProductReleases["alpha"].Availability);
        Assert.Contains("HTTP 404", s.ProductReleases["alpha"].Note);
        Assert.DoesNotContain(fake.Urls, u => u.Contains("/repos/acme/alpha-origin/releases"));
        Assert.Contains(s.Timeline.Notes, n => n.Contains("perfil de distribuição indisponível"));
    }

    [Fact]
    public async Task CacheRoundTripComesBackStaleWithTheSameContent()
    {
        var live = await Make().Item1.LoadAsync(T);
        var cached = SnapshotCache.Load(SnapshotCache.Serialize(live));

        Assert.NotNull(cached);
        Assert.True(cached!.Stale);
        Assert.Equal(live.Products.Select(p => p.Id), cached.Products.Select(p => p.Id));
        Assert.All(cached.Products, p => Assert.Equal(Availability.Stale, p.Name.Availability));
        Assert.Equal(live.Timeline.Past.Select(e => e.Id), cached.Timeline.Past.Select(e => e.Id));
        Assert.Equal(live.Timeline.Now.Select(e => e.Kind), cached.Timeline.Now.Select(e => e.Kind));
        Assert.Equal(live.Timeline.Notes, cached.Timeline.Notes);
        Assert.Equal("v1.2.3", cached.ProductReleases["alpha"].Value!.Single().Tag);
        Assert.Equal(Availability.Stale, cached.ProductReleases["alpha"].Availability);
        Assert.Equal(Availability.Stale, cached.ReleaseChannels!["alpha"].Availability);
    }

    [Fact]
    public async Task Phase3CacheWithoutChannelOrArtifactFieldsStillLoads()
    {
        var live = await Make().Item1.LoadAsync(T);
        var n = System.Text.Json.Nodes.JsonNode.Parse(SnapshotCache.Serialize(live))!;
        n.AsObject().Remove("ReleaseChannels");
        foreach (var r in n["ProductReleases"]!["alpha"]!["Value"]!.AsArray())
        { r!.AsObject().Remove("Artifacts"); r.AsObject().Remove("ArtifactNote"); }
        var cached = SnapshotCache.Load(n.ToJsonString());
        Assert.NotNull(cached);
        Assert.True(cached.Stale);
        Assert.Null(cached.ReleaseChannels);
        Assert.Null(cached.ProductReleases["alpha"].Value!.Single().Artifacts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ quebrado")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("null")]
    public void BrokenOrEmptyCacheMeansNoPreviousState(string? cached) => Assert.Null(SnapshotCache.Load(cached));

    [Fact]
    public async Task FileSourceReadsRawTextAndListsOnlyFiles()
    {
        var files = new RepositoryFileSource(new HttpClient(new Fake(Route)), new RepositoryRef("acme", "hubrepo"));
        var text = await files.ReadTextAsync("ROADMAP.md", T);
        Assert.Equal(Roadmap, text.Value);
        Assert.Equal("https://raw.githubusercontent.com/acme/hubrepo/main/ROADMAP.md", text.Source);

        var list = await files.ListFilesAsync("docs/governance/handoffs", T);
        Assert.Equal(["HO-1.json", "HO-2.json", "LEIA.md"], list.Value);

        var missing = await files.ReadTextAsync("nao-existe.md", T);
        Assert.Equal("HTTP 404", missing.Note);
        Assert.Equal(Availability.NotAvailable, missing.Availability);
    }

    [Fact]
    public void VersionFilesListsTheAuthorityFilesOfProductsExceptTheHub()
    {
        Assert.Equal(["apps/alpha/VERSION", "apps/beta/package.json"], EcosystemReader.VersionFiles(Eco));
        Assert.Empty(EcosystemReader.VersionFiles("não é json"));
        Assert.Empty(EcosystemReader.VersionFiles("[]"));
    }
}
