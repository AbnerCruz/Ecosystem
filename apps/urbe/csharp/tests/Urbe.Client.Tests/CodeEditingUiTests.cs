using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // CST-141: in a code file Enter keeps the indentation, Tab indents and a closing brace goes back a level
    [Fact]
    public async Task Code_TheKeysOfACodeFileKeepItsIndentation() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 1000, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync([new CityNote("app.js", "function f() {}"), new CityNote("Nota.md", "- item")]);
            await Settle(window, view);
            view.OpenNoteByPath("app.js");
            await Settle(window, view);
            var box = view.Editor.Editor;
            box.Focus();
            box.CaretIndex = "function f() {".Length;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("function f() {\n    \n}", box.Text);
            Assert.Equal("function f() {\n    ".Length, box.CaretIndex);
            window.KeyTextInput("return 1;");
            window.KeyTextInput("\n");   // a phone's keyboard sends the line break as text
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("function f() {\n    return 1;\n    \n}", box.Text);
            window.KeyTextInput("}");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("function f() {\n    return 1;\n}\n}", box.Text);   // the brace went back a level
            box.SelectionStart = 0; box.SelectionEnd = box.Text!.IndexOf('\n');
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("    function f() {", box.Text);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("function f() {", box.Text);

            // a note keeps its own keys: Enter in a list goes on with the list as before
            view.OpenNoteByPath("Nota.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            box.Focus();
            box.CaretIndex = box.Text!.Length;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("    ", box.Text);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
