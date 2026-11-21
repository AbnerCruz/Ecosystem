using Avalonia.Controls;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-101 with the app as the folder, 1:1: the note open in the editor is moved or deleted outside the Urbe
/// and the person comes back. Moved, the editor follows it; deleted, the person is told and saving creates it again
/// with what was written (nothing is lost).</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Vault_TheOpenNoteMovedOrDeletedOutsideIsFollowedOrCreatedAgain() => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-gone-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "P"));
        File.WriteAllText(Path.Combine(vault, "P", "Aberta.md"), "# Aberta\n\nTexto.\n");
        File.WriteAllText(Path.Combine(vault, "Movida.md"), "# Movida\n\nVai mudar de pasta.\n");
        var prev = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "s.json");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);

            // moved outside: the editor goes with the note
            view.OpenNoteByPath("Movida.md");
            await Settle(window, view);
            Directory.CreateDirectory(Path.Combine(vault, "Arquivo"));
            File.Move(Path.Combine(vault, "Movida.md"), Path.Combine(vault, "Arquivo", "Movida.md"));
            Assert.True(await view.RefreshFromDiskAsync());
            await Settle(window, view);
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Arquivo/Movida.md", view.Editor.Current!.Path);
            Assert.Contains("movida fora do Numa", view.Editor.NoticeText ?? "");
            view.Editor.Editor.Text = "# Movida\n\nVai mudar de pasta.\n\nE foi.\n";
            Assert.True(await view.Editor.SaveAsync());
            Assert.Contains("E foi.", File.ReadAllText(Path.Combine(vault, "Arquivo", "Movida.md")));
            Assert.False(File.Exists(Path.Combine(vault, "Movida.md")));

            // deleted outside (its folder too): told at once; saving creates it again with what was written
            view.OpenNoteByPath("P/Aberta.md");
            await Settle(window, view);
            Directory.Delete(Path.Combine(vault, "P"), true);
            Assert.True(await view.RefreshFromDiskAsync());
            await Settle(window, view);
            Assert.True(view.Editor.IsVisible);
            Assert.Contains("apagada fora do Numa", view.Editor.NoticeText ?? "");
            view.Editor.Editor.Text = "# Aberta\n\nTexto.\n\nDigitado depois.\n";
            Assert.True(await view.Editor.SaveAsync());
            await Settle(window, view);
            Assert.Equal("# Aberta\n\nTexto.\n\nDigitado depois.\n", File.ReadAllText(Path.Combine(vault, "P", "Aberta.md")));
            Assert.NotNull(view.Documents!.Get("P/Aberta.md"));
            Assert.Contains("P", view.Library!.Folders);
            Assert.Contains("criada de novo", view.Editor.NoticeText ?? "");
            return true;
        }
        finally
        {
            window.Close();
            UrbeApp.LocalStatePath = prev;
            try { Directory.Delete(dir, true); } catch { }
        }
    }, CancellationToken.None));
}
