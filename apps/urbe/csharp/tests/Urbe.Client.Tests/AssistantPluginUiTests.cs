using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;
using Urbe.Core.Plugins;

namespace Urbe.Client.Tests;

/// <summary>
/// Owner (15/10/2026): "faz um plugin de cronômetro para a aba do editor" must work, for any plugin idea. The Assistente
/// writes a .plugin.json for the native API; approved in the chat, it is written to Personalização/plugins, turned on,
/// its button shows in the editor's top bar and its panel runs; turned off in Personalização, everything goes away.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_WritesAPluginThatRunsInTheEditor() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-plugin-ai-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nTexto.\n");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var device = new MemoryDeviceStore();
            var view = new MainView(storage);
            view.Customization.Device = device;
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var model = new ScriptedModel();
                var panel = view.Assistant;
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Open();   // loads the configuration of this device first
                panel.Config.Providers.Add(new AiProviderConfig("or", "openrouter", "OpenRouter", "sk-or-teste", ""));
                panel.Config.ProviderId = "or";
                panel.Config.Model = "org/modelo-b";

                var args = new JsonObject { ["path"] = "Cronômetro", ["plugin"] = JsonNode.Parse(VaultPluginSpec.ExampleTimer) }.ToJsonString();
                var call = new JsonObject
                {
                    ["choices"] = new JsonArray(new JsonObject
                    {
                        ["delta"] = new JsonObject { ["tool_calls"] = new JsonArray(new JsonObject { ["index"] = 0, ["id"] = "call_p", ["function"] = new JsonObject { ["name"] = "write_plugin", ["arguments"] = args } }) }
                    })
                }.ToJsonString();
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_g","function":{"name":"plugin_guide","arguments":"{}"}}]}}]}""", """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse(call, """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Pronto: o cronômetro está no topo do editor."}}]}"""));
                panel.Send("faz um plugin de cronômetro para a aba do editor");
                for (int i = 0; i < 300 && panel.PendingApproval is null; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(panel.PendingApproval is not null, string.Join("\n", panel.Conversation.Messages.SelectMany(m => m.Content).Select(b => b.Type + ": " + (b.Text ?? b.Content)[..Math.Min(400, (b.Text ?? b.Content).Length)])) + "\nERR " + panel.Conversation.Runs.LastOrDefault()?.Error);
                var pending = Assert.IsType<AiApprovalRequest>(panel.PendingApproval);
                Assert.Equal(("write_plugin", "plugin", "Personalização/plugins/Cronômetro.plugin.json"), (pending.Call.Name, pending.Access, pending.Preview!.Path));
                Assert.Contains("\"guide\"", "\"guide\"");
                Assert.Contains("PLUGINS DO URBE NATIVO", model.Sent[1]);   // the guide went to the model
                for (int i = 0; i < 10; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                var texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Criar e ligar plugin · Personalização/plugins/Cronômetro.plugin.json", texts);
                Assert.False(File.Exists(Path.Combine(vault, "Personalização", "plugins", "Cronômetro.plugin.json")));
                ClickButtonWithText(panel, "Ligar plugin");
                await panel.CurrentRun!;
                Dispatcher.UIThread.RunJobs();

                // written, approved for this exact text, running
                var file = Path.Combine(vault, "Personalização", "plugins", "Cronômetro.plugin.json");
                Assert.True(File.Exists(file));
                Assert.Equal("Cronômetro", (string?)JsonNode.Parse(File.ReadAllText(file))!["nome"]);
                var running = Assert.IsType<Urbe.Client.Plugins.VaultPluginAdapter>(view.RunningPlugin("vault:Cronômetro"));
                Assert.Contains(view.Customization.Plugins(), p => p.Path.EndsWith("Cronômetro.plugin.json", StringComparison.Ordinal) && p.Status == "aprovado");

                // the button in the editor's top bar opens the panel; Iniciar and the clock count; Inserir na nota writes at the caret
                view.OpenNoteByPath("Comece.md");
                Dispatcher.UIThread.RunJobs();
                var button = Assert.Single(view.Editor.PluginButtons.Children.OfType<Button>());
                Assert.Equal("Cronômetro", ToolTip.GetTip(button));
                button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(running.Panel!.IsOpen);
                var card = (Control)running.Panel;
                ClickButtonWithText(card, "Iniciar");
                Assert.True(running.Runtime!.ClockOn);
                for (int i = 0; i < 3; i++) await running.Runtime.TickAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("00:03", card.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                Assert.Contains("Pausar", card.GetLogicalDescendants().OfType<Button>().Select(b => b.Content as string));
                ClickButtonWithText(card, "Inserir na nota");
                Assert.Contains("⏱ 00:03", view.Editor.Editor.Text);
                Snapshot(window, "native-plugin-ai-timer.png");

                // turned off in Personalização: the button and the panel go away
                view.Customization.DisablePlugin("Personalização/plugins/Cronômetro.plugin.json");
                Dispatcher.UIThread.RunJobs();
                Assert.Null(view.RunningPlugin("vault:Cronômetro"));
                Assert.Empty(view.Editor.PluginButtons.Children);
                return true;
            }
            finally
            {
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
