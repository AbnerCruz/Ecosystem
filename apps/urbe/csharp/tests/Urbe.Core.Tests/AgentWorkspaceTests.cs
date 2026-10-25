using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AgentWorkspaceTests
{
    private sealed class Runtime : ILocalTextRuntime
    {
        public LocalModel Recommended => new("real-runtime-id", "Runtime model", "", "model.gguf", "https://example.test/model", 10, "", 4096, 1000, true);
        public IReadOnlyList<LocalModel> Models => [Recommended];
        public bool IsInstalled(LocalModel model) => false;
        public Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel) => Task.CompletedTask;
        public void Remove(LocalModel model) { }
        public Task<AiResult> ChatAsync(LocalModel model, AiRequest request, Action<AiStreamEvent> onEvent, CancellationToken cancel) => throw new NotSupportedException();
    }

    [Fact]
    public void NewInstallOnlyHasAssistantWithRuntimeModelNoImagesAndReadOnly()
    {
        var storage = new MemoryAssistantStorage();
        var config = new AssistantStore(storage).InitializeAgentWorkspaces(new Runtime());
        Assert.Equal("geral", Assert.Single(AgentNames.All(config)).Id);
        Assert.Equal(new AiModelChoice("device", "real-runtime-id"), config.AgentModels["geral"]);
        Assert.Empty(config.AgentImages);
        Assert.Equal("readonly", config.PolicyFor("geral"));
        Assert.Equal("readonly", config.PolicyFor("new-agent"));
        Assert.False(AssistantAgent.AllowTool(AssistantAgent.Agents[0], config.PolicyFor("geral"), "write"));
        Assert.True(AssistantAgent.NeedsApproval("destructive", "auto", true));
        Assert.True(AssistantAgent.NeedsApproval("plugin", "auto", true));
        var loaded = new AssistantStore(storage).InitializeAgentWorkspaces(null);
        Assert.Equal(config.AgentModels, loaded.AgentModels);
        Assert.Empty(loaded.AgentImages);
    }

    [Fact]
    public void MigrationPreservesBytesHistoryAndConfiguredAgentsAndIsIdempotent()
    {
        var storage = new MemoryAssistantStorage();
        var fixture = """{"version":1,"agentId":"geral","policy":"auto","providers":[{"id":"p","preset":"openai","apiKey":"private"}],"providerId":"p","model":"old","imageProviderId":"p","imageModel":"visual","agentNames":{"escritor":"Autor"},"agentModels":{"escritor":{"providerId":"p","model":"text"}}}""";
        storage.Write("config.json", fixture);
        var conversation = """{"id":"legacy","title":"Mixed legacy","agentId":"escritor","vault":"v","policy":"auto","messages":[{"id":"m","role":"user","content":[{"type":"text","text":"keep me"}]}]}""";
        storage.Write("conversa-legacy.json", conversation);
        var store = new AssistantStore(storage);
        var config = store.InitializeAgentWorkspaces(new Runtime());
        Assert.Equal(fixture, storage.Read("config-before-agent-workspaces.json"));
        Assert.Equal(conversation, storage.Read("conversa-legacy.json"));
        Assert.Equal(["geral", "escritor"], AgentNames.All(config).Select(a => a.Id));
        Assert.Equal("Autor", AgentNames.CallName(config.CustomAgents[0], config));
        Assert.Equal("text", config.AgentModels["escritor"].Model);
        Assert.Equal("old", config.Model);
        Assert.Equal("visual", config.AgentImages["geral"].Model);
        Assert.Equal("readonly", config.PolicyFor("escritor")); // a conversation's auto is not an agent grant
        var first = storage.Read("config.json");
        store.InitializeAgentWorkspaces(new Runtime());
        Assert.Equal(first, storage.Read("config.json"));
        Assert.Single(store.ConversationsFor("escritor", "v"));
        Assert.Empty(store.ConversationsFor("geral", "v"));
        Assert.Empty(store.ConversationsFor("escritor", "other"));
    }

    [Fact]
    public void AnUnconfiguredLegacyAssistantGetsTheLocalDefaultWithoutReplacingExplicitChoices()
    {
        var storage = new MemoryAssistantStorage();
        var original = "{\"version\":1,\"instructions\":\"Keep these\",\"providers\":[{\"id\":\"device\",\"preset\":\"openai\"}]}";
        storage.Write("config.json", original);
        var config = new AssistantStore(storage).InitializeAgentWorkspaces(new Runtime());
        Assert.Equal("Keep these", config.Instructions);
        Assert.Equal("openai", config.Providers.First(p => p.Id == "device").Preset);
        var choice = config.AgentModels["geral"];
        Assert.Equal("device", config.Providers.First(p => p.Id == choice.ProviderId).Preset);
        Assert.NotEqual("device", choice.ProviderId); // no collision with an existing provider identity
        Assert.Equal("real-runtime-id", choice.Model);
        Assert.Equal(original, storage.Read("config-before-agent-workspaces.json"));

        var other = new MemoryAssistantStorage();
        other.Write("config.json", "{\"version\":1,\"model\":\"chosen-before\"}");
        var kept = new AssistantStore(other).InitializeAgentWorkspaces(new Runtime());
        Assert.Equal("chosen-before", kept.Model);
        Assert.False(kept.AgentModels.ContainsKey("geral"));
    }

    [Fact]
    public void AgentSettingsRoundTripIndependentlyAndMissingProviderDoesNotSubstitute()
    {
        var storage = new MemoryAssistantStorage();
        var store = new AssistantStore(storage);
        var c = store.InitializeAgentWorkspaces(null);
        c.AgentPolicies["one"] = "ask";
        c.AgentPolicies["two"] = "auto";
        c.AgentModels["one"] = new("removed", "model-one");
        c.AgentModels["two"] = new("p", "model-two");
        c.AgentImages["two"] = new("p", "image-two");
        c.AgentInstructionBlocks["one"] = [new("Style", "Brief")];
        store.SetConfigVerified(c);
        var back = new AssistantStore(storage).GetConfig();
        Assert.Equal("ask", back.PolicyFor("one"));
        Assert.Equal("auto", back.PolicyFor("two"));
        Assert.Equal("readonly", back.PolicyFor("three"));
        Assert.Equal("model-one", back.ModelFor(new AiConversation { AgentId = "one" }).Model);
        Assert.Equal("removed", back.ModelFor(new AiConversation { AgentId = "one" }).ProviderId);
        Assert.False(back.AgentImages.ContainsKey("one"));
        Assert.Equal("image-two", back.AgentImages["two"].Model);
        Assert.Equal("Brief", back.AgentInstructionBlocks["one"][0].Content);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"agentWorkspaceVersion\":99}")]
    public void UnreadableOrFutureConfigurationIsNeverOverwritten(string original)
    {
        var storage = new MemoryAssistantStorage();
        storage.Write("config.json", original);
        Assert.Throws<IOException>(() => new AssistantStore(storage).InitializeAgentWorkspaces(new Runtime()));
        Assert.Equal(original, storage.Read("config.json"));
    }

    private sealed class FailConfigStorage : IAssistantStorage
    {
        public readonly MemoryAssistantStorage Inner = new();
        public bool Fail;
        public string? Read(string name) => Inner.Read(name);
        public void Write(string name, string text) { if (!Fail || name != "config.json") Inner.Write(name, text); }
        public void Delete(string name) => Inner.Delete(name);
        public IEnumerable<string> List() => Inner.List();
    }

    [Fact]
    public void FailedConfigWriteKeepsOriginalAndBackupAndRetryIsSafe()
    {
        var storage = new FailConfigStorage();
        const string original = "{\"version\":1,\"model\":\"preserved\"}";
        storage.Write("config.json", original);
        storage.Fail = true;
        var store = new AssistantStore(storage);
        Assert.Throws<IOException>(() => store.InitializeAgentWorkspaces(null));
        Assert.Equal(original, storage.Read("config.json"));
        Assert.Equal(original, storage.Read("config-before-agent-workspaces.json"));
        storage.Fail = false;
        Assert.Equal("preserved", store.InitializeAgentWorkspaces(null).Model);
    }

    private sealed class BrokenStorage : IAssistantStorage
    {
        public string? Read(string name) => name == "config.json" ? "{\"version\":1}" : null;
        public void Write(string name, string text) { }
        public void Delete(string name) { }
        public IEnumerable<string> List() => [];
    }

    [Fact]
    public void FailedBackupStopsMigration() => Assert.Throws<IOException>(() => new AssistantStore(new BrokenStorage()).InitializeAgentWorkspaces(null));
}
