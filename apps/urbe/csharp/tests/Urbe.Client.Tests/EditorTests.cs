using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>The native note editor (UC-18): modes, Fonte toolbar, Visual blocks, wikilink suggestions, autosave. Headless: not device validation (NN-017).</summary>
public sealed class EditorTests
{
    private static HeadlessUnitTestSession Session => HeadlessUiTests.Session;

    private static (EditorOverlay Editor, Window Window) Show(string path = "Notas/x.md", string text = "linha um\nlinha dois", IEnumerable<CityNote>? notes = null)
    {
        var editor = new EditorOverlay { Notes = notes is null ? null : () => notes };
        var window = new Window { Width = 800, Height = 700, Content = editor };
        window.Show();
        editor.Open(new CityNote(path, text));
        return (editor, window);
    }

    private static IEnumerable<T> All<T>(Visual v) where T : class => v.GetVisualDescendants().OfType<T>();

    private static void Click(Visual root, string tip)
    {
        TopLevel.GetTopLevel(root)?.UpdateLayout();   // templates of freshly shown controls
        var b = All<Button>(root).First(x => ToolTip.GetTip(x) as string == tip);
        b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Fonte_ToolbarInsertsAtTheCursorLikeTheOriginal() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var (editor, _) = Show();
        editor.SetMode(EditorMode.Fonte);
        editor.Editor.CaretIndex = 12;
        Click(editor, "Título 2");
        Assert.Equal("linha um\nlin## ha dois", editor.Editor.Text);      // insertAtCursor: at the caret, not at the line start
        editor.Editor.Text = "linha um";
        editor.Editor.SelectionStart = 0;
        editor.Editor.SelectionEnd = 5;
        Click(editor, "Itálico");
        Assert.Equal("_linha_ um", editor.Editor.Text);
        Assert.Equal(7, editor.Editor.CaretIndex);
        editor.Editor.SelectionStart = editor.Editor.SelectionEnd = 0;
        Click(editor, "Link externo");
        Assert.StartsWith("[](url)", editor.Editor.Text);
        Assert.Equal("MD · 1 linhas · 2 palavras", All<TextBlock>(editor).First(t => t.Text?.StartsWith("MD ·") == true).Text);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Fonte_WikilinkSuggestionsCompleteTheNameLikeTheOriginal() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var notes = new[] { new CityNote("Notas/x.md", "x"), new CityNote("Notas/Ideias.md", "plano"), new CityNote("Notas/Receitas.md", "bolo") };
        var (editor, _) = Show(notes: notes, text: "ver ");
        editor.SetMode(EditorMode.Fonte);
        editor.Editor.Text = "ver [[Ide";
        editor.Editor.CaretIndex = editor.Editor.Text.Length;
        Dispatcher.UIThread.RunJobs();
        var item = All<Button>(editor).First(b => b.Content is TextBlock { Text: "Ideias" });
        Assert.DoesNotContain(All<Button>(editor), b => b.Content is TextBlock { Text: "x" });   // the open note is not suggested
        item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ver [[Ideias]]", editor.Editor.Text);
        Assert.Equal(editor.Editor.Text.Length, editor.Editor.CaretIndex);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Fonte_WikilinksAndEmbedsFindEveryFileAndAFileLinkOpensIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-040/041: [[…]] and ![[…]] list images, PDFs and other files, not only notes; a file link opens the file
        var notes = new[] { new CityNote("Notas/x.md", "x"), new CityNote("Notas/Retratos de família.md", "texto") };
        var (editor, _) = Show(notes: notes, text: "ver ");
        string? opened = null;
        editor.Files = () => ["Imagens/retrato.png", "Personagens/retrato.png", "Livros/Calculo.pdf"];
        editor.OpenFile = path => { opened = path; return Task.CompletedTask; };
        editor.SetMode(EditorMode.Fonte);
        editor.Editor.Text = "ver ![[retr";
        editor.Editor.CaretIndex = editor.Editor.Text.Length;
        Dispatcher.UIThread.RunJobs();
        var names = All<Button>(editor).Select(b => (b.Content as TextBlock)?.Text).Where(t => t is not null).ToList();
        Assert.Contains("Retratos de família", names);
        Assert.Contains("Imagens/retrato.png", names);       // two files share the name: their paths
        Assert.Contains("Personagens/retrato.png", names);
        All<Button>(editor).First(b => b.Content is TextBlock { Text: "Imagens/retrato.png" }).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ver ![[Imagens/retrato.png]]", editor.Editor.Text);
        await editor.FollowLinkAsync("Calculo.pdf");
        Assert.Equal("Livros/Calculo.pdf", opened);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Visual_RendersTheNoteAndEditsOneBlockAtATime() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var (editor, window) = Show(text: "---\ntags: [a]\n---\n# Título\n\nUm **forte** e [[Outra]].\n\n- a\n- [x] b\n\n| h | i |\n|---|---|\n| 1 | 2 |\n\n> [!tip] Dica\n> corpo\n\n$$x^2$$\n");
        Assert.Equal(EditorMode.Visual, editor.Mode);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        var snap = Environment.GetEnvironmentVariable("URBE_SNAPSHOT_DIR");
        if (!string.IsNullOrEmpty(snap)) { Directory.CreateDirectory(snap); window.CaptureRenderedFrame()!.Save(Path.Combine(snap, "native-editor-visual.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); }
        Assert.Contains(All<TextBlock>(editor.Surface), t => t.Inlines?.Any(i => i is Avalonia.Controls.Documents.Run { Text: "Título" }) == true || t.Text == "Título");
        Assert.Empty(All<TextBox>(editor.Surface));

        editor.Surface.Run("bold");                                    // nothing is being edited: no-op
        var blocks = MarkdownEngine.SplitBlocks(editor.Surface.Text);
        Assert.Equal(["frontmatter", "heading", "paragraph", "list", "table", "quote", "paragraph"], blocks.Select(b => b.Kind));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Visual_TappingABlockEditsItsMarkdownAndWritesBackToTheNote() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var (editor, window) = Show(text: "# Título\n\nprimeiro\n\nsegundo\n");
        var holders = editor.Surface.Children.OfType<Border>().Where(b => b.MinHeight == 0).ToList();
        // tap the second rendered block ("primeiro")
        var target = holders[1];
        Dispatcher.UIThread.RunJobs();
        // a finger's tap (down and up in place)
        var at = target.TranslatePoint(new Point(10, target.Bounds.Height / 2), window)!.Value;
        var finger = window.TouchBegin(at, RawInputModifiers.None);
        window.TouchEnd(finger, at, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var box = All<TextBox>(editor.Surface).Single();
        Assert.Equal("primeiro", box.Text);
        box.Text = "primeiro editado";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("# Título\n\nprimeiro editado\n\nsegundo\n", editor.Editor.Text);

        // the selection bubble and the tools act on the block
        box.SelectionStart = 0; box.SelectionEnd = 8;
        Dispatcher.UIThread.RunJobs();
        Click(editor.Surface, "Negrito");
        Assert.Equal("# Título\n\n**primeiro** editado\n\nsegundo\n", editor.Editor.Text);

        editor.Surface.FinishEdit();
        Assert.Empty(All<TextBox>(editor.Surface));
        // back to Fonte shows the same text
        editor.SetMode(EditorMode.Fonte);
        Assert.Equal("# Título\n\n**primeiro** editado\n\nsegundo\n", editor.Editor.Text);
        return true;
    }, CancellationToken.None));

    /// <summary>
    /// A finger that lands on a block and drags scrolls the note (the 1.8.4's preview scrolls under the finger); only a
    /// tap turns the block into its edit box. Before, the press alone opened the box under the finger and the scroll
    /// stuck.
    /// </summary>
    [Fact]
    public async Task Visual_DraggingAFingerOverTheBlocksScrollsAndDoesNotEdit() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => "Parágrafo " + i + " com algum texto para ocupar a linha."));
        var (editor, window) = Show(text: text);
        Dispatcher.UIThread.RunJobs();
        var scroll = editor.Surface.GetVisualAncestors().OfType<ScrollViewer>().First();
        Assert.Equal(0, scroll.Offset.Y);
        var block = editor.Surface.Children.OfType<Border>().Where(b => b.MinHeight == 0).ElementAt(3);
        var start = block.TranslatePoint(new Point(40, block.Bounds.Height / 2), window)!.Value;
        var finger = window.TouchBegin(start, RawInputModifiers.None);
        for (int i = 1; i <= 12; i++)
        {
            window.TouchMove(finger, new Point(start.X, start.Y - i * 25), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        window.TouchEnd(finger, new Point(start.X, start.Y - 300), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(All<TextBox>(editor.Surface));                 // no block went into editing
        Assert.True(scroll.Offset.Y > 150, "rolou " + scroll.Offset.Y);
        return true;
    }, CancellationToken.None));

