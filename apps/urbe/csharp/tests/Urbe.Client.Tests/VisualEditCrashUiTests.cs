using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// Owner's report (15/10/2026): "o editor crashando algumas vezes, quando tento escrever, apagar". In the Visual mode the
/// block being edited is shortened (backspace over a selection, the keyboard replacing a composed word) while its
/// selection still points past the new end; the tools under the caret must not read outside the text.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task VisualEdit_ShorteningTheTextWithASelectionDoesNotCrash() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var surface = new VisualSurface();
            var window = new Window { Width = 600, Height = 500, Content = surface };
            window.Show();
            try
            {
                surface.SetText("Um parágrafo com bastante texto para selecionar e apagar.\n");
                Dispatcher.UIThread.RunJobs();
                typeof(VisualSurface).GetMethod("Edit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(surface, [0]);
                Dispatcher.UIThread.RunJobs();
                var box = surface.GetLogicalDescendants().OfType<TextBox>().Single();
                box.SelectionStart = 10;
                box.SelectionEnd = box.Text!.Length;
                Dispatcher.UIThread.RunJobs();
                // the text shrinks under the selection, as Backspace or the IME do
                box.Text = "Um";
                Dispatcher.UIThread.RunJobs();
                box.Text = "";
                Dispatcher.UIThread.RunJobs();
                box.Text = "Novo texto";
                Dispatcher.UIThread.RunJobs();
                surface.FinishEdit();
                Assert.Equal("Novo texto\n", surface.Text);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}

/// <summary>Owner's report (UC-48): a tap on a paragraph needed a second tap to put the caret where the finger was.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task VisualEdit_TheCaretGoesWhereTheParagraphWasTappedAndADoubleTapWordIsFound() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var surface = new VisualSurface();
            var window = new Window { Width = 600, Height = 500, Content = surface };
            window.Show();
            try
            {
                const string text = "Um parágrafo com bastante texto para tocar no meio dele.";
                surface.SetText(text + "\n");
                Dispatcher.UIThread.RunJobs();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(VisualSurface).GetField("_tapAt", flags)!.SetValue(surface, (Avalonia.Point?)new Avalonia.Point(160, 14));
                typeof(VisualSurface).GetMethod("Edit", flags)!.Invoke(surface, [0]);
                await Pump(200);
                var box = surface.GetLogicalDescendants().OfType<TextBox>().Single();
                Assert.InRange(box.CaretIndex, 5, text.Length - 5);

                var wordAt = typeof(VisualSurface).GetMethod("WordAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                Assert.Equal((3, 12), ((int, int))wordAt.Invoke(null, [text, 6])!);
                Assert.Equal((3, 12), ((int, int))wordAt.Invoke(null, [text, 12])!);
                surface.FinishEdit();
                Assert.Equal(text + "\n", surface.Text);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}

/// <summary>Owner (UC-49): palavra{"comentário"} shows a 💬 over the word; a tap shows the comments and adds one.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task VisualEdit_WordCommentsShowAMarkThatListsAndAddsComments() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var surface = new VisualSurface();
            var window = new Window { Width = 600, Height = 500, Content = surface };
            window.Show();
            try
            {
                surface.SetText("Uma palavra{\"primeiro\"} aqui.\n");
                IReadOnlyList<string>? shown = null;
                surface.AskComment = list => { shown = list; return Task.FromResult<string?>("segundo \"x\""); };
                await Pump(60);
                // the mark is a run of the text (never a control in it, which clipped the words around it)
                var line = surface.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text?.Contains("💬") == true));
                Assert.Empty(line.Inlines!.OfType<Avalonia.Controls.Documents.InlineUIContainer>());
                Assert.True(MarkdownReader.TapCommentMark(line, 0));
                await Pump(60);
                Assert.Equal(["primeiro"], shown);
                Assert.Equal("Uma palavra{\"primeiro\"}{\"segundo \\\"x\\\"\"} aqui.\n", surface.Text);
                Assert.Empty(surface.GetLogicalDescendants().OfType<TextBox>());   // the block stays rendered
                Assert.Contains(surface.GetLogicalDescendants().OfType<TextBlock>(), t => t.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text?.Contains("💬2") == true));
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}

/// <summary>Owner (UC-49): 💬 Comentar a palavra, in the selection bubble, comments a word with no 💬 yet.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task VisualEdit_CommentsTheSelectedWordWithoutAMarkYet() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var surface = new VisualSurface();
            var window = new Window { Width = 600, Height = 500, Content = surface };
            window.Show();
            try
            {
                surface.SetText("Uma palavra aqui.\n");
                IReadOnlyList<string>? shown = null;
                surface.AskComment = list => { shown = list; return Task.FromResult<string?>("ver depois"); };
                await Pump(60);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(VisualSurface).GetMethod("Edit", flags)!.Invoke(surface, [0]);
                await Pump(100);
                var box = surface.GetLogicalDescendants().OfType<TextBox>().Single();
                box.SelectionStart = 4; box.SelectionEnd = 11;   // "palavra"
                surface.Run("wordComment");
                await Pump(60);
                Assert.Equal([], shown);
                // no selection: the word at the caret
                box.SelectionStart = box.SelectionEnd = box.Text!.IndexOf("aqui", StringComparison.Ordinal) + 2;
                surface.AskComment = list => { shown = list; return Task.FromResult<string?>("lugar"); };
                surface.Run("wordComment");
                await Pump(60);
                surface.FinishEdit();
                Assert.Equal("Uma palavra{\"ver depois\"} aqui{\"lugar\"}.\n", surface.Text);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
