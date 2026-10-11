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
        target.RaiseEvent(new PointerPressedEventArgs(target, new Pointer(1, PointerType.Mouse, true), window, new Point(2, 2), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
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
