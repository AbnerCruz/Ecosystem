using System.Net;
using System.Text;
using System.Text.Json;
using AgentRuntime;
using AgentRuntime.Providers.ChatCompletions;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class LunetAiF3Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private static readonly Uri Chat = new("https://example.com/v1/chat/completions");

    [Fact]
    public async Task StreamKeepsTextAcrossToolFragmentsAndIgnoresReplayAndEmptyDelta()
    {
        var data = """
            : comment from the provider

            id: 1
            data: {"choices":[{"index":0,"delta":{"content":"Análise. "}}]}

            id: 1
            data: {"choices":[{"index":0,"delta":{"content":"Análise. "}}]}

            data: {"choices":[{"index":0,"delta":{"content":""}}]}

            data: {"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"c1","function":{"name":"read","arguments":"{\"path\":"}}]}}]}

            data: {"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"Game.cs\"}"}}]},"finish_reason":"tool_calls"}]}

            data: {"choices":[],"usage":{"prompt_tokens":2,"completion_tokens":3}}

            data: [DONE]

            """;
        var partial = new List<string>();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(data));
        var json = await LunetAiStream.ReadAsync(input, partial.Add, TestContext.Current.CancellationToken);
        using var result = JsonDocument.Parse(json);
        var message = result.RootElement.GetProperty("choices")[0].GetProperty("message");
        Assert.Equal("Análise. ", Assert.Single(partial));
        Assert.Equal("Análise. ", message.GetProperty("content").GetString());
        var call = message.GetProperty("tool_calls")[0];
        Assert.Equal("c1", call.GetProperty("id").GetString());
        Assert.Equal("""{"path":"Game.cs"}""", call.GetProperty("function").GetProperty("arguments").GetString());
    }

    [Fact]
    public async Task GlobalChatKeepsInterruptedTextAndRunIdentityAfterReopening()
    {
        using var response = new HttpClient(new HttpStub(_ => Sse("""
            data: {"choices":[{"delta":{"content":"Texto recebido"}}]}

            """)));
        var path = Path.Combine(_root, "global.json");
        var chat = new WorkspaceAiChat(path, response);
        var config = new LunetAiLoadedSettings(LunetAiSettings.Default with { Endpoint = Chat.ToString(), Model = "test" }, "test-key-long");
        await Assert.ThrowsAsync<InvalidDataException>(() => chat.AskAsync("Ideias", config, TestContext.Current.CancellationToken));
        var history = new WorkspaceAiChat(path, response).History;
        Assert.Equal(2, history.Count);
        Assert.Equal(history[0].RunId, history[1].RunId);
        Assert.NotEqual(history[0].Id, history[1].Id);
        Assert.True(history[1].Interrupted);
        Assert.Equal("Texto recebido", history[1].Text);
        Assert.False(history[1].Pending);
    }

    [Fact]
    public async Task AnthropicPagesDeduplicateAndRefreshFailuresDoNotReplaceLastCompleteList()
    {
        int hits = 0;
        bool failSecond = false;
        using var http = new HttpClient(new HttpStub(request =>
        {
            hits++;
            Assert.True(request.Headers.Contains("x-api-key"));
            if (request.RequestUri!.Query == "?after_id=next")
                return failSecond ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) :
                    Json("""{"data":[{"id":"one","context_window":20},{"id":"two"}],"has_more":false}""");
            return Json("""{"data":[{"id":"one","display_name":"One"}],"has_more":true,"last_id":"next"}""");
        }));
        var catalog = new LunetAiModelCatalog(http);
        var ct = TestContext.Current.CancellationToken;
        var models = await catalog.ListAsync("anthropic", Chat.ToString(), "key-a", ct);
        Assert.Equal(["one", "two"], models.Select(m => m.Id));
        Assert.Equal(20L, models[0].Context);
        Assert.Null(models[0].Tools);
        Assert.Same(models, await catalog.ListAsync("anthropic", Chat.ToString(), "key-a", ct));
        Assert.Equal(2, hits);
        failSecond = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => catalog.ListAsync("anthropic", Chat.ToString(), "key-a", ct, refresh: true));
        Assert.Same(models, await catalog.ListAsync("anthropic", Chat.ToString(), "key-a", ct));
        await Assert.ThrowsAsync<HttpRequestException>(() => catalog.ListAsync("anthropic", Chat.ToString(), "key-b", ct));
    }

    [Fact]
    public void ProjectOverrideIsIsolatedFromOtherGameAndProvider()
    {
        var models = new LunetAiProjectModels(_root);
        var global = LunetAiSettings.Default with { Model = "global" };
        models.Set("a", global, "local");
        Assert.Equal("local", new LunetAiProjectModels(_root).Get("a", global));
        Assert.Equal("global", global.Model);
        Assert.Null(models.Get("b", global));
        Assert.Null(models.Get("a", global with { ProviderId = "openai" }));
        models.Set("a", global, null);
        Assert.Null(models.Get("a", global));
    }

    [Fact]
    public async Task LegacyAutoHasNoWriteAuthorityAndProjectCanBeExcluded()
    {
        var game = new ProjectStore(_root).Create("HiddenGame");
        game.WriteText("Secret.cs", "class SecretGameType {}");
        var model = new InspectModel();
        var session = new LunetAiSession(game, Path.Combine(_root, "history.json"));
        await session.AskAsync("crie algo", new LunetAiRequest(
            LunetAiSettings.Default with { Model = "test" }, "test-key-long", LunetAiEditorContext.Empty with { IncludeProject = false },
            Provider: model, Mode: LunetAiMode.Auto), TestContext.Current.CancellationToken);
        Assert.Equal(LunetAiMode.Teach, session.LastMode);
        Assert.Empty(model.Request!.Tools);
        var sent = string.Join("\n", model.Request.Messages.SelectMany(m => m.Content).OfType<TextBlock>().Select(t => t.Text));
        Assert.DoesNotContain("HiddenGame", sent);
        Assert.DoesNotContain("Secret.cs", sent);
        Assert.DoesNotContain("SecretGameType", sent);
    }

    [Fact]
    public async Task CompatibleProviderCanReturnTextWithoutInventingAProviderPrice()
    {
        using var http = new HttpClient(new HttpStub(_ =>
            Json("""{"choices":[{"message":{"content":"OK"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":2}}""")));
        var profile = new ModelProfile("chat-completions", "mock", new ModelCapabilities(true, false, true, false, false, 4096), new Money(500, "USD"));
        var request = new ModelRequest(profile, "Lunet", [new Message(MessageRole.User, [new TextBlock("Oi")])], []);
        var ct = TestContext.Current.CancellationToken;
        var strict = new ChatCompletionsProvider(http, Chat, "test-key-long", 0, 0);
        Assert.Equal("cost-unknown", (await Assert.ThrowsAsync<ProviderException>(async () => await strict.CompleteAsync(request, ct))).Kind);
        var compatible = new ChatCompletionsProvider(http, Chat, "test-key-long", 0, 0, allowUnpriced: true);
        var reply = await compatible.CompleteAsync(request, ct);
        Assert.False(reply.Usage.CostKnown);
        Assert.True(reply.Usage.TokensKnown);
        Assert.Equal(profile.MaxCostPerCall, reply.Usage.Cost);
        Assert.Equal("OK", Assert.IsType<TextBlock>(Assert.Single(reply.Content)).Text);
    }

    private sealed class InspectModel : IModelProvider
    {
        public string ProviderId => "test";
        public ModelRequest? Request { get; private set; }
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return ValueTask.FromResult(new ModelResponse([new TextBlock("Resposta")], StopReason.EndTurn, new Usage(1, 1, new Money(1, "USD"))));
        }
    }

    private static HttpResponseMessage Json(string data) => new(HttpStatusCode.OK)
        { Content = new StringContent(data, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Sse(string data) => new(HttpStatusCode.OK)
        { Content = new StringContent(data, Encoding.UTF8, "text/event-stream") };

    private sealed class HttpStub(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(reply(request));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
