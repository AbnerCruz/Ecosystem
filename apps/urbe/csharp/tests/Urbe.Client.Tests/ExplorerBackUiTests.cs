using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-010 in Notas: Voltar closes what is going on there first — a selection, then the Lixeira — and only
/// then Notas itself; it never leaves the app with a selection half made.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Back_InNotasClosesTheSelectionThenTheTrashThenNotas() => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-back-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "A.md"), "# A\n");
        File.WriteAllText(Path.Combine(vault, "B.md"), "# B\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "s.json");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.NotesOverEditor);
            Assert.True(view.Explorer.HoldNote("A.md"));
            Assert.True(view.Explorer.TryOpenNote("B.md"));
            Assert.Equal(2, view.Explorer.SelectedPaths.Count);

            Assert.True(view.HandleSystemBack());          // the selection ends, Notas stays
            Assert.Empty(view.Explorer.SelectedPaths);
            Assert.True(view.NotesOverEditor);

            view.ShowTrashView();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Lixeira", view.Explorer.Title);
            Assert.True(view.HandleSystemBack());          // the Lixeira goes back to the tree
            Assert.NotEqual("Lixeira", view.Explorer.Title);
            Assert.True(view.NotesOverEditor);

            Assert.True(view.HandleSystemBack());          // then Notas closes
            Assert.False(view.NotesOverEditor);
            return true;
        }
        finally
        {
            window.Close();
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }, CancellationToken.None));
}
