using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_ALeftoverSelectionDoesNotFreezeTheChat() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-008 (owner: the action was processed but the screen did not show it until the AI tab was closed and
        // reopened): a selection left in an earlier message held every render back
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant; panel.Open(); panel.SelectAgent("geral");
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p"; panel.Config.Model = "test-model";
            var model = new ScriptedModel(); panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Primeira resposta, com um trecho para copiar."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Oi");
            await panel.CurrentRun!;
            await Settle(window, view);
            var first = panel.Log.GetVisualDescendants().OfType<SelectableTextBlock>().First(t => (t.Text ?? "").Contains("trecho para copiar") || t.Inlines?.Count > 0);
            first.SelectionStart = 0;
            first.SelectionEnd = 5;   // the person copied something and left it selected

            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Segunda resposta chegou."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Mais uma");
            await panel.CurrentRun!;
            for (int i = 0; i < 10; i++) await Settle(window, view);
            string Shown() => string.Join("\n", panel.Log.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Concat(t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text) ?? [])));
            Assert.Contains("Segunda resposta chegou.", Shown());
            Assert.Contains("Mais uma", Shown());
            Assert.Equal(5, first.SelectionEnd);   // and the earlier selection is still there
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
