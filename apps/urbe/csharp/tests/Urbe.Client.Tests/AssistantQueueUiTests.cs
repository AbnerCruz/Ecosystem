using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-009: "segundo envio, fila de mensagens e preservação de anexos durante execuções, erros e cancelamentos". A
/// message queued while the agent works is never lost: a stop keeps it waiting (with a button to send it), and a
/// provider error keeps the person's message and the queue for "Tentar de novo".
/// </summary>
public sealed partial class HeadlessUiTests
{
    private sealed class FailingModel : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"data":[]}""") });
            Calls++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { Content = new StringContent("""{"error":{"message":"falha do provedor"}}""") });
        }
    }

    private static AssistantPanel Configured(MainView view)
    {
        var panel = view.Assistant;
        panel.Open(); panel.SelectAgent("geral");
        panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
        panel.Config.ProviderId = "p"; panel.Config.Model = "test-model";
        return panel;
    }

    private static void Queue(AssistantPanel panel, string text)
    {
        panel.Input.Text = text;
        panel.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Enviar após a resposta atual")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [Fact]
    public async Task Queue_AStopKeepsTheQueuedMessageUntilThePersonSendsIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = Configured(view);
            var model = new ParallelModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Send("Primeira");
            var run = panel.CurrentRun!;
            Queue(panel, "Segunda");
            panel.StopCompact();
            Assert.Same(run, await Task.WhenAny(run, Task.Delay(3000)));
            for (int i = 0; i < 3; i++) await Settle(window, view);
            Assert.Equal("aborted", panel.Conversation.Runs[^1].Stopped);
            // the queued message waits, visible, and is not sent by itself after a stop
            Assert.Equal("Segunda", Assert.Single(panel.Conversation.QueuedMessages).Content[0].Text);
            Assert.Equal(1, model.Started);
            Assert.Contains("Na fila · Segunda", ShownIn(panel));
            model.Release.TrySetResult(true);
            ClickButtonWithText(panel, "Retomar mensagens na fila");
            await panel.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(10));
            for (int i = 0; i < 3; i++) await Settle(window, view);
            Assert.Empty(panel.Conversation.QueuedMessages);
            Assert.Equal(2, model.Started);
            Assert.Contains(panel.Conversation.Messages, m => m.Role == "user" && m.Content.Any(b => b.Text == "Segunda"));
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task Queue_AProviderErrorKeepsTheMessageAndOffersToTryAgain() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = Configured(view);
            var model = new FailingModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Send("Mensagem importante");
            await panel.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(10));
            for (int i = 0; i < 3; i++) await Settle(window, view);
            Assert.NotNull(panel.Conversation.Runs[^1].Error);
            Assert.Contains(panel.Conversation.Messages, m => m.Role == "user" && m.Content.Any(b => b.Text == "Mensagem importante"));
            Assert.Contains("Mensagem importante", ShownIn(panel));
            var calls = model.Calls;
            ClickButtonWithText(panel, "Tentar de novo");
            await panel.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(model.Calls > calls);   // tried again with the same message
            Assert.Single(panel.Conversation.Messages, m => m.Role == "user" && m.Content.Any(b => b.Text == "Mensagem importante"));
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
