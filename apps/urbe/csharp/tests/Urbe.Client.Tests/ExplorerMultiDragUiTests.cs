using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-037: two notes selected, one of them held and dragged onto a folder: both move, and nothing opens. A hold on
    /// a row already selected keeps it in the selection (the drag carried only the others before).
    /// </summary>
    [Fact]
    public async Task Explorer_DraggingOneOfASelectionMovesTheWholeSelection() => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-multidrag-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
        File.WriteAllText(Path.Combine(vault, "Um.md"), "# Um\n");
        File.WriteAllText(Path.Combine(vault, "Dois.md"), "# Dois\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Casa.md"), "# Casa\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilNotesSaved(view);
            view.ShowNotes();
            var e = view.Explorer;
            e.HoldDelay = TimeSpan.FromMilliseconds(5);
            await Settled();
            Control RowOf(string text) => e.GetVisualDescendants().OfType<Button>()
                .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text) && b.MinHeight == 48);
            Point Centre(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            Assert.True(e.HoldNote("Um.md"));
            Assert.True(e.TryOpenNote("Dois.md"));   // while selecting, a tap adds
            await Settled();
            Assert.Equal(["Dois.md", "Um.md"], e.SelectedPaths.Order(StringComparer.Ordinal));
            // hold Dois (already selected) and drag it onto Bairro
            var from = Centre(RowOf("Dois.md"));
            window.MouseDown(from, MouseButton.Left);
            await Task.Delay(40);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Dois.md", e.SelectedPaths);
            var to = Centre(RowOf("Bairro"));
            window.MouseMove(new Point(from.X, (from.Y + to.Y) / 2), RawInputModifiers.LeftMouseButton);
            window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.True(e.Dragging);
            window.MouseUp(to, MouseButton.Left);
            for (int i = 0; i < 100 && !File.Exists(Path.Combine(vault, "Bairro", "Um.md")); i++) await Settled();
            Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
            Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Dois.md")));
            Assert.Null(view.Editor.Current);   // nothing was opened by the hold or the drop
            await UntilNotesSaved(view);
            return true;
        }
        finally
        {
            window.Close();
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
