using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_TheQuestionCardShowsWhatIsStillOpenAndWhatChanged() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-115: the doubts cleared, the understanding the agent declares, what is open and what changed
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync([new CityNote("Comece.md", "# Comece")]);
                var panel = view.Assistant;
                panel.Open();
                panel.SelectAgent("geral");
                panel.Providers = new AssistantProviders(new ScriptedModel(), (_, _) => Task.CompletedTask);
                panel.Config.Providers.Add(new AiProviderConfig("test", "openrouter"));
                panel.Config.ProviderId = "test";
                panel.Config.Model = "org/modelo-b";
                void Ask(string id, JsonObject input)
                {
                    panel.Conversation.Messages.Add(new AiChatMessage { Id = id, Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = id + "-call", Name = "ask_user", Input = input }] });
                    panel.Conversation.Messages.Add(new AiChatMessage { Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = id + "-call", Content = "Pergunta exibida." }] });
                }
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "r", Role = "user", Content = [AiChatBlock.TextBlock("Prepare o documento.")] });
                Ask("q1", new JsonObject { ["question"] = "Qual é o público?", ["resolved"] = 0, ["total"] = 3, ["understanding"] = 30, ["pending"] = new JsonArray("público", "prazo", "tom") });
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "a1", Role = "user", Content = [AiChatBlock.TextBlock("Iniciantes; sem pressa.")] });
                Ask("q2", new JsonObject { ["question"] = "Prefere tom formal?", ["resolved"] = 2, ["total"] = 4, ["understanding"] = 65, ["pending"] = new JsonArray("tom", "formato") });
                panel.Conversation.Runs.Add(new AiRunRecord { Stopped = "needs_input" });
                panel.RenderAll();
                await Settle(window, view);
                var texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
                Assert.Contains("2 de 4 dúvidas esclarecidas · compreensão declarada 65% (estimativa)", texts);
                Assert.Contains("Desde a pergunta anterior: 2 esclarecidas · 1 nova (formato)", texts);
                Assert.Contains("Ainda em aberto: tom · formato", texts);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
