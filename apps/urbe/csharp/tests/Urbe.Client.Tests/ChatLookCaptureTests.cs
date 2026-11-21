using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // Opt-in (URBE_CHAT_SHOT=dir): the chat as a person sees it, with steps and a formatted answer, phone and wide.
    [Fact]
    public async Task ChatLook_Capture() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        if (Environment.GetEnvironmentVariable("URBE_CHAT_SHOT") is not { } outDir) return true;
        var dir = Path.Combine(Path.GetTempPath(), "urbe-chatlook-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Fotossíntese.md"), "# Fotossíntese\n\nAs plantas usam luz.\n");
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var model = new ScriptedModel();
            view.AssistantLauncher.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            var panel = view.Assistant;
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.SelectAgent("geral");
            ClickButtonWithText(panel, "OpenRouter");
            panel.GetLogicalDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Chave da API").Text = "sk";
            ClickButtonWithText(panel, "Salvar e escolher modelo");
            for (int i = 0; i < 50 && !panel.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Modelo B"); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            ClickButtonWithText(panel, "Modelo B");
            model.Chats.Enqueue(Sse(
                """{"choices":[{"delta":{"content":"Vou ler a nota."}}]}""",
                """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"c1","function":{"name":"read_note","arguments":"{\"path\":\"Fotossíntese\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            var answer = "## Resumo\\n\\nA **fotossíntese** transforma luz em energia química, como diz [[Fotossíntese]].\\n\\n1. A luz é captada pela clorofila.\\n2. A água é quebrada e libera oxigênio.\\n3. O carbono vira açúcar.\\n\\n| Fase | Onde | Produto |\\n|---|---|---|\\n| Clara | Tilacoide | ATP e NADPH |\\n| Escura | Estroma | Glicose |\\n\\n> Sem luz, a fase clara para.\\n\\n```python\\nenergia = luz * eficiencia\\n```\\n\\nFórmula: $6CO_2 + 6H_2O \\\\to C_6H_{12}O_6 + 6O_2$";
            model.Chats.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"" + answer + "\"}}]}", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisado."}}]}"""));
            panel.Send("Resuma a fotossíntese a partir da minha nota");
            await panel.CurrentRun!;
            for (int i = 0; i < 30; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
            Directory.CreateDirectory(outDir);
            window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "chat-phone.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            window.Width = 1280; window.Height = 900;
            for (int i = 0; i < 20; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
            window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "chat-wide.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            return true;
        }
        finally { UrbeApp.AssistantFolder = before; window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
