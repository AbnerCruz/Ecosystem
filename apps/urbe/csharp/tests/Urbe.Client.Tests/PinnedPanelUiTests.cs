using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-023: a long pinned passage scrolls on its own, never moving the note; with the keyboard open (a shorter
    /// screen) both stay on screen; it is unpinned with a full-size touch target.
    /// </summary>
    [Fact]
    public async Task Pinned_ScrollsOnItsOwnFitsTheKeyboardAndUnpinsByTouch() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var passage = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => "Trecho fixado " + i + ", longo o bastante para quebrar a linha num celular."));
            var (editor, window) = StandaloneEditor("# Nota\n\n" + passage + "\n\n" + LongNote());
            try
            {
                editor.SetMode(EditorMode.Fonte);
                await Pump(window);
                var source = editor.Editor;
                (source.SelectionStart, source.SelectionEnd) = (source.Text!.IndexOf("Trecho fixado 1,", StringComparison.Ordinal), source.Text.IndexOf("\n\n# Longa", StringComparison.Ordinal));
                editor.PinSelectionOrNote();
                await Pump(window);
                Assert.True(editor.Pinned.IsVisible);
                var scrolls = editor.Pinned.GetVisualDescendants().OfType<ScrollViewer>().ToList();
                Assert.True(scrolls.Any(s => s.Extent.Height > s.Viewport.Height), "pinned " + editor.Pinned.Bounds + " text=" + editor.Pinned.Text?.Length + " " + string.Join(" | ", scrolls.Select(s => s.Extent + "/" + s.Viewport)));
                var pinnedScroll = scrolls.First(s => s.Extent.Height > s.Viewport.Height);
                var noteScroll = editor.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible && !s.GetVisualAncestors().Contains(editor.Pinned)).MaxBy(s => s.Viewport.Height)!;
                var noteBefore = noteScroll.Offset;
                pinnedScroll.Offset = new Vector(0, 300);
                await Pump(window);
                Assert.Equal(300, pinnedScroll.Offset.Y, 1);
                Assert.Equal(noteBefore, noteScroll.Offset);

                // the keyboard takes half of the screen: the panel shrinks with it and both are still there
                Snapshot(window, "pinned-phone.png");
                var tall = editor.Pinned.Bounds.Height;
                Assert.True(noteScroll.Viewport.Height > 40, $"antes do teclado: viewport={noteScroll.Viewport} source={source.Bounds} pinned={editor.Pinned.Bounds}");
                window.Height = 430;
                await Pump(window);
                Assert.InRange(editor.Pinned.Bounds.Height, 40, tall - 1);
                Assert.True(noteScroll.Viewport.Height > 40, $"a nota sumiu: viewport={noteScroll.Viewport} source={source.Bounds} pinned={editor.Pinned.Bounds} tall={tall} visible={source.IsEffectivelyVisible} mode={editor.Mode}");
                var unpin = editor.Pinned.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Desafixar");
                Assert.True(unpin.Bounds.Width >= UrbeTokens.MinTouch && unpin.Bounds.Height >= UrbeTokens.MinTouch, unpin.Bounds.ToString());
                var top = unpin.TranslatePoint(default, window)!.Value.Y;
                Assert.InRange(top, 0, window.Bounds.Height - unpin.Bounds.Height);
                unpin.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump(window);
                Assert.False(editor.Pinned.IsVisible);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
