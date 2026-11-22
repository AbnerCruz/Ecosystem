using System.Net;
using System.Text;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class LearningFeatureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "lunet-learn-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    [Fact]
    public void CatalogListsAllCoursesButOnlyFirstChallengeIsAvailable()
    {
        Assert.Equal(12, LearningCatalog.Tracks.Count);
        Assert.Equal(60, LearningCatalog.Courses.Count);
        Assert.Equal(60, LearningCatalog.Courses.Select(c => c.Id).Distinct().Count());
        Assert.Equal(1, LearningCatalog.Courses.Count(c => c.Available));
        Assert.Equal("LRN-001", LearningCatalog.Courses.First(c => c.Available).Id);
        Assert.True(LearningCatalog.IsProjectAvailable("LRN-001-P1"));
        Assert.False(LearningCatalog.IsProjectAvailable("LRN-001-P2"));
        Assert.False(LearningCatalog.IsProjectAvailable("LRN-002-P1"));
        Assert.Equal(4, LearningCatalog.FirstProjectSteps.Count);
        Assert.Equal(4, LearningCatalog.FirstProjectSteps.Select(s => s.Id).Distinct().Count());
        Assert.All(LearningCatalog.FirstProjectSteps, s => Assert.False(string.IsNullOrWhiteSpace(s.Objective)));
    }

    [Fact]
    public void LearnerProgressPersistsApartFromProjectAndCanBeRevised()
    {
        var root = Path.Combine(_folder, "personal");
        var path = Path.Combine(root, "progress.json");
        var projects = new ProjectStore(Path.Combine(_folder, "projects"));
        var game = projects.Create("Movimento");
        var original = game.ReadText("Game.cs");
        var store = new LearningProgressStore(path);
        Assert.Equal(0, store.CompletedCount);
        store.LinkProject("Movimento", game.Manifest.GameId);
        store.SetCompleted("LRN-001-P1-S1", true);
        store.SetCompleted("LRN-001-P1-S2", true);
        var reopened = new LearningProgressStore(path);
        Assert.Equal(2, reopened.CompletedCount);
        Assert.True(reopened.Completed("LRN-001-P1-S1"));
        Assert.Equal(game.Manifest.GameId, reopened.ProjectGameId);
        Assert.Equal("Movimento", reopened.ProjectName);
        reopened.SetCompleted("LRN-001-P1-S1", false);
        Assert.Equal(1, new LearningProgressStore(path).CompletedCount);
        Assert.Equal(original, game.ReadText("Game.cs"));
        Assert.Throws<ArgumentException>(() => reopened.SetCompleted("UNKNOWN", true));
        Assert.Equal(1, new LearningProgressStore(path).CompletedCount);
    }

    [Fact]
    public void BrokenProgressNeverOverwritesOriginalData()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "progress.json");
        File.WriteAllText(path, "{broken");
        Assert.Throws<InvalidDataException>(() => new LearningProgressStore(path));
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    [Fact]
    public void LauncherOpensLearnWithoutOpeningAnyProject()
    {
        var path = Path.Combine(_folder, "launcher.json");
        var pref = new LauncherPreferences(path);
        pref.SelectSection("learn");
        Assert.Equal("learn", new LauncherPreferences(path).Section);
        Assert.Null(new LauncherPreferences(path).LastProject);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string? LastBody;
        public int Requests;
        public bool Fail;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Fail ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            {
                Content = new StringContent(Fail ? "{\"error\":\"invalid key\"}"
                    : "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Posso ajudar com C# e jogos.\"}}]}",
                    Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task WorkspaceChatRespondsAndPersistsWithoutOpeningAProject()
    {
        var path = Path.Combine(_folder, "ai", "workspace-history.json");
        var transport = new FakeHandler();
        using var http = new HttpClient(transport);
        var store = new WorkspaceAiChat(path, http);
        var ai = new LunetAiLoadedSettings(
            new LunetAiSettings("openrouter", "https://openrouter.ai/api/v1/chat/completions", "test-model", 16, false),
            "test-secret-key-00000");
        var answer = await store.AskAsync("Me explique vetores.", ai);
        Assert.Contains("C#", answer);
        Assert.Equal(2, store.History.Count);
        Assert.Equal("user", store.History[0].Role);
        Assert.Equal("assistant", store.History[1].Role);
        Assert.Equal(2, new WorkspaceAiChat(path, http).History.Count);
        Assert.Equal(1, transport.Requests);
        Assert.Contains("Me explique vetores", transport.LastBody!);
        Assert.DoesNotContain("Game.cs", transport.LastBody!);
        Assert.DoesNotContain("test-secret-key", File.ReadAllText(path));
        store.Clear();
        Assert.Empty(new WorkspaceAiChat(path, http).History);
    }

    [Fact]
    public async Task GlobalChatDoesNotRegisterFailedResponsesAsSuccessfulTurns()
    {
        var path = Path.Combine(_folder, "ai", "failed.json");
        var transport = new FakeHandler { Fail = true };
        using var http = new HttpClient(transport);
        var chat = new WorkspaceAiChat(path, http);
        var ai = new LunetAiLoadedSettings(
            new LunetAiSettings("openrouter", "https://openrouter.ai/api/v1/chat/completions", "test-model", 16, false),
            "test-secret-key-00000");
        await Assert.ThrowsAsync<HttpRequestException>(() => chat.AskAsync("Teste", ai));
        Assert.Empty(chat.History);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void CorruptedGlobalChatPreservesTheOriginalBytes()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "workspace-history.json");
        File.WriteAllText(path, "{\"bad\"");
        using var http = new HttpClient(new FakeHandler());
        Assert.Throws<InvalidDataException>(() => new WorkspaceAiChat(path, http));
        Assert.Equal("{\"bad\"", File.ReadAllText(path));
    }
}
