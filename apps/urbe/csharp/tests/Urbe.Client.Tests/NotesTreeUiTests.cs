using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>The Notas tree of the 1.8.4 (mobile-ui.js render/flatten, model.js tree/expand) in the native client.</summary>
public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// Folders first, then notes, by name; a tap opens and closes a folder (kept on the device); a selected folder is
    /// where Nova creates ("Em “Bairro”."), and the new note gets its empty file and the editor; the search lists the
    /// notes with their folder; on a wide window Notas fills the screen too, and "Mostrar nas notas" puts it over the
    /// editor with Fechar.
    /// </summary>
    [Fact]
    public async Task Notes_IsTheExpandableTreeOfTheOriginal() =>
        Assert.True(await Session.Dispatch(NotesTreeScenario, CancellationToken.None));

    private static async Task<bool> NotesTreeScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-tree-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro", "Sub"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
        File.WriteAllText(Path.Combine(vault, "Árvore.md"), "# Árvore\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Sub", "Dois.md"), "# Dois\n");
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
            Assert.False(e.Overlaid);

            // root: the folder first, then the notes by name (localeCompare: Árvore before Comece)
            Assert.Equal(new[] { "Bairro" }, e.VisibleFolderPaths);
            Assert.Equal(new[] { "Árvore.md", "Comece.md" }, e.VisibleNotePaths);
            Assert.True(e.TapFolder("Bairro"));
            Assert.Equal(new[] { "Bairro", "Bairro/Sub" }, e.VisibleFolderPaths);
            Assert.Equal(new[] { "Bairro/Um.md", "Árvore.md", "Comece.md" }, e.VisibleNotePaths);
            Assert.True(e.TapFolder("Bairro/Sub"));
            Assert.Equal(new[] { "Bairro/Sub/Dois.md", "Bairro/Um.md", "Árvore.md", "Comece.md" }, e.VisibleNotePaths);
            Assert.True(e.TapFolder("Bairro"));                                   // closes, the sub-folder stays open inside
            Assert.Equal(new[] { "Árvore.md", "Comece.md" }, e.VisibleNotePaths);
            Assert.True(e.TapFolder("Bairro"));
            Snapshot(window, "native-notes-tree.png");

            // the open folders are on the device: a new view opens them again
            Assert.True(new ExplorerState(UrbeApp.LocalStatePath).IsExpanded("Bairro/Sub"));

            // search: "Buscar arquivos" by path or title, with the folder under the name
            e.SearchText = "dois";
            Assert.Equal(new[] { "Bairro/Sub/Dois.md" }, e.VisibleNotePaths);
            Assert.Contains(e.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Bairro/Sub");
            e.SearchText = "nada disso";
            Assert.Contains(e.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Nenhum arquivo com “nada disso”.");
            e.SearchText = "";

            // the floating selection bar
            Assert.True(e.HoldNote("Comece.md"));
            Dispatcher.UIThread.RunJobs();
            Snapshot(window, "native-notes-bar.png");
            e.ClearSelection();

            // Nova with Bairro selected: "Em “Bairro”.", the note is created there and opens
            Assert.True(e.HoldFolder("Bairro"));
            e.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Nova nota")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Em “Bairro”.");
            ClickButtonWithText(view.Dialogs, "Nova nota");
            await Settled();
            var input = view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single();
            input.Text = "Ideias";
            ClickButtonWithText(view.Dialogs, "Criar nota");
            await Settled();
            Assert.Equal("", File.ReadAllText(Path.Combine(vault, "Bairro", "Ideias.md")));
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Bairro/Ideias.md", view.Editor.Current?.Path);
            Assert.NotNull(view.Documents!.Get("Bairro/Ideias.md"));

            // wide: the Explorer fills the window; over the editor it has Fechar, which goes back to the note
            window.Width = 1280;
            await Settled();
            Assert.True(e.Bounds.Width > 1100);
            var close = e.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Fechar");
            Assert.False(close.IsVisible);
            view.ShowNotesOverEditor();
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.NotesOverEditor);
            Assert.True(close.IsVisible);
            Snapshot(window, "native-notes-wide.png");
            close.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.NotesOverEditor);
            Assert.True(view.Editor.IsVisible);
            view.Editor.IsVisible = false;
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
