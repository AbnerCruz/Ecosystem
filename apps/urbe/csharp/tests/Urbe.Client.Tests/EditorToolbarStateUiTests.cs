using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-018: a lit button is the Markdown under the caret, and pressing it takes that Markdown off, in Visual and
    /// in Fonte alike; the button goes dark after.
    /// </summary>
    [Fact]
    public async Task Editor_TheBarShowsAndTogglesTheMarkdownUnderTheCaretInBothModes() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = StandaloneEditor(ShotNote);
            Button Tip(string tip) => editor.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == tip && !b.GetLogicalAncestors().Contains(editor.Surface));
            void Press(Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            try
            {
                await Pump(window);
                // Visual: inside **negrito**, Negrito is lit; pressing it leaves the plain word, and the button dark
                Tap(editor, window, 5);
                await Pump(window);
                var box = editor.Surface.EditBox!;
                box.CaretIndex = box.Text!.IndexOf("negrito", StringComparison.Ordinal) + 2;
                await Pump(window);
                Assert.Contains("bold", editor.ActiveTools);
                Assert.DoesNotContain("italic", editor.ActiveTools);
                Press(Tip("Negrito"));
                await Pump(window);
                Assert.Contains(", com negrito,", editor.Surface.EditBox!.Text);
                Assert.DoesNotContain("bold", editor.ActiveTools);
                Assert.Equal(Avalonia.Media.Brushes.Transparent, Tip("Negrito").Background);
                // CST-020: `código` off and on again, the word still selected each time, nothing added around it
                box = editor.Surface.EditBox!;
                int at = box.Text!.IndexOf("código", StringComparison.Ordinal);
                (box.SelectionStart, box.SelectionEnd) = (at, at + "código".Length);
                await Pump(window);
                Assert.Contains("code", editor.ActiveTools);
                Press(Tip("Código"));
                await Pump(window);
                box = editor.Surface.EditBox!;
                Assert.Contains(" e código no meio", box.Text);
                Assert.Equal("código", box.SelectedText);
                Press(Tip("Código"));
                await Pump(window);
                box = editor.Surface.EditBox!;
                Assert.Contains(" e `código` no meio", box.Text);
                Assert.Equal("código", box.SelectedText);
                Assert.DoesNotContain("\n\n", box.Text!.TrimEnd());
                editor.FinishWriting();
                await Pump(window);

                // Fonte: the same rule, read from the whole note under the caret
                editor.SetMode(EditorMode.Fonte);
                await Pump(window);
                var source = editor.Editor;
                Assert.DoesNotContain("**negrito**", source.Text);
                source.CaretIndex = source.Text!.IndexOf("Ideias", StringComparison.Ordinal) + 1;
                await Pump(window);
                Assert.Contains("h2", editor.ActiveTools);
                Press(Tip("Título 2"));
                await Pump(window);
                Assert.Contains("\nIdeias\n", source.Text);
                Assert.DoesNotContain("h2", editor.ActiveTools);
                source.CaretIndex = source.Text!.IndexOf("Revisar", StringComparison.Ordinal);
                await Pump(window);
                Assert.Contains("task", editor.ActiveTools);
                Assert.DoesNotContain("list", editor.ActiveTools);   // a task is not lit as a plain list too
                source.CaretIndex = source.Text!.IndexOf("se lê", StringComparison.Ordinal);
                await Pump(window);
                Assert.Contains("quote", editor.ActiveTools);
                // Fonte keeps the selection too when a word becomes code
                at = source.Text!.IndexOf("neblina", StringComparison.Ordinal);
                (source.SelectionStart, source.SelectionEnd) = (at, at + "neblina".Length);
                await Pump(window);
                Press(Tip("Código"));
                await Pump(window);
                Assert.Contains("com `neblina`,", source.Text);
                Assert.Equal("neblina", source.SelectedText);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
