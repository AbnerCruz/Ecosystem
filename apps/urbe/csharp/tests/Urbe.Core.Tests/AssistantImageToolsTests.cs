using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>UC-63: generate_image — the provider draws (OpenAI images API, base64), the vault keeps a new file, the note shows it.</summary>
public sealed class AssistantImageToolsTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13];

    private sealed class Answer(int status, string body) : HttpMessageHandler
    {
        public List<(string Url, JsonObject Body, string? Auth)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request.RequestUri!.ToString(),
                request.Content is null ? new JsonObject() : (JsonObject)JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!,
                request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static string Drawn(byte[] bytes) => new JsonObject { ["data"] = new JsonArray(new JsonObject { ["b64_json"] = Convert.ToBase64String(bytes) }) }.ToJsonString();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenAiDrawsThroughTheImagesApiInBase64()
    {
        var http = new Answer(200, Drawn(Png));
        var providers = new AssistantProviders(http);
        var bytes = await providers.GenerateImageAsync(new AiProviderConfig("p", "openai", ApiKey: "sk-test"), "um gato azul", size: "1536x1024", cancel: Ct);
        Assert.Equal(Png, bytes);
        var (url, body, auth) = Assert.Single(http.Seen);
        Assert.Equal("https://api.openai.com/v1/images/generations", url);
        Assert.Equal("Bearer sk-test", auth);
        Assert.Equal("gpt-image-1", (string?)body["model"]);
        Assert.Equal("um gato azul", (string?)body["prompt"]);
        Assert.Equal("1536x1024", (string?)body["size"]);
        Assert.Null(body["response_format"]);   // the gpt-image models refuse it and always answer in base64
    }

    [Fact]
    public async Task GeminiAsksForBase64AndProvidersThatDoNotDrawSaySo()
    {
        var http = new Answer(200, Drawn(Png));
        var providers = new AssistantProviders(http);
        var gemini = new AiProviderConfig("g", "gemini", ApiKey: "AIza-test");
        Assert.True(AssistantProviders.DrawsImages(gemini));
        await providers.GenerateImageAsync(gemini, "um farol", cancel: Ct);
        Assert.Equal("gemini-2.5-flash-image", (string?)http.Seen[0].Body["model"]);
        Assert.Equal("b64_json", (string?)http.Seen[0].Body["response_format"]);

        var anthropic = new AiProviderConfig("a", "anthropic", ApiKey: "sk-ant");
        Assert.False(AssistantProviders.DrawsImages(anthropic));
        Assert.True(AssistantProviders.DrawsImages(new AiProviderConfig("o", "openrouter", ApiKey: "k")));
        await Assert.ThrowsAsync<AiProviderException>(() => providers.GenerateImageAsync(anthropic, "x", cancel: Ct));
        await Assert.ThrowsAsync<AiProviderException>(() => providers.GenerateImageAsync(new AiProviderConfig("p", "openai"), "x", cancel: Ct));   // no key
        Assert.Single(http.Seen);
    }

    [Fact]
    public async Task OpenRouterDrawsWithoutChangingTheChatModel()
    {
        var http = new Answer(200, Drawn(Png));
        var providers = new AssistantProviders(http);
        var chat = new AiProviderConfig("claude", "anthropic", ApiKey: "sk-ant");
        var visual = new AiProviderConfig("router", "openrouter", ApiKey: "sk-or");
        Assert.Equal(visual, AssistantProviders.PickImageProvider(chat, [chat, visual]));
        Assert.Equal(visual, AssistantProviders.PickImageProvider(visual, [chat, visual]));
        var bytes = await providers.GenerateImageAsync(visual, "retrato", size: "1024x1536", cancel: Ct);
        Assert.Equal(Png, bytes);
        var (url, body, auth) = Assert.Single(http.Seen);
        Assert.Equal("https://openrouter.ai/api/v1/images", url);
        Assert.Equal("Bearer sk-or", auth);
        Assert.Equal("bytedance-seed/seedream-4.5", (string?)body["model"]);
        Assert.Equal("2:3", (string?)body["aspect_ratio"]);
        Assert.Null(body["size"]);
        Assert.Null(body["response_format"]);
    }

    [Fact]
    public void SelectsAnotherConfiguredImageProviderButNeverAnUnconfiguredOne()
    {
        var chat = new AiProviderConfig("claude", "anthropic", ApiKey: "sk-ant");
        var noKey = new AiProviderConfig("openai-no-key", "openai");
        var visual = new AiProviderConfig("gemini", "gemini", ApiKey: "gemini-key");
        Assert.Null(AssistantProviders.PickImageProvider(chat, [chat, noKey]));
        Assert.Equal(visual, AssistantProviders.PickImageProvider(chat, [chat, noKey, visual]));
        Assert.False(AssistantProviders.DrawsImages(new AiProviderConfig("custom", "custom", BaseUrl: "https://example.com/v1")));
    }

    [Fact]
    public async Task SomethingThatIsNotAPictureIsRefused()
    {
        var providers = new AssistantProviders(new Answer(200, Drawn(Encoding.UTF8.GetBytes("<html>erro</html>"))));
        await Assert.ThrowsAsync<AiProviderException>(() => providers.GenerateImageAsync(new AiProviderConfig("p", "openai", ApiKey: "k"), "x", cancel: Ct));
        providers = new AssistantProviders(new Answer(200, """{"data":[{"url":"https://exemplo/img.png"}]}"""));
        await Assert.ThrowsAsync<AiProviderException>(() => providers.GenerateImageAsync(new AiProviderConfig("p", "openai", ApiKey: "k"), "x", cancel: Ct));
    }

    [Fact]
    public async Task ExplicitImageProviderAndImageModelOverrideAutomaticFallback()
    {
        var chat = new AiProviderConfig("text", "anthropic", ApiKey: "sk-ant");
        var router = new AiProviderConfig("router", "openrouter", ApiKey: "sk-or");
        var openai = new AiProviderConfig("openai", "openai", ApiKey: "sk-ai");
        Assert.Equal(openai, AssistantProviders.PickImageProvider(chat, [chat, router, openai], "openai", "gpt-image-2.5-flare"));
        Assert.Null(AssistantProviders.PickImageProvider(chat, [chat, router, openai], "missing", "any"));
        Assert.Null(AssistantProviders.PickImageProvider(chat, [chat, new AiProviderConfig("empty", "openai")], "empty", "gpt-image-2.5-flare"));

        var custom = new AiProviderConfig("custom", "custom", BaseUrl: "https://my-server.example/v1");
        Assert.Null(AssistantProviders.PickImageProvider(chat, [custom], "custom"));
        Assert.Equal(custom, AssistantProviders.PickImageProvider(chat, [custom], "custom", "flux-latest"));
        var handler = new Answer(200, Drawn(Png));
        await new AssistantProviders(handler).GenerateImageAsync(custom, "um castelo", model: "flux-latest", cancel: Ct);
        Assert.Equal("flux-latest", (string?)Assert.Single(handler.Seen).Body["model"]);
    }

    [Fact]
    public async Task OpenRouterUsesTheDedicatedImageModelCatalog()
    {
        var handler = new Answer(200, """{"data":[{"id":"bytedance-seed/seedream-4.5","name":"Seedream 4.5"},{"id":"other/image-gen","name":"Other Image"}]}""");
        var catalog = await new AssistantProviders(handler).ListImageModelsAsync(
            new AiProviderConfig("or", "openrouter", ApiKey: "sk-or"), Ct);
        Assert.Equal(2, catalog.Count);
        Assert.Equal("other/image-gen", catalog[1].Id);
        var request = Assert.Single(handler.Seen);
        Assert.Equal("https://openrouter.ai/api/v1/images/models", request.Url);
        Assert.Equal("Bearer sk-or", request.Auth);
    }

    [Fact]
    public void ImageModelDoesNotAffectTheChatModelChoice()
    {
        var cfg = new AiConfig { ProviderId = "text", Model = "anthropic/claude-sonnet" };
        cfg.Providers.Add(new AiProviderConfig("text", "openrouter", ApiKey: "sk-or"));
        cfg.ImageProviderId = "text";
        cfg.ImageModel = "bytedance-seed/seedream-4.5";
        Assert.Equal(new AiModelChoice("text", "anthropic/claude-sonnet"), cfg.ModelFor(new AiConversation { ProviderId = "text" }));
        Assert.Equal("bytedance-seed/seedream-4.5", cfg.ImageModel);
    }

    [Fact]
    public void ThePictureGoesToImagensWithANameFromTheDescriptionNeverOverAFile()
    {
        var taken = new HashSet<string> { "Imagens/Um gato azul no telhado.png" };
        Assert.Equal("Imagens/Um gato azul no telhado 2.png", AssistantImageTools.PlanPath(null, "Um gato azul no telhado.", ".png", taken.Contains));
        Assert.Equal("Personagens/Lia.jpg", AssistantImageTools.PlanPath("Personagens/Lia.png", "x", ".jpg", taken.Contains));
        Assert.Equal("Imagens/Imagem.webp", AssistantImageTools.PlanPath(null, "!!!", ".webp", _ => false));
        Assert.Throws<AiToolException>(() => AssistantImageTools.PlanPath(".urbe/x", "x", ".png", _ => false));
        Assert.Equal(".png", AssistantProviders.ImageExtension(Png));
        Assert.Equal(".jpg", AssistantProviders.ImageExtension(new byte[] { 0xff, 0xd8, 0xff, 0xe0 }));
        Assert.Null(AssistantProviders.ImageExtension("GIF89a"u8));
    }

    private sealed class Host : IAssistantHost, IAssistantImages
    {
        private readonly DocumentStore _documents = new();
        public Dictionary<string, byte[]> Saved { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool CanDraw { get; set; } = true;
        public Host() => _documents.Upsert(new DocumentInput { Id = "doc_1", Path = "Lia.md", Content = "# Lia\n" });
        public DocumentStore Documents => _documents;
        public KnowledgeIndex Knowledge => new(_documents);
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public int ReadChars => 2000;
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) =>
            Task.FromResult(_documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IAssistantImages? Images => this;
        public Task<byte[]> DrawAsync(string prompt, string size, CancellationToken cancel) => Task.FromResult(Png);
        public Task SaveAsync(string path, byte[] bytes) { Saved.Add(path, bytes); return Task.CompletedTask; }
        public bool Exists(string path) => Saved.ContainsKey(path) || _documents.Get(path) is not null;
    }

    [Fact]
    public async Task TheToolKeepsThePictureAndPutsItAtTheEndOfTheNote()
    {
        var host = new Host();
        var tool = AssistantImageTools.Tool;
        Assert.Equal("write", tool.Access);   // asked for approval like every tool that writes
        var input = new JsonObject { ["prompt"] = "Retrato da Lia, aquarela", ["size"] = "1024x1536", ["note"] = "Lia" };
        Assert.Contains("Imagens/", tool.Preview!(input, host).Note);
        var output = await tool.Run(input, host);
        var (path, bytes) = Assert.Single(host.Saved);
        Assert.Equal("Imagens/Retrato da Lia aquarela.png", path);
        Assert.Equal(Png, bytes);
        Assert.Equal("# Lia\n\n![[Imagens/Retrato da Lia aquarela.png]]\n", host.Documents.Get("Lia.md")!.Content);
        Assert.Single(output.Changes!);

        // a provider that does not draw: nothing is written
        host.CanDraw = false;
        await Assert.ThrowsAsync<AiToolException>(() => tool.Run(new JsonObject { ["prompt"] = "outra" }, host));
        Assert.Single(host.Saved);
        await Assert.ThrowsAsync<AiToolException>(() => tool.Run(new JsonObject { ["prompt"] = "x", ["size"] = "9x9" }, new Host()));
    }
}
