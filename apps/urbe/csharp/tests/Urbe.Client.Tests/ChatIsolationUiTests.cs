using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-014: "mensagens, estados, formulários, aprovações e resultados devem aparecer na conversa correta". An answer
/// that keeps arriving for one agent's conversation while the person is in another agent's never shows there, and it
/// is all there when the person goes back to it.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_AnAnswerArrivingForAnotherConversationStaysInIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-isolation-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant; panel.Open();
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p"; panel.Config.Model = "test-model";
            panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "read", false, ""));
            panel.Config.CustomAgents.Add(new("two", "Dois", "sparkle", "", "read", false, ""));
            var model = new GatedModel(); panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.SelectAgent("one");
            panel.Send("Pergunta para Um.");
            var run = panel.CurrentRun!;
            var first = panel.Conversation;
            await model.WriteAsync("""{"choices":[{"delta":{"content":"Texto de Um."}}]}""");
            await WaitFor(window, view, () => ShownIn(panel).Contains("Texto de Um.") ? panel : null);

            // another agent while Um works: nothing of Um's conversation here, and here nothing is running
            panel.SelectAgent("two");
            await Settle(window, view);
            await model.WriteAsync("""{"choices":[{"delta":{"content":" Mais de Um."}}]}""");
            for (int i = 0; i < 5; i++) await Settle(window, view);
            Assert.NotSame(first, panel.Conversation);
            var here = ShownIn(panel);
            Assert.DoesNotContain("Texto de Um.", here);
            Assert.DoesNotContain("Mais de Um.", here);
            Assert.DoesNotContain("Pergunta para Um.", here);
            Assert.Contains(panel.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Enviar");
            Assert.Empty(panel.Conversation.Messages);

            // back to Um's conversation from the agents' list: all of it, still running
            panel.ShowAgents();
            await Settle(window, view);
            ClickButtonWithText(panel, "Um · Trabalhando");
            await Settle(window, view);
            Assert.Same(first, panel.Conversation);
            Assert.Contains("Texto de Um. Mais de Um.", ShownIn(panel));
            Assert.Contains(panel.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Parar");
            await model.WriteAsync("""{"choices":[{"delta":{},"finish_reason":"stop"}]}""");
            await model.EndAsync();
            await run;
            for (int i = 0; i < 3; i++) await Settle(window, view);
            Assert.Equal("end", first.Runs[^1].Stopped);
            var store = new AssistantStore(new FolderAssistantStorage(folder));
            Assert.DoesNotContain(store.ConversationsFor("two", first.Vault), c => c.Messages.Count > 0);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