    /// <summary>In Fonte the same: a finger dragged over the text scrolls it.</summary>
    [Fact]
    public async Task Source_DraggingAFingerScrolls() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => "Parágrafo " + i + " com algum texto para ocupar a linha."));
        var (editor, window) = Show(text: text);
        editor.SetMode(EditorMode.Fonte);
        Dispatcher.UIThread.RunJobs();
        var scroll = editor.Editor.GetVisualAncestors().OfType<ScrollViewer>().First();
        var start = editor.Editor.TranslatePoint(new Point(60, 300), window)!.Value;
        var finger = window.TouchBegin(start, RawInputModifiers.None);
        for (int i = 1; i <= 12; i++)
        {
            window.TouchMove(finger, new Point(start.X, start.Y - i * 25), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        window.TouchEnd(finger, new Point(start.X, start.Y - 300), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(scroll.Offset.Y > 150, "rolou " + scroll.Offset.Y + " seleção " + editor.Editor.SelectionStart + "-" + editor.Editor.SelectionEnd);
        Assert.Equal(editor.Editor.SelectionStart, editor.Editor.SelectionEnd);   // the drag selected nothing

        // a flick: the note glides on after the finger leaves (released right after the last move, at about
        // 1 px/ms, so the glide is long enough on a slow machine and never runs into the end of the note)
        scroll.Offset = default;
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(TouchIntent.SettleMs + 50);
        var flickFrom = editor.Editor.TranslatePoint(new Point(60, 400), window)!.Value;
        var flick = window.TouchBegin(flickFrom, RawInputModifiers.None);
        for (int i = 1; i <= 4; i++)
        {
            await Task.Delay(30);
            window.TouchMove(flick, new Point(flickFrom.X, flickFrom.Y - i * 30), RawInputModifiers.None);
        }
        window.TouchEnd(flick, new Point(flickFrom.X, flickFrom.Y - 120), RawInputModifiers.None);
        var released = scroll.Offset.Y;
        // the glide's first frame comes from a timer: a slow machine may take longer than a frame to run it
        for (int i = 0; i < 20 && scroll.Offset.Y <= released; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        Assert.True(scroll.Offset.Y > released, $"a nota não deslizou ({released} → {scroll.Offset.Y})");

        // the note still glides: a tap only stops it (no caret, so no keyboard), as on a phone (22/10/2026)
        var caretBefore = editor.Editor.CaretIndex;
        var stopAt = editor.Editor.TranslatePoint(new Point(30, scroll.Offset.Y + 100), window)!.Value;
        var stop = window.TouchBegin(stopAt, RawInputModifiers.None);
        window.TouchEnd(stop, stopAt, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(caretBefore, editor.Editor.CaretIndex);
        await Task.Delay(TouchIntent.SettleMs + 80);
        Dispatcher.UIThread.RunJobs();

        // once still, a tap puts the caret under the finger
        var tapAt = editor.Editor.TranslatePoint(new Point(30, scroll.Offset.Y + 100), window)!.Value;
        var tap = window.TouchBegin(tapAt, RawInputModifiers.None);
        window.TouchEnd(tap, tapAt, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.Editor.IsFocused);
        Assert.Equal(TouchTextScroll.IndexAt(editor.Editor, new Point(30, scroll.Offset.Y + 100)), editor.Editor.CaretIndex);
        Assert.True(editor.Editor.CaretIndex > 100, "cursor " + editor.Editor.CaretIndex);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task AutosaveWritesThroughTheVaultHostAndReportsHonestly() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var editor = new EditorOverlay { SaveOnClose = true };
        var window = new Window { Width = 800, Height = 500, Content = editor };
        window.Show();
        string? written = null;
        editor.SaveRequested += n => { written = n.Content; return Task.CompletedTask; };
        editor.Open(new CityNote("Notas/a.md", "um"));
        editor.SetMode(EditorMode.Fonte);
        editor.Editor.Text = "um dois";
        Assert.True(editor.HasChanges);
        for (var i = 0; i < 60 && written is null; i++) { await Task.Delay(50); Dispatcher.UIThread.RunJobs(); }
        Assert.Equal("um dois", written);
        Assert.False(editor.HasChanges);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task TypingCoalescesAutosavesAndAFlushWritesAtOnce() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // UC-48: one save per pause in typing, not one every 400 ms; leaving the app writes what is pending
        var editor = new EditorOverlay { SaveOnClose = true };
        var window = new Window { Width = 800, Height = 500, Content = editor };
        window.Show();
        var saves = new List<string>();
        editor.SaveRequested += n => { saves.Add(n.Content); return Task.CompletedTask; };
        editor.Open(new CityNote("Notas/a.md", "um"));
        editor.SetMode(EditorMode.Fonte);
        for (var i = 0; i < 8; i++)
        {
            editor.Editor.Text += " " + i;
            await Task.Delay(150);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.Empty(saves);
        await editor.FlushAsync();
        Assert.Equal(new[] { "um 0 1 2 3 4 5 6 7" }, saves);
        Assert.False(editor.HasChanges);
        // typing that never pauses is still written within the maximum wait
        var start = DateTime.UtcNow;
        while (saves.Count < 2 && DateTime.UtcNow - start < TimeSpan.FromSeconds(6))
        {
            editor.Editor.Text += ".";
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.Equal(2, saves.Count);
        window.Close();
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task NonMarkdownFilesOpenInFonteWithCodeTools() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var (editor, _) = Show("Personalização/tema.json", "{\"a\":[1,2]}");
        Assert.Equal(EditorMode.Fonte, editor.Mode);
        Dispatcher.UIThread.RunJobs();
        TopLevel.GetTopLevel(editor)!.UpdateLayout();
        var format = All<Button>(editor).First(b => b.Content as string == "{ } Formatar");
        format.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("{\n  \"a\": [\n    1,\n    2\n  ]\n}", editor.Editor.Text);
        Assert.StartsWith("JSON · 6 linhas", All<TextBlock>(editor).First(t => t.Text?.StartsWith("JSON ·") == true).Text);
        return true;
    }, CancellationToken.None));
}
