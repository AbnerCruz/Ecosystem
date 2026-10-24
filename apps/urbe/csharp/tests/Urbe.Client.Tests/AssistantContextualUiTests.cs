using System.Net;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>Contextual IA uses the SAME runtime/conversation; all actions work without opening the full panel.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task NoteAgent_RunShowsNonModalCompactResultWithoutOpeningChat() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync([new CityNote("Comece.md", "# Comece\n\nTexto do teste.")]);
                await Settle(window, view);
                view.OpenNoteByPath("Comece.md");
                var panel = view.Assistant;
                panel.Open();
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.ProviderId = "openrouter";
                panel.Config.Model = "org/modelo-b";
                panel.Conversation.ProviderId = "openrouter";
                panel.Conversation.Model = "org/modelo-b";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"content":"Texto analisado; não é necessário alterar a nota."}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"stop"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"Revisão concluída."}}]}"""));
                panel.Close();

                var problem = panel.AskInNote("Comece.md", "Comece",
                    new NoteMarks.AgentCall(0, 0, "", "Analise a nota, sem editar"));
                Assert.Null(problem);
                Assert.True(panel.CompactActive);
                Assert.True(view.AssistantMini.IsVisible);
                Assert.False(panel.IsVisible);
                Assert.True(view.Editor.IsVisible);
                Assert.NotNull(panel.CurrentRun);
                await panel.CurrentRun!;
                for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs();
                Assert.Equal("result", panel.CompactStatus);
                Assert.True(view.AssistantMini.IsVisible);
                Assert.False(panel.IsVisible);
                Assert.Contains(view.AssistantMini.GetLogicalDescendants().OfType<TextBlock>(),
                    t => t.Text?.Contains("Trabalho concluído", StringComparison.Ordinal) == true);
                Assert.NotEmpty(panel.Conversation.Messages);
                panel.DismissCompact();
                Assert.False(view.AssistantMini.IsVisible);
                Assert.True(view.AssistantLauncher.IsVisible);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task NoteAgent_ApprovalIsAvailableInCompactModeAndCanBeRejected() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync([new CityNote("Comece.md", "# Comece\n\n- uma")]);
                await Settle(window, view);
                view.OpenNoteByPath("Comece.md");
                var panel = view.Assistant;
                panel.Open();
                var model = new ScriptedModel();
                panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-teste"));
                panel.Config.ProviderId = "openrouter";
                panel.Config.Model = "org/modelo-b";
                panel.Conversation.ProviderId = "openrouter";
                panel.Conversation.Model = "org/modelo-b";
                model.Chats.Enqueue(Sse(
                    """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"compact_approval","function":{"name":"edit_note","arguments":"{\"path\":\"Comece\",\"old_text\":\"- uma\",\"new_text\":\"- primeira\"}"}}]}}]}""",
                    """{"choices":[{"delta":{},"finish_reason":"tool_calls"}]}"""));
                model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"A alteração foi recusada."}}]}"""));
                panel.Close();

                Assert.Null(panel.AskInNote("Comece.md", "Comece",
                    new NoteMarks.AgentCall(0, 0, "", "Substitua a primeira tarefa")));
                for (var i = 0; i < 200 && panel.PendingApproval is null; i++)
                {
                    await Task.Delay(10);
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.NotNull(panel.PendingApproval);
                Assert.False(panel.IsVisible);
                Assert.True(view.AssistantMini.IsVisible);
                Assert.Equal("approval", panel.CompactStatus);
                Assert.Contains(view.AssistantMini.GetLogicalDescendants().OfType<TextBlock>(),
                    t => t.Text?.Contains("Aprovar alteração", StringComparison.Ordinal) == true);
                // Editing on the compact surface must survive opening the full conversation.
                ClickButtonWithText(view.AssistantMini, "Editar");
                var draft = view.AssistantMini.GetLogicalDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Texto proposto");
                draft.Text = "# Uma proposta editada à mão";
                panel.Open();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.Text == "# Uma proposta editada à mão");
                panel.Close();
                panel.Answer(AiApproval.Reject());
                await panel.CurrentRun!;
                for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs();
                Assert.True(view.Editor.IsVisible);
                Assert.False(panel.IsVisible);
                Assert.Null(panel.PendingApproval);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
    [Fact]
    public async Task NoteAgent_QuestionDraftSurvivesMinimizeAndOpeningConversation() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync([new CityNote("Comece.md", "# Comece")]);
                var panel = view.Assistant;
                panel.Open();
                panel.Providers = new AssistantProviders(new ScriptedModel(), (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("test", "openrouter"));
                panel.Config.ProviderId = "test";
                panel.Config.Model = "org/modelo-b";
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "qrequest", Role = "user", Content = [AiChatBlock.TextBlock("Prepare o documento.")] });
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "question", Role = "assistant", Content = [new AiChatBlock
                    { Type = "tool_use", Id = "question-call", Name = "ask_user", Input = new System.Text.Json.Nodes.JsonObject
                    { ["question"] = "Qual é o público?", ["resolved"] = 0, ["total"] = 1 } }] });
                panel.Conversation.Messages.Add(new AiChatMessage { Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock
                    { Type = "tool_result", ToolUseId = "question-call", Content = "Pergunta exibida." }] });
                panel.Conversation.Runs.Add(new AiRunRecord { Stopped = "needs_input" });
                panel.RenderAll();
                var input = panel.GetLogicalDescendants().OfType<TextBox>().Single(t => t.PlaceholderText?.StartsWith("Responda aqui") == true);
                input.Text = "Leitores iniciantes, mas com interesse em";
                panel.Close();
                panel.Open();
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.Text == "Leitores iniciantes, mas com interesse em");
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

}
