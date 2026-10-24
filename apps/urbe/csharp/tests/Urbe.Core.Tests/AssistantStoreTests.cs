using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>store.js (configuration and conversations only on the device) and tools.js revert() (undo of a run).</summary>
public sealed class AssistantStoreTests
{
    [Fact]
    public void ConfigurationAndConversationsRoundTrip()
    {
        var storage = new MemoryAssistantStorage();
        var store = new AssistantStore(storage);
        Assert.Equal(("ask", "geral", 30, 0), (store.GetConfig().Policy, store.GetConfig().AgentId, store.GetConfig().MaxSteps, store.GetConfig().Providers.Count));
        var cfg = new AiConfig { ProviderId = "or", Model = "a/b", Policy = "auto", Instructions = "Breve.", MaxSteps = 12 };
        cfg.Providers.Add(new AiProviderConfig("or", "openrouter", "OpenRouter", "sk-or-1", ""));
        cfg.Memory["Cofre"] = ["Mora em Recife"];
        cfg.CustomAgents.Add(new AiAgentDef("a_1", "Revisor", "sparkle", "Revisa", "write", false, "Revise."));
        cfg.ImageProviderId = "or";
        cfg.ImageModel = "bytedance-seed/seedream-4.5";
        cfg.AgentModels["a_1"] = new AiModelChoice("or", "openai/gpt-5");
        cfg.AgentInstructionBlocks["a_1"] = [new AiInstructionBlock("Tom", "Seja conciso."), new AiInstructionBlock("Fontes", "Cite as notas.")];
        cfg.ModelCache["or"] = new AiModelCache(5, [new AiModelInfo("a/b", "B", 8000, (1, 2), true)]);
        store.SetConfig(cfg);
        var back = store.GetConfig();
        Assert.Equal(AssistantStore.ConfigToJson(cfg).ToJsonString(), AssistantStore.ConfigToJson(back).ToJsonString());

        var conv = new AiConversation { Id = "c_1", Title = "Pedido", Vault = "Cofre", Model = "a/b", Usage = new AiUsage(10, 5, .5) };
        conv.Messages.Add(new AiChatMessage { Id = "m1", Role = "user", Content = [AiChatBlock.TextBlock("oi"), new AiChatBlock { Type = "text", Text = "[ctx]", Hidden = true }], Meta = new AiChatMeta { Context = "Comece.md" } });
        conv.Runs.Add(new AiRunRecord { Id = "r1", Stopped = "end", Changes = [new AiChange("doc_1", new AiNoteSnap("A.md", "a"), null, true)] });
        store.SaveConversation(conv, now: 100);
        store.SaveConversation(new AiConversation { Id = "c_2", Messages = [new AiChatMessage()] }, now: 200);
        Assert.Equal(["c_2", "c_1"], store.ListConversations().Select(c => c.Id));
        var got = store.GetConversation("c_1")!;
        Assert.Equal(AssistantStore.ConversationToJson(conv).ToJsonString(), AssistantStore.ConversationToJson(got).ToJsonString());
        Assert.True(got.Messages[0].Content[1].Hidden);
        store.DeleteConversation("c_1");
        Assert.Null(store.GetConversation("c_1"));
    }

    [Fact]
    public void AgentModelsOverrideTheChatOnlyWhenExplicitlyConfiguredAndProviderStillExists()
    {
        var config = new AiConfig { ProviderId = "base", Model = "default-model" };
        config.Providers.Add(new AiProviderConfig("base", "openrouter", ApiKey: "sk-base"));
        config.Providers.Add(new AiProviderConfig("special", "anthropic", ApiKey: "sk-ant"));
        var chat = new AiConversation { AgentId = "geral", ProviderId = "base", Model = "chat-model" };
        Assert.Equal(new AiModelChoice("base", "chat-model"), config.ModelFor(chat));
        config.AgentModels["geral"] = new AiModelChoice("special", "claude-5");
        Assert.Equal(new AiModelChoice("special", "claude-5"), config.ModelFor(chat));
        chat.AgentId = "escritor";
        Assert.Equal(new AiModelChoice("base", "chat-model"), config.ModelFor(chat));
        config.AgentModels["escritor"] = new AiModelChoice("missing", "missing-model");
        Assert.Equal(new AiModelChoice("base", "chat-model"), config.ModelFor(chat));
        chat.Model = "";
        Assert.Equal(new AiModelChoice("base", "default-model"), config.ModelFor(chat));
    }

