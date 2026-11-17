using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-090: a consulted specialist answers with its own model and its own (read-only) tools, sees neither the
    /// asker's private memory nor shows its own to the asker, is linked to the asking conversation, and its answer
    /// comes back to it.
    /// </summary>
    [Fact]
    public async Task Delegation_TheSpecialistKeepsItsModelPermissionsAndMemoryApart() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-delegiso-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Céu.md"), "# Céu\n\nEstrelas.\n");
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "assistente");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "sel.json")));
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var panel = view.Assistant;
                panel.Open();
                var name = ((IAssistantApp)view).VaultName;
                var key = name.Length > 0 ? name : "Urbe";
                panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
                panel.Config.ProviderId = "p";
                panel.Config.Model = "modelo-de-quem-pede";
                panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "write", false, ""));
                panel.Config.CustomAgents.Add(new("two", "Dois", "sparkle", "", "read", false, ""));
                panel.Config.AgentToolScopes["one"] = "write";
                panel.Config.AgentPolicies["one"] = "auto";
                panel.Config.AgentModels["two"] = new AiModelChoice("p", "modelo-do-especialista");
                panel.Config.AgentDelegationAuto["one"] = true;
                panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "m1", Text = "Estrela: SEGREDO-DO-UM", Kind = "fact", Scope = "agent", AgentId = "one", Vault = key });
                panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "m2", Text = "Estrela: MEMORIA-DO-DOIS", Kind = "fact", Scope = "agent", AgentId = "two", Vault = key });
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-1","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"O que é uma estrela?\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Estrela é um astro luminoso."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Segundo o especialista, um astro luminoso."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                panel.SelectAgent("one");
                panel.Conversation.ProviderId = "p"; panel.Conversation.Model = "modelo-de-quem-pede";
                panel.Send("Fale sobre estrelas; consulte o especialista.");
                for (int i = 0; i < 200 && panel.CurrentRun is null; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                await panel.CurrentRun!;
                Assert.Equal(3, model.Sent.Count);
                static string Model(string body) => (string)JsonNode.Parse(body)!["model"]!;
                static List<string> Tools(string body) => JsonNode.Parse(body)!["tools"]?.AsArray().Select(t => (string)t!["function"]!["name"]!).ToList() ?? [];
                var (asker, specialist, back) = (model.Sent[0], model.Sent[1], model.Sent[2]);
                Assert.Equal("modelo-de-quem-pede", Model(asker));
                Assert.Equal("modelo-do-especialista", Model(specialist));
                Assert.Contains("edit_note", Tools(asker));
                Assert.DoesNotContain("edit_note", Tools(specialist));   // the specialist's own read-only scope
                Assert.Contains("SEGREDO-DO-UM", asker);              // each one does get its own memory…
                Assert.Contains("MEMORIA-DO-DOIS", specialist);
                Assert.DoesNotContain("SEGREDO-DO-UM", specialist);   // …and never the other's
                Assert.DoesNotContain("MEMORIA-DO-DOIS", asker);
                Assert.DoesNotContain("MEMORIA-DO-DOIS", back);
                Assert.Contains("Estrela é um astro luminoso.", back);   // the answer came back to the asker
                var store = new AssistantStore(new FolderAssistantStorage(UrbeApp.AssistantFolder));
                var child = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
                Assert.Equal(panel.Conversation.Id, child.OriginConversationId);
                Assert.Equal("one", child.OriginAgentId);
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
