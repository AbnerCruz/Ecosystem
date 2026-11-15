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
        throw new InvalidOperationException(word + " in [" + string.Join("|", holder.GetVisualDescendants().OfType<TextBlock>().Select(VisualSurfaceText)) + "]");
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

    [Theory]
    [InlineData("Texto com **negrito** aqui.", "negrito", 3)]
    [InlineData("Um [[Outra nota|apelido]] no fim.", "apelido", 2)]
    [InlineData("Um [site bom](https://exemplo.com) no fim.", "bom", 1)]
    [InlineData("# Título grande", "grande", 2)]
    [InlineData("## Seção do texto", "texto", 2)]
    [InlineData("- item da lista", "lista", 2)]
    [InlineData("- [ ] tarefa aberta", "aberta", 2)]
    [InlineData("> citação longa", "longa", 2)]
    [InlineData("Código `inline` aqui.", "inline", 2)]
    [InlineData("1. primeiro item", "item", 2)]
    [InlineData("Texto ~~riscado~~ fim.", "fim", 1)]
    // the source of a formula or a word comment is longer than its drawing: the text after it may wrap one line lower
    [InlineData("Antes da fórmula $x^2 + y^2$ e depois dela.", "depois", 2, false)]
    [InlineData("Texto com #tag e #outra/sub no meio.", "meio", 1)]
    [InlineData("Visível %%escondido%% e o resto.", "resto", 2)]
    [InlineData("- pai\n  - filho aninhado", "aninhado", 2)]
    [InlineData("Palavra{\"um comentário\"} comentada e fim.", "fim", 1, false)]
    [InlineData("Texto escrito pela IA aqui {ia:\"modelo\"}", "aqui", 2)]
    [InlineData("Link [[Nota]] e **negrito *aninhado* aqui** fim.", "fim", 1)]
    [InlineData("Um parágrafo bem comprido que quebra em várias linhas na tela estreita do celular, para que o toque caia numa linha do meio e depois noutra mais abaixo, onde está a palavra escondida.", "escondida", 4)]
    public async Task ATapPutsTheCaretOnTheLetterTappedWithoutMovingTheLine(string line, string word, int offset, bool stable = true) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Antes.\n\n" + line + "\n\nDepois.\n", width: 360);
            // while writing (a link is then a place for the caret too)
            Edit(editor, 0);
            window.UpdateLayout();
            var holder = editor.Surface.BlockView(editor.Surface.BlockStarts[1])!;
            var p = PointOf(window, holder, word, offset);
            Click(window, p);
            var box = editor.Surface.EditBox!;
            Assert.NotNull(box);
            var expected = box.Text!.IndexOf(word, StringComparison.Ordinal) + offset;
            Assert.InRange(box.CaretIndex, expected - 1, expected + 1);
            // the letter is still under the finger: the line did not jump when the block became a box
            window.UpdateLayout();
            var presenter = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
            var r = presenter.TextLayout.HitTestTextPosition(box.CaretIndex);
            var at = presenter.TranslatePoint(new Point(r.X, r.Y + r.Height / 2), window)!.Value;
            Assert.True(Math.Abs(at.Y - p.Y) < r.Height * (stable ? .75 : 1.75), $"line moved {at.Y - p.Y:0.0}px (line {r.Height:0.0})");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task OpeningTheSameNoteAgainKeepsTheBlockBeingWritten() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = Open("Um.\n\nDois.\n");
            editor.SaveRequested += _ => Task.CompletedTask;
            var box = Edit(editor, 1);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput(" e três");
            await editor.SaveAsync();
            Dispatcher.UIThread.RunJobs();
            editor.Open(new CityNote("Fluxo.md", editor.Editor.Text!));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(box, editor.Surface.EditBox);
            Assert.True(box.IsFocused);
            window.KeyTextInput("!");
            Assert.Equal("Um.\n\nDois. e três!\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ANoteChangedElsewhereKeepsTheBlockBeingWrittenWhenItIsStillThere() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um.\n\nDois.\n");
            var box = Edit(editor, 1);
            box.Focus();
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 2;
            // saved (nothing pending): the Assistente adds a paragraph at the top
            editor.Open(new CityNote("Fluxo.md", "Novo começo.\n\nUm.\n\nDois.\n"));
            Dispatcher.UIThread.RunJobs();
            var now = editor.Surface.EditBox;
            Assert.NotNull(now);
            Assert.Equal("Dois.", now!.Text);
            Assert.Equal(2, now.CaretIndex);
            Assert.True(now.IsFocused);
            window.KeyTextInput("X");
            Assert.Equal("Novo começo.\n\nUm.\n\nDoXis.\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task TickingATaskWhileWritingKeepsWriting() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Texto.\n\n- [ ] primeira\n");
            var box = Edit(editor, 0);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput(" A");
            Dispatcher.UIThread.RunJobs();
            var task = editor.Surface.BlockView(editor.Surface.BlockStarts[1])!.GetVisualDescendants().OfType<Border>().First(b => b.Width == 18 && b.Height == 18);
            Click(window, task.TranslatePoint(new Point(9, 9), window)!.Value);
            Assert.Same(box, editor.Surface.EditBox);
            Assert.True(box.IsFocused);
            window.KeyTextInput("B");
            Assert.Equal("Texto. AB\n\n- [x] primeira\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task TheBlockBeingWrittenShowsItsMarkdownFormatted() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("# Estudo\n\nTexto com **negrito**, *itálico*, `código`, [[Outra nota|apelido]], #tag e $x^2$ no meio, ~~riscado~~ e %%nota%%.\n\n- [ ] tarefa aberta\n- item com [link](https://exemplo.com)\n\nDepois.\n", width: 390, height: 640);
            var box = Edit(editor, 1, new Point(30, 10));
            window.UpdateLayout();
            var presenter = box.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.True(presenter.Formatted);
            // the bold word is drawn bold, the stars around it dimmed: different runs in the layout
            var runs = presenter.TextLayout.TextLines.SelectMany(l => l.TextRuns).OfType<Avalonia.Media.TextFormatting.ShapedTextRun>().ToList();
            Assert.Contains(runs, r => r.Properties.Typeface.Weight == Avalonia.Media.FontWeight.Bold);
            Assert.Contains(runs, r => r.Properties.Typeface.Style == Avalonia.Media.FontStyle.Italic);
            Shot(window, "editor-box-formatted.png");
            // a code block is written as it is
            editor.Surface.FinishEdit();
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    internal static void Shot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("URBE_SNAPSHOT_DIR");
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame!.Save(Path.Combine(dir, name), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [Fact]
    public async Task UndoInVisualGoesBackAcrossBlocksAndReopensWhereTheChangeWas() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um.\n\nDois.\n");
            var box = Edit(editor, 0);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            foreach (var c in " abc") window.KeyTextInput(c.ToString());
            Dispatcher.UIThread.RunJobs();
            box = Edit(editor, 1);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            foreach (var c in " xyz") window.KeyTextInput(c.ToString());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Um. abc\n\nDois. xyz\n", Source(editor));
            // Ctrl+Z in the box: the note's undo
            window.KeyPress(Avalonia.Input.Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
            window.KeyRelease(Avalonia.Input.Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Um. abc\n\nDois.\n", Source(editor));
            editor.Undo();
            Assert.Equal("Um.\n\nDois.\n", Source(editor));
            editor.Undo();
            Assert.Equal("Um.\n\nDois.\n", Source(editor));
            // the block where the last change was undone is written, caret there, keyboard kept
            Assert.Equal("Um.", editor.Surface.EditBox!.Text);
            Assert.Equal(3, editor.Surface.EditBox!.CaretIndex);
            Assert.True(editor.Surface.EditBox!.IsFocused);
            editor.Redo();
            Assert.Equal("Um. abc\n\nDois.\n", Source(editor));
            window.KeyTextInput("!");
            Assert.False(editor.CanRedoVisual);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShiftArrowsSelectAcrossBlocks() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um dois.\n\nTrês quatro.\n\nCinco.\n");
            var box = Edit(editor, 1);
            box.Focus();
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 4;   // "Três| quatro"
            void Shift(Avalonia.Input.Key k, PhysicalKey p)
            {
                window.KeyPress(k, RawInputModifiers.Shift, p, null);
                window.KeyRelease(k, RawInputModifiers.Shift, p, null);
                Dispatcher.UIThread.RunJobs();
            }
            Shift(Avalonia.Input.Key.Up, PhysicalKey.ArrowUp);
            box = editor.Surface.EditBox!;
            Assert.Equal("Um dois.\n\nTrês quatro.", box.Text);
            Assert.Equal("\n\nTrês", box.SelectedText);
            Shift(Avalonia.Input.Key.Left, PhysicalKey.ArrowLeft);
            Assert.Equal(".\n\nTrês", editor.Surface.EditBox!.SelectedText);
            window.KeyTextInput("X");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Um doisX quatro.\n\nCinco.\n", Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ShiftClickSelectsFromTheCaretToTheLetterClicked() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um dois.\n\nTrês quatro.\n\nCinco seis.\n");
            var box = Edit(editor, 0);
            box.Focus();
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 3;   // "Um |dois"
            var holder = editor.Surface.BlockView(editor.Surface.BlockStarts[2])!;
            var p = PointOf(window, holder, "seis", 0);
            window.MouseDown(p, MouseButton.Left, RawInputModifiers.Shift);
            window.MouseUp(p, MouseButton.Left, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            box = editor.Surface.EditBox!;
            Assert.Equal("Um dois.\n\nTrês quatro.\n\nCinco seis.", box.Text);
            Assert.Equal("dois.\n\nTrês quatro.\n\nCinco ", box.SelectedText);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task CtrlATwiceSelectsTheWholeNoteInVisual() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um.\n\nDois.\n\nTrês.\n");
            var box = Edit(editor, 1);
            box.Focus();
            void CtrlA()
            {
                window.KeyPress(Avalonia.Input.Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
                window.KeyRelease(Avalonia.Input.Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
                Dispatcher.UIThread.RunJobs();
            }
            CtrlA();
            Assert.Equal("Dois.", editor.Surface.EditBox!.SelectedText);
            CtrlA();
            Assert.Equal("Um.\n\nDois.\n\nTrês.", editor.Surface.EditBox!.SelectedText);
            Assert.Equal(EditorMode.Visual, editor.Mode);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task BackspaceAtTheStartOfACodeBlockMovesUpWithoutBreakingTheFence() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            const string note = "Texto.\n\n```csharp\nvar x = 1;\n```\n";
            var (editor, window) = Open(note);
            var box = Edit(editor, 1);
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 0;
            Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
            Assert.Equal("Texto.", editor.Surface.EditBox!.Text);
            Assert.Equal(6, editor.Surface.EditBox!.CaretIndex);
            Assert.Equal(note, Source(editor));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task FormulasOnANarrowScreenAreDrawnWhole() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var note = "# Fórmulas\n\nA energia $E = mc^2$ e a soma $\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}$ aparecem no meio do texto, com $\\int_0^\\infty e^{-x^2}\\,dx = \\frac{\\sqrt{\\pi}}{2}$ numa linha que quebra.\n\n" +
                "$$\n\\frac{\\partial^2 u}{\\partial t^2} = c^2 \\left( \\frac{\\partial^2 u}{\\partial x^2} + \\frac{\\partial^2 u}{\\partial y^2} + \\frac{\\partial^2 u}{\\partial z^2} \\right) + f(x, y, z, t) \\cdot \\sum_{k=0}^{\\infty} a_k\n$$\n\n" +
                "Texto depois da fórmula, que não pode ficar por baixo dela.\n\n$$\n\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}^{-1} = \\frac{1}{ad-bc}\\begin{pmatrix} d & -b \\\\ -c & a \\end{pmatrix}\n$$\n\nFim.\n";
            var (editor, window) = Open(note, width: 360, height: 800);
            window.UpdateLayout();
            Shot(window, "editor-formulas-narrow.png");
            // the wide display formulas are drawn smaller to fit the column, never cut while it is enough
            var fits = editor.Surface.GetVisualDescendants().OfType<FormulaFit>().ToList();
            Assert.Equal(2, fits.Count);
            Assert.All(fits, f => Assert.True(f.Scale < 1));
            foreach (var f in fits.Where(f => f.Scale > FormulaFit.MinScale))
            {
                var sv = f.GetVisualDescendants().OfType<ScrollViewer>().First();
                Assert.True(sv.Extent.Width <= sv.Viewport.Width + 1, $"scrolls: {sv.Extent.Width:0} > {sv.Viewport.Width:0}");
            }
            // no drawn block overlaps the next one
            var starts = editor.Surface.BlockStarts;
            for (int i = 0; i + 1 < starts.Count; i++)
            {
                var a = editor.Surface.BlockView(starts[i])!;
                var b = editor.Surface.BlockView(starts[i + 1])!;
                var ya = a.TranslatePoint(new Point(0, a.Bounds.Height), editor.Surface)!.Value.Y;
                var yb = b.TranslatePoint(new Point(0, 0), editor.Surface)!.Value.Y;
                Assert.True(ya <= yb + .5, $"block {i} ends at {ya:0} under block {i + 1} at {yb:0}");
                // and nothing inside a block is drawn past its bottom
                foreach (var c in a.GetVisualDescendants().OfType<Control>().Where(c => c.IsVisible && c.Bounds.Height > 0))
                {
                    var bottom = c.TranslatePoint(new Point(0, c.Bounds.Height), a)!.Value.Y;
                    if (c.FindAncestorOfType<ScrollViewer>() is { } sv && a.IsVisualAncestorOf(sv)) continue;
                    Assert.True(bottom <= a.Bounds.Height + 1, $"block {i}: {c.GetType().Name} ends at {bottom:0} past {a.Bounds.Height:0}");
                }
            }
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));


    [Fact]
    public async Task MovingBetweenBlocksNeverLeavesTheKeyboardWithoutATextField() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = Open("Um parágrafo.\n\nDois parágrafos.\n\nTrês.\n\n```\ncódigo\n```\n\nQuatro.\n");
            var focus = TopLevel.GetTopLevel(window)!.FocusManager!;
            var gaps = new List<string>();
            void Watch(TextBox b) => b.LostFocus += (_, e) => { if (e is not FocusChangedEventArgs { NewFocusedElement: TextBox }) gaps.Add("sem campo de texto depois de [" + b.Text + "]"); };
            var box = Edit(editor, 0);
            box.Focus();
            Watch(box);
            void Next(string what)
            {
                Dispatcher.UIThread.RunJobs();
                var now = editor.Surface.EditBox!;
                Assert.True(now.IsFocused, what);
                if (!ReferenceEquals(now, box)) { box = now; Watch(box); }
            }
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput("x");
            // a tap on another block
            var holder = editor.Surface.BlockView(editor.Surface.BlockStarts[1])!;
            Click(window, PointOf(window, holder, "Dois", 1));
            Next("tap");
            // arrows across blocks, into and out of code
            Key(window, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown); Next("down");
            Key(window, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown); Next("down into code");
            Key(window, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown); Next("down out of code");
            // Backspace at the start joins, Enter-like new block at the end
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = 0;
            Key(window, Avalonia.Input.Key.Left, PhysicalKey.ArrowLeft); Next("left");
            Key(window, Avalonia.Input.Key.Right, PhysicalKey.ArrowRight); Next("right");
            Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace); Next("backspace at start");
            Assert.Empty(gaps);
            // the boxes left behind are gone from the tree
            Assert.Single(editor.Surface.GetVisualDescendants().OfType<TextBox>());
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));


    [Fact]
    public async Task TheCaretStaysInSightWhileTypingNewLinesAtTheBottom() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            // a phone with the keyboard open: little room for the text
            var (editor, window) = Open(LongNote(12), width: 360, height: 420);
            var surface = editor.Surface;
            var scroll = surface.FindAncestorOfType<ScrollViewer>()!;
            // a block near the bottom of what is visible
            var index = surface.BlockStarts.Select((s, i) => (s, i)).First(t =>
                surface.BlockView(t.s) is { } v && v.TranslatePoint(new Point(0, 0), scroll) is { } q && q.Y > scroll.Viewport.Height * .6).i;
            var box = Edit(editor, index);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            for (int i = 0; i < 8; i++)
            {
                window.KeyPress(Avalonia.Input.Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Avalonia.Input.Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyTextInput("linha " + i);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var presenter = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
                var r = presenter.TextLayout.HitTestTextPosition(box.CaretIndex);
                var y = presenter.TranslatePoint(new Point(0, r.Bottom), scroll)!.Value.Y;
                Assert.True(y <= scroll.Viewport.Height + 1 && y - r.Height >= -1, $"line {i}: caret at {y:0} outside 0..{scroll.Viewport.Height:0}");
            }
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task WhenTheKeyboardOpensTheTappedLineStaysInSight() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = Open(LongNote(12), width: 360, height: 800);
            var surface = editor.Surface;
            var scroll = surface.FindAncestorOfType<ScrollViewer>()!;
            var index = surface.BlockStarts.Select((s, i) => (s, i)).Last(t =>
                surface.BlockView(t.s) is { } v && v.TranslatePoint(new Point(0, 0), scroll) is { } q && q.Y < scroll.Viewport.Height - 60).i;
            var box = Edit(editor, index);
            box.Focus();
            // the soft keyboard takes the bottom half
            window.Height = 420;
            for (int i = 0; i < 5; i++) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); await Task.Delay(20); }
            var presenter = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
            var r = presenter.TextLayout.HitTestTextPosition(box.CaretIndex);
            var y = presenter.TranslatePoint(new Point(0, r.Bottom), scroll)!.Value.Y;
            Assert.True(y <= scroll.Viewport.Height + 1 && y - r.Height >= -1, $"caret at {y:0} outside 0..{scroll.Viewport.Height:0}");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task AFingerOnTheBlockBeingWrittenAsksForTheKeyboard() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var before = PlatformInput.ShowKeyboard;
            int asked = 0;
            PlatformInput.ShowKeyboard = () => { asked++; return true; };
            try
            {
                var (editor, window) = Open("Um parágrafo.\n");
                var box = Edit(editor, 0);
                asked = 0;
                // the keyboard was closed with Back; the finger taps the same line again
                var touch = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
                var args = new TappedEventArgs(InputElement.TappedEvent,
                    new PointerEventArgs(InputElement.PointerReleasedEvent, box, touch, window, new Point(5, 5), 0, new PointerPointProperties(), KeyModifiers.None));
                box.RaiseEvent(args);
                Assert.Equal(1, asked);
                window.Close();
            }
            finally { PlatformInput.ShowKeyboard = before; }
            return true;
        }, TestContext.Current.CancellationToken));
}
