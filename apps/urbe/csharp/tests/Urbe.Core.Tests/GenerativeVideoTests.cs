using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-173: generative video — the job followed to the end, failures and non-videos told, a cancelled job stopped at the provider.</summary>
public sealed class GenerativeVideoTests
{
    private const string Mp4 = "AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAMXbW9vdgAAAGxtdmhkAAAAAAAAAAAAAAAAAAAD6AAAAMgAAQAAAQAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAkF0cmFrAAAAXHRraGQAAAADAAAAAAAAAAAAAAABAAAAAAAAAMgAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAAEAAAAAkAAAAAAAkZWR0cwAAABxlbHN0AAAAAAAAAAEAAADIAAAAAAABAAAAAAG5bWRpYQAAACBtZGhkAAAAAAAAAAAAAAAAAAAoAAAACABVxAAAAAAALWhkbHIAAAAAAAAAAHZpZGUAAAAAAAAAAAAAAABWaWRlb0hhbmRsZXIAAAABZG1pbmYAAAAUdm1oZAAAAAEAAAAAAAAAAAAAACRkaW5mAAAAHGRyZWYAAAAAAAAAAQAAAAx1cmwgAAAAAQAAASRzdGJsAAAAwHN0c2QAAAAAAAAAAQAAALBhdmMxAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAEAAJABIAAAASAAAAAAAAAABFUxhdmM2MC4zMS4xMDIgbGlieDI2NAAAAAAAAAAAAAAAGP//AAAANmF2Y0MBZAAK/+EAGWdkAAqs2UR/nwEQAAADABAAAAMAoPEiWWABAAZo6+PLIsD9+PgAAAAAEHBhc3AAAAABAAAAAQAAABRidHJ0AAAAAAAAccAAAHHAAAAAGHN0dHMAAAAAAAAAAQAAAAEAAAgAAAAAHHN0c2MAAAAAAAAAAQAAAAEAAAABAAAAAQAAABRzdHN6AAAAAAAAAtgAAAABAAAAFHN0Y28AAAAAAAAAAQAAA0cAAABidWR0YQAAAFptZXRhAAAAAAAAACFoZGxyAAAAAAAAAABtZGlyYXBwbAAAAAAAAAAAAAAAAC1pbHN0AAAAJal0b28AAAAdZGF0YQAAAAEAAAAATGF2ZjYwLjE2LjEwMAAAAAhmcmVlAAAC4G1kYXQAAAKtBgX//6ncRem95tlIt5Ys2CDZI+7veDI2NCAtIGNvcmUgMTY0IHIzMTA4IDMxZTE5ZjkgLSBILjI2NC9NUEVHLTQgQVZDIGNvZGVjIC0gQ29weWxlZnQgMjAwMy0yMDIzIC0gaHR0cDovL3d3dy52aWRlb2xhbi5vcmcveDI2NC5odG1sIC0gb3B0aW9uczogY2FiYWM9MSByZWY9MyBkZWJsb2NrPTE6MDowIGFuYWx5c2U9MHgzOjB4MTEzIG1lPWhleCBzdWJtZT03IHBzeT0xIHBzeV9yZD0xLjAwOjAuMDAgbWl4ZWRfcmVmPTEgbWVfcmFuZ2U9MTYgY2hyb21hX21lPTEgdHJlbGxpcz0xIDh4OGRjdD0xIGNxbT0wIGRlYWR6b25lPTIxLDExIGZhc3RfcHNraXA9MSBjaHJvbWFfcXBfb2Zmc2V0PS0yIHRocmVhZHM9MSBsb29rYWhlYWRfdGhyZWFkcz0xIHNsaWNlZF90aHJlYWRzPTAgbnI9MCBkZWNpbWF0ZT0xIGludGVybGFjZWQ9MCBibHVyYXlfY29tcGF0PTAgY29uc3RyYWluZWRfaW50cmE9MCBiZnJhbWVzPTMgYl9weXJhbWlkPTIgYl9hZGFwdD0xIGJfYmlhcz0wIGRpcmVjdD0xIHdlaWdodGI9MSBvcGVuX2dvcD0wIHdlaWdodHA9MiBrZXlpbnQ9MjUwIGtleWludF9taW49NSBzY2VuZWN1dD00MCBpbnRyYV9yZWZyZXNoPTAgcmNfbG9va2FoZWFkPTQwIHJjPWNyZiBtYnRyZWU9MSBjcmY9MjMuMCBxY29tcD0wLjYwIHFwbWluPTAgcXBtYXg9NjkgcXBzdGVwPTQgaXBfcmF0aW89MS40MCBhcT0xOjEuMDAAgAAAACNliIQAP//+5nX4FNjyHpLEBiCpwrFXKecBbOdNDh9VKAxncw==";

