using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.Ui;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-121: in a real answer, the code block copies its exact code, "Copiar" copies the whole Markdown and
    /// "Selecionar texto" shows the same original; none of them sends, runs or opens anything else.
    /// </summary>
    [Fact]
    public async Task Chat_CodeAndTextAreCopiedExactlyWithoutSideEffects() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-chatcopy-" + Guid.NewGuid().ToString("N"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "assistant");
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            async Task Pump() { for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
            try
            {
                await view.LoadAsync([new CityNote("Comece.md", "# Comece")]);
                await Settle(window, view);
                const string code = "def soma(a, b):\n\treturn a + b  # ação < 1 && `x`\n";
                var answer = "Aqui está a função:\n\n```python\n" + code + "```\n\nUse com **cuidado**.";
                var model = new ScriptedModel();
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste-copiar"));
                panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
                panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/m";
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":""" + System.Text.Json.JsonSerializer.Serialize(answer) + """}}]}"""));
                panel.Send("Escreva uma função");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                await panel.CurrentRun!;
                await Pump();
                var messages = panel.Conversation.Messages.Count;
                var sent = model.Sent.Count;

                var block = panel.GetLogicalDescendants().OfType<CodeBlockView>().Single();
                block.CopyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (int i = 0; i < 30 && block.CopyButton.Content as string != "Copiado"; i++) await Pump();
                Assert.Equal(code.TrimEnd('\n'), await window.Clipboard!.TryGetTextAsync());   // the fence's line break is not code

                panel.GetLogicalDescendants().OfType<Button>().Last(b => b.Content as string == "Copiar" || b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Copiar"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));   // the answer's, under it
                await Pump();
                Assert.Equal(answer, await window.Clipboard!.TryGetTextAsync());

                ClickButtonWithText(panel, "Selecionar texto");
                await Pump();
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.IsReadOnly && t.Text == answer);

                Assert.Equal(messages, panel.Conversation.Messages.Count);   // nothing was sent or run by copying
                Assert.Equal(sent, model.Sent.Count);
                Assert.Null(panel.CurrentRun is { IsCompleted: false } ? panel.CurrentRun : null);
                return true;
            }
            finally
            {
                window.Close();
                UrbeApp.AssistantFolder = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
