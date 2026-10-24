using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.Ui;
using Urbe.Core.Assistant;
using System.Text.Json.Nodes;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task CodeBlocks_CopyCurrentExactSourceAndPreserveWhitespace() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var code = new CodeBlockView("  original\n\tvalue\n", "qualquer-linguagem");
        var window = new Window { Width = 412, Height = 600, Content = code };
        window.Show();
        try
        {
            const string edited = "  ação = \"á\";\n\t// 😀\n\n";
            code.TextView.Text = edited;
            code.CopyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 30 && code.CopyButton.Content as string != "Copiado"; i++)
            { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(edited, await window.Clipboard!.TryGetTextAsync());
            Assert.Equal("Copiado", code.CopyButton.Content);
            code.WrapButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, code.TextView.TextWrapping);
            Assert.Equal(edited, code.SourceText);
            var plain = new MarkdownReader().BuildMarkdown("```\n  sem linguagem\n\tlinha\n```");
            Assert.Single(plain.GetLogicalDescendants().OfType<CodeBlockView>());
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public void ModelPrice_UnknownIsNeverFreeAndSmallPricesAreNotRoundedToZero()
    {
        Assert.Contains("indisponível", AssistantPanel.ModelPrice(null));
        Assert.Contains("0.000001", AssistantPanel.ModelPrice(new AiModelInfo("x", "x", null, (.000001, .000002), true)));
        Assert.Contains("entrada", AssistantPanel.ModelPrice(new AiModelInfo("x", "x", null, (1, 2), true)));
    }

    [Fact]
    public async Task AssistantExperience_SelectionSurvivesRefreshAndHistoryIsFlatLazy() => Assert.True(await Session.Dispatch<bool>(async () =>
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
            panel.Config.Providers.Add(new AiProviderConfig("test", "openrouter"));
            panel.Config.ProviderId = "test";
            panel.Config.Model = "test/model";
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "request", Role = "user", Content = [AiChatBlock.TextBlock("Analise meu projeto e mostre o resultado.")] });
            for (int i = 0; i < 77; i++)
            {
                var id = "read-" + i;
                panel.Conversation.Messages.Add(new AiChatMessage { Id = id, Role = "assistant", Content =
                    [new AiChatBlock { Type = "tool_use", Id = id, Name = "read_note", Input = new JsonObject { ["path"] = "Uma pasta bastante longa/Uma nota com um nome muito comprido para o telefone.md" } }] });
                panel.Conversation.Messages.Add(new AiChatMessage { Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content =
                    [new AiChatBlock { Type = "tool_result", ToolUseId = id, Content = "Conteúdo preservado.\n" + new string('x', 4000) }] });
            }
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "answer", Role = "assistant", Content =
                [AiChatBlock.TextBlock("**Resultado:** sua nota está disponível. [S1]\n\n```csharp\n  var exemplo = 42;\n```\n\nTexto selecionável, com acentos e 😀.")] });
            panel.Conversation.WebSources.Add(new AiWebSource("S1", "Documentação", "https://example.org/a", "https://example.org/a", "example.org", null, "2026-10-24", "página acessada"));
            panel.RenderAll();
            await Settle(window, view);
            var activity = panel.Log.GetLogicalDescendants().OfType<Expander>().First();
            Assert.Null(activity.Content);
            Snapshot(window, "native-assistant-experience-phone.png");
            activity.IsExpanded = true;
            await Settle(window, view);
            var stage = ((Panel)activity.Content!).Children.OfType<Border>().First().Child as Expander;
            Assert.NotNull(stage);
            Assert.Null(stage.Content);
            stage.IsExpanded = true;
            await Settle(window, view);
            Assert.Contains("Conteúdo preservado.", stage.GetLogicalDescendants().OfType<CodeBlockView>().Last().SourceText);
            Snapshot(window, "native-assistant-experience-activity.png");
            // Select from the visible response; refreshing must leave that exact control mounted.
            activity.IsExpanded = false;
            await Settle(window, view);
            var selection = panel.Log.GetVisualDescendants().OfType<SelectableTextBlock>().First(t => t.Text?.StartsWith("Analise") == true);
            selection.SelectionStart = 0;
            selection.SelectionEnd = 7;
            var selectedView = panel.Log.Children[0];
            panel.RenderAll();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(selectedView, panel.Log.Children[0]);
            Assert.Equal("Analise", selection.SelectedText);
            selection.SelectionEnd = 0;
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(panel.Input.ContextMenu);
            window.Width = 1280;
            await Settle(window, view);
            Snapshot(window, "native-assistant-experience-desktop.png");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
