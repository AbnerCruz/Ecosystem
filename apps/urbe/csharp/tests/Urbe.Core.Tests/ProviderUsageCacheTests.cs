using System.Net;
using System.Text;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class ProviderUsageCacheTests
{
    private sealed class Script(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
            };
            return Task.FromResult(response);
        }
    }

    private static AiRequest Request() => new("example", [new AiMessage("user", [AiBlock.TextBlock("Olá")])], "Instruções");

    [Fact]
    public async Task OpenAiCachedInputIsPartOfPromptTotalNotAnExtraAmount()
    {
        var sse = "data: " + """{"model":"example","choices":[{"delta":{"content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":120,"completion_tokens":50,"prompt_tokens_details":{"cached_tokens":80},"completion_tokens_details":{"reasoning_tokens":9},"cost":0.004}}""" + "\n\n" +
            "data: [DONE]\n\n";
        var provider = new AssistantProviders(new Script(sse));
        var result = await provider.StreamAsync(new AiProviderConfig("p", "openai", ApiKey: "test-only-key"), Request(), cancel: TestContext.Current.CancellationToken);
        Assert.Equal(120, result.Usage.Input);
        Assert.Equal(50, result.Usage.Output);
        Assert.Equal(80, result.Usage.CacheReadInput);
        Assert.Equal(9, result.Usage.ReasoningOutput);
        Assert.Equal(.004, result.Usage.Cost);
        Assert.Equal("reported", result.Usage.CostSource);
    }

    [Fact]
    public async Task AnthropicInputUsesDisjointBucketsAndRetainsTheirCategories()
    {
        var sse = "event: message_start\ndata: " +
            """{"type":"message_start","message":{"model":"example","usage":{"input_tokens":10,"cache_read_input_tokens":40,"cache_creation_input_tokens":20,"output_tokens":1}}}""" +
            "\n\n" + "event: content_block_start\ndata: " +
            """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":"oi"}}""" +
            "\n\n" + "event: message_delta\ndata: " +
            """{"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":7}}""" + "\n\n";
        var provider = new AssistantProviders(new Script(sse));
        var result = await provider.StreamAsync(new AiProviderConfig("p", "anthropic", ApiKey: "test-only-key"), Request(), cancel: TestContext.Current.CancellationToken);
        Assert.Equal(70, result.Usage.Input);
        Assert.Equal(7, result.Usage.Output);
        Assert.Equal(40, result.Usage.CacheReadInput);
        Assert.Equal(20, result.Usage.CacheCreationInput);
        Assert.Null(result.Usage.Cost);
        Assert.Equal("unknown", result.Usage.CostSource);
    }

    [Fact]
    public async Task MissingProviderCostOrCacheIsNotFreeOrFabricated()
    {
        var sse = "data: " + """{"model":"example","choices":[{"delta":{"content":"ok"},"finish_reason":"stop"}]}""" + "\n\n" + "data: [DONE]\n\n";
        var provider = new AssistantProviders(new Script(sse));
        var r = await provider.StreamAsync(new AiProviderConfig("p", "openai", ApiKey: "test-only-key"), Request(), cancel: TestContext.Current.CancellationToken);
        Assert.Null(r.Usage.Cost);
        Assert.Null(r.Usage.CacheReadInput);
        Assert.Null(r.Usage.CacheCreationInput);
        Assert.Equal("unknown", r.Usage.CostSource);
    }
}
