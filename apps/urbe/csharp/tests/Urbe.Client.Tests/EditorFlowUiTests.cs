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

    private static void Key(Window window, Avalonia.Input.Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static string Source(EditorOverlay editor) => editor.Editor.Text!.Replace("\r\n", "\n");

    [Fact]
    public async Task BackspaceAtTheStartOfABlockJoinsItToTheOneAbove() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um.\n\nDois.\n");
            var box = Edit(editor, 1);
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 0;
            Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
            box = editor.Surface.EditBox!;
            Assert.Equal("Um.\nDois.", box.Text);
            Assert.Equal(4, box.CaretIndex);
            Assert.True(box.IsFocused);
            Assert.Equal("Um.\nDois.\n", Source(editor));
            Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
            Assert.Equal("Um.Dois.\n", Source(editor));
            editor.Surface.FinishEdit();
            Assert.Single(editor.Surface.BlockStarts);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task DeleteAtTheEndOfABlockJoinsTheOneBelow() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("# Título\n\nTexto.\n");
            var box = Edit(editor, 0);
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = box.Text!.Length;
            Key(window, Avalonia.Input.Key.Delete, PhysicalKey.Delete);
            box = editor.Surface.EditBox!;
            Assert.Equal("# Título\nTexto.", box.Text);
            Assert.Equal("# Título".Length, box.CaretIndex);
            Assert.Equal("# Título\nTexto.\n", Source(editor));
            // the joined box holds both blocks: neither is drawn again under it
            Assert.Single(editor.Surface.GetVisualDescendants().OfType<TextBox>(), t => t == box);
            Assert.DoesNotContain(editor.Surface.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains(MarkdownReader.TextClass) && (t.Text ?? "").Contains("Texto"));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task BackspaceOnAnEmptyNewBlockReturnsToTheEndOfTheOneAbove() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um.\n\nDois.\n");
            var box = Edit(editor, 1);
            box.SelectAll();
            Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
            Assert.Equal("", editor.Surface.EditBox!.Text);
            Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
            box = editor.Surface.EditBox!;
            Assert.Equal("Um.", box.Text);
            Assert.Equal(3, box.CaretIndex);
            Assert.Equal("Um.\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ArrowsCrossBlocksAsInOneText() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Primeiro bloco.\n\nSegundo bloco.\n\nTerceiro.\n");
            var box = Edit(editor, 1);
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 4;
            Key(window, Avalonia.Input.Key.Up, PhysicalKey.ArrowUp);
            box = editor.Surface.EditBox!;
            Assert.Equal("Primeiro bloco.", box.Text);
            Assert.InRange(box.CaretIndex, 3, 5);   // the same column
            Assert.True(box.IsFocused);
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = box.Text!.Length;
            Key(window, Avalonia.Input.Key.Right, PhysicalKey.ArrowRight);
            box = editor.Surface.EditBox!;
            Assert.Equal("Segundo bloco.", box.Text);
            Assert.Equal(0, box.CaretIndex);
            Key(window, Avalonia.Input.Key.Left, PhysicalKey.ArrowLeft);
            Assert.Equal("Primeiro bloco.", editor.Surface.EditBox!.Text);
            Assert.Equal("Primeiro bloco.".Length, editor.Surface.EditBox!.CaretIndex);
            Key(window, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown);
            Key(window, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown);
            Assert.Equal("Terceiro.", editor.Surface.EditBox!.Text);
            // nothing changed in the text by moving around
            Assert.Equal("Primeiro bloco.\n\nSegundo bloco.\n\nTerceiro.\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Theory]
    [InlineData("Texto do agente {ia:\"m\"}\n\nMeu texto.\n", 0, true)]
    [InlineData("Texto do agente {ia:\"m\"}\n\n```csharp\nvar x = 1;\n```\n", 0, true)]
    [InlineData("Meu texto.\n\nTexto do agente {ia:\"m\"}\n", 0, true)]
    public async Task TypingInBlocksJoinedWithAuthoredTextKeepsWriting(string note, int block, bool forward) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open(note);
            var box = Edit(editor, block);
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = forward ? box.Text!.Length : 0;
            Key(window, forward ? Avalonia.Input.Key.Delete : Avalonia.Input.Key.Back, forward ? PhysicalKey.Delete : PhysicalKey.Backspace);
            box = editor.Surface.EditBox!;
            var joined = box.Text!;
            foreach (var at in new[] { joined.Length, joined.IndexOf('\n') + 1, joined.IndexOf('\n'), 3 })
            {
                box.SelectionStart = box.SelectionEnd = box.CaretIndex = at;
                Assert.True(box.IsFocused, "not focused before typing at " + at);
                window.KeyTextInput("Z");
                Dispatcher.UIThread.RunJobs();
                Assert.True(ReferenceEquals(box, editor.Surface.EditBox), $"ended at {at}: [{joined}] now [{editor.Surface.EditBox?.Text}] note [{Source(editor)}]");
                Assert.Equal('Z', box.Text![at]);
                Assert.Contains("Z", Source(editor));
            }
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASingleTapOnAnotherBlockWhileWritingEditsIt(bool slowRelease) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = Open("Primeiro parágrafo com texto.\n\nSegundo parágrafo com texto.\n\nTerceiro.\n");
            var box = Edit(editor, 0);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput(" mais");
            Dispatcher.UIThread.RunJobs();
            var holder = editor.Surface.BlockView(editor.Surface.BlockStarts[1])!;
            var p = PointOf(window, holder, "parágrafo", 3);
            var hit = window.InputHitTest(p) as Visual;
            Assert.True(hit is not null && hit.GetSelfAndVisualAncestors().Contains(holder), "hit " + string.Join(" < ", hit?.GetSelfAndVisualAncestors().Take(6).Select(v => v.GetType().Name) ?? []));
            window.MouseDown(p, MouseButton.Left);
            // the finger stays down a moment: whatever runs in between must not take the tap away
            Dispatcher.UIThread.RunJobs();
            if (slowRelease) { await Task.Delay(150); Dispatcher.UIThread.RunJobs(); }
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var now = editor.Surface.EditBox;
            Assert.NotNull(now);
            Assert.StartsWith("Segundo", now!.Text);
            Assert.True(now.IsFocused);
            Assert.InRange(now.CaretIndex, "Segundo pa".Length - 1, "Segundo par".Length + 1);
            Assert.StartsWith("Primeiro parágrafo com texto. mais", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ATaskTickedBelowAfterTypingChangesItsOwnLine() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Texto.\n\n- [ ] primeira\n- [ ] segunda\n");
            var box = Edit(editor, 0);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput(" Mais coisas escritas aqui");
            Dispatcher.UIThread.RunJobs();
            var tasks = editor.Surface.BlockView(editor.Surface.BlockStarts[1])!.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 18 && b.Height == 18).ToList();
            Click(window, tasks[1].TranslatePoint(new Point(9, 9), window)!.Value);
            Assert.Equal("Texto. Mais coisas escritas aqui\n\n- [ ] primeira\n- [x] segunda\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