    /// <summary>A Videos API: queued, then in progress, then done; the content is a real MP4 (or what the test gives).</summary>
    private sealed class Server(string final = "completed", byte[]? content = null, bool router = false) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public string? Model { get; private set; }
        private int _polls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Calls.Add(request.Method + " " + request.RequestUri!.AbsolutePath);
            if (router && request.RequestUri.AbsolutePath.EndsWith("/videos/models", StringComparison.Ordinal))
                return Json("""{"data":[{"id":"google/veo-3.1","name":"Veo","supported_durations":[8]}]}""");
            if (request.Method == HttpMethod.Post)
            {
                var form = await request.Content!.ReadAsStringAsync(cancel);
                if (router)
                {
                    var input = JsonNode.Parse(form)!;
                    Model = (string?)input["model"];
                    Assert.Equal(8, (int?)input["duration"]);
                    return Json("""{"id":"video_1","status":"pending"}""");
                }
                Model = form.Contains("sora-2") ? "sora-2" : null;
                return Json("""{"id":"video_1","status":"queued","progress":0}""");
            }
            if (request.Method == HttpMethod.Delete) return Json("""{"id":"video_1","deleted":true}""");
            if (request.RequestUri.AbsolutePath.EndsWith("/content", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content ?? Convert.FromBase64String(Mp4)) };
            _polls++;
            return _polls < 2 ? Json("""{"id":"video_1","status":"in_progress","progress":40}""")
                : final == "failed" ? Json("""{"id":"video_1","status":"failed","error":{"message":"conteúdo recusado"}}""")
                : Json("{\"id\":\"video_1\",\"status\":\"" + final + "\"}");
        }
        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static readonly AiProviderConfig OpenAi = new("p1", "openai", "OpenAI", "sk-test-0000000000");

    [Fact]
    public async Task OpenRouterListsItsVideoModelsAndDownloadsTheSelectedJob()
    {
        var cancel = TestContext.Current.CancellationToken;
        var server = new Server(router: true);
        var service = new AssistantProviders(server, (_, _) => Task.CompletedTask);
        var provider = new AiProviderConfig("router", "openrouter", ApiKey: "test");
        var model = Assert.Single(await service.ListVideoModelsAsync(provider, cancel));
        Assert.Equal(("google/veo-3.1", "Veo"), model);
        Assert.Equal(Convert.FromBase64String(Mp4),
            await service.GenerateVideoAsync(provider, "Farol", model.Id, 8, "1280x720", cancel: cancel));
        Assert.Equal(model.Id, server.Model);
        Assert.Contains("GET /api/v1/videos/video_1/content", server.Calls);
    }

    [Fact]
    public async Task TheJobIsFollowedToAVerifiedMp4()
    {
        var server = new Server();
        var providers = new AssistantProviders(server, (_, _) => Task.CompletedTask);
        var progress = new List<int>();
        var bytes = await providers.GenerateVideoAsync(OpenAi, "Um farol", null, 4, "1280x720", new Progress<int>(progress.Add), TestContext.Current.CancellationToken);
        Assert.Equal(Convert.FromBase64String(Mp4), bytes);
        Assert.Equal("sora-2", server.Model);
        Assert.Equal(["POST /v1/videos", "GET /v1/videos/video_1", "GET /v1/videos/video_1", "GET /v1/videos/video_1/content"], server.Calls);
    }

    [Fact]
    public async Task AFailureOrSomethingThatIsNotAVideoIsToldNeverSaved()
    {
        var failed = new AssistantProviders(new Server("failed"), (_, _) => Task.CompletedTask);
        var e = await Assert.ThrowsAsync<AiProviderException>(() => failed.GenerateVideoAsync(OpenAi, "x", null, 4, "1280x720", cancel: TestContext.Current.CancellationToken));
        Assert.Contains("conteúdo recusado", e.Message);
        var junk = new AssistantProviders(new Server(content: Encoding.UTF8.GetBytes("<html>erro</html>")), (_, _) => Task.CompletedTask);
        Assert.Contains("não é um vídeo", (await Assert.ThrowsAsync<AiProviderException>(() => junk.GenerateVideoAsync(OpenAi, "x", null, 4, "1280x720", cancel: TestContext.Current.CancellationToken))).Message);
        // a provider without generative video says so before any call
        var none = new Server();
        await Assert.ThrowsAsync<AiProviderException>(() => new AssistantProviders(none).GenerateVideoAsync(new AiProviderConfig("g", "gemini", "Gemini", "k-000000000000"), "x", null, 4, "1280x720", cancel: TestContext.Current.CancellationToken));
        Assert.Empty(none.Calls);
    }

    [Fact]
    public async Task CancellingStopsThePaidJobAtTheProvider()
    {
        var server = new Server();
        using var cancel = new CancellationTokenSource();
        var providers = new AssistantProviders(server, (_, token) => { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => providers.GenerateVideoAsync(OpenAi, "x", null, 4, "1280x720", cancel: cancel.Token));
        Assert.Equal("DELETE /v1/videos/video_1", server.Calls[^1]);
    }

    private sealed class Host(IAssistantVideos? videos) : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge => new(Documents);
        public int ReadChars => 2000;
        public IAssistantVideos? Videos => videos;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private sealed class Videos(bool on) : IAssistantVideos
    {
        public Dictionary<string, byte[]> Saved { get; } = [];
        public bool CanGenerate => on;
        public string Unavailable => "desligado";
        public string Model => "sora-2 (OpenAI)";
        public Task<byte[]> GenerateAsync(string prompt, int seconds, string size, CancellationToken cancel) => Task.FromResult(Convert.FromBase64String(Mp4));
        public Task SaveAsync(string path, byte[] bytes) { Saved[path] = bytes; return Task.CompletedTask; }
        public bool Exists(string path) => Saved.ContainsKey(path);
    }

    [Fact]
    public async Task TheToolSaysItIsAPaidExternalModelAndKeepsTheClipInTheVault()
    {
        var tool = AssistantVideoTools.Tool;
        Assert.Equal("write", tool.Access);
        var input = new JsonObject { ["prompt"] = "Ondas quebrando ao amanhecer", ["seconds"] = 8 };
        var videos = new Videos(true);
        var preview = tool.Preview!(input, new Host(videos)).Note!;
        Assert.Contains("cobra por segundo", preview);
        Assert.Contains("sora-2", preview);
        var output = await tool.Run(input, new Host(videos));
        var saved = Assert.Single(videos.Saved);
        Assert.Equal("Vídeos/Ondas quebrando ao amanhecer.mp4", saved.Key);
        Assert.Contains("8 s", output.Content);
        // off: told why, nothing generated; bad duration refused
        Assert.Equal("desligado", (await Assert.ThrowsAsync<AiToolException>(() => tool.Run(input, new Host(new Videos(false))))).Message);
        await Assert.ThrowsAsync<AiToolException>(() => tool.Run(new JsonObject { ["prompt"] = "x", ["seconds"] = 7 }, new Host(videos)));
        Assert.Contains("motion graphics", tool.Description);
    }
}
