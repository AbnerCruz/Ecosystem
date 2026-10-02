using System.Net;
using Hub.Core;

namespace Hub.Tests;

public class GitHubReaderTests
{
    // Respostas gravadas (recortadas) da API pública; nomes neutros — o Hub nunca nomeia Products no código (NN-002).
    const string Pulls = """
    [ { "number": 7, "title": "Primeira", "draft": false, "html_url": "https://example.invalid/pr/7", "head": { "ref": "feature-a" } },
      { "number": 9, "title": "Segunda", "draft": true,  "html_url": "https://example.invalid/pr/9", "head": { "ref": "feature-b" } },
      { "title": "sem número" } ]
    """;
    const string Branches = """[ { "name": "main" }, { "name": "feature-a" }, { "name": "feature-b" } ]""";
    const string Releases = """
    [ { "tag_name": "v1.2.0", "name": "Versão 1.2", "prerelease": false, "published_at": "2026-09-30T10:00:00Z", "html_url": "https://example.invalid/r/1", "assets": [ {}, {} ] },
      { "tag_name": "v1.3.0-beta", "name": null, "prerelease": true, "html_url": "https://example.invalid/r/2" } ]
    """;
    const string Issues = """
    [ { "number": 3, "title": "Tarefa", "html_url": "https://example.invalid/i/3", "labels": [ { "name": "state:working" }, { "name": "bug" } ] },
      { "number": 4, "title": "Sem estado", "html_url": "https://example.invalid/i/4", "labels": [] },
      { "number": 7, "title": "É um PR", "html_url": "https://example.invalid/pr/7", "labels": [], "pull_request": { "url": "x" } } ]
    """;
    const string Runs = """
    { "workflow_runs": [
      { "name": "ci", "status": "completed", "conclusion": "success", "head_branch": "main", "head_sha": "aaa", "html_url": "https://example.invalid/run/3" },
      { "name": "pages", "status": "in_progress", "conclusion": null, "head_branch": "main", "head_sha": "aaa", "html_url": "https://example.invalid/run/2" },
      { "name": "ci", "status": "completed", "conclusion": "failure", "head_branch": "main", "head_sha": "bbb", "html_url": "https://example.invalid/run/1" } ] }
    """;

    sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    static HttpResponseMessage Route(HttpRequestMessage r)
    {
        var path = r.RequestUri!.AbsolutePath;
        return path switch
        {
            "/repos/acme/widgets/pulls" => Ok(Pulls),
            "/repos/acme/widgets/branches" => Ok(Branches),
            "/repos/acme/widgets/releases" => Ok(Releases),
            "/repos/acme/widgets/issues" => Ok(Issues),
            "/repos/acme/widgets/actions/runs" => Ok(Runs),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    static CancellationToken T => TestContext.Current.CancellationToken;

    static readonly RepositoryRef Repo = new("acme", "widgets");

    static (GitHubReader Reader, Fake Handler) Make(Func<HttpRequestMessage, HttpResponseMessage>? respond = null, string? token = null)
    {
        var h = new Fake(respond ?? Route);
        return (new GitHubReader(new HttpClient(h), Repo, token), h);
    }

    [Theory]
    [InlineData("https://github.com/acme/widgets", "acme", "widgets")]
    [InlineData("https://github.com/acme/widgets/", "acme", "widgets")]
    [InlineData("https://github.com/acme/widgets.git", "acme", "widgets")]
    [InlineData("https://GitHub.com/acme/widgets/tree/main", "acme", "widgets")]
    public void ParsesRepositoryFromTheDeclaredUrl(string url, string owner, string name) =>
        Assert.Equal(new RepositoryRef(owner, name), RepositoryRef.TryParse(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é url")]
    [InlineData("https://example.invalid/acme/widgets")]
    [InlineData("https://github.com/acme")]
    public void RejectsWhatIsNotAGitHubRepositoryUrl(string? url) => Assert.Null(RepositoryRef.TryParse(url));

    [Fact]
    public async Task ReadsPullRequestsSkippingMalformedEntries()
    {
        var d = await Make().Reader.ReadPullRequestsAsync(T);
        Assert.Equal(Availability.Derived, d.Availability);
        Assert.Equal([7, 9], d.Value!.Select(p => p.Number));
        Assert.True(d.Value![1].Draft);
        Assert.Equal("feature-a", d.Value![0].HeadRef);
        Assert.Equal("https://api.github.com/repos/acme/widgets/pulls?state=open&per_page=100", d.Source);
    }

    [Fact]
    public async Task ReadsBranchesReleasesAndCiRuns()
    {
        var (r, h) = Make();
        Assert.Equal(["main", "feature-a", "feature-b"], (await r.ReadBranchesAsync(T)).Value!.Select(b => b.Name));

        var rel = (await r.ReadReleasesAsync(T)).Value!;
        Assert.Equal(2, rel[0].Assets);
        Assert.True(rel[1].Prerelease);
        Assert.Null(rel[1].PublishedAt);

        var ci = await r.ReadCiRunsAsync("main", T);
        Assert.Equal(3, ci.Value!.Count);
        Assert.Contains(h.Requests, q => q.RequestUri!.Query.Contains("branch=main"));
    }

    [Fact]
    public async Task IssuesDropPullRequestsAndExposeTheStateLabel()
    {
        var issues = (await Make().Reader.ReadIssuesAsync(T)).Value!;
        Assert.Equal([3, 4], issues.Select(i => i.Number));
        Assert.Equal("working", issues[0].State);
        Assert.Null(issues[1].State);
    }

    [Fact]
    public async Task OnlyEverIssuesGetRequestsAndNoTokenIsSentByDefault()
    {
        var (r, h) = Make();
        await r.ReadAllAsync("main", T);
        Assert.Equal(5, h.Requests.Count);
        Assert.All(h.Requests, q =>
        {
            Assert.Equal(HttpMethod.Get, q.Method);
            Assert.Null(q.Headers.Authorization);
            Assert.NotEmpty(q.Headers.UserAgent);
            Assert.Equal("api.github.com", q.RequestUri!.Host);
        });
    }

    [Fact]
    public async Task TokenIsSentOnlyWhenProvidedAtRuntime()
    {
        var (r, h) = Make(token: "token-de-teste");
        await r.ReadBranchesAsync(T);
        Assert.Equal("Bearer", h.Requests.Single().Headers.Authorization!.Scheme);

        var (blank, h2) = Make(token: "   ");
        await blank.ReadBranchesAsync(T);
        Assert.Null(h2.Requests.Single().Headers.Authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "HTTP 403")]
    [InlineData(HttpStatusCode.NotFound, "HTTP 404")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    public async Task HttpErrorsBecomeNotAvailableWithTheReason(HttpStatusCode code, string note)
    {
        var d = await Make(_ => new HttpResponseMessage(code)).Reader.ReadPullRequestsAsync(T);
        Assert.Equal(Availability.NotAvailable, d.Availability);
        Assert.Null(d.Value);
        Assert.Equal(note, d.Note);
    }

    [Fact]
    public async Task NetworkFailureNeverThrows()
    {
        var d = await Make(_ => throw new HttpRequestException("sem rede")).Reader.ReadReleasesAsync(T);
        Assert.Equal(Availability.NotAvailable, d.Availability);
        Assert.Equal("falha de rede", d.Note);
    }

    [Fact]
    public async Task TimeoutBecomesNotAvailable()
    {
        var d = await Make(_ => throw new TaskCanceledException("timeout")).Reader.ReadIssuesAsync(T);
        Assert.Equal("tempo esgotado", d.Note);
    }

    [Theory]
    [InlineData("{ quebrado", "resposta inválida")]
    [InlineData("""{ "message": "Not Found" }""", "resposta inesperada")]
    [InlineData("123", "resposta inesperada")]
    public async Task InvalidBodiesBecomeNotAvailable(string body, string note)
    {
        var d = await Make(_ => Ok(body)).Reader.ReadBranchesAsync(T);
        Assert.Equal(Availability.NotAvailable, d.Availability);
        Assert.Equal(note, d.Note);
    }

    [Fact]
    public async Task CiRunsWithoutTheExpectedPropertyAreNotAvailable()
    {
        var d = await Make(_ => Ok("[]")).Reader.ReadCiRunsAsync("main", T);
        Assert.Equal("resposta inesperada", d.Note);
    }

    [Fact]
    public async Task CancellationRequestedByTheCallerIsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (r, _) = Make(_ => throw new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => r.ReadPullRequestsAsync(cts.Token));
    }

    [Fact]
    public async Task OnePartFailingDoesNotAffectTheOthers()
    {
        var snap = await Make(r => r.RequestUri!.AbsolutePath.EndsWith("/issues") ? new HttpResponseMessage(HttpStatusCode.Forbidden) : Route(r)).Reader.ReadAllAsync("main", T);
        Assert.Equal(Availability.NotAvailable, snap.Issues.Availability);
        Assert.Equal(Availability.Derived, snap.PullRequests.Availability);
        Assert.Equal(Availability.Derived, snap.Releases.Availability);
        Assert.Equal(Availability.Derived, snap.CiRuns.Availability);
    }

    [Fact]
    public async Task TimelineGetsGitHubEntriesAndKeepsTheBaselineIntact()
    {
        var snap = await Make().Reader.ReadAllAsync("main", T);
        var baseline = new PastNowNext([new(EntryKind.RoadmapItem, "X-1", "base", "ROADMAP.md")], [], []);
        var r = TimelineGitHub.AddGitHub(baseline, snap);

        Assert.Contains(r.Past, e => e.Id == "X-1");                                         // o que já havia continua
        Assert.Equal(["v1.2.0", "v1.3.0-beta"], r.Past.Where(e => e.Kind == EntryKind.Release).Select(e => e.Id));
        Assert.Equal(["PR #7", "PR #9"], r.Now.Where(e => e.Kind == EntryKind.PullRequest).Select(e => e.Id));
        Assert.Equal("rascunho · feature-b", r.Now.Single(e => e.Id == "PR #9").Detail);
        Assert.Equal("state:working", r.Now.Single(e => e.Id == "#3").Detail);
        Assert.Equal("sem state:", r.Now.Single(e => e.Id == "#4").Detail);
        Assert.Equal(["feature-a", "feature-b"], r.Now.Where(e => e.Kind == EntryKind.Branch).Select(e => e.Id)); // a branch padrão não é "em andamento"
        Assert.Empty(r.Notes);
        Assert.Empty(r.Next);                                                                // o GitHub nunca fabrica o Next
    }

    [Fact]
    public async Task CiShowsOnlyTheLatestRunOfEachWorkflow()
    {
        var r = TimelineGitHub.AddGitHub(new PastNowNext([], [], []), await Make().Reader.ReadAllAsync("main", T));
        var ci = r.Now.Where(e => e.Kind == EntryKind.CiRun).ToArray();
        Assert.Equal(["ci", "pages"], ci.Select(e => e.Id));
        Assert.Equal("success", ci[0].Detail);       // a execução mais nova de "ci", não a falha antiga
        Assert.Equal("in_progress", ci[1].Detail);
    }

    [Fact]
    public async Task UnavailableSourcesBecomeNotesNeverInventedEntries()
    {
        var snap = await Make(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)).Reader.ReadAllAsync("main", T);
        var r = TimelineGitHub.AddGitHub(new PastNowNext([], [], []), snap);
        Assert.Empty(r.Past); Assert.Empty(r.Now); Assert.Empty(r.Next);
        Assert.Equal(5, r.Notes.Count);
        Assert.All(r.Notes, n => { Assert.Contains("acme/widgets", n); Assert.Contains("HTTP 403", n); });
    }
}
