using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantImagePricingTests
{
    private sealed class Endpoint(string response) : HttpMessageHandler
    {
        public List<(string Url, string? Authorization)> Requested { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Requested.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public void OfficialImagePricingDisplaysBillableUnitsAndNeverPretendsUnknownIsFree()
    {
        var google = AssistantImagePricing.Published("gemini", "gemini-nano-banana-2.1");
        Assert.NotNull(google);
        Assert.Contains("0,0336", google.Rate);
        Assert.Contains("/imagem", google.Rate);
        Assert.Contains("entrada", google.Explanation);
        Assert.Contains("ai.google.dev", google.SourceUrl);
        var openai = AssistantImagePricing.Published("openai", "gpt-image-2.5-sunburst");
        Assert.NotNull(openai);
        Assert.Contains("30/1M", openai.Rate);
        Assert.Contains("tokens", openai.Rate);
        Assert.Null(AssistantImagePricing.Published("custom", "some-model"));
        Assert.Null(AssistantImagePricing.Published("openai", "invented-model"));
    }

    [Fact]
    public async Task FetchesProviderSpecificOpenRouterOutputImageRateFromEndPoint()
    {
        var http = new Endpoint("""
        {"id":"bytedance-seed/seedream-4.5","endpoints":[
          {"provider_name":"a","pricing":[
            {"billable":"input_image","unit":"image","cost_usd":0.001},
            {"billable":"output_image","unit":"image","cost_usd":0.04}]},
          {"provider_name":"b","pricing":[{"billable":"output_image","unit":"image","cost_usd":0.05}]}
        ]}
        """);
        var providers = new AssistantProviders(http);
        var cfg = new AiProviderConfig("or", "openrouter", ApiKey: "key");
        var result = await providers.GetImageQuoteAsync(cfg, "bytedance-seed/seedream-4.5", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains("0,04", result.Rate);
        Assert.Contains("/imagem", result.Rate);
        Assert.StartsWith("A partir de", result.Rate);
        Assert.DoesNotContain("0,001", result.Rate);
        var req = Assert.Single(http.Requested);
        Assert.EndsWith("/images/models/bytedance-seed/seedream-4.5/endpoints", req.Url);
        Assert.Equal("Bearer key", req.Authorization);
        Assert.DoesNotContain("key", req.Url);
    }

    [Fact]
    public void DistinguishesMegapixelsAndOutputTokensFromAFlatImagePrice()
    {
        var megapixel = AssistantImagePricing.FromOpenRouterEndpoints(JsonNode.Parse("""
        {"endpoints":[{"pricing":[{"billable":"output_image","unit":"megapixel","cost_usd":0.03}]}]}
        """), "maker/model");
        Assert.NotNull(megapixel);
        Assert.Contains("/megapixel", megapixel.Rate);
        var token = AssistantImagePricing.FromOpenRouterEndpoints(JsonNode.Parse("""
        {"endpoints":[{"pricing":[{"billable":"output_image","unit":"token","cost_usd":0.00003}]}]}
        """), "maker/model");
        Assert.NotNull(token);
        Assert.Contains("30", token.Rate);
        Assert.Contains("/1M tokens", token.Rate);
        var inputOnly = AssistantImagePricing.FromOpenRouterEndpoints(JsonNode.Parse("""
        {"endpoints":[{"pricing":[{"billable":"input_image","unit":"image","cost_usd":0.01}]}]}
        """), "maker/model");
        Assert.Null(inputOnly);
    }

    [Fact]
    public async Task UnknownCustomPricingIsNotInventedOrFetched()
    {
        var http = new Endpoint("{}");
        var p = new AssistantProviders(http);
        var result = await p.GetImageQuoteAsync(new AiProviderConfig("c", "custom", BaseUrl: "https://example.com/v1"), "some-model", TestContext.Current.CancellationToken);
        Assert.Null(result);
        Assert.Empty(http.Requested);
        Assert.False(AssistantImagePricing.ValidOpenRouterSlug("../evil"));
        Assert.True(AssistantImagePricing.ValidOpenRouterSlug("qwen/qwen-image-2.1-turbo"));
    }
}
