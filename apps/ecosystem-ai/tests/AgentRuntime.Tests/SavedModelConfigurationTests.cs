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

    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("http://LOCALHOST:11434/v1/")]
    public void Local_loopback_permits_keyless_model_without_changing_budgets(string baseUrl)
    {
        var provider = Provider() with { BaseUrl = baseUrl };
        var configuration = new SavedModelConfiguration(1, [provider], provider.Id, provider.Models[0].Id);
        configuration.Validate();
        var reopened = JsonSerializer.Deserialize<SavedModelConfiguration>(JsonSerializer.Serialize(configuration))!;
        var selected = reopened.Selected(_ => null)!;
        Assert.Null(selected.ApiKey);
        Assert.Equal("http", new Uri(selected.Endpoint).Scheme);
        Assert.EndsWith("/chat/completions", selected.Endpoint, StringComparison.Ordinal);
        Assert.Equal(20, selected.RunBudgetCents);
        Assert.Equal(5, selected.MaxCallCents);
        Assert.True(SavedModelConfiguration.IsLocalHttp(SavedModelConfiguration.BaseUri(baseUrl)));
    }

    [Theory]
    [InlineData("http://provider.test/v1")]
    [InlineData("http://localhost.evil.example/v1")]
    [InlineData("http://127.0.0.2:11434/v1")]
    [InlineData("http://[::1]:11434/v1")]
    [InlineData("http://user@localhost:11434/v1")]
    [InlineData("http://localhost:11434/v1?api_key=hidden")]
    [InlineData("http://localhost:11434/v1#secret")]
    public void Cleartext_does_not_escape_explicit_loopback_hosts(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => SavedModelConfiguration.BaseUri(baseUrl));
    }

    [Fact]
    public void Remote_https_selection_still_fails_without_saved_credential()
    {
        var provider = Provider();
        var config = new SavedModelConfiguration(1, [provider], provider.Id, provider.Models[0].Id);
        Assert.Throws<ArgumentException>(() => config.Selected(_ => null));
        Assert.Throws<ArgumentException>(() => config.Selected(_ => "short"));
        Assert.Equal("sentinel-secret", config.Selected(_ => "sentinel-secret")!.ApiKey);
    }

    [Fact]
    public async Task Keyless_local_discovery_sends_no_authorization_header()
    {
        var provider = Provider() with { BaseUrl = "http://localhost:11434/v1" };
        var count = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            count++;
            Assert.Equal("http://localhost:11434/v1/models", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"id":"local-model"}]}""")
            };
        }));
        var models = await ProviderModelDiscovery.ListAsync(client, provider, null);
        Assert.Equal(["local-model"], models);
        Assert.Equal(1, count);
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
