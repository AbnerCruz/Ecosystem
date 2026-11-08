using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task AssistantTabAndAgentNavigationKeepCurrentConversation()
        => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "urbe-ai-session-" + Guid.NewGuid().ToString("N"));
            var previous = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = folder;
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                var id = panel.Conversation.Id;
                panel.Conversation.Messages.Add(new AiChatMessage { Role = "user", Content = [AiChatBlock.TextBlock("Não esqueça este pedido.")] });
                panel.Close();
                panel.Open();
                Assert.Equal(id, panel.Conversation.Id);
                Assert.Contains(panel.Conversation.Messages, m => m.Role == "user");
                Assert.Null(panel.ViewTitle); // No agent picker just because the tab reopened.

                panel.SelectAgent("pesquisador");
                var specialist = panel.Conversation.Id;
                panel.SelectAgent("geral");
                Assert.Equal(id, panel.Conversation.Id);
                panel.SelectAgent("pesquisador");
                Assert.Equal(specialist, panel.Conversation.Id);
                return true;
            }
            finally
            {
                window.Close();
                UrbeApp.AssistantFolder = previous;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task EditorAssistantCompanionStaysAboveNoteAndExpandsSameChat()
        => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "urbe-ai-companion-" + Guid.NewGuid().ToString("N"));
            var previous = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = folder;
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                view.Editor.Open(new CityNote("Comece.md", "# Comece\n\nUma nota."));
                Assert.True(view.Editor.IsVisible);
                view.ToggleAssistant();
                var panel = view.Assistant;
                var id = panel.Conversation.Id;
                Assert.True(panel.CompactActive);
                Assert.False(panel.IsVisible);
                Assert.True(view.AssistantMini.IsVisible);
                Assert.Equal(Avalonia.Layout.VerticalAlignment.Top, view.AssistantMini.VerticalAlignment);
                var composer = view.AssistantMini.GetLogicalDescendants().OfType<TextBox>()
                    .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Mensagem rápida ao Assistente");
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "test"));
                panel.Config.ProviderId = "openrouter";
                panel.Config.Model = "org/modelo";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"content":"Mensagem rápida respondida."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                composer.Text = "Pergunta enviada pelo popup";
                var send = view.AssistantMini.GetLogicalDescendants().OfType<Button>()
                    .Single(b => b.Content is string text && text == "Enviar");
                send.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await panel.CurrentRun!;
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Editor.IsVisible);
                Assert.False(panel.IsVisible);
                Assert.Contains(panel.Conversation.Messages, m => m.Role == "user" &&
                    m.Content.Any(b => b.Text == "Pergunta enviada pelo popup"));
                Assert.Contains(panel.Conversation.Messages, m => m.Role == "assistant" &&
                    m.Content.Any(b => b.Text == "Mensagem rápida respondida."));
                view.ToggleAssistant();
                Assert.False(view.AssistantMini.IsVisible);
                view.ToggleAssistant();
                Assert.Equal(id, panel.Conversation.Id);
                panel.Open(); // Expand action uses the same underlying method.
                Assert.True(panel.IsVisible);
                Assert.True(view.Editor.IsVisible);
                Assert.Equal(id, panel.Conversation.Id);
                return true;
            }
            finally
            {
                window.Close();
                UrbeApp.AssistantFolder = previous;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
