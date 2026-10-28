using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_AnAgentLooksUpOnlyItsOwnEarlierConversations() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-089: the agent's own conversations in this vault, never another agent's
        var folder = Path.Combine(Path.GetTempPath(), "urbe-history-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p";
            panel.Config.Model = "test-model";
            panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "read", false, ""));
            panel.Config.CustomAgents.Add(new("two", "Dois", "sparkle", "", "read", false, ""));
            var vault = panel.Conversation.Vault;
            var store = new AssistantStore(new FolderAssistantStorage(folder));
            store.SaveConversation(new AiConversation { Id = "one-old", AgentId = "one", Vault = vault, Title = "Roteiro da viagem", Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("Planeje a viagem a Lisboa")] }] }, 10);
            store.SaveConversation(new AiConversation { Id = "two-old", AgentId = "two", Vault = vault, Title = "Segredo do outro", Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("assunto do agente dois")] }] }, 20);
            var model = new ScriptedModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            model.Chats.Enqueue(Sse(
                """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"h1","function":{"name":"list_conversations","arguments":"{}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Falamos da viagem a Lisboa."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.SelectAgent("one");
            panel.Send("Do que falamos antes?");
            await panel.CurrentRun!;
            var result = panel.Conversation.Messages.SelectMany(m => m.Content).Single(b => b.Type == "tool_result" && b.ToolUseId == "h1").Content;
            Assert.Contains("Roteiro da viagem", result);
            Assert.Contains("Planeje a viagem a Lisboa", result);
            Assert.DoesNotContain("Segredo do outro", result);
            Assert.DoesNotContain("Do que falamos antes?", result);   // not the conversation going on
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
