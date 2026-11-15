using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>The app is the folder, 1:1 (observation of 15/11/2026): what changes on disk outside the app shows when the
/// app comes back, and a folder that is gone from the disk is gone from the app, even if the record still lists it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Vault_FoldersDeletedOrCreatedOutsideTheAppFollowTheDisk() =>
        Assert.True(await Session.Dispatch(DiskMirrorScenario, CancellationToken.None));

    private static async Task<bool> DiskMirrorScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-mirror-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Velha", "Dentro"));
        Directory.CreateDirectory(Path.Combine(vault, "Vazia"));
        Directory.CreateDirectory(Path.Combine(vault, "Fica"));
        Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        File.WriteAllText(Path.Combine(vault, "Velha", "Dentro", "Nota.md"), "# Nota\n");
        File.WriteAllText(Path.Combine(vault, "Fica", "Plano.md"), "# Plano\n");
        // the record still lists a folder deleted before this opening: the disk wins
        File.WriteAllText(Path.Combine(vault, ".urbe", "notas.json"), "{\"versao\":1,\"notas\":{},\"pastas\":[\"Fantasma\",\"Fantasma/Sub\",\"Vazia\"]}");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            await UntilNotesSaved(view);
            Assert.DoesNotContain("Fantasma", view.Library!.Folders);
            Assert.DoesNotContain("Fantasma/Sub", view.Library.Folders);
            Assert.Contains("Vazia", view.Library.Folders);
            Assert.Contains("Velha/Dentro", view.Library.Folders);
            // the record written after the opening has only the folders on disk
            var record = File.ReadAllText(Path.Combine(vault, ".urbe", "notas.json"));
            Assert.DoesNotContain("Fantasma", record);

            // each folder of the tree tells how many notes it holds, its subfolders included
            view.ShowNotes();
            Assert.Equal(1, view.Explorer.NoteCountOf("Velha"));
            Assert.Equal(1, view.Explorer.NoteCountOf("Fica"));
            Assert.Equal(0, view.Explorer.NoteCountOf("Vazia"));

            // nothing changed: coming back does not rebuild the vault
            Assert.False(await view.RefreshFromDiskAsync());

            // outside the app: a folder with a note deleted, an empty one deleted, one created, a note edited
            Directory.Delete(Path.Combine(vault, "Velha"), recursive: true);
            Directory.Delete(Path.Combine(vault, "Vazia"));
            Directory.CreateDirectory(Path.Combine(vault, "Nova", "Sub"));
            File.WriteAllText(Path.Combine(vault, "Fica", "Plano.md"), "# Plano editado fora\n");
            Assert.True(await view.RefreshFromDiskAsync());
            await Settle(window, view);
            Assert.DoesNotContain(view.Library!.Folders, f => f.StartsWith("Velha", StringComparison.Ordinal));
            Assert.DoesNotContain("Vazia", view.Library.Folders);
            Assert.Contains("Nova", view.Library.Folders);
            Assert.Contains("Nova/Sub", view.Library.Folders);
            Assert.Null(view.Documents!.Get("Velha/Dentro/Nota.md"));
            Assert.DoesNotContain(view.Documents.List(), d => d.Path.StartsWith("Velha/", StringComparison.Ordinal));
            Assert.Contains(view.Documents.List(), d => d.Path == "Fica/Plano.md" && d.Content.Contains("editado fora"));
            // and a save still works on the folder as it is now (no "modified outside" refusal)
            Assert.True(view.Explorer.TryOpenNote("Comece.md"));
            await Settle(window, view);
            view.Editor.Editor.Text = "# Comece\n\nDepois da volta.\n";
            Assert.True(await view.Editor.SaveAsync());
            Assert.Contains("Depois da volta.", File.ReadAllText(Path.Combine(vault, "Comece.md")));
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