    [Fact]
    public void ExistingConfigurationCanBeReadAndAgentBlocksExtendRatherThanReplaceTheRole()
    {
        var oldConfig = System.Text.Json.Nodes.JsonNode.Parse(
            """{"providers":[{"id":"p","preset":"openai","apiKey":"existing-key"}],"providerId":"p","model":"gpt-old","customAgents":[{"id":"writer","name":"Writer","prompt":"Original.","tools":"write"}]}"""
        )!.AsObject();
        var config = AssistantStore.ConfigFromJson(oldConfig);
        Assert.Null(config.ImageProviderId);
        Assert.Empty(config.AgentModels);
        Assert.Empty(config.AgentInstructionBlocks);
        Assert.Equal("gpt-old", config.ModelFor(new AiConversation { AgentId = "writer" }).Model);
        config.AgentInstructionBlocks["writer"] =
        [
            new AiInstructionBlock("Tom", "Seja conciso."),
            new AiInstructionBlock("Processo", "Confira as fontes.")
        ];
        var agent = config.AgentWithInstructions(Assert.Single(config.CustomAgents));
        Assert.Contains("Original.", agent.Prompt);
        Assert.Contains("### Tom\nSeja conciso.", agent.Prompt);
        Assert.Contains("### Processo\nConfira as fontes.", agent.Prompt);
        Assert.Equal("write", agent.Tools);
        Assert.Equal("Original.", Assert.Single(config.CustomAgents).Prompt); // original never mutated
    }

    private sealed class UndoHost : IAssistantUndoHost
    {
        public DocumentStore Documents { get; } = new();
        public List<string> Log { get; } = [];
        public Task<bool> RestoreTrashedAsync(string id) { Log.Add("restore " + id); Documents.Upsert(new DocumentInput { Id = id, Path = "Lixo.md", Content = "x" }); return Task.FromResult(true); }
        public Task TrashAsync(UrbeDocument doc) { Log.Add("trash " + doc.Path); Documents.Remove(doc.Id); return Task.CompletedTask; }
        public Task WriteBackAsync(UrbeDocument current, string path, string content)
        {
            Log.Add("write " + current.Path + " → " + path);
            Documents.Upsert(new DocumentInput { Id = current.Id, Path = path, Content = content });
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RevertUndoesNewestFirstAndKeepsNotesEditedAfterwards()
    {
        var host = new UndoHost();
        host.Documents.Upsert(new DocumentInput { Id = "d1", Path = "Novo.md", Content = "criado" });
        host.Documents.Upsert(new DocumentInput { Id = "d2", Path = "Pasta/Movido.md", Content = "depois" });
        host.Documents.Upsert(new DocumentInput { Id = "d3", Path = "Mexido.md", Content = "o usuário mexeu" });
        var changes = new List<AiChange>
        {
            new("d1", null, new AiNoteSnap("Novo.md", "criado")),
            new("d2", new AiNoteSnap("Movido.md", "antes"), new AiNoteSnap("Pasta/Movido.md", "depois")),
            new("d3", new AiNoteSnap("Mexido.md", "a"), new AiNoteSnap("Mexido.md", "b")),
            new("d4", new AiNoteSnap("Lixo.md", "x"), null, Trashed: true)
        };
        var report = await AssistantUndo.RevertAsync(host, changes);
        Assert.Equal(["restore d4", "write Pasta/Movido.md → Movido.md", "trash Novo.md"], host.Log);
        Assert.Equal(3, report.Restored);
        Assert.Equal(["Mexido.md"], report.Skipped);
        Assert.Equal("antes", host.Documents.Get("Movido.md")!.Content);
        Assert.Null(host.Documents.Get("Novo.md"));
    }
}
