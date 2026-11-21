using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#1005: a deck the Assistente made shows in the conversation as a study object with "Abrir", not only as a step.</summary>
    [Fact]
    public async Task Flashcards_ADeckMadeInTheChatShowsAsAStudyObject() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            var panel = view.Assistant;
            panel.Open();
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter"));
            var msgs = panel.Conversation.Messages;
            msgs.Add(new AiChatMessage { Id = "q", Role = "user", Content = [AiChatBlock.TextBlock("faça dois flashcards utilizando a ferramenta nativa, com o tema animais")] });
            msgs.Add(new AiChatMessage { Id = "a1", Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = "c1", Name = "create_flashcards",
                Input = new JsonObject { ["title"] = "Animais", ["cards"] = new JsonArray() } }] });
            msgs.Add(new AiChatMessage { Id = "r1", Role = "user", Meta = new() { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "c1",
                Content = "Conjunto \"Animais\" (deck_id animais) salvo na biblioteca de Estudo com 2 cartões; verificado ao reler." }] });
            msgs.Add(new AiChatMessage { Id = "a2", Role = "assistant", Content = [AiChatBlock.TextBlock("Pronto: dois cartões no conjunto Animais.")] });
            panel.RenderAll();
            await Settle(window, view);
            var card = panel.Log.GetLogicalDescendants().OfType<Border>().SingleOrDefault(b => b.Tag as string == "flashcards:animais");
            Assert.NotNull(card);
            Assert.Contains("2 cartões", string.Join(" ", card!.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
