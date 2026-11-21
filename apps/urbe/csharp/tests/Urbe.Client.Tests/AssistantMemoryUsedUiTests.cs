using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-086: the answer shows the memories it drew on, and one is corrected from there without leaving the chat.</summary>
    [Fact]
    public async Task Assistant_TheAnswerShowsTheMemoriesItUsedAndOneIsCorrectedThere() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-memused-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 1280, Height = 760, Content = view };
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
                panel.GetLogicalDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Chave da API").Text = "sk-or-teste";
                ClickButtonWithText(panel, "Salvar e escolher modelo");
                for (int i = 0; i < 50 && !panel.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Modelo B"); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(panel, "Modelo B");

                var memory = AssistantMemoryEngine.Remember(panel.Config, "Prefiro respostas curtas", ((IAssistantApp)view).VaultName, kind: "preference");
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Certo."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                panel.Send("Oi");
                await panel.CurrentRun!;
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains("Prefiro respostas curtas", model.Sent[0]);
                Assert.Equal([memory.Id], panel.Conversation.Runs.Last().MemoryUsed);

                // the link under the answer, then Editar: the memory changes and the chat stays
                ClickButtonWithText(panel, "Usou 1 lembrança");
                await Task.Delay(10); Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Prefiro respostas curtas");
                await Task.Delay(10); Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Editar");
                await Task.Delay(10); Dispatcher.UIThread.RunJobs();
                view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Single().Text = "Prefiro respostas curtas e com fontes";
                ClickButtonWithText(view.Dialogs, "Confirmar");
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Prefiro respostas curtas e com fontes", AssistantMemoryEngine.ResolveForEdit(panel.Config, memory.Id)!.Text);
                Assert.Null(panel.ViewTitle);   // still the chat, not the memory manager
                return true;
            }
            finally { UrbeApp.AssistantFolder = before; window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
