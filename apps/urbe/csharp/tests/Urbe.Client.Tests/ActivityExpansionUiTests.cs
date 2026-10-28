using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Activity_ExpandedLongRunIsNotRebuiltOnEveryRender() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-112 ("expandir muitos passos e ferramentas trava"): the current request's timeline was rebuilt, every
        // stage and every expanded result, on each render (each streamed token); now unchanged stages are kept
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
            msgs.Add(new AiChatMessage { Id = "q", Role = "user", Content = [AiChatBlock.TextBlock("Faça uma pesquisa longa")] });
            var big = new string('x', 20_000);
            for (int i = 0; i < 300; i++)
            {
                msgs.Add(new AiChatMessage { Id = "a" + i, Role = "assistant", Meta = new() { Reasoning = "pensando " + i },
                    Content = [new AiChatBlock { Type = "tool_use", Id = "c" + i, Name = "read_note", Input = new JsonObject { ["path"] = "N" + i + ".md" } }] });
                msgs.Add(new AiChatMessage { Id = "r" + i, Role = "user", Meta = new() { ToolResults = true },
                    Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "c" + i, Content = big }] });
            }
            panel.RenderAll();
            var fold = panel.Log.GetLogicalDescendants().OfType<Expander>().First();
            fold.IsExpanded = true;
            await Settle(window, view);
            var list = (Panel)fold.Content!;
            var stages = list.Children.ToList();
            Assert.True(stages.Count >= 300);
            var firstStage = list.Children.OfType<Border>().First().Child as Expander;
            firstStage!.IsExpanded = true;
            await Settle(window, view);
            var details = firstStage.Content;

            var clock = Stopwatch.StartNew();
            for (int k = 0; k < 20; k++) panel.RenderAll();
            clock.Stop();
            Console.WriteLine("activity.render300 " + clock.ElapsedMilliseconds / 20.0 + " ms per render");

            var foldAgain = panel.Log.GetLogicalDescendants().OfType<Expander>().First();
            Assert.Same(fold, foldAgain);
            Assert.True(foldAgain.IsExpanded);
            var listAgain = (Panel)foldAgain.Content!;
            Assert.Same(list, listAgain);
            Assert.Equal(stages, listAgain.Children.ToList());   // the same 300+ controls, not new ones
            Assert.Same(details, ((Expander)((Border)listAgain.Children.OfType<Border>().First()).Child!).Content);

            // a new step is added without rebuilding the others
            msgs.Add(new AiChatMessage { Id = "a300", Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = "c300", Name = "read_note", Input = new JsonObject { ["path"] = "Z.md" } }] });
            panel.RenderAll();
            Assert.Same(stages[0], listAgain.Children[0]);
            Assert.Equal(stages.Count + 1, listAgain.Children.Count);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
