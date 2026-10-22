using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>renomearAsset / moverAsset / removerAsset: the attachment changes in the vault, the notes never.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Attachments_RenameMoveAndDeleteInTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-attach-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
            File.WriteAllBytes(Path.Combine(vault, "foto.png"), [137, 80, 78, 71, 1, 2, 3]);
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Avalonia.Controls.Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);

                // holding the file in the Explorer: Visualizar, Renomear, Mover, Excluir
                view.ShowNotes();
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Explorer.HoldFile("foto.png"));
                Dispatcher.UIThread.RunJobs();
                var options = view.Dialogs.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text).ToList();
                foreach (var o in new[] { "Visualizar", "Renomear", "Mover", "Excluir" }) Assert.Contains(o, options);
                ClickButtonWithText(view.Dialogs, "Visualizar");
                for (int i = 0; i < 40 && view.FilePreview.Current != "foto.png"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("foto.png", view.FilePreview.Current);
                view.FilePreview.Close();

                // Renomear: same folder, same bytes; another extension is refused
                await view.RenameAttachmentAsync("foto.png", "capa.jpg");
                Assert.True(File.Exists(Path.Combine(vault, "foto.png")));
                await view.RenameAttachmentAsync("foto.png", "capa.png");
                Assert.False(File.Exists(Path.Combine(vault, "foto.png")));
                Assert.Equal(new byte[] { 137, 80, 78, 71, 1, 2, 3 }, File.ReadAllBytes(Path.Combine(vault, "capa.png")));

                // Mover: into Bairro
                await view.MoveAttachmentAsync("capa.png", "Bairro");
                Assert.True(File.Exists(Path.Combine(vault, "Bairro", "capa.png")));
                Assert.False(File.Exists(Path.Combine(vault, "capa.png")));

                // Excluir: confirmed, gone from the vault; the notes untouched
                await view.DeleteAttachmentAsync("Bairro/capa.png", confirmed: true);
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "capa.png")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Um.md")));

                // the storage never moves or deletes a note as an attachment
                await Assert.ThrowsAsync<InvalidOperationException>(() => storage.DeleteFileAsync("Comece.md", CancellationToken.None));
                await Assert.ThrowsAsync<InvalidOperationException>(() => storage.MoveFileAsync("Comece.md", "Bairro/Comece.md", CancellationToken.None));
                Assert.True(File.Exists(Path.Combine(vault, "Comece.md")));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}

/// <summary>v20AgruparSelecao: two notes into a new folder — the neighbourhood, the folder and both files.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task GroupIntoFolder_MovesTheNotesIntoANewFolder() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                int folders = view.Library!.Folders.Count;
                await view.GroupIntoFolderAsync(["Comece.md"], "Só uma");
                Assert.Equal(folders, view.Library.Folders.Count);   // at least two
                await view.GroupIntoFolderAsync(["Comece.md", "Bairro/Um.md"], "Grupo");
                Assert.Contains("Grupo", view.Library.Folders);
                Assert.True(File.Exists(Path.Combine(vault, "Grupo", "Comece.md")));
                Assert.True(File.Exists(Path.Combine(vault, "Grupo", "Um.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Comece.md")));
                Assert.All(view.Documents!.List(), d => Assert.StartsWith("Grupo/", d.Path));
                await UntilNotesSaved(view);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
