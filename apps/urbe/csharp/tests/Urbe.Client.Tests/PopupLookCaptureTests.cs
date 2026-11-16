using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // Opt-in (URBE_CHAT_SHOT=dir): the contextual AI pop-up over the editor, with steps and a long formatted answer.
    [Fact]
    public async Task PopupLook_Capture() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        if (Environment.GetEnvironmentVariable("URBE_CHAT_SHOT") is not { } outDir) return true;
        var view = new MainView();
        var window = new Window { Width = 360, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync([new CityNote("Comece.md", "# Comece\n\nTexto do teste, com um parágrafo para o editor ter o que mostrar.\n\nOutro parágrafo.")]);
            await Settle(window, view);
            view.OpenNoteByPath("Comece.md");
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            var model = new ScriptedModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk"));
            panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/modelo-b";
            panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/modelo-b";
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"c1","function":{"name":"read_note","arguments":"{\"path\":\"Comece\"}"}}]}}]}""", """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            var answer = "## Sugestões\\n\\nA nota está clara. Três ajustes:\\n\\n1. Dê um título mais específico.\\n2. Junte os dois parágrafos.\\n3. Acrescente um exemplo.\\n\\n> O primeiro parágrafo já resume a ideia.";
            model.Chats.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"" + answer + "\"}}]}", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisado."}}]}"""));
            panel.Close();
            panel.AskInNote("Comece.md", "Comece", new NoteMarks.AgentCall(0, 0, "", "Como melhorar esta nota?"));
            await panel.CurrentRun!;
            for (int i = 0; i < 30; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
            Directory.CreateDirectory(outDir);
            window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "popup-360.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            window.Width = 900;
            for (int i = 0; i < 20; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
            window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "popup-900.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            // the phone turned sideways: little height, the composer must stay whole and the conversation still scroll
            window.Width = 760; window.Height = 360;
            for (int i = 0; i < 20; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
            window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "popup-landscape.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
