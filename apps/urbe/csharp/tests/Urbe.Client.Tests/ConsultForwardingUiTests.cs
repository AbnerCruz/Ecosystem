using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-091 (FeaturesCheck: "Encaminhamento de perguntas e aprovações entre agentes"): when a consulted specialist asks
/// the person something or needs an approval, it shows in the conversation that asked for the consultation, and the
/// answer goes back to the specialist, without switching to its conversation by hand.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static async Task<(MainView View, Window Window, AssistantPanel Panel, ScriptedModel Model, string Folder, string? Before)> TwoAgentsAsync(string specialistScope, string vault)
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-forward-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = folder;
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        var panel = view.Assistant;
        panel.Open();
        panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
        panel.Config.ProviderId = "p";
        panel.Config.Model = "test-model";
        panel.Config.CustomAgents.Add(new("one", "Um", "sparkle", "", "read", false, ""));
        panel.Config.CustomAgents.Add(new("two", "Revisora", "sparkle", "", specialistScope, false, ""));
        panel.Config.AgentDelegationAuto["one"] = true;
        var model = new ScriptedModel();
        panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
        panel.SelectAgent("one");
        return (view, window, panel, model, folder, before);
    }

    private static async Task<T> WaitFor<T>(Window window, MainView view, Func<T?> found) where T : class
    {
        for (int i = 0; i < 200; i++)
        {
            if (found() is { } hit) return hit;
            await Settle(window, view);
            await Task.Delay(20);
        }
        throw new TimeoutException("nada apareceu");
    }

    [Fact]
    public async Task Consult_TheSpecialistsQuestionIsAnsweredFromTheAskingConversation() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-forward-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("read", vault);
        try
        {
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-1","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"Revise o título\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"ask-1","function":{"name":"ask_user","arguments":"{\"question\":\"O título deve ser formal ou casual?\",\"resolved\":0,\"total\":1}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Sugiro um título formal: Relatório anual."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"A revisora sugeriu: Relatório anual."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Peça à revisora um título.");
            var run = panel.CurrentRun!;
            var card = await WaitFor(window, view, () => panel.Log.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pergunta de Revisora"));
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "O título deve ser formal ou casual?");
            Assert.False(run.IsCompleted);   // the asking agent waits for the answer
            card.BringIntoView();
            await Settle(window, view);
            Snapshot(window, "native-consult-question.png");
            card.GetLogicalDescendants().OfType<TextBox>().Single().Text = "Formal, por favor.";
            ClickButtonWithText(card, "Responder e continuar");
            await run;
            await Settle(window, view);
            Assert.Equal("end", panel.Conversation.Runs[^1].Stopped);
            var store = new AssistantStore(new FolderAssistantStorage(folder));
            var specialist = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
            Assert.Contains(specialist.Messages, m => m.Role == "user" && m.Content.Any(b => b.Text == "Formal, por favor."));
            var result = panel.Conversation.Messages.SelectMany(m => m.Content).First(b => b.Type == "tool_result" && b.ToolUseId == "consult-1");
            Assert.False(result.IsError);
            Assert.Contains("Relatório anual", result.Content);
            Assert.DoesNotContain(panel.Log.GetLogicalDescendants().OfType<Border>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pergunta de Revisora");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Consult_TheSpecialistsApprovalIsGivenFromTheAskingConversation() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-forward-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nTexto.\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("write", vault);
        panel.Config.AgentPolicies["two"] = "ask";
        try
        {
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-1","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"Acrescente uma linha\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"w-1","function":{"name":"append_to_note","arguments":"{\"path\":\"Comece.md\",\"content\":\"Linha da revisora.\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Linha acrescentada."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Feito pela revisora."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Peça à revisora para acrescentar uma linha.");
            var run = panel.CurrentRun!;
            // a read-only agent consulting one that writes always asks first
            await WaitFor(window, view, () => view.Dialogs.IsOpen ? view.Dialogs : null);
            ClickButtonWithText(view.Dialogs, "Iniciar consulta");
            var card = await WaitFor(window, view, () => panel.Log.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pedido de Revisora"));
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("Comece.md"));
            card.BringIntoView();
            await Settle(window, view);
            Snapshot(window, "native-consult-approval.png");
            ClickButtonWithText(card, "Aprovar");
            await run;
            await Settle(window, view);
            Assert.Contains("Linha da revisora.", await ReadWhenWritten(Path.Combine(vault, "Comece.md"), t => t.Contains("revisora")));
            Assert.Equal("end", panel.Conversation.Runs[^1].Stopped);
            Assert.Null(panel.PendingApproval);
            Assert.DoesNotContain(panel.Log.GetLogicalDescendants().OfType<Border>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pedido de Revisora");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Consult_TheSpecialistsFormIsFilledFromTheAskingConversation() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-forward-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("read", vault);
        try
        {
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-1","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"Monte a ficha\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"form-1","function":{"name":"request_form","arguments":"{\"title\":\"Ficha\",\"fields\":[{\"id\":\"nome\",\"label\":\"Nome\",\"type\":\"text\",\"required\":true}]}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Ficha de Arven pronta."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"A revisora montou a ficha."}}]}""", """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
            panel.Send("Peça à revisora a ficha.");
            var run = panel.CurrentRun!;
            var card = await WaitFor(window, view, () => panel.Log.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Formulário de Revisora"));
            var form = card.GetLogicalDescendants().OfType<Urbe.Client.Ui.FormRenderer>().Single();
            form.GetLogicalDescendants().OfType<TextBox>().Single().Text = "Arven";
            form.SubmitButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await run;
            await Settle(window, view);
            Assert.Equal("end", panel.Conversation.Runs[^1].Stopped);
            var result = panel.Conversation.Messages.SelectMany(m => m.Content).First(b => b.Type == "tool_result" && b.ToolUseId == "consult-1");
            Assert.False(result.IsError);
            Assert.Contains("Ficha de Arven pronta.", result.Content);
            var store = new AssistantStore(new FolderAssistantStorage(folder));
            var specialist = Assert.Single(store.ConversationsFor("two", panel.Conversation.Vault));
            Assert.Equal("submitted", specialist.Forms.Single().State);
            Assert.Contains(specialist.Messages, m => m.Role == "user" && m.Content.Any(b => (b.Text ?? "").Contains("Arven")));
            Assert.DoesNotContain(panel.Log.GetLogicalDescendants().OfType<Border>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Formulário de Revisora");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Consult_StoppingWhileTheSpecialistAsksEndsBothAndRemovesTheCard() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var vault = Path.Combine(Path.GetTempPath(), "urbe-forward-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var (view, window, panel, model, folder, before) = await TwoAgentsAsync("read", vault);
        try
        {
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"consult-1","function":{"name":"consult_agent","arguments":"{\"agent_id\":\"two\",\"task\":\"Revise\"}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"ask-1","function":{"name":"ask_user","arguments":"{\"question\":\"Formal?\",\"resolved\":0,\"total\":1}"}}]}}]}""",
                """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
            panel.Send("Peça à revisora.");
            var run = panel.CurrentRun!;
            await WaitFor(window, view, () => panel.Log.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pergunta de Revisora"));
            panel.StopCompact();
            Assert.Same(run, await Task.WhenAny(run, Task.Delay(3000)));
            for (int i = 0; i < 3; i++) await Settle(window, view);
            Assert.Equal("aborted", panel.Conversation.Runs[^1].Stopped);
            Assert.False(panel.SpecialistWaiting);
            Assert.DoesNotContain(panel.Log.GetLogicalDescendants().OfType<Border>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Pergunta de Revisora");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); Directory.Delete(vault, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
