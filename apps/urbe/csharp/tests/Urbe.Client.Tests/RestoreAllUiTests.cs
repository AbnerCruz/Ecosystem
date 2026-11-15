using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Restaurar tudo, in the Lixeira: a folder deleted by mistake comes back whole, each note to where it was
/// (its folder made again); a note whose place was taken meanwhile stays in the Lixeira and the person is told.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Trash_RestoreAllPutsEveryNoteBackAndKeepsTheOnesWhosePlaceIsTaken() => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-restoreall-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Estudos", "Física"));
        File.WriteAllText(Path.Combine(vault, "Estudos", "Física", "Aula 1.md"), "# Aula 1\n");
        File.WriteAllText(Path.Combine(vault, "Estudos", "Física", "Aula 2.md"), "# Aula 2\n");
        File.WriteAllText(Path.Combine(vault, "Estudos", "Plano.md"), "# Plano\n");
        File.WriteAllText(Path.Combine(vault, "Solta.md"), "# Solta\n");
        File.WriteAllText(Path.Combine(vault, "Fica.md"), "# Fica\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "s.json");
        async Task Settled() { for (int i = 0; i < 60; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilNotesSaved(view);
            // the folder Estudos and Solta.md go to the Lixeira
            var deleting = view.SelectionActionAsync("delete", [NotesExplorer.FolderPrefix + "Estudos", "Solta.md"]);
            await Settled();
            ClickButtonWithText(view.Dialogs, "Excluir");
            await deleting;
            await Settled();
            Assert.False(Directory.Exists(Path.Combine(vault, "Estudos")));
            Assert.False(File.Exists(Path.Combine(vault, "Solta.md")));
            // another app writes a note where Solta.md was; coming back shows it
            File.WriteAllText(Path.Combine(vault, "Solta.md"), "# Outra solta\n");
            await view.RefreshFromDiskAsync();
            await Settled();

            view.ShowNotes();
            view.ShowTrashView();
            await Settled();
            ClickButtonWithText(view.Explorer, "Restaurar tudo");
            for (int i = 0; i < 300 && !File.Exists(Path.Combine(vault, "Estudos", "Plano.md")); i++) await Settled();
            await Settled();

            Assert.Equal("# Aula 1\n", File.ReadAllText(Path.Combine(vault, "Estudos", "Física", "Aula 1.md")));
            Assert.Equal("# Aula 2\n", File.ReadAllText(Path.Combine(vault, "Estudos", "Física", "Aula 2.md")));
            Assert.Equal("# Plano\n", File.ReadAllText(Path.Combine(vault, "Estudos", "Plano.md")));
            Assert.Equal("# Outra solta\n", File.ReadAllText(Path.Combine(vault, "Solta.md")));   // never overwritten
            Assert.NotNull(view.Documents!.Get("Estudos/Física/Aula 1.md"));
            Assert.Contains("Estudos/Física", view.Library!.Folders);
            // only the note whose place was taken stays in the Lixeira, on disk too
            Assert.Equal(["Solta.md"], view.TrashOriginalPaths());
            var trash = File.ReadAllText(Path.Combine(vault, ".urbe", "trash.v2.json"));
            Assert.DoesNotContain("Aula 1", trash);
            Assert.Contains("Solta", trash);
            Assert.Contains("ficou na Lixeira", view.NoticeText ?? "");
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
