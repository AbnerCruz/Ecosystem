using System.Net;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private sealed class HttpErrorModel(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"id":"org/modelo-b","name":"Modelo B","context_length":64000,"supported_parameters":["tools"]}]}""") }
                : new HttpResponseMessage(code) { Content = new StringContent(body) });
    }

    /// <summary>
    /// CST-080: the provider fails while the agent works from the note: the small card says it could not finish and
    /// why, the note is untouched, nothing jumps to the full conversation, and the conversation still opens from it.
    /// </summary>
    [Fact]
    public async Task NoteAgent_AFailingProviderIsToldInTheCompactCardWithoutLeavingTheNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                const string note = "# Comece\n\nTexto do teste.";
                await view.LoadAsync([new CityNote("Comece.md", note)]);
                await Settle(window, view);
                view.OpenNoteByPath("Comece.md");
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                panel.Providers = new AssistantProviders(new HttpErrorModel(HttpStatusCode.InternalServerError, """{"error":{"message":"servidor fora do ar"}}"""), (_, _) => Task.CompletedTask);
                panel.Config.AgentPolicies["geral"] = "ask";
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.ProviderId = "openrouter";
                panel.Config.Model = "org/modelo-b";
                panel.Conversation.ProviderId = "openrouter";
                panel.Conversation.Model = "org/modelo-b";
                panel.Close();
                Assert.Null(panel.AskInNote("Comece.md", "Comece", new NoteMarks.AgentCall(0, 0, "", "Resuma a nota")));
                if (panel.CurrentRun is { } run) { try { await run; } catch (Exception e) when (e is not OutOfMemoryException) { } }
                for (int i = 0; i < 60 && panel.CompactStatus == "thinking"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("error", panel.CompactStatus);
                var texts = view.AssistantMini.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
                Assert.Contains("Não foi possível concluir", texts);
                Assert.Contains(texts, t => t.Contains("500", StringComparison.Ordinal) || t.Contains("servidor fora do ar", StringComparison.Ordinal));
                Assert.True(view.AssistantMini.IsVisible);
                Assert.False(panel.IsVisible);
                Assert.True(view.Editor.IsVisible);
                Assert.Equal(note, view.Editor.Current!.Content);
                // the whole conversation is one tap away, with the same failure in it
                view.AssistantMini.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Abrir conversa")
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(panel.IsVisible);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
