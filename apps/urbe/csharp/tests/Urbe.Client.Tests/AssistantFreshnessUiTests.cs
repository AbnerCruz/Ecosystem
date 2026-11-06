using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>CST-145: a long conversation (or a new subject after a pause) offers a new chat with a summary, never by itself.</summary>
public sealed partial class HeadlessUiTests
{
    private static void FreshnessConfig(AssistantPanel panel)
    {
        panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", "OpenRouter", "sk-or-test"));
        panel.Config.ProviderId = "p";
        panel.Config.Model = "acme/small";
        panel.Config.ModelCache["p"] = new AiModelCache(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            [new AiModelInfo("acme/small", "Pequeno", 32000, (.5, 1), true, ["text"], ["text"])]);
    }

    private static void Turns(AiConversation conv, int turns, string question, string answer)
    {
        for (int i = 0; i < turns; i++)
        {
            conv.Messages.Add(new AiChatMessage { Id = "u" + conv.Messages.Count, Role = "user", Content = [AiChatBlock.TextBlock(question)] });
            conv.Messages.Add(new AiChatMessage { Id = "a" + conv.Messages.Count, Role = "assistant", Content = [AiChatBlock.TextBlock(answer)] });
        }
    }

    private static Button? HintButton(AssistantPanel panel, string name) => panel.GetLogicalDescendants().OfType<Button>()
        .FirstOrDefault(b => b.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(b) == name);

    [Fact]
    public async Task Assistant_ALongConversationOffersANewChatWithASummary() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var panel = view.Assistant;
            panel.Open();
            panel.HandleBack();
            FreshnessConfig(panel);
            var old = panel.Conversation;
            old.Title = "Orçamento da viagem";
            Turns(old, 3, "Como organizo o orçamento da viagem?", "Separe em transporte e hospedagem. " + new string('x', 5000));
            panel.RenderAll();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(panel.ConversationHint);                     // still the right place

            Turns(old, 1, "E os passeios?", "Reserve uma parte para passeios. " + new string('y', 14000));
            panel.RenderAll();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ConversationHintReason.Long, panel.ConversationHint?.Reason);
            Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == ConversationFreshness.LongText);
            Assert.Same(old, panel.Conversation);                    // never switched by itself
            Snapshot(window, "assistant-long-conversation-hint");

            panel.Input.Text = "e o seguro?";
            HintButton(panel, "Começar uma nova conversa com um resumo desta")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var fresh = panel.Conversation;
            Assert.NotSame(old, fresh);
            Assert.Empty(fresh.Messages);                            // nothing was sent
            Assert.Equal(old.AgentId, fresh.AgentId);
            var seed = panel.Input.Text ?? "";
            Assert.StartsWith("Continuando a conversa “Orçamento da viagem”", seed);
            Assert.Contains("(id: " + old.Id + ";", seed);
            Assert.Contains("- Começou com: Como organizo o orçamento da viagem?", seed);
            Assert.EndsWith("\n\ne o seguro?", seed);
            Assert.Equal(8, old.Messages.Count);                     // the old conversation is kept as it was
            Assert.Null(panel.ConversationHint);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task Assistant_ANewSubjectAfterAPauseHintsAndTheHintCanBeDismissed() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var panel = view.Assistant;
            panel.Open();
            panel.HandleBack();
            FreshnessConfig(panel);
            var conv = panel.Conversation;
            conv.Title = "Orçamento da viagem";
            Turns(conv, 4, "Como organizo o orçamento da viagem a Lisboa?", "Separe o orçamento da viagem em transporte e hospedagem.");
            conv.Updated = DateTimeOffset.UtcNow.AddHours(-9).ToUnixTimeMilliseconds();
            panel.RenderAll();
            Dispatcher.UIThread.RunJobs();
            panel.Input.Text = "E o orçamento dos passeios da viagem?";
            Dispatcher.UIThread.RunJobs();
            Assert.Null(panel.ConversationHint);                     // the same subject: go on here

            panel.Input.Text = "Receita de bolo de cenoura com cobertura de chocolate";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ConversationHintReason.NewTopicAfterPause, panel.ConversationHint?.Reason);

            HintButton(panel, "Continuar nesta conversa")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(panel.ConversationHint);
            panel.Input.Text = "Receita de pão de queijo mineiro tradicional";
            Dispatcher.UIThread.RunJobs();
            Assert.Null(panel.ConversationHint);                     // dismissed for this conversation
            Assert.Same(conv, panel.Conversation);
            Assert.Equal("Receita de pão de queijo mineiro tradicional", panel.Input.Text);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
