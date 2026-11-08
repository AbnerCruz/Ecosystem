using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantHistoryEffortTests
{
    private sealed class HeaderOnlyStorage : IAssistantStorage
    {
        private readonly MemoryAssistantStorage _storage = new();
        public int FullReads { get; private set; }
        public string? Read(string name) { FullReads++; return _storage.Read(name); }
        public string? ReadPrefix(string name, int maxChars)
        {
            var value = _storage.Read(name);
            return value is null ? null : value[..Math.Min(value.Length, maxChars)];
        }
        public void Write(string name, string text) => _storage.Write(name, text);
        public void Delete(string name) => _storage.Delete(name);
        public IEnumerable<string> List() => _storage.List();
    }

    [Fact]
    public void HistoryListsMetadataWithoutHydratingConversationBodies()
    {
        var storage = new HeaderOnlyStorage();
        var store = new AssistantStore(storage);
        store.SaveConversation(new AiConversation
        {
            Id = "geral-1", AgentId = "geral", Vault = "Cofre", Title = "Rascunho",
            Messages = [new AiChatMessage { Role = "user", Content = [AiChatBlock.TextBlock(new string('x', 300_000))] }]
        }, 123);
        store.SaveConversation(new AiConversation { Id = "other", AgentId = "escritor", Vault = "Cofre", Title = "Privado" }, 456);

        var first = store.ConversationSummariesFor("geral", "Cofre");
        Assert.Single(first);
        Assert.Equal(("geral-1", "Rascunho", 123L), (first[0].Id, first[0].Title, first[0].Updated));
        Assert.Equal(0, storage.FullReads);

        // An updated conversation invalidates the lightweight view.
        var chat = store.GetConversation("geral-1")!;
        chat.Title = "Atualizado";
        store.SaveConversation(chat, 789);
        var second = store.ConversationSummariesFor("geral", "Cofre");
        Assert.Equal(("Atualizado", 789L), (second[0].Title, second[0].Updated));
        store.DeleteConversation("geral-1");
        Assert.Empty(store.ConversationSummariesFor("geral", "Cofre"));
    }

    [Fact]
    public void EffortMapsToRealProviderWiresButNeverToUnsupportedOnes()
    {
        var openRouter = new AiProviderConfig("r", "openrouter");
        var openAi = new AiProviderConfig("o", "openai");
        var oldModel = new AiProviderConfig("legacy", "custom");
        var claude = new AiProviderConfig("c", "anthropic");
        var high = new AiRequest("openai/gpt-5", [], ReasoningEffort: "high");
        var routerWire = AssistantProviders.OpenAiWire(high, AssistantProviders.Resolve(openRouter));
        Assert.Equal("high", (string?)routerWire["reasoning"]?["effort"]);

        var mid = new AiRequest("gpt-5.1", [], ReasoningEffort: "medium");
        var openAiWire = AssistantProviders.OpenAiWire(mid, AssistantProviders.Resolve(openAi));
        Assert.Equal("medium", (string?)openAiWire["reasoning_effort"]);
        Assert.True(AssistantProviders.SupportsEffort(openAi, mid.Model));

        var unsupported = AssistantProviders.OpenAiWire(
            new AiRequest("small-chat-model", [], ReasoningEffort: "high"),
            AssistantProviders.Resolve(oldModel));
        Assert.Null(unsupported["reasoning"]);
        Assert.Null(unsupported["reasoning_effort"]);

        var claudeWire = AssistantProviders.AnthropicWire(new AiRequest("claude-opus-5-5", [], ReasoningEffort: "low"));
        Assert.Equal("low", (string?)claudeWire["output_config"]?["effort"]);
        Assert.True(AssistantProviders.SupportsEffort(claude, "claude-opus-5-5"));
        Assert.False(AssistantProviders.SupportsEffort(claude, "claude-haiku-4-5"));
    }
}
