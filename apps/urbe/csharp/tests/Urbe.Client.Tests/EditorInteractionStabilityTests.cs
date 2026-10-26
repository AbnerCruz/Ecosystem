using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>UC-57/805: keep user-owned reading position, entire-note selection and stable input semantics.</summary>
public sealed class EditorInteractionStabilityTests
{
    [Fact]
    public void ChatViewportOnlyFollowsWhenReaderIntentionallyReturnsToBottom()
    {
        Assert.True(ChatViewportPolicy.MayAutoFollow(true, false));
        Assert.False(ChatViewportPolicy.MayAutoFollow(true, true));
        Assert.False(ChatViewportPolicy.FollowingAfterGesture(true, true, false));
        Assert.False(ChatViewportPolicy.FollowingAfterGesture(false, false, true)); // programmatic extent changes cannot resume follow
        Assert.True(ChatViewportPolicy.FollowingAfterGesture(false, true, true));
    }

    [Fact]
    public void CursorCorrectionsNeverOverrideReaderNavigation()
    {
        Assert.Equal(0.94, EditorScrolling.BandBottom, 2);
        Assert.False(EditorScrolling.ShouldKeepCaret(true, true));
        Assert.False(EditorScrolling.ShouldKeepCaret(false, false));
        Assert.True(EditorScrolling.ShouldKeepCaret(true, false));
    }

    [Fact]
    public async Task VisualBlockHasIndependentSelectionAndExplicitWholeNoteSelection() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            const string markdown = "# Título\\n\\nPrimeiro bloco\\n\\nSegundo bloco\\n";
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 800, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Teste.md", markdown));
            Assert.Equal(EditorMode.Visual, editor.Mode);
            editor.SelectEntireNote();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(EditorMode.Fonte, editor.Mode);
            Assert.Equal(0, editor.Editor.SelectionStart);
            Assert.Equal(markdown.Length, editor.Editor.SelectionEnd);
            Assert.Equal(markdown, editor.Editor.SelectedText);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task EnteringAndLeavingAnUnchangedBlockDoesNotRecreateAllOtherMarkdownViews() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 800, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Tres.md", "# Título\\n\\nprimeiro\\n\\nsegundo\\n"));
            window.UpdateLayout();
            var starts = editor.Surface.BlockStarts;
            Assert.True(starts.Count >= 3);
            var first = editor.Surface.BlockView(starts[1])!;
            var stable = editor.Surface.BlockView(starts[2])!;
            editor.Surface.TapBlock(starts[1], first, first, new Avalonia.Point(10, 10), select: false);
            Assert.Same(stable, editor.Surface.BlockView(starts[2]));
            editor.Surface.FinishEdit();
            Assert.Same(stable, editor.Surface.BlockView(starts[2]));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task LargeNoteTypingKeepsWholeContentAndTracksLocalWork() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var original = "# Longa\\n\\n" + new string('a', 15000);
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 820, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Longa.md", original));
            editor.SetMode(EditorMode.Fonte);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var time = Stopwatch.StartNew();
            for (int i = 0; i < 15; i++) editor.Editor.Text = original + i;
            time.Stop();
            var allocation = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(original + "14", editor.Current?.Content);
            Console.WriteLine($"EDITOR_LOCAL_15_EDITS ticks={time.ElapsedTicks} allocated_bytes={allocation}; headless diagnostic, no device latency claim");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
