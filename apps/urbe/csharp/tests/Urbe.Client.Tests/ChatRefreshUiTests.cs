using System.IO.Pipelines;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.Ui;
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

    /// <summary>A model whose next answer streams only what the test writes (a message caught while it is written).</summary>
    private sealed class GatedModel : HttpMessageHandler
    {
        public Pipe Stream { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(Stream.Reader.AsStream()) };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(res);
        }
        public async Task WriteAsync(string json) => await Stream.Writer.WriteAsync(Encoding.UTF8.GetBytes("data: " + json + "\n\n"));
        public async Task EndAsync()
        {
            await Stream.Writer.WriteAsync(Encoding.UTF8.GetBytes("data: [DONE]\n\n"));
            await Stream.Writer.CompleteAsync();
        }
    }

    private static string TextOf(TextBlock t) => t.Text ?? string.Concat(t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text) ?? []);

    private static string ShownIn(AssistantPanel panel) => string.Join("\n", panel.Log.GetVisualDescendants().OfType<TextBlock>().Select(TextOf));

    private static async Task Wait(Window window, MainView view, int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until) { await Settle(window, view); await Task.Delay(40); }
    }

    [Fact]
    public async Task Assistant_AFormShowsAtOnceWithASelectionLeftInAnEarlierAnswer() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // FeaturesCheck (Atualização visual da conversa de IA): the input was processed but the screen did not show it
        // until the chat was opened again. A full render (a form, the end of a run, an approval answered) waited for
        // any selection anywhere in the log to go away.
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
            var first = panel.Log.GetVisualDescendants().OfType<SelectableTextBlock>().First(t => TextOf(t).Contains("trecho para copiar"));
            first.SelectionStart = 0;
            first.SelectionEnd = 8;

            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"form-one","function":{"name":"request_form","arguments":"{\"title\":\"Ficha\",\"fields\":[{\"id\":\"nome\",\"label\":\"Nome\",\"type\":\"text\"}]}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            panel.Send("Uma ficha");
            await panel.CurrentRun!;
            await Settle(window, view);
            Assert.Single(panel.Log.GetLogicalDescendants().OfType<FormRenderer>());   // at once, not after the selection goes
            Assert.Contains(first, panel.Log.GetVisualDescendants().OfType<SelectableTextBlock>());
            Assert.Equal(8, first.SelectionEnd);   // the selection stays where it was
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task Assistant_AnAnswerKeepsArrivingWithASelectionLeftInIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // the person copies a part of the answer while it is still being written and leaves it selected: the rest
        // arrives once the selection is no longer being moved, and the selection is kept on the new text
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
            var model = new GatedModel(); panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Send("Oi");
            var run = panel.CurrentRun!;
            await model.WriteAsync("""{"choices":[{"delta":{"content":"Primeira parte da resposta."}}]}""");
            SelectableTextBlock? part = null;
            for (int i = 0; i < 100 && part is null; i++)
            {
                await Wait(window, view, 40);
                part = panel.Log.GetVisualDescendants().OfType<SelectableTextBlock>().FirstOrDefault(t => TextOf(t).Contains("Primeira parte"));
            }
            Assert.NotNull(part);
            part.SelectionStart = 0;
            part.SelectionEnd = 8;
            await model.WriteAsync("""{"choices":[{"delta":{"content":" Segunda parte chegou."}}]}""");
            await model.WriteAsync("""{"choices":[{"delta":{},"finish_reason":"stop"}]}""");
            await model.EndAsync();
            await run;
            await Wait(window, view, 300);
            Assert.DoesNotContain("Segunda parte chegou.", ShownIn(panel));   // not under a selection being made
            await Wait(window, view, 2600);
            Assert.Contains("Segunda parte chegou.", ShownIn(panel));
            var now = panel.Log.GetVisualDescendants().OfType<SelectableTextBlock>().First(t => TextOf(t).Contains("Segunda parte"));
            Assert.Equal((0, 8), (now.SelectionStart, now.SelectionEnd));
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
