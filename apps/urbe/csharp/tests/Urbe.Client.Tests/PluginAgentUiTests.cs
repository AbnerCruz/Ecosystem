using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-146: an approved plugin asks an agent to work on the note it delimited; the person agrees, the agent works
    /// with its own permissions, and a write outside the plugin's scope is refused while the one inside goes through.
    /// </summary>
    [Fact]
    public async Task Plugin_AsksAnAgentThatWritesOnlyInsideTheDelimitedScope() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-plugagent-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Projetos"));
        Directory.CreateDirectory(Path.Combine(vault, "Personalização", "plugins"));
        File.WriteAllText(Path.Combine(vault, "Projetos", "Plano.md"), "# Plano\n\n- velho\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n\n- velho\n");
        const string pluginPath = "Personalização/plugins/Revisor.plugin.json";
        File.WriteAllText(Path.Combine(vault, pluginPath), """
            {"urbe-plugin": 1, "nome": "Revisor", "versao": "1.0", "permissoes": ["comandos", "agentes"],
             "estado": {"rec": "", "rec_acao": "", "rec_fontes": [], "rec_estado": ""},
             "comandos": [{"titulo": "Revisar o plano", "acoes": [{"perguntarAgente": "Atualize o plano", "escopo": ["Projetos/Plano.md"], "agente": "geral", "resposta": "rec"}]}]}
            """);
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Pump(int n = 20) { for (int i = 0; i < n; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var model = new ScriptedModel();
            var panel = view.Assistant;
            panel.Open();
            panel.Config.AgentPolicies["geral"] = "auto";
            panel.Config.AgentToolScopes["geral"] = "write";
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk"));
            panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
            panel.Close();
            model.Chats.Enqueue(Sse(
                """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"fora","function":{"name":"edit_note","arguments":"{\"path\":\"Outra\",\"old_text\":\"- velho\",\"new_text\":\"- novo\"}"}},{"index":1,"id":"dentro","function":{"name":"edit_note","arguments":"{\"path\":\"Projetos/Plano\",\"old_text\":\"- velho\",\"new_text\":\"- novo\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Plano atualizado; a outra nota está fora do que o plugin indicou.\n```json\n{\"acao\": \"Revisar o item novo\", \"fontes\": [\"Projetos/Plano.md\"]}\n```"}}]}"""));

            view.Customization.EnablePlugin(pluginPath);
            await Pump();
            var running = Assert.IsType<Urbe.Client.Plugins.VaultPluginAdapter>(view.RunningPlugin("vault:Revisor"));
            var ask = running.Runtime!.RunAsync((JsonArray)running.Runtime.Spec.Commands[0]!["acoes"]!);
            for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) await Pump(2);
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Pode trabalhar em: Projetos/Plano.md") == true);
            ClickButtonWithText(view.Dialogs, "Pedir");
            await ask;   // CST-146: the plugin's action waits for the agent's answer
            await Pump();
            // the answer came back to the plugin, in parts, with its source checked in the vault
            Assert.Equal("Plano atualizado; a outra nota está fora do que o plugin indicou.", running.Runtime.Vars["rec"]);
            Assert.Equal(("concluido", "Revisar o item novo"), (running.Runtime.Vars["rec_estado"], running.Runtime.Vars["rec_acao"]));
            Assert.Equal(["Projetos/Plano.md"], (List<object?>)running.Runtime.Vars["rec_fontes"]!);
            Assert.Contains("```json", model.Sent[0]);   // the agent was asked for the structured close
            Assert.Equal("# Outra\n\n- velho\n", File.ReadAllText(Path.Combine(vault, "Outra.md")));   // outside: refused
            Assert.Contains("- novo", File.ReadAllText(Path.Combine(vault, "Projetos", "Plano.md")));  // inside: done
            Assert.Contains("Fora do escopo", model.Sent[1]);
            Assert.True(panel.CompactActive);   // shown in the small card, the person stays where they were
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
