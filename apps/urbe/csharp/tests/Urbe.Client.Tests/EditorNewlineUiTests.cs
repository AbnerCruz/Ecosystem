using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.TextInput;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed class EditorNewlineUiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VisualParagraphKeepsTheSameFocusedBoxAfterPhysicalOrSoftEnter(bool soft) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 360, Height = 640, Content = editor };
            window.Show();
            editor.Open(new CityNote("Nota.md", "Primeira"));
            var block = editor.Surface.BlockView(0)!;
            editor.Surface.TapBlock(0, block, block, new Point(20, 10), select: false);
            Dispatcher.UIThread.RunJobs();
            var box = editor.Surface.EditBox!;
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = box.Text!.Length;
            box.Focus();
            if (soft) window.KeyTextInput("\n");
            else window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyTextInput("Segunda");
            Dispatcher.UIThread.RunJobs();
            Assert.Same(box, editor.Surface.EditBox);
            Assert.True(box.IsFocused);
            Assert.Equal("Primeira\nSegunda", editor.Current!.Content.Replace("\r\n", "\n"));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ReturnInsideCodeDoesNotContinueAMarkdownList() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 360, Height = 640, Content = editor };
            window.Show();
            editor.Open(new CityNote("Codigo.md", "```text\n- exemplo\n```"));
            var block = editor.Surface.BlockView(0)!;
            editor.Surface.TapBlock(0, block, block, new Point(20, 10), select: false);
            Dispatcher.UIThread.RunJobs();
            var box = editor.Surface.EditBox!;
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = box.Text!.IndexOf("exemplo", StringComparison.Ordinal) + "exemplo".Length;
            box.Focus();
            window.KeyTextInput("\n");
            window.KeyTextInput("literal");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("- exemplo\nliteral\n```", editor.Current!.Content.Replace("\r\n", "\n"));
            Assert.Same(box, editor.Surface.EditBox);
            Assert.True(box.IsFocused);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task SourceExplicitlyRequestsReturnFromImeAndKeepsTypingAfterEnter() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 360, Height = 640, Content = editor };
            window.Show();
            editor.Open(new CityNote("Nota.md", "Primeira"));
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Focus();
            editor.Editor.CaretIndex = editor.Editor.Text!.Length;
            Assert.True(TextInputOptions.GetMultiline(editor.Editor));
            Assert.Equal(TextInputReturnKeyType.Return, TextInputOptions.GetReturnKeyType(editor.Editor));
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyTextInput("Segunda");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Primeira\nSegunda", editor.Editor.Text!.Replace("\r\n", "\n"));
            Assert.True(editor.Editor.IsFocused);
            Assert.Equal(EditorMode.Fonte, editor.Mode);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public async Task SoftKeyboardNewlineContinuesChecklistWithoutReplacingTheFocusedBox(string newline) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 360, Height = 640, Content = editor };
            window.Show();
            editor.Open(new CityNote("Tarefas.md", "- [x] tarefa"));
            var block = editor.Surface.BlockView(0)!;
            editor.Surface.TapBlock(0, block, block, new Point(30, 10), select: false);
            Dispatcher.UIThread.RunJobs();
            var box = editor.Surface.EditBox!;
            box.SelectionStart = box.SelectionEnd = box.CaretIndex = box.Text!.Length;
            box.Focus();
            window.KeyTextInput(newline);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(box, editor.Surface.EditBox);
            Assert.True(box.IsFocused);
            Assert.Equal("- [x] tarefa\n- [ ] ", box.Text!.Replace("\r\n", "\n"));
            Assert.True(TextInputOptions.GetMultiline(box));
            Assert.Equal(TextInputReturnKeyType.Return, TextInputOptions.GetReturnKeyType(box));
            window.KeyTextInput("seguinte");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("- [ ] seguinte", editor.Current!.Content);
            Assert.True(box.IsFocused);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
