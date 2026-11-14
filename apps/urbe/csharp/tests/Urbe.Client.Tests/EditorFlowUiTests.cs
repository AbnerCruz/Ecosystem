using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>The Visual editor's flow while writing: taps, links, moving between blocks (editor review, 14/11/2026).</summary>
public sealed class EditorFlowUiTests
{
    internal static (EditorOverlay Editor, Window Window) Open(string text, double width = 390, double height = 800)
    {
        var editor = new EditorOverlay();
        var window = new Window { Width = width, Height = height, Content = editor };
        window.Show();
        editor.Open(new CityNote("Fluxo.md", text));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (editor, window);
    }

    internal static TextBox Edit(EditorOverlay editor, int blockIndex, Point? at = null)
    {
        var start = editor.Surface.BlockStarts[blockIndex];
        var holder = editor.Surface.BlockView(start)!;
        var p = at ?? new Point(8, 8);
        editor.Surface.TapBlock(start, holder, holder.InputHitTest(p) as Visual, p, select: false);
        Dispatcher.UIThread.RunJobs();
        return editor.Surface.EditBox!;
    }

    /// <summary>The window point of the <paramref name="index"/>-th character of the drawn text that contains <paramref name="word"/>.</summary>
    internal static Point PointOf(Window window, Control holder, string word, int offset = 1)
    {
        foreach (var tb in holder.GetVisualDescendants().OfType<TextBlock>())
        {
            var text = VisualSurfaceText(tb);
            var i = text.IndexOf(word, StringComparison.Ordinal);
            if (i < 0) continue;
            var r = tb.TextLayout.HitTestTextPosition(i + offset);
            return tb.TranslatePoint(new Point(r.X + 1, r.Y + r.Height / 2), window)!.Value;
        }
        throw new InvalidOperationException(word);
    }

    private static string VisualSurfaceText(TextBlock tb)
    {
        if (tb.Inlines is not { Count: > 0 } inlines) return tb.Text ?? "";
        var sb = new System.Text.StringBuilder();
        void Add(Avalonia.Controls.Documents.Inline i)
        {
            switch (i)
            {
                case Avalonia.Controls.Documents.Run r: sb.Append(r.Text); break;
                case Avalonia.Controls.Documents.LineBreak: sb.Append('\n'); break;
                case Avalonia.Controls.Documents.InlineUIContainer: sb.Append('￼'); break;
                case Avalonia.Controls.Documents.Span span: foreach (var c in span.Inlines) Add(c); break;
            }
        }
        foreach (var i in inlines) Add(i);
        return sb.ToString();
    }

    internal static void Click(Window window, Point p)
    {
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task ALinkTappedWhileWritingPlacesTheCaretAndOffersToOpenIt() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Primeiro parágrafo.\n\nVeja [[Outra nota]] depois.\n");
            var followed = new List<string>();
            editor.Surface.WikiLinkTapped += followed.Add;
            Edit(editor, 0);
            Assert.True(editor.Surface.IsEditing);
            var holder = editor.Surface.BlockView(editor.Surface.BlockStarts[1])!;
            Click(window, PointOf(window, holder, "Outra", 2));
            Assert.Empty(followed);
            var box = editor.Surface.EditBox!;
            Assert.StartsWith("Veja [[Outra", box.Text);
            Assert.InRange(box.CaretIndex, "Veja [[".Length, "Veja [[Outra nota".Length);
            Assert.True(editor.Surface.OpenLinkChip.IsVisible);
            editor.Surface.OpenLinkChip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(["Outra nota"], followed);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ALinkTappedWhileReadingIsFollowed() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Veja [[Outra nota]] depois.\n");
            var followed = new List<string>();
            editor.Surface.WikiLinkTapped += followed.Add;
            var holder = editor.Surface.BlockView(editor.Surface.BlockStarts[0])!;
            Click(window, PointOf(window, holder, "Outra", 2));
            Assert.Equal(["Outra nota"], followed);
            Assert.False(editor.Surface.IsEditing);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    private static string LongNote(int sections)
    {
        var sb = new System.Text.StringBuilder("# Nota longa\n\n");
        for (int i = 1; i <= sections; i++)
            sb.Append("## Seção ").Append(i).Append("\n\nParágrafo ").Append(i).Append(" com **negrito** e [[Link ").Append(i).Append("]].\n\n- [ ] tarefa ").Append(i).Append("\n\n");
        return sb.ToString();
    }

    [Fact]
    public async Task LeavingABlockRedrawsOnlyThatBlock() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open(LongNote(30));
            var surface = editor.Surface;
            var box = Edit(editor, 5);
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput(" mais texto");
            Dispatcher.UIThread.RunJobs();
            surface.FinishEdit();
            Assert.Equal(1, surface.LastBuildDrawn);
            // moving to another block: only the left one is drawn again (the new box is not a drawing)
            Edit(editor, 20);
            Assert.True(surface.LastBuildDrawn <= 1, "drawn " + surface.LastBuildDrawn);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task AReusedBlockActsOnItsCurrentPlaceAfterTheTextAboveGrew() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Primeiro.\n\n- [ ] tarefa\n");
            var surface = editor.Surface;
            var box = Edit(editor, 0);
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput(" Bem mais comprido agora.");
            Dispatcher.UIThread.RunJobs();
            surface.FinishEdit();
            Assert.Equal(1, surface.LastBuildDrawn);
            var task = surface.BlockView(surface.BlockStarts[1])!.GetVisualDescendants().OfType<Border>().First(b => b.Width == 18 && b.Height == 18);
            Click(window, task.TranslatePoint(new Point(9, 9), window)!.Value);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Primeiro. Bem mais comprido agora.\n\n- [x] tarefa\n", editor.Editor.Text!.Replace("\r\n", "\n"));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
