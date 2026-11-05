using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>CST-220: the capability view and the attachment review read the one model catalog.</summary>
public sealed partial class HeadlessUiTests
{
    private sealed class OneAnswer : System.Net.Http.HttpMessageHandler
    {
        public readonly List<string> Bodies = [];
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken token)
        {
            if (request.Content is not null)
            {
                var body = await request.Content.ReadAsStringAsync(token);
                lock (Bodies) Bodies.Add(body);
            }
            return new(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"Vi.\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",
                    System.Text.Encoding.UTF8, "text/event-stream")
            };
        }
    }

    private static void SelectionConfig(AssistantPanel panel)
    {
        panel.Config.Providers.Add(new AiProviderConfig("or", "openrouter", "OpenRouter", "sk-or-test"));
        panel.Config.ProviderId = "or";
        panel.Config.Model = "acme/text-only";
        panel.Config.ModelCache["or"] = new AiModelCache(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), [
            new AiModelInfo("acme/text-only", "Só texto", 32000, (.5, 1), true, ["text"], ["text"]),
            new AiModelInfo("acme/vision", "Visão", 128000, (1, 2), true, ["text", "image", "file"], ["text"], .1)
        ]);
    }

    [Fact]
    public async Task ModelCapabilities_ShowsWhatEachModelDoesAndVideoHonestly() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            SelectionConfig(panel);
            panel.ShowSettings();
            Dispatcher.UIThread.RunJobs();
            var open = panel.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "Ver capacidades dos modelos");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("O que cada modelo faz", panel.ViewTitle);
            var texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains("Só texto · em uso", texts);
            Assert.Contains("Lê: texto, imagem, PDF", texts);
            Assert.Contains("Lê: texto", texts);
            Assert.Contains(texts, t => t.StartsWith("Preço: US$ 1 entrada · US$ 2 saída · US$ 0.1 entrada em cache", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.StartsWith("Onde roda: no provedor", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.StartsWith("Vídeo indisponível", StringComparison.Ordinal) && t.Contains("nenhum modelo configurado gera vídeo"));
            Snapshot(window, "native-assistant-model-capabilities-phone.png");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task ModelSelection_AnImageForATextOnlyModelOffersTheFallbackBeforeSending() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            var model = new OneAnswer();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            SelectionConfig(panel);
            ((List<AiChatBlock>)panel.PendingAttachments).Add(new AiChatBlock { Type = "attachment", FileName = "foto.png", MimeType = "image/png", Content = "iVBORw0KGgo=" });
            var selection = panel.AttachmentSelection()!;
            Assert.Equal(AiSelectionOutcome.Fallback, selection.Outcome);
            Assert.Equal("acme/vision", selection.Fallback!.ModelId);

            panel.Input.Text = "O que tem na foto?";
            panel.Input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            for (int i = 0; i < 40 && !view.Dialogs.IsOpen; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            Assert.True(view.Dialogs.IsOpen);
            Assert.Empty(model.Bodies); // nothing sent before the person answers
            var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains(texts, t => t.Contains("não consegue ler imagem") && t.Contains("Visão · OpenRouter consegue"));
            var use = view.Dialogs.GetLogicalDescendants().OfType<Button>().First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Usar Visão") || b.Content as string == "Usar Visão");
            use.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 200 && (panel.CurrentRun is null || !panel.CurrentRun.IsCompleted); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(new AiModelChoice("or", "acme/vision"), panel.Config.AgentModels[panel.Conversation.AgentId]);
            Assert.Contains(model.Bodies, b => b.Contains("\"model\":\"acme/vision\"") && b.Contains("image_url"));
            Assert.Equal(2, panel.CurrentModelCapabilities!.Price!.Output);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
