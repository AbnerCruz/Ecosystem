using Avalonia.Controls;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-005: "o Urbe não pode sobrescrever silenciosamente um arquivo modificado por outro aplicativo". A change made
/// outside the Urbe to some file of the vault (a sync, another editor) used to block every save ("Reabra o vault"),
/// and "Tentar de novo" kept failing. Now the save reads the folder again: another file's change is kept and shown,
/// and when the note itself was changed outside, both texts stay — the person's goes to a conflict copy.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static async Task<(MainView View, Window Window, string Dir, string Vault)> OpenRealVaultAsync(params (string Path, string Text)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-outside-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        foreach (var (path, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(vault, path))!);
            File.WriteAllText(Path.Combine(vault, path), text);
        }
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        return (view, window, dir, vault);
    }

    [Fact]
    public async Task Save_AnotherFileChangedOutsideNoLongerBlocksTheNote() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await OpenRealVaultAsync(("Diário.md", "# Diário\n\nOntem.\n"), ("Lista.md", "# Lista\n\n- pão\n"));
        try
        {
            view.OpenNoteByPath("Diário.md");
            await Settle(window, view);
            File.WriteAllText(Path.Combine(vault, "Lista.md"), "# Lista\n\n- pão\n- café (do outro aplicativo)\n");
            view.Editor.Editor.Text = "# Diário\n\nHoje escrevi.\n";
            Assert.True(await view.Editor.SaveAsync());
            Assert.Equal("# Diário\n\nHoje escrevi.\n", await ReadWhenWritten(Path.Combine(vault, "Diário.md"), t => t.Contains("Hoje")));
            Assert.Equal("# Lista\n\n- pão\n- café (do outro aplicativo)\n", File.ReadAllText(Path.Combine(vault, "Lista.md")));
            Assert.Contains("café", view.Library!.Store.Get("Lista.md")!.Content);   // the change shows in the Urbe
            Assert.False(view.Editor.HasChanges);
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Diário.md", view.Editor.Current!.Path);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Save_TheNoteChangedOutsideKeepsBothTexts() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await OpenRealVaultAsync(("Ideias.md", "# Ideias\n\nUma.\n"));
        try
        {
            view.OpenNoteByPath("Ideias.md");
            await Settle(window, view);
            File.WriteAllText(Path.Combine(vault, "Ideias.md"), "# Ideias\n\nUma.\n\nEscrita no computador.\n");
            view.Editor.Editor.Text = "# Ideias\n\nUma.\n\nEscrita no celular.\n";
            Assert.True(await view.Editor.SaveAsync());
            // what was written outside is not overwritten
            Assert.Equal("# Ideias\n\nUma.\n\nEscrita no computador.\n", File.ReadAllText(Path.Combine(vault, "Ideias.md")));
            // and what the person wrote is in a copy next to it, open in the editor
            var copy = Directory.GetFiles(vault, "Ideias (conflito *.md").Single();
            Assert.Equal("# Ideias\n\nUma.\n\nEscrita no celular.\n", File.ReadAllText(copy));
            Assert.Equal(Path.GetFileName(copy), view.Editor.Current!.Path);
            Assert.Contains("alterada fora do Urbe", view.Editor.NoticeText ?? view.NoticeText ?? "");
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
