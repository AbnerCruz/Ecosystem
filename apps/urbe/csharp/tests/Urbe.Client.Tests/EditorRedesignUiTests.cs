using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.Ui;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-69 phase 2 — the note editor redesigned against UX.md (P1–P13): the block stays in place when it enters or leaves
/// edit mode, the caret stays in sight while typing, a tap lands on the tapped character, the bar rests while reading
/// and comes with the keyboard, saving is a quiet dot, leaving never waits on the screen.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private const string ShotNote = "# Diário da cidade\n\nHoje a praça amanheceu com neblina, e a padaria da esquina abriu mais cedo. Escrevo aqui o que quero lembrar desta semana, sem pressa.\n\n## Ideias\n\n- Uma rua nova ligando a [[Biblioteca]] ao porto\n- Plantar árvores no bairro das notas antigas\n- [ ] Revisar o capítulo dois\n\n> A cidade se lê como um livro: cada casa guarda uma ideia.\n\nUm parágrafo mais longo para ver como o texto respira numa tela de celular, com **negrito**, _itálico_ e `código` no meio da frase, quebrando em várias linhas.\n";

    private static string LongNote() =>
        "# Longa\n\n" + string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"Parágrafo {i}: uma frase comprida o bastante para quebrar em duas ou três linhas num celular de bolso, como acontece ao escrever de verdade."));

    private static async Task Pump(Window window, int times = 6)
    {
        for (int i = 0; i < times; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (EditorOverlay Editor, Window Window) StandaloneEditor(string text, double height = 900)
    {
        var editor = new EditorOverlay();
        var window = new Window { Width = 412, Height = height, Content = editor };
        window.Show();
        editor.Open(new CityNote("Notas/Nota.md", text));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (editor, window);
    }

    private static ScrollViewer VisualScroll(EditorOverlay editor) => editor.Surface.FindAncestorOfType<ScrollViewer>()!;

    private static TextBlock FirstText(Control block) =>
        block.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains(MarkdownReader.TextClass));

    private static double TopIn(Visual v, Visual root) => v.TranslatePoint(default, root)!.Value.Y;

    private static Visual Presenter(TextBox box) => box.GetVisualDescendants().OfType<TextPresenter>().First();

    /// <summary>Taps the drawn block at <paramref name="index"/>, at a point of its first text line.</summary>
    private static (double TextTopBefore, int Start) Tap(EditorOverlay editor, Window window, int index, double x = 60, bool select = false)
    {
        var surface = editor.Surface;
        var start = surface.BlockStarts[index];
        var holder = surface.BlockView(start)!;
        var text = FirstText(holder);
        var before = TopIn(text, window);
        var at = text.TranslatePoint(new Point(x, text.LineHeight / 2), holder)!.Value;
        surface.TapBlock(start, holder, text, at, select);
        return (before, start);
    }

    [Fact]
    public async Task Editor_ATappedBlockStaysInPlaceEnteringAndLeavingEditMode() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor(LongNote());
            try
            {
                var scroll = VisualScroll(editor);
                EditorScrolling.SetOffsetY(scroll, 900);
                await Pump(window);
                // a block in the middle of the screen
                int index = editor.Surface.BlockStarts.Select((s, i) => (s, i))
                    .First(p => editor.Surface.BlockView(p.s) is { } b && TopIn(b, scroll) > 200).i;
                var (before, _) = Tap(editor, window, index);
                await Pump(window);
                var box = editor.Surface.EditBox!;
                Assert.True(box.IsFocused);
                Assert.InRange(TopIn(Presenter(box), window) - before, -2, 2);

                // the block grows while writing; then the next block is tapped: it stays where it was too, although the
                // block above goes back to its drawn height
                box.Text += "\nmais uma linha\ne outra";
                await Pump(window);
                var (nextBefore, nextStart) = Tap(editor, window, index + 1);
                await Pump(window);
                var nextBox = editor.Surface.EditBox!;
                Assert.InRange(TopIn(Presenter(nextBox), window) - nextBefore, -2, 2);

                // leaving edit mode: the drawn block appears where the text being written was
                var textTop = TopIn(Presenter(nextBox), window);
                editor.Surface.FinishEdit();
                await Pump(window);
                var drawn = editor.Surface.BlockView(editor.Surface.BlockStarts[index + 1])!;
                Assert.InRange(TopIn(FirstText(drawn), window) - textTop, -2, 2);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_TheCaretStaysInTheUpperTwoThirdsWhileTyping() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // a phone with the keyboard open: 900 - 330 dp left for the app
            var (editor, window) = StandaloneEditor(LongNote(), 570);
            try
            {
                var scroll = VisualScroll(editor);
                await Pump(window);
                // a block low on the screen
                int index = editor.Surface.BlockStarts.Select((s, i) => (s, i))
                    .First(p => editor.Surface.BlockView(p.s) is { } b && TopIn(b, scroll) > scroll.Viewport.Height * .55).i;
                Tap(editor, window, index);
                await Pump(window);
                var box = editor.Surface.EditBox!;
                box.CaretIndex = box.Text!.Length;
                await Pump(window);
                double last = scroll.Offset.Y;
                for (int i = 0; i < 14; i++)
                {
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                    window.KeyTextInput("linha nova " + i);
                    await Pump(window, 3);
                    var caret = EditorScrolling.CaretRect(box, scroll)!.Value;
                    Assert.True(caret.Top >= 0, $"linha {i}: cursor acima da área visível ({caret.Top})");
                    Assert.True(caret.Bottom <= scroll.Viewport.Height * EditorScrolling.BandBottom + 2, $"linha {i}: cursor em {caret.Bottom} de {scroll.Viewport.Height}");
                    // never a jump: at most a couple of lines per new line (the first may bring the caret up into the band)
                    if (i > 0) Assert.InRange(scroll.Offset.Y - last, -1, box.LineHeight * 2.5);
                    last = scroll.Offset.Y;
                }
                Assert.True(scroll.Offset.Y > 0);
                Assert.False(editor.HeaderHidden);   // the editor's own scrolling never folds the header
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public void Editor_ATapMapsTheDrawnCharacterToTheMarkdown()
    {
        // heading, bold, link, list: the caret lands on the same character of the source
        Assert.Equal(2, VisualSurface.SourceIndex("# Título", "Título", 0));
        Assert.Equal(5, VisualSurface.SourceIndex("# Título", "Título", 3));
        Assert.Equal(4, VisualSurface.SourceIndex("a **forte** b", "a forte b", 2));
        Assert.Equal(6, VisualSurface.SourceIndex("a **forte** b", "a forte b", 4));
        Assert.Equal(11, VisualSurface.SourceIndex("a **forte** b", "a forte b", 7));
        Assert.Equal(11, VisualSurface.SourceIndex("ver [[Outra nota]] já", "ver Outra nota já", 9));
        Assert.Equal(9, VisualSurface.SourceIndex("- um\n- dois", "um\ndois", 5));
        Assert.Equal(6, VisualSurface.SourceIndex("- [ ] tarefa", "tarefa", 0));
        Assert.Equal(6, VisualSurface.SourceIndex("[sítio](https://a.b/c) e", "sítio e", 5));
    }

    [Fact]
    public async Task Editor_ATapPutsTheCaretOnTheTappedCharacterOfWrappedHeadingAndListLines() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor(ShotNote);
            try
            {
                await Pump(window);
                var surface = editor.Surface;
                // a wrapped paragraph, on its second line
                var start = surface.BlockStarts[1];
                var holder = surface.BlockView(start)!;
                var text = FirstText(holder);
                var p = new Point(150, text.LineHeight * 1.5);
                var hit = text.TextLayout.HitTestPoint(p);
                var expected = hit.TextPosition + (hit.IsTrailing ? 1 : 0);
                surface.TapBlock(start, holder, text, text.TranslatePoint(p, holder)!.Value, select: false);
                await Pump(window);
                Assert.InRange(surface.EditBox!.CaretIndex, expected - 1, expected + 1);
                // the heading "## Ideias": a tap on "I" puts the caret before it, after the marks
                var (_, h) = Tap(editor, window, 2, x: 2);
                await Pump(window);
                Assert.Equal("## ".Length, surface.EditBox!.CaretIndex);
                // the second item of the list: the caret in "Plantar", not in the first line
                var listStart = surface.BlockStarts[3];
                var list = surface.BlockView(listStart)!;
                var second = list.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains(MarkdownReader.TextClass)).ElementAt(1);
                var q = new Point(4, second.LineHeight / 2);
                surface.TapBlock(listStart, list, second, second.TranslatePoint(q, list)!.Value, select: false);
                await Pump(window);
                var box = surface.EditBox!;
                Assert.StartsWith("Plantar", box.Text![box.CaretIndex..]);
                _ = h;
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_ALongPressSelectsTheWordWithTheBubbleAndAHapticTick() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var kinds = new List<HapticKind>();
            void Record(HapticKind k) => kinds.Add(k);
            PlatformHaptics.Requested += Record;
            var (editor, window) = StandaloneEditor(ShotNote);
            try
            {
                await Pump(window);
                Tap(editor, window, 1, x: 70, select: true);
                await Pump(window);
                var box = editor.Surface.EditBox!;
                var selected = box.Text![Math.Min(box.SelectionStart, box.SelectionEnd)..Math.Max(box.SelectionStart, box.SelectionEnd)];
                Assert.Matches(@"^\w+$", selected);
                Assert.Contains(HapticKind.Hold, kinds);
                Assert.True(editor.Surface.Bubble.IsVisible);
                // the bubble floats over the text: the block did not move down to make room for it
                Assert.True(TopIn(editor.Surface.Bubble, window) < TopIn(Presenter(box), window));
                return true;
            }
            finally { PlatformHaptics.Requested -= Record; window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_TheBarRestsWhileReadingAndComesWithEditingAboveTheKeyboard() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor(ShotNote);
            try
            {
                await Pump(window);
                static bool Shown(Control c) => c.GetSelfAndLogicalAncestors().OfType<Control>().All(x => x.IsVisible);
                Button Tip(string tip) => editor.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == tip && !b.GetLogicalAncestors().Contains(editor.Surface));
                Assert.True(editor.ToolbarAtBottom);
                Assert.True(editor.Reading);
                Assert.False(Shown(Tip("Negrito")));
                Tap(editor, window, 5);   // the paragraph with **negrito**
                await Pump(window);
                Assert.False(editor.Reading);
                var bold = Tip("Negrito");
                Assert.True(Shown(bold));
                // the most used first, every target 48 dp, the bar is the last thing above the keyboard
                var bar = bold.GetLogicalAncestors().OfType<StackPanel>().First();
                var order = bar.Children.OfType<Button>().Select(b => AutomationProperties.GetName(b)).Take(4).ToArray();
                Assert.Equal(new[] { "Negrito", "Itálico", "Lista", "Tarefa" }, order);
                Assert.All(bar.Children.OfType<Button>(), b => Assert.True(b.Bounds.Height >= 48 && b.Bounds.Width >= 48, AutomationProperties.GetName(b)));
                Assert.InRange(window.Bounds.Height - (TopIn(bold, window) + bold.Bounds.Height), 0, 8);
                // lit by the text under the caret
                var box = editor.Surface.EditBox!;
                box.CaretIndex = box.Text!.IndexOf("negrito", StringComparison.Ordinal) + 2;
                await Pump(window);
                Assert.Contains("bold", editor.ActiveTools);
                Assert.NotEqual(Avalonia.Media.Brushes.Transparent, bold.Background);
                Snapshot(window, "editor-toolbar.png");
                // Concluir: back to calm reading
                editor.FinishWriting();
                await Pump(window);
                Assert.True(editor.Reading);
                Assert.False(Shown(Tip("Negrito")));
                // every control of the editor has a name for screen readers (P12)
                var unnamed = editor.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible)
                    .Where(b => string.IsNullOrEmpty(AutomationProperties.GetName(b)) && ToolTip.GetTip(b) is null).ToList();
                Assert.Empty(unnamed);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_TheHeaderGivesWayWhileScrollingDownAndReturnsGoingUp() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor(LongNote());
            try
            {
                var scroll = VisualScroll(editor);
                await Pump(window);
                // a programmatic move (the caret, a block kept in place) does not fold it
                scroll.Offset = new Vector(0, 400);
                await Pump(window, 2);
                Assert.False(editor.HeaderHidden);
                var middle = new Point(200, 450);
                for (int i = 0; i < 6; i++) { window.MouseWheel(middle, new Vector(0, -1)); await Pump(window, 2); }
                Assert.True(editor.HeaderHidden);
                await Task.Delay(350);
                for (int i = 0; i < 3; i++) { window.MouseWheel(middle, new Vector(0, 1)); await Pump(window, 2); }
                Assert.False(editor.HeaderHidden);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_SavingIsAQuietDotAndLeavingNeverWaits() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor("# Nota\n\ntexto\n");
            try
            {
                Assert.Equal("preview", editor.SaveState);
                var gate = new TaskCompletionSource();
                bool fail = true;
                var saved = new List<string>();
                editor.SaveRequested += async note =>
                {
                    await gate.Task;
                    if (fail) throw new IOException("sem espaço");
                    saved.Add(note.Content);
                };
                editor.SaveOnClose = true;
                editor.Open(new CityNote("Notas/Nota.md", "# Nota\n\ntexto\n"));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("saved", editor.SaveState);
                editor.SetMode(EditorMode.Fonte);
                editor.Editor.Text += "mais\n";
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("pending", editor.SaveState);
                // no "Salvando..." anywhere: the words of the footer are only the note's numbers
                Assert.DoesNotContain(editor.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text is "Salvando..." or "Salvo" or "Alterado");

                // leaving: the editor is gone at once, while the save still runs
                var back = editor.BackAsync();
                Assert.False(editor.IsVisible);
                Assert.False(back.IsCompleted);
                gate.SetResult();
                await back;
                // the save failed: nothing is lost — the note comes back with the text, still pending, and a retry saves it
                Assert.Contains("Notas/Nota.md", editor.UnsavedNotes);
                editor.Open(new CityNote("Notas/Nota.md", "# Nota\n\ntexto\n"));
                Dispatcher.UIThread.RunJobs();
                Assert.EndsWith("mais\n", editor.Editor.Text);
                Assert.Equal("pending", editor.SaveState);
                Assert.False(await editor.SaveAsync());
                Assert.Equal("error", editor.SaveState);
                Assert.StartsWith("Não foi possível salvar", editor.NoticeText);
                fail = false;
                await editor.RetrySavesAsync();
                Assert.Equal("saved", editor.SaveState);
                Assert.EndsWith("mais\n", saved[^1]);
                Assert.Empty(editor.UnsavedNotes);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_TheTextSizeOfPersonalizacaoReachesTheNoteAndReadsRegular() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor(ShotNote);
            try
            {
                await Pump(window);
                var para = FirstText(editor.Surface.BlockView(editor.Surface.BlockStarts[1])!);
                Assert.Equal(17, para.FontSize);
                Assert.Equal(Avalonia.Media.FontWeight.Normal, para.FontWeight);
                editor.ApplyAppearance(720, 18, 1.6, textSize: 20);
                await Pump(window);
                para = FirstText(editor.Surface.BlockView(editor.Surface.BlockStarts[1])!);
                Assert.Equal(20, para.FontSize);
                Assert.Equal(32, para.LineHeight);
                // the edit box reads like the drawn paragraph: same size, line height and regular weight
                Tap(editor, window, 1);
                await Pump(window);
                var box = editor.Surface.EditBox!;
                Assert.Equal(20, box.FontSize);
                Assert.Equal(32, box.LineHeight);
                Assert.Equal(Avalonia.Media.FontWeight.Normal, box.FontWeight);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task Ux_EditorShots() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            view.Editor.Open(new CityNote("Tutorial/Diário.md", ShotNote));
            await Pump(window);
            Snapshot(window, "editor-reading.png");
            view.Margin = new Thickness(0, 0, 0, 330);   // the keyboard
            await Pump(window);
            Tap(view.Editor, window, 1, x: 120);
            await Pump(window);
            Snapshot(window, "editor-editing.png");
            var box = view.Editor.Surface.EditBox!;
            var word = box.Text!.IndexOf("neblina", StringComparison.Ordinal);
            box.SelectionStart = word;
            box.SelectionEnd = word + "neblina".Length;
            await Pump(window);
            Snapshot(window, "editor-selection.png");
            view.Editor.FinishWriting();
            view.Margin = default;
            await Pump(window);
            var more = view.Editor.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Mais ações");
            more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Pump(window);
            Assert.True(view.ActionSheet.IsOpen);
            Snapshot(window, "editor-more.png");
            window.Close();
            return true;
        }, CancellationToken.None));
}
