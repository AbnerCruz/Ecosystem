using TabletopRpg.ModelGateway;

namespace TabletopRpg.Core.Tests;

public sealed class ModelGatewayTests
{
    [Fact]
    public void Request_is_an_immutable_copy_of_explicit_messages()
    {
        var messages = new[] { new ModelMessage(ModelMessageRole.User, "Only what this character knows.") };
        var turn = new ModelTurnRequest("turn-001", "game-intent.v1", messages);
        messages[0] = new ModelMessage(ModelMessageRole.User, "Secret world state");

        Assert.Equal("turn-001", turn.TurnId);
        Assert.Equal("game-intent.v1", turn.ResponseContract);
        Assert.Equal("Only what this character knows.", Assert.Single(turn.Messages).Content);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ModelMessage>)turn.Messages)[0] =
                new ModelMessage(ModelMessageRole.User, "Injected"));
    }

    [Fact]
    public void Request_rejects_oversized_empty_and_invalid_input()
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelTurnRequest("", "game-intent.v1", [new(ModelMessageRole.User, "hi")]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ModelTurnRequest("turn", "game-intent.v1", Array.Empty<ModelMessage>()));
        Assert.Throws<ArgumentException>(() =>
            new ModelTurnRequest("turn", "game-intent.v1", [new((ModelMessageRole)99, "hi")]));
        Assert.Throws<ArgumentException>(() =>
            new ModelTurnRequest("turn", "game-intent.v1", [new(ModelMessageRole.User, new string('a', 16_385))]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ModelTurnRequest("turn", "game-intent.v1",
                Enumerable.Range(0, 65).Select(_ => new ModelMessage(ModelMessageRole.User, "hi"))));
        Assert.Throws<ArgumentException>(() => new ModelTurnResponse(" "));
    }

    [Theory]
    [InlineData("http://example.org/v1")]
    [InlineData("https://user:pass@example.org/v1")]
    [InlineData("https://example.org/v1?key=secret")]
    [InlineData("https://example.org/v1#fragment")]
    public void Remote_configuration_rejects_unsafe_endpoints(string endpoint)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelGatewaySettings("provider", "model", new Uri(endpoint)));
    }

    [Fact]
    public void Configuration_allows_https_and_local_loopback_without_network()
    {
        var remote = new ModelGatewaySettings("compatible", "text-model", new Uri("https://models.example/v1"));
        var local = new ModelGatewaySettings("offline", "small-model", new Uri("http://127.0.0.1:11434/api"));

        Assert.Equal(Uri.UriSchemeHttps, remote.Endpoint.Scheme);
        Assert.True(local.Endpoint.IsLoopback);
        Assert.Null(local.CredentialEnvironmentVariable);
    }

    [Fact]
    public void Environment_loader_only_reads_nonsecret_settings()
    {
        const string secret = "private-token-must-not-be-stored";
        var entries = new Dictionary<string, string>
        {
            [ModelGatewaySettings.ProviderVariable] = "openai-compatible",
            [ModelGatewaySettings.ModelVariable] = "story-model",
            [ModelGatewaySettings.EndpointVariable] = "https://models.example/v1",
            [ModelGatewaySettings.CredentialNameVariable] = "RPG_SECRET_TOKEN",
            ["RPG_SECRET_TOKEN"] = secret
        };
        var reads = new List<string>();
        string? Read(string name)
        {
            reads.Add(name);
            return entries.GetValueOrDefault(name);
        }

        var settings = ModelGatewaySettings.FromEnvironment(Read);

        Assert.Equal("openai-compatible", settings.ProviderId);
        Assert.Equal("story-model", settings.ModelId);
        Assert.Equal("RPG_SECRET_TOKEN", settings.CredentialEnvironmentVariable);
        Assert.DoesNotContain("RPG_SECRET_TOKEN", reads.Take(4));
        Assert.DoesNotContain(secret, settings.ToString()!);
        Assert.DoesNotContain("RPG_SECRET_TOKEN", reads); // no secret read during settings construction
        var resolver = new EnvironmentModelCredentialSource(Read);
        Assert.Equal(secret, resolver.Resolve(settings));
        Assert.Equal("RPG_SECRET_TOKEN", reads.Last());
    }

    [Fact]
    public void Credential_source_fails_closed_without_echoing_secret()
    {
        var settings = new ModelGatewaySettings(
            "provider", "model", new Uri("https://example.org/v1"), "KEY_FOR_RPG");
        var resolver = new EnvironmentModelCredentialSource(_ => null);

        var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(settings));
        Assert.DoesNotContain("KEY_FOR_RPG", error.Message);
        Assert.Null(new EnvironmentModelCredentialSource(_ => "not-needed").Resolve(
            new ModelGatewaySettings("local", "model", new Uri("http://localhost:11434"))));
    }

    [Fact]
    public async Task Gateway_is_only_called_by_an_explicit_turn_and_respects_cancellation()
    {
        var gateway = new CountingGateway();
        Assert.Equal(0, gateway.Calls);

        using var cancellation = new CancellationTokenSource();
        var turn = new ModelTurnRequest(
            "t", "game-intent.v1", [new(ModelMessageRole.User, "I search the room.")]);
        var result = await gateway.GenerateAsync(turn, cancellation.Token);

        Assert.Equal(1, gateway.Calls);
        Assert.Equal(cancellation.Token, gateway.LastToken);
        Assert.Equal("{\"kind\":\"speak\"}", result.Text);
    }

    private sealed class CountingGateway : IModelGateway
    {
        public int Calls { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<ModelTurnResponse> GenerateAsync(
            ModelTurnRequest turn,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = cancellationToken;
            return Task.FromResult(new ModelTurnResponse("{\"kind\":\"speak\"}"));
        }
    }
}
