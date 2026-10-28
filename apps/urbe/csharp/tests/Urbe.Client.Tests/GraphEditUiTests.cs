using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_CreatesAGraphInTheVaultThatTheEditorOpens() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-068: an agent's graph goes through the approval into the vault, as a graph the editor opens
        var dir = Path.Combine(Path.GetTempPath(), "urbe-graph-ai-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Personagens"));
        File.WriteAllText(Path.Combine(vault, "Personagens", "Ana.md"), "# Ana\n");
        File.WriteAllText(Path.Combine(vault, "Personagens", "Bruno.md"), "# Bruno\n");
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "ai");
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
        var view = new MainView(storage);
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p";
            panel.Config.Model = "test-model";
            panel.Config.AgentPolicies["geral"] = "auto";
            var model = new ScriptedModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            var args = new System.Text.Json.Nodes.JsonObject
            {
                ["path"] = "Mapas/Família.grafo.json", ["title"] = "Família",
                ["operations"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"op":"add_note","ref":"a","path":"Personagens/Ana"},{"op":"add_note","ref":"b","path":"Personagens/Bruno"},{"op":"connect","from":"a","to":"b","label":"irmãos"}]""")
            }.ToJsonString();
            model.Chats.Enqueue(Sse(
                "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"g1\",\"function\":{\"name\":\"edit_graph\",\"arguments\":" + System.Text.Json.JsonSerializer.Serialize(args) + "}}]}}]}",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Fiz o grafo da família."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Faça um grafo da família com Ana e Bruno");
            await panel.CurrentRun!;
            var result = panel.Conversation.Messages.SelectMany(m => m.Content).Single(b => b.Type == "tool_result" && b.ToolUseId == "g1");
            Assert.False(result.IsError, result.Content);
            var file = Path.Combine(vault, "Mapas", "Família.grafo.json");
            Assert.True(File.Exists(file));
            var graph = GraphDocument.Parse(File.ReadAllText(file));
            Assert.Equal(2, graph.Nodes.Count);
            Assert.Single(graph.Edges);
            await view.OpenGraphAsync("Mapas/Família.grafo.json");
            Assert.True(view.GraphOpen);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
