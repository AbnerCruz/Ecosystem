using System.Net;
using System.Text;
using System.Text.Json;
using EcosystemAi.Product;

namespace AgentRuntime.Tests;

public sealed class SavedModelConfigurationTests
{
    private static SavedProvider Provider(string name = "Configured", string model = "chosen") =>
        new(Guid.NewGuid().ToString("N"), name, "https://provider.example.test/v1", [new(model, model, 1.5m, 3m)]);
    [Fact]
    public void Multiple_providers_and_their_selected_model_survive_roundtrip_without_credentials()
    {
        var a = Provider(); var b = Provider("Other", "second");
        var config = new SavedModelConfiguration(1, [a, b], b.Id, "second");
        var saved = JsonSerializer.Serialize(config);
        var reopened = JsonSerializer.Deserialize<SavedModelConfiguration>(saved)!;
        reopened.Validate();
        var calls = new List<string>();
        var settings = reopened.Selected(id => { calls.Add(id); return "sentinel-provider-key"; })!;
        Assert.Equal([b.Id], calls);
        Assert.Equal("https://provider.example.test/v1/chat/completions", settings.Endpoint);
        Assert.Equal("second", settings.Model);
        Assert.Equal(1.5m, settings.InputUsdPerMillion);
        Assert.DoesNotContain("sentinel-provider-key", saved);
        Assert.DoesNotContain("sentinel-provider-key", JsonSerializer.Serialize(settings));
        Assert.Equal(2, reopened.Providers.Count);
        Assert.Null(reopened.RemoveProvider(b.Id).Selected(_ => throw new Exception("Must not read a key")));
    }
    [Fact]
    public void Foreign_selection_invalid_urls_duplicate_ids_and_bad_budgets_fail_closed()
    {
        var p = Provider();
        Assert.Throws<InvalidDataException>(() => new SavedModelConfiguration(1, [p], p.Id, "missing").Validate());
        Assert.Throws<InvalidDataException>(() => new SavedModelConfiguration(1, [p, p]).Validate());
        Assert.Throws<InvalidDataException>(() => new SavedModelConfiguration(1, [p with { Models = [p.Models[0], p.Models[0]] }]).Validate());
        Assert.Throws<ArgumentException>(() => new SavedModelConfiguration(1, [p with { Models = [p.Models[0] with { MaxCallCents = 30 }] }]).Validate());
        foreach (var value in new[] { "http://provider.test/v1", "https://key@provider.test/v1", "https://provider.test/v1?api_key=hidden", "https://provider.test/v1#secret" })
            Assert.Throws<ArgumentException>(() => SavedModelConfiguration.BaseUri(value));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(answer(request));
    }
    [Fact]
    public async Task Discovery_is_one_GET_with_provider_credential_and_deduplicated_bounded_ids()
    {
        var calls = 0;
        using var handler = new Handler(r =>
        {
            calls++;
            Assert.Equal(HttpMethod.Get, r.Method);
            Assert.Equal("https://provider.example.test/v1/models", r.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", r.Headers.Authorization?.Scheme);
            Assert.Equal("sentinel-provider-key", r.Headers.Authorization?.Parameter);
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"id":"z"},{"id":"a"},{"id":"a"},{"id":""},{"other":1}]}""") };
        });
        using var http = new HttpClient(handler);
        Assert.Equal(["a", "z"], await ProviderModelDiscovery.ListAsync(http, Provider(), "sentinel-provider-key"));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Discovery_does_not_expose_provider_error_payload_or_secret()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.Unauthorized) { Content = new StringContent("sentinel-provider-key") });
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<IOException>(() => ProviderModelDiscovery.ListAsync(http, Provider(), "sentinel-provider-key"));
        Assert.DoesNotContain("sentinel-provider-key", error.Message);
    }
}
