using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-113: "Somente leitura" chosen on screen is what is saved, survives a restart, is the catalog the model gets,
    /// and a write the model asks for anyway is refused by name; the note is untouched.
    /// </summary>
    [Fact]
    public async Task Agent_ToolSelectionOnScreenIsSavedSentAndEnforced() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-toolsel-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Um.md"), "# Um\n\n- velho\n");
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            async Task Pump() { for (int i = 0; i < 20; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
            Window? window = null;
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var panel = view.Assistant;
                panel.Open();
                panel.ShowSettings();
                await Pump();
                Button Named(string name) => panel.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);
                Named("Configurações: Agentes").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump();
                ClickButtonWithText(panel, "Configurar");
                await Pump();
                ClickButtonWithText(panel, "Alterar permissões");
                for (int i = 0; i < 50 && !view.Dialogs.IsOpen; i++) await Pump();
                ClickButtonWithText(view.Dialogs, "Somente leitura");
                await Pump();
                Assert.Equal("read", panel.Config.ToolScopeFor(AssistantAgent.AgentById("geral")));
                window.Close();

                // a restart: a new app on the same folders reads the same choice
                view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                Assert.Equal("read", panel.Config.ToolScopeFor(AssistantAgent.AgentById("geral")));
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk"));
                panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
                panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/m";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"w","function":{"name":"edit_note","arguments":"{\"path\":\"Um\",\"old_text\":\"- velho\",\"new_text\":\"- novo\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Não posso editar."}}]}"""));
                panel.Send("Troque velho por novo em Um");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                await panel.CurrentRun!;
                await Pump();
                var catalog = System.Text.Json.Nodes.JsonNode.Parse(model.Sent[0])!["tools"]!.AsArray().Select(t => (string)t!["function"]!["name"]!).ToList();
                Assert.Contains("read_note", catalog);
                Assert.DoesNotContain("edit_note", catalog);
                Assert.DoesNotContain("write_note", catalog);
                Assert.Contains("edit_note não é permitida para este agente", model.Sent[1]);
                Assert.Equal("# Um\n\n- velho\n", File.ReadAllText(Path.Combine(vault, "Um.md")));
                return true;
            }
            finally
            {
                window?.Close();
                UrbeApp.AssistantFolder = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
