using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-38 (DEC-0045): the Notas screen of the 1.8.4 becomes the Explorer the owner asked for — every file with its
/// extension and an icon, options on folders, and a held note dragged onto a folder moves there.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Theory]
    [InlineData("Casa.md", UrbeTheme.Icons.File)]
    [InlineData("a/foto.PNG", UrbeTheme.Icons.Image)]
    [InlineData("livro.pdf", UrbeTheme.Icons.Book)]
    [InlineData("dados.json", UrbeTheme.Icons.FileCode)]
    [InlineData("Capa.page.json", UrbeTheme.Icons.Page)]
    [InlineData("musica.mp3", UrbeTheme.Icons.FileOther)]
    public void Explorer_IconByExtension(string path, string icon) => Assert.Equal(icon, NotesExplorer.IconFor(path));

    [Fact]
    public async Task Explorer_ShowsEveryFileHasFolderOptionsAndMovesByDragging() =>
        Assert.True(await Session.Dispatch(ExplorerScenario, CancellationToken.None));

    private static async Task<bool> ExplorerScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-explorer-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
        File.WriteAllBytes(Path.Combine(vault, "Bairro", "foto.png"), [137, 80, 78, 71]);
        File.WriteAllBytes(Path.Combine(vault, "livro.pdf"), [37, 80, 68, 70]);
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilNotesSaved(view);
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();
            var e = view.Explorer;
            Assert.Equal("Notas", view.ActiveDestination);
            // the base of the app: its header carries the vault folder's name
            Assert.Contains(e.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "vault");

            // every file, with its extension; the hidden .urbe stays out
            Assert.Equal(new[] { "livro.pdf" }, e.VisibleFilePaths);
            Assert.Contains(e.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Comece.md");
            Assert.Contains(e.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "livro.pdf");
            Assert.True(e.TapFolder("Bairro"));
            Assert.Equal(new[] { "Bairro/foto.png", "livro.pdf" }, e.VisibleFilePaths);
            Assert.DoesNotContain(e.VisibleFilePaths, p => p.StartsWith(".urbe", StringComparison.Ordinal));
            Snapshot(window, "native-explorer.png");

            // a file that is not a note opens its preview (openFilePreview): a PDF offers Abrir in another app
            Assert.True(e.TryOpenFile("livro.pdf"));
            for (int i = 0; i < 40 && view.FilePreview.Current != "livro.pdf"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            await Settled();
            Assert.True(view.FilePreview.IsVisible);
            Assert.Equal("livro.pdf", view.FilePreview.Current);
            Assert.Contains(view.FilePreview.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").StartsWith("PDF · 4 B · livro.pdf", StringComparison.Ordinal));
            view.FilePreview.Close();
            await Settled();

            // the "⋯" of a folder: its options, and Nova nota aqui creates the note in it
            Assert.True(e.OpenFolderMenu("Bairro"));
            Dispatcher.UIThread.RunJobs();
            var options = view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            foreach (var o in new[] { "Nova nota aqui", "Nova subpasta", "Renomear", "Mover para", "Mover para a lixeira" }) Assert.Contains(o, options);
            ClickButtonWithText(view.Dialogs, "Nova nota aqui");
            await Settled();
            view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Lá";
            ClickButtonWithText(view.Dialogs, "Criar nota");
            await Settled();
            Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Lá.md")));
            view.Editor.IsVisible = false;
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();

            // hold Comece.md, drag it onto Bairro and let go: it moves there (file, house and neighbourhood)
            e.HoldDelay = TimeSpan.FromMilliseconds(5);
            await Settled();
            Control RowOf(string text) => e.GetVisualDescendants().OfType<Button>()
                .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text) && b.MinHeight == 48);
            Point Centre(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            var from = Centre(RowOf("Comece.md"));
            window.MouseDown(from, MouseButton.Left);
            await Task.Delay(40);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Comece.md", e.SelectedPaths);
            var to = Centre(RowOf("Bairro"));
            window.MouseMove(new Point(from.X, (from.Y + to.Y) / 2), RawInputModifiers.LeftMouseButton);
            window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.True(e.Dragging);
            window.MouseUp(to, MouseButton.Left);
            await Settled();
            Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Comece.md")));
            Assert.False(File.Exists(Path.Combine(vault, "Comece.md")));
            Assert.NotNull(view.Documents!.Get("Bairro/Comece.md"));
            Assert.Empty(e.SelectedPaths);
            await UntilNotesSaved(view);
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
