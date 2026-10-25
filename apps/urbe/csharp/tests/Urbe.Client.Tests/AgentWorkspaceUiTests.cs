using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task AgentWorkspaces_SelectResumeFilterAndRestartWithoutReassigningHistory() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-agent-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var store = new AssistantStore(new FolderAssistantStorage(folder));
        var cfg = new AiConfig { AgentWorkspaceVersion = 1 };
        cfg.CustomAgents.Add(new("one", "Um", "sparkle", "DO NOT DISPLAY SUMMARY", "all", false, "private instructions"));
        cfg.CustomAgents.Add(new("two", "Dois", "sparkle", "OTHER SUMMARY", "all", false, ""));
        cfg.AgentPolicies["one"] = "ask";
        cfg.AgentModels["one"] = new("p", "model-one");
        store.SetConfigVerified(cfg);
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            Assert.Equal("Agentes", panel.ViewTitle);
            await Settle(window, view);
            Snapshot(window, "native-agent-workspaces.png");
            Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("DO NOT DISPLAY") == true);
            var vault = panel.Conversation.Vault;
            store.SaveConversation(new AiConversation { Id = "one-old", AgentId = "one", Vault = vault, Title = "Private one", Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("only one")] }] }, 10);
            store.SaveConversation(new AiConversation { Id = "two-old", AgentId = "two", Vault = vault, Title = "Private two", Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("only two")] }] }, 20);
            panel.SelectAgent("one");
            Assert.Equal("one-old", panel.Conversation.Id);
            ClickNamed(panel, "Conversas");
            Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Private one");
            Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Private two");
            panel.SelectAgent("two");
            Assert.Equal("two-old", panel.Conversation.Id);
            Assert.DoesNotContain(panel.Conversation.Messages.SelectMany(m => m.Content), b => b.Text == "only one");
            panel.SelectAgent("one");
            Assert.Equal("one-old", panel.Conversation.Id);
            Assert.Equal("one", store.GetConversation("one-old")!.AgentId);
            Assert.Equal("two", store.GetConversation("two-old")!.AgentId);
            Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text is "⌃" or "⌄");
            panel.Close();
            var next = new MainView();
            window.Content = next;
            await next.LoadAsync(TutorialNotes.Load());
            next.Assistant.Open();
            Assert.Equal("one-old", next.Assistant.Conversation.Id);
            Assert.Equal("ask", next.Assistant.Config.PolicyFor("one"));
            Assert.Equal("readonly", next.Assistant.Config.PolicyFor("two"));
            Assert.Equal("model-one", next.Assistant.Config.AgentModels["one"].Model);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; Directory.Delete(folder, true); }
    }, CancellationToken.None));
}
