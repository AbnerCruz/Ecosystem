using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AgentWorkspaceTests
{
    [Fact]
    public void NewInstallOnlyHasAssistantNoModelNoImagesAndReadOnly()
    {
        var storage = new MemoryAssistantStorage();
        var config = new AssistantStore(storage).InitializeAgentWorkspaces();
        Assert.Equal("geral", Assert.Single(AgentNames.All(config)).Id);
        Assert.Empty(config.AgentModels); // no model is chosen for the person: they connect one
        Assert.Empty(config.AgentImages);
        Assert.Equal("readonly", config.PolicyFor("geral"));
        Assert.Equal("readonly", config.PolicyFor("new-agent"));
        Assert.False(AssistantAgent.AllowTool(AssistantAgent.Agents[0], config.PolicyFor("geral"), "write"));
        Assert.True(AssistantAgent.NeedsApproval("destructive", "auto", true));
        Assert.True(AssistantAgent.NeedsApproval("plugin", "auto", true));
        var loaded = new AssistantStore(storage).InitializeAgentWorkspaces();
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
        var config = store.InitializeAgentWorkspaces();
        Assert.Equal(fixture, storage.Read("config-before-agent-workspaces.json"));
        Assert.Equal(conversation, storage.Read("conversa-legacy.json"));
        Assert.Equal(["geral", "escritor"], AgentNames.All(config).Select(a => a.Id));
        Assert.Equal("Autor", AgentNames.CallName(config.CustomAgents[0], config));
        Assert.Equal("text", config.AgentModels["escritor"].Model);
        Assert.Equal("old", config.Model);
        Assert.Equal("visual", config.AgentImages["geral"].Model);
        Assert.Equal("readonly", config.PolicyFor("escritor")); // a conversation's auto is not an agent grant
        var first = storage.Read("config.json");
        store.InitializeAgentWorkspaces();
        Assert.Equal(first, storage.Read("config.json"));
        Assert.Single(store.ConversationsFor("escritor", "v"));
        Assert.Empty(store.ConversationsFor("geral", "v"));
        Assert.Empty(store.ConversationsFor("escritor", "other"));
    }

    [Fact]
    public void AgentSettingsRoundTripIndependentlyAndMissingProviderDoesNotSubstitute()
    {
        var storage = new MemoryAssistantStorage();
        var store = new AssistantStore(storage);
        var c = store.InitializeAgentWorkspaces();
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

    [Fact]
    public void PermissionScopeActuallyReachesRuntimeAndPersists()
    {
        var store = new AssistantStore(new MemoryAssistantStorage());
        var config = store.InitializeAgentWorkspaces();
        var agent = new AiAgentDef("writer", "Writer", "edit", "", "read", false, "Write notes");
        Assert.False(AssistantAgent.AllowTool(config.AgentWithInstructions(agent), config.PolicyFor(agent.Id), "write"));
        config.AgentToolScopes[agent.Id] = "write";
        config.AgentPolicies[agent.Id] = "ask";
        store.SetConfigVerified(config);
        var loaded = store.GetConfig();
        var effective = loaded.AgentWithInstructions(agent);
        Assert.Equal("write", effective.Tools);
        Assert.True(AssistantAgent.AllowTool(effective, loaded.PolicyFor(agent.Id), "write"));
        Assert.False(AssistantAgent.AllowTool(effective, loaded.PolicyFor(agent.Id), "destructive"));
        Assert.True(AssistantAgent.NeedsApproval("write", loaded.PolicyFor(agent.Id), false));
        loaded.AgentPolicies[agent.Id] = "auto";
        Assert.False(AssistantAgent.NeedsApproval("write", loaded.PolicyFor(agent.Id), false));
        loaded.AgentToolScopes[agent.Id] = "all";
        Assert.True(AssistantAgent.AllowTool(loaded.AgentWithInstructions(agent), loaded.PolicyFor(agent.Id), "destructive"));
        Assert.True(AssistantAgent.NeedsApproval("destructive", "auto", false));
        Assert.True(AssistantAgent.NeedsApproval("plugin", "auto", false));
        loaded.AgentPolicies[agent.Id] = "readonly";
        Assert.False(AssistantAgent.AllowTool(loaded.AgentWithInstructions(agent), loaded.PolicyFor(agent.Id), "write"));
    }

    [Fact]
    public void RemovingAgentHistoryDeletesOnlyOwnedConversationsInEveryVault()
    {
        var store = new AssistantStore(new MemoryAssistantStorage());
        store.SaveConversation(new AiConversation { Id = "one-a", AgentId = "one", Vault = "A" }, 1);
        store.SaveConversation(new AiConversation { Id = "one-b", AgentId = "one", Vault = "B" }, 2);
        store.SaveConversation(new AiConversation { Id = "two-a", AgentId = "two", Vault = "A" }, 3);
        Assert.Equal(2, store.DeleteAgentConversationsVerified("one"));
        Assert.Null(store.GetConversation("one-a"));
        Assert.Null(store.GetConversation("one-b"));
        Assert.NotNull(store.GetConversation("two-a"));
        Assert.Equal(0, store.DeleteAgentConversationsVerified("one"));
    }

    private sealed class FailedDeleteStorage : IAssistantStorage
    {
        public readonly MemoryAssistantStorage Inner = new();
        public string? Read(string name) => Inner.Read(name);
        public void Write(string name, string text) => Inner.Write(name, text);
        public void Delete(string name) { } // filesystem wrapper swallowed a failure
        public IEnumerable<string> List() => Inner.List();
    }

    [Fact]
    public void HistoryDeletionFailsClosedOnSilentDiskError()
    {
        var storage = new FailedDeleteStorage();
        var store = new AssistantStore(storage);
        store.SaveConversation(new AiConversation { Id = "protected", AgentId = "one", Vault = "A" }, 1);
        Assert.Throws<IOException>(() => store.DeleteAgentConversationsVerified("one"));
        Assert.NotNull(store.GetConversation("protected"));
    }

    [Fact]
    public void DelegationProvenanceSurvivesStorageRoundTripWithoutSharingMessages()
    {
        var store = new AssistantStore(new MemoryAssistantStorage());
        var parent = new AiConversation { Id = "parent", AgentId = "writer", Vault = "V" };
        store.SaveConversation(parent);
        var specialist = new AiConversation
        {
            Id = "child", AgentId = "artist", OriginConversationId = parent.Id,
            OriginAgentId = parent.AgentId, Vault = "V",
            Messages = [new AiChatMessage { Role = "user", Content = [AiChatBlock.TextBlock("Gere um desenho")] }]
        };
        store.SaveConversation(specialist);
        var read = Assert.IsType<AiConversation>(store.GetConversation("child"));
        Assert.Equal("parent", read.OriginConversationId);
        Assert.Equal("writer", read.OriginAgentId);
        Assert.Equal("artist", read.AgentId);
        Assert.Single(read.Messages);
        Assert.Empty(store.GetConversation("parent")!.Messages);
        Assert.Null(store.GetConversation("parent")!.OriginConversationId);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"agentWorkspaceVersion\":99}")]
    public void UnreadableOrFutureConfigurationIsNeverOverwritten(string original)
    {
        var storage = new MemoryAssistantStorage();
        storage.Write("config.json", original);
        Assert.Throws<IOException>(() => new AssistantStore(storage).InitializeAgentWorkspaces());
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
        Assert.Throws<IOException>(() => store.InitializeAgentWorkspaces());
        Assert.Equal(original, storage.Read("config.json"));
        Assert.Equal(original, storage.Read("config-before-agent-workspaces.json"));
        storage.Fail = false;
        Assert.Equal("preserved", store.InitializeAgentWorkspaces().Model);
    }

    private sealed class BrokenStorage : IAssistantStorage
    {
        public string? Read(string name) => name == "config.json" ? "{\"version\":1}" : null;
        public void Write(string name, string text) { }
        public void Delete(string name) { }
        public IEnumerable<string> List() => [];
    }

    [Fact]
    public void FailedBackupStopsMigration() => Assert.Throws<IOException>(() => new AssistantStore(new BrokenStorage()).InitializeAgentWorkspaces());
}
