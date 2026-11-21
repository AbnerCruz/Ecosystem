using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#50: the steps open in the panel that rises from the bottom; this taps the activity line and returns its list.</summary>
    private static Panel OpenSteps(AssistantPanel panel)
    {
        var open = panel.Log.GetLogicalDescendants().OfType<Button>()
            .First(b => (Avalonia.Automation.AutomationProperties.GetName(b) ?? "").StartsWith("Atividade do agente", StringComparison.Ordinal));
        open.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.StepsSheet.IsOpen);
        return panel.StepsList!;
    }

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
            // #50: opening 300 steps froze the app (all 600 lines laid out at once); the panel draws the latest page only
            await Settle(window, view);
            var tap = Stopwatch.StartNew();
            var list = OpenSteps(panel);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            tap.Stop();
            PerfLog("activity.open300 " + tap.ElapsedMilliseconds + " ms");
            if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(tap.ElapsedMilliseconds < 300, tap.ElapsedMilliseconds + " ms");
            await Settle(window, view);
            Snapshot(window, "activity-steps-panel.png");
            var stages = list.Children.OfType<Ui.Deferred>().ToList();
            Assert.Equal(AssistantPanel.StepsPage, stages.Count);
            Assert.Contains("Ver anteriores (580)", list.Children.OfType<Button>().Select(b => b.Content as string));
            Assert.DoesNotContain(list.Children.OfType<Button>(), b => (b.Content as string ?? "").StartsWith("Ver mais recentes", StringComparison.Ordinal));
            // the latest step is the last one, in chronological order
            var lastStage = (Border)stages[^1].View;
            Assert.Contains("N299", string.Join(" ", lastStage.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));

            // earlier pages, then back: never more than StepsMax drawn, nothing lost or repeated
            for (int k = 0; k < 4; k++) { Named(list, "Ver as etapas anteriores").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(AssistantPanel.StepsMax, list.Children.OfType<Ui.Deferred>().Count());
            // 600 lines (300 steps and their 300 reasoning summaries): four pages back from the latest 20 is [500, 560)
            Assert.Contains("Ver mais recentes (40)", list.Children.OfType<Button>().Select(b => b.Content as string));
            for (int k = 0; k < 2; k++) { Named(list, "Ver as etapas mais recentes").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
            Assert.DoesNotContain(list.Children.OfType<Button>(), b => (b.Content as string ?? "").StartsWith("Ver mais recentes", StringComparison.Ordinal));
            stages = list.Children.OfType<Ui.Deferred>().ToList();
            Assert.Equal(stages.Count, stages.Distinct().Count());

            // re-rendering (each streamed token) keeps the same controls
            var clock = Stopwatch.StartNew();
            for (int k = 0; k < 20; k++) panel.RenderAll();
            clock.Stop();
            Console.WriteLine("activity.render300 " + clock.ElapsedMilliseconds / 20.0 + " ms per render");
            Assert.Same(list, panel.StepsList);
            Assert.Equal(stages, list.Children.OfType<Ui.Deferred>().ToList());

            // the panel at the latest step follows a new one
            msgs.Add(new AiChatMessage { Id = "a300", Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = "c300", Name = "read_note", Input = new JsonObject { ["path"] = "Z.md" } }] });
            panel.RenderAll();
            Assert.Contains("Z.md", string.Join(" ", ((Border)list.Children.OfType<Ui.Deferred>().Last().View).GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));

            // the X is in the panel's bar, outside the list that scrolls; it closes the panel only
            var close = Named(panel.StepsSheet.Card, "Fechar etapas");
            Assert.DoesNotContain(close, list.GetLogicalDescendants());
            close.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.False(panel.StepsSheet.IsOpen);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public async Task Activity_StepsMadeAboveTheScreenDoNotMoveWhatIsOnIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // the steps far from the screen wait as lines of about their height; a step whose title wraps is taller than
        // that, and making it above the screen pushed the steps being read down
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
            msgs.Add(new AiChatMessage { Id = "q", Role = "user", Content = [AiChatBlock.TextBlock("Leia tudo")] });
            for (int i = 0; i < 200; i++)
            {
                // a plugin's step is shown by its name: this one takes several lines
                var name = "etapa " + i + string.Concat(Enumerable.Repeat(" de um plugin com um nome que não cabe numa linha só", 4));
                msgs.Add(new AiChatMessage { Id = "a" + i, Role = "assistant",
                    Content = [new AiChatBlock { Type = "tool_use", Id = "c" + i, Name = name, Input = new JsonObject() }] });
                msgs.Add(new AiChatMessage { Id = "r" + i, Role = "user", Meta = new() { ToolResults = true },
                    Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "c" + i, Content = "texto " + i }] });
            }
            panel.RenderAll();
            var list = OpenSteps(panel);
            // the most the panel draws at once
            for (int k = 0; k < 2; k++) { Named(list, "Ver as etapas anteriores").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
            await Settle(window, view);
            var scroll = list.FindAncestorOfType<ScrollViewer>()!;
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height * 4);
            scroll.Offset = new Avalonia.Vector(0, scroll.Extent.Height / 2);
            await Settle(window, view);
            Assert.True(list.Children.OfType<Ui.Deferred>().Where(d => d.IsMade).All(d => d.Bounds.Height > 65));   // taller than the estimate
            Control Visible() => list.Children.First(c => c.TranslatePoint(default, scroll) is { } p && p.Y >= 0 && p.Y < scroll.Viewport.Height);
            for (int k = 0; k < 6; k++)
            {
                var reading = Visible();
                var y = reading.TranslatePoint(default, scroll)!.Value.Y;
                scroll.Offset = scroll.Offset.WithY(scroll.Offset.Y - 200);
                await Settle(window, view);
                Assert.Equal(y + 200, reading.TranslatePoint(default, scroll)!.Value.Y, 1.0);
            }
            // every step is still in the list, in order, and the top can be reached
            scroll.Offset = default;
            await Settle(window, view);
            Assert.True(list.Children.OfType<Ui.Deferred>().First().IsMade);
            Assert.Equal(AssistantPanel.StepsMax, list.Children.OfType<Ui.Deferred>().Count());
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));

    [Fact]
    public void Activity_ALongTextIsSplitIntoWholeLinesAndLosesNothing()
    {
        var lines = string.Join("\n", Enumerable.Range(0, 1000).Select(i => "linha " + i + " " + new string('a', i % 90)));
        var pieces = Ui.CodeBlockView.Split(lines).ToList();
        Assert.Equal(lines, string.Join("\n", pieces));
        Assert.All(pieces, p => Assert.True(p.Length <= Ui.CodeBlockView.PieceChars && p.Split('\n').Length <= Ui.CodeBlockView.PieceLines));
        Assert.True(pieces.Count > 1000 / Ui.CodeBlockView.PieceLines);
        // a single enormous line (minified JSON) goes on in the next piece, never cutting a character in half
        var line = string.Concat(Enumerable.Repeat("{\"k\":\"😀\"},", 4000));
        var cut = Ui.CodeBlockView.Split(line).ToList();
        Assert.Equal(line, string.Concat(cut));
        Assert.All(cut, p => Assert.False(char.IsHighSurrogate(p[^1])));
        // blank lines at the end of a piece are kept
        var blanks = string.Join("\n", Enumerable.Range(0, 400).Select(i => i % 3 == 0 ? "" : "texto " + i));
        Assert.Equal(blanks, string.Join("\n", Ui.CodeBlockView.Split(blanks)));
    }

    [Fact]
    public async Task Activity_AStepWithAHugeResultOpensAtOnceAndShowsAllOfIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // the results were shown in pages of 12 000 characters ("Página 1 de 17", Anterior/Próxima), and even one page
        // took 300 ms to open; now the whole result is there and only the pieces near the screen are made
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
            var result = string.Join("\n", Enumerable.Range(0, 5000).Select(i => "linha " + i + " do resultado de uma ferramenta"));
            Assert.True(result.Length > 200_000);
            msgs.Add(new AiChatMessage { Id = "q", Role = "user", Content = [AiChatBlock.TextBlock("Leia")] });
            msgs.Add(new AiChatMessage { Id = "a", Role = "assistant",
                Content = [new AiChatBlock { Type = "tool_use", Id = "c", Name = "read_note", Input = new JsonObject { ["path"] = "N.md" } }] });
            msgs.Add(new AiChatMessage { Id = "r", Role = "user", Meta = new() { ToolResults = true },
                Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "c", Content = result }] });
            panel.RenderAll();
            var steps = OpenSteps(panel);
            await Settle(window, view);
            var stage = steps.Children.Select(c => c is Ui.Deferred d ? d.View : c).OfType<Border>().First().Child as Expander;
            var tap = Stopwatch.StartNew();
            stage!.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            tap.Stop();
            PerfLog("activity.openHugeResult " + tap.ElapsedMilliseconds + " ms");
            if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(tap.ElapsedMilliseconds < 150, tap.ElapsedMilliseconds + " ms");
            await Settle(window, view);
            var code = stage.GetLogicalDescendants().OfType<Ui.CodeBlockView>().Last();
            Assert.Equal(result, code.SourceText);   // Copiar copies all of it
            Assert.DoesNotContain(stage.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Página ", StringComparison.Ordinal) == true);
            var pieces = code.Pieces!.Children.Cast<Ui.Deferred>().ToList();
            Assert.Equal(Ui.CodeBlockView.Split(result).Count(), pieces.Count);
            Assert.True(pieces.Count > 60);
            Assert.True(pieces[0].IsMade);
            Assert.InRange(pieces.Count(p => p.IsMade), 1, 6);
            // the reader reaches the end of the result: the last lines are made there
            var scroll = code.Pieces.GetVisualAncestors().OfType<ScrollViewer>().First(s => s.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
            var last = pieces[^1];
            var bottom = last.TranslatePoint(default, scroll)!.Value.Y + scroll.Offset.Y;
            scroll.Offset = new Vector(0, bottom - 100);
            await Settle(window, view);
            Assert.True(last.IsMade);
            Assert.EndsWith("linha 4999 do resultado de uma ferramenta", ((SelectableTextBlock)last.Child!).Text);
            // the joint between two pieces, seen: no gap, no overlap
            var alone = new Ui.CodeBlockView(result, "Texto", 11.5);
            var seen = new Window { Width = 412, Height = 500, Content = new ScrollViewer { Content = alone } };
            seen.Show();
            await Settle(seen, view);
            var outer = (ScrollViewer)seen.Content!;
            outer.Offset = new Vector(0, alone.Pieces!.Children[1].TranslatePoint(default, outer)!.Value.Y - 200);
            await Settle(seen, view);
            Snapshot(seen, "activity-huge-result-joint");
            seen.Close();
            // wrapping applies to every piece, made or still to be made
            code.WrapButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.All(pieces.Where(p => p.IsMade), p => Assert.Equal(Avalonia.Media.TextWrapping.Wrap, ((SelectableTextBlock)p.Child!).TextWrapping));
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, ((SelectableTextBlock)pieces[30].View).TextWrapping);
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
