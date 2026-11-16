using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-154: an agent changes two notes and the second write is refused (it is open with the person's unsaved text).
    /// The first change stays recorded and undoable, the person's text is untouched, and the agent is told why.
    /// </summary>
    [Fact]
    public async Task Assistant_ASecondWriteThatFailsKeepsTheFirstUndoableAndThePersonsText() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-partial-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Um.md"), "# Um\n\n- velho\n");
            File.WriteAllText(Path.Combine(vault, "Dois.md"), "# Dois\n\n- velho\n");
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.OpenNoteByPath("Dois.md");
                await Settle(window, view);
                view.Editor.SetMode(EditorMode.Fonte);
                view.Editor.Editor.Text = "# Dois\n\n- velho\n- escrito pela pessoa, ainda não salvo\n";
                var model = new ScriptedModel();
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                panel.Config.AgentPolicies["geral"] = "auto";
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk"));
                panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
                panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/m";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"a","function":{"name":"edit_note","arguments":"{\"path\":\"Um\",\"old_text\":\"- velho\",\"new_text\":\"- novo\"}"}},{"index":1,"id":"b","function":{"name":"edit_note","arguments":"{\"path\":\"Dois\",\"old_text\":\"- velho\",\"new_text\":\"- novo\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Alterei Um; Dois estava aberta com mudanças."}}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisado."}}]}"""));
                panel.Send("Troque velho por novo em Um e Dois");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.True(panel.CurrentRun is not null, "não começou: " + string.Join(" | ", panel.Conversation.Messages.Select(m => m.Role)) + " / " + view.NoticeText);
                await panel.CurrentRun!;
                for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains("- novo", File.ReadAllText(Path.Combine(vault, "Um.md")));
                Assert.Equal("# Dois\n\n- velho\n", File.ReadAllText(Path.Combine(vault, "Dois.md")));
                Assert.Contains("escrito pela pessoa", view.Editor.Editor.Text);   // the person's unsaved text is still there
                Assert.Contains("não salvas", model.Sent[1]);                        // the agent was told why Dois was not written
                var run = panel.Conversation.Runs.Last();
                Assert.Equal(["Um.md"], run.Changes.Select(c => (c.After ?? c.Before)!.Path));
                await panel.UndoLastAsync(confirmed: true);
                Assert.Equal("# Um\n\n- velho\n", File.ReadAllText(Path.Combine(vault, "Um.md")));
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
