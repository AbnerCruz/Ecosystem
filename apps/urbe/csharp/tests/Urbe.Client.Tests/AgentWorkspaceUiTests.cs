using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private sealed class ParallelModel : System.Net.Http.HttpMessageHandler
    {
        public int Started;
        public readonly TaskCompletionSource<bool> BothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method == System.Net.Http.HttpMethod.Get)
                return new(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("""{"data":[]}""") };
            if (System.Threading.Interlocked.Increment(ref Started) >= 2) BothStarted.TrySetResult(true);
            await Release.Task.WaitAsync(token);
            return new(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(Sse(
                    """{"choices":[{"delta":{"content":"Resposta concluída."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""), System.Text.Encoding.UTF8, "text/event-stream")
            };
        }
    }

    [Fact]
    public async Task Multichat_IndependentAgentRunsSurviveSwitches() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-multichat-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            var model = new ParallelModel();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p";
            panel.Config.Model = "test-model";
            panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "read", false, ""));
            panel.Config.CustomAgents.Add(new("two", "Dois", "sparkle", "", "read", false, ""));

            panel.SelectAgent("one");
            var firstId = panel.Conversation.Id;
            panel.Send("Tarefa individual um");
            var firstRun = panel.CurrentRun!;
            panel.SelectAgent("two");
            var secondId = panel.Conversation.Id;
            Assert.NotEqual(firstId, secondId);
            panel.Send("Tarefa individual dois");
            var secondRun = panel.CurrentRun!;
            await model.BothStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(panel.Running);
            panel.SelectAgent("one"); // New blank chat; old runs are still alive.
            Assert.False(panel.Running);
            Assert.False(firstRun.IsCompleted);
            Assert.False(secondRun.IsCompleted);
            model.Release.TrySetResult(true);
            await Task.WhenAll(firstRun, secondRun);
            var store = new AssistantStore(new FolderAssistantStorage(folder));
            Assert.Equal("one", store.GetConversation(firstId)!.AgentId);
            Assert.Equal("two", store.GetConversation(secondId)!.AgentId);
            Assert.Contains(store.GetConversation(firstId)!.Messages.SelectMany(m => m.Content), b => b.Text == "Tarefa individual um");
            Assert.DoesNotContain(store.GetConversation(secondId)!.Messages.SelectMany(m => m.Content), b => b.Text == "Tarefa individual um");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Delegation_FollowUpReturnsToSameSpecialistThread() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "urbe-delegation-followup-" + Guid.NewGuid().ToString("N"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = folder;
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                var panel = view.Assistant;
                panel.Open();
                panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
                panel.Config.ProviderId = "p";
                panel.Config.Model = "test-model";
                panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "read", false, ""));
                panel.Config.CustomAgents.Add(new("two", "Dois", "sparkle", "", "read", false, ""));
                panel.Config.AgentDelegationAuto["one"] = true;
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-first","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"Qual a cor de Marte?\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Marte é avermelhado."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Recebi a consulta."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                panel.SelectAgent("one");
                panel.Send("Pergunte ao especialista a cor de Marte");
                await panel.CurrentRun!;
                var store = new AssistantStore(new FolderAssistantStorage(folder));
                var child = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
                Assert.Equal(panel.Conversation.Id, child.OriginConversationId);
                Assert.Equal("one", child.OriginAgentId);
                var call = System.Text.Json.JsonSerializer.Serialize(new
                {
                    choices = new[] { new { delta = new { tool_calls = new[]
                    {
                        new { index = 0, id = "consult-followup", function = new
                        {
                            name = "consult_agent",
                            arguments = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                agent_id = "two", task = "E qual o motivo?", conversation_id = child.Id
                            })
                        }}
                    } } } }
                });
                model.Chats.Enqueue(Sse(call, """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Por causa de minerais de ferro oxidados."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"O especialista explicou."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                panel.Send("Peça uma explicação ao mesmo especialista");
                await panel.CurrentRun!;
                var followup = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
                Assert.Equal(child.Id, followup.Id);
                Assert.Equal(2, followup.Messages.Count(m => m.Role == "user" && m.Meta?.ToolResults != true));
                Assert.Contains(followup.Messages.SelectMany(m => m.Content), b => b.Text == "E qual o motivo?");
                Assert.Equal("end", panel.Conversation.Runs.Last().Stopped);
                return true;
            }
            finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Delegation_UsesSpecialistsOwnConversationWithoutSharingSourceHistory() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "urbe-delegation-" + Guid.NewGuid().ToString("N"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = folder;
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                var panel = view.Assistant;
                panel.Open();
                panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
                panel.Config.ProviderId = "p";
                panel.Config.Model = "test-model";
                panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "read", false, ""));
                panel.Config.CustomAgents.Add(new("two", "Dois", "sparkle", "", "read", false, ""));
                panel.Config.AgentDelegationAuto["one"] = true;
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-1","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"Explique a palavra estrela\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"content":"Estrela é um astro luminoso."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"content":"O especialista respondeu."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                panel.SelectAgent("one");
                panel.Send("Pergunta do usuário com contexto reservado; procure o especialista.");
                await panel.CurrentRun!;
                var store = new AssistantStore(new FolderAssistantStorage(folder));
                var specialist = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
                Assert.Equal("Explique a palavra estrela", specialist.Messages.First(m => m.Role == "user").Content[0].Text);
                Assert.DoesNotContain("contexto reservado", string.Join("\n", specialist.Messages
                    .SelectMany(m => m.Content).Where(b => b.Type == "text").Select(b => b.Text)));
                Assert.Equal("end", panel.Conversation.Runs.Last().Stopped);
                Assert.Contains(panel.Conversation.Messages.SelectMany(m => m.Content),
                    b => b.Type == "tool_use" && b.Name == "consult_agent");
                return true;
            }
            finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task AgentWorkspaces_SelectResumeFilterAndRestartWithoutReassigningHistory() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-agent-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var store = new AssistantStore(new FolderAssistantStorage(folder));
        var cfg = new AiConfig { AgentWorkspaceVersion = 1 };
        cfg.CustomAgents.Add(new("one", "Um", "sparkle", "DO NOT DISPLAY SUMMARY", "all", false, "private instructions"));
        cfg.CustomAgents.Add(new("two", "Dois", "sparkle", "OTHER SUMMARY", "all", false, ""));
        cfg.AgentPolicies["one"] = "ask";
        cfg.AgentToolScopes["one"] = "write";
        cfg.AgentModels["one"] = new("p", "model-one");
        store.SetConfigVerified(cfg);
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            Assert.Equal("Agentes", panel.ViewTitle);
            await Settle(window, view);
            Snapshot(window, "native-agent-workspaces.png");
            Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("DO NOT DISPLAY") == true);
            var vault = panel.Conversation.Vault;
            store.SaveConversation(new AiConversation { Id = "one-old", AgentId = "one", Vault = vault, Title = "Private one", Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("only one")] }] }, 10);
            store.SaveConversation(new AiConversation { Id = "two-old", AgentId = "two", Vault = vault, Title = "Private two", Messages = [new() { Role = "user", Content = [AiChatBlock.TextBlock("only two")] }] }, 20);
            panel.SelectAgent("one");
            Assert.NotEqual("one-old", panel.Conversation.Id);
            Assert.Empty(panel.Conversation.Messages);
            ClickNamed(panel, "Conversas");
            Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Private one");
            Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Private two");
            panel.SelectAgent("two");
            Assert.NotEqual("two-old", panel.Conversation.Id);
            Assert.Empty(panel.Conversation.Messages);
            Assert.DoesNotContain(panel.Conversation.Messages.SelectMany(m => m.Content), b => b.Text == "only one");
            panel.SelectAgent("one");
            Assert.NotEqual("one-old", panel.Conversation.Id);
            Assert.Empty(panel.Conversation.Messages);
            Assert.Equal("one", store.GetConversation("one-old")!.AgentId);
            Assert.Equal("two", store.GetConversation("two-old")!.AgentId);
            Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text is "⌃" or "⌄");
            panel.Close();
            var next = new MainView();
            window.Content = next;
            await next.LoadAsync(TutorialNotes.Load());
            next.Assistant.Open();
            Assert.NotEqual("one-old", next.Assistant.Conversation.Id);
            Assert.Empty(next.Assistant.Conversation.Messages);
            Assert.Single(store.ConversationsFor("one", vault));
            Assert.Equal("ask", next.Assistant.Config.PolicyFor("one"));
            Assert.Equal("write", next.Assistant.Config.ToolScopeFor(next.Assistant.Config.CustomAgents.First(a => a.Id == "one")));
            Assert.Equal("readonly", next.Assistant.Config.PolicyFor("two"));
            Assert.Equal("model-one", next.Assistant.Config.AgentModels["one"].Model);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; Directory.Delete(folder, true); }
    }, CancellationToken.None));
}
