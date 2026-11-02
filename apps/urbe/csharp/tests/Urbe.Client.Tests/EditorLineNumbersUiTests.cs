using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task EditorLineNumbersFollowLogicalLinesAcrossWrapAndEdits() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 320, Height = 780, Content = editor };
            window.Show();
            var text = "Linha um longa " + new string('a', 150) + "\nLinha dois\n\nLinha quatro";
            editor.Open(new CityNote("Linhas.md", text));
            editor.SetMode(EditorMode.Fonte);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, editor.LineNumbers.LineStarts.Count);
            Assert.Equal(text.IndexOf('\n') + 1, editor.LineNumbers.LineStarts[1]);
            Assert.True(editor.LineNumbers.Bounds.Width >= 28);
            Snapshot(window, "native-editor-line-numbers-320.png");
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = text.Length;
            editor.InsertText("\nLinha cinco");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, editor.LineNumbers.LineStarts.Count);
            editor.Undo();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(text, editor.Current!.Content);
            Assert.Equal(4, editor.LineNumbers.LineStarts.Count);
            editor.SetMode(EditorMode.Visual);
            Assert.Equal(text, editor.Current.Content);
            window.Width = 1280;
            editor.SetMode(EditorMode.Fonte);
            window.UpdateLayout();
            Snapshot(window, "native-editor-line-numbers-desktop.png");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
