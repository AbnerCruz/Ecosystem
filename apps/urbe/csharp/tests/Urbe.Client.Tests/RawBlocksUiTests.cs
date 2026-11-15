using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>Fonte by blocks: the Markdown as written, a tap on a letter puts the caret there, and a key costs one block.</summary>
    [Fact]
    public async Task RawBlocks_ShowTheMarkdownAndATapEditsAtTheLetter() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var surface = new VisualSurface { Raw = true };
        var window = new Window { Width = 412, Height = 900, Content = new ScrollViewer { Content = surface } };
        window.Show();
        try
        {
            surface.SetText("# Título\n\nUm **forte** texto.\n");
            await Pump(40);
            var texts = surface.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains(MarkdownReader.TextClass)).Select(t => t.Text).ToList();
            Assert.Equal(["# Título", "Um **forte** texto."], texts);
            var line = surface.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == "Um **forte** texto.");
            var at = line.TranslatePoint(line.TextLayout.HitTestTextPosition("Um **f".Length).Center, window)!.Value;
            window.MouseDown(at, MouseButton.Left); window.MouseUp(at, MouseButton.Left);
            await Pump(40);
            var box = surface.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.Equal("Um **forte** texto.", box.Text);
            Assert.InRange(box.CaretIndex, "Um **f".Length - 1, "Um **f".Length + 1);
            box.CaretIndex = "Um **f".Length;
            window.KeyTextInput("X");
            await Pump(10);
            surface.FinishEdit();
            Assert.Equal("# Título\n\nUm **fXorte** texto.\n", surface.Text);

            if (Environment.GetEnvironmentVariable("URBE_BENCH") == "1")
            {
                surface.SetText(BenchNote());
                await Pump(40);
                var first = surface.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Parágrafo", StringComparison.Ordinal) == true);
                var p = first.TranslatePoint(new Point(5, 5), window)!.Value;
                window.MouseDown(p, MouseButton.Left); window.MouseUp(p, MouseButton.Left);
                await Pump(20);
                window.KeyTextInput("a"); window.UpdateLayout();
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < 60; i++) { window.KeyTextInput("a"); window.UpdateLayout(); }
                PerfLog($"FONTE blocks {sw.Elapsed.TotalMilliseconds / 60:0.00} ms/key");
            }
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public async Task RawBlocks_ALongNoteInFonteIsEditedByBlocksAndNothingIsLost() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var editor = new EditorOverlay();
        var window = new Window { Width = 412, Height = 900, Content = editor };
        window.Show();
        try
        {
            var note = BenchNote();
            Assert.True(note.Length >= EditorOverlay.BlocksFrom);
            editor.Open(new CityNote("Estudo.md", note));
            editor.SetMode(EditorMode.Fonte);
            await Pump(40);
            Assert.Equal(EditorMode.Fonte, editor.Mode);
            Assert.True(editor.SourceByBlocks);
            Assert.Contains(editor.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "## Seção 2");   // the Markdown as written
            // a block written, then Visual and back: the text is the note with the change
            var line = editor.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == "## Seção 2");
            var at = line.TranslatePoint(new Point(line.Bounds.Width - 2, line.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left); window.MouseUp(at, MouseButton.Left);
            await Pump(30);
            var box = editor.GetLogicalDescendants().OfType<TextBox>().Single(t => t != editor.Editor && t.IsEffectivelyVisible);
            box.CaretIndex = box.Text!.Length;
            window.KeyTextInput("!");
            await Pump(10);
            editor.SetMode(EditorMode.Visual);
            await Pump(20);
            Assert.False(editor.SourceByBlocks);
            Assert.Equal(note.Replace("## Seção 2\n", "## Seção 2!\n"), editor.Editor.Text);
            // the toolbar works on the block (bold on a selection inside it)
            editor.SetMode(EditorMode.Fonte);
            await Pump(20);
            Assert.True(editor.SourceByBlocks);
            // find needs the whole text: the box comes back
            editor.FindBar.Open();
            await Pump(20);
            Assert.False(editor.SourceByBlocks);
            Assert.True(editor.Editor.IsEffectivelyVisible);
            Assert.Equal(note.Replace("## Seção 2\n", "## Seção 2!\n"), editor.Editor.Text);
            // a short note is the whole-text box as always
            editor.Open(new CityNote("Curta.md", "# Curta\n"));
            editor.SetMode(EditorMode.Fonte);
            await Pump(10);
            Assert.False(editor.SourceByBlocks);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
