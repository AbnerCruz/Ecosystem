using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// #1000 (owner, P0): the more steps, the heavier each render of the chat while the agent works. This measures a
    /// render (what each streamed token costs) with 1000 steps, with the panel of steps closed and open.
    /// </summary>
    [Fact]
    public async Task Steps_ARenderWithAThousandStepsIsCheap() => Assert.True(await Session.Dispatch<bool>(async () =>
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
            msgs.Add(new AiChatMessage { Id = "q", Role = "user", Content = [AiChatBlock.TextBlock("Faça muita coisa")] });
            for (int i = 0; i < 1000; i++)
            {
                msgs.Add(new AiChatMessage { Id = "a" + i, Role = "assistant", Meta = new() { Reasoning = "pensando no passo " + i },
                    Content = [new AiChatBlock { Type = "tool_use", Id = "c" + i, Name = "read_note", Input = new JsonObject { ["path"] = "N" + i + ".md" } }] });
                msgs.Add(new AiChatMessage { Id = "r" + i, Role = "user", Meta = new() { ToolResults = true },
                    Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "c" + i, Content = "conteúdo " + i }] });
            }
            panel.RenderAll();
            await Settle(window, view);
            // the model writes its next words: a streamed message grows token by token
            var streaming = new AiChatMessage { Id = "s", Role = "assistant", Meta = new() { Streaming = true, Reasoning = "" }, Content = [AiChatBlock.TextBlock("")] };
            msgs.Add(streaming);
            panel.RenderAll();
            int token = 0;
            double Measure(bool render = true)
            {
                void Token()
                {
                    streaming.Meta!.Reasoning += " palavra" + token++;
                    if (render) panel.RenderChanged(streaming);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                }
                for (int k = 0; k < 3; k++) Token();
                var clock = Stopwatch.StartNew();
                for (int k = 0; k < 30; k++) Token();
                return clock.Elapsed.TotalMilliseconds / 30;
            }
            var idle = Measure(render: false);
            var closed = Measure();
            OpenSteps(panel);
            await Settle(window, view);
            var open = Measure();
            // what makes it cheap, independent of the clock: a token swaps only the step it changed, in its place;
            // the other steps and the folded line are the same controls (before, the list was emptied and refilled)
            var list = panel.StepsList!;
            var kept = list.Children.ToList();
            var head = panel.Log.GetLogicalDescendants().OfType<Button>()
                .First(b => (Avalonia.Automation.AutomationProperties.GetName(b) ?? "").StartsWith("Atividade do agente", StringComparison.Ordinal)).Content as Panel;
            var headKids = head!.Children.ToList();
            int detached = 0;
            foreach (var c in kept.Take(kept.Count - 1)) c.DetachedFromLogicalTree += (_, _) => detached++;
            streaming.Meta!.Reasoning += " mais";
            panel.RenderChanged(streaming);
            var now = list.Children.ToList();
            Assert.Equal(kept.Count, now.Count);
            Assert.Equal(kept.Count - 1, kept.Zip(now).Count(p => ReferenceEquals(p.First, p.Second)));
            Assert.False(ReferenceEquals(kept[^1], now[^1]));   // the step being written is the one renewed
            Assert.Equal(0, detached);                           // and no other step left the list and came back
            Assert.Equal(headKids, head.Children.ToList());
            await Settle(window, view);
            Snapshot(window, "steps-1000-open.png");
            PerfLog($"BENCH steps1000 render closed {closed:0.00} ms · open {open:0.00} ms (a frame with nothing changed: {idle:0.00} ms)");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
