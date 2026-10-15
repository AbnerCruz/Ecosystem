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
