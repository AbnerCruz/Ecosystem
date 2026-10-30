using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-101: defects that only show when two features meet (a note open while its file changes elsewhere, …).</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Combined_ANoteDeletedOutsideWhileOpenKeepsWhatIsTyped() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Aberta.md", "texto\n"));
        var view = vault.View;
        view.Editor.Open(new CityNote("Aberta.md", "texto\n"));
        view.Editor.SetMode(EditorMode.Fonte);
        await Settle(vault.Window, view);
        File.Delete(Path.Combine(vault.Folder, "Aberta.md"));   // a sync, another app
        view.Editor.Editor.Text = "texto\n\nescrito depois\n";
        var dialogs = await AnsweringAsync(vault, () => view.Editor.SaveAsync(), _ => "Ok");
        await Settle(vault.Window, view);
        // what was typed is somewhere on the disk, and the person was not told it was saved when it was not
        var all = Directory.GetFiles(vault.Folder, "*.md", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
        Assert.Contains(all, t => t.Contains("escrito depois"));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Combined_RenamingANoteKeepsTheLinksToIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n\nVeja [[Rascunho]] e [[Rascunho#Parte|a parte]] e ![[Rascunho]].\n"),
            ("Rascunho.md", "# Parte\n"), ("Pasta/Outra.md", "ligada a [[Rascunho]]\n"));
        var view = vault.View;
        await view.RenameNoteAsync("Rascunho.md", "Final.md");
        await Settle(vault.Window, view);
        Assert.Equal("# Comece\n\nVeja [[Final]] e [[Final#Parte|a parte]] e ![[Final]].\n", vault.Read("Comece.md"));
        Assert.Equal("ligada a [[Final]]\n", vault.Read("Pasta/Outra.md"));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Combined_LinksFollowAFolderRenamedAndTheOpenNoteKeepsWhatWasTyped() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Pasta/Ideia.md", "ideia\n"),
            ("Índice.md", "[[Pasta/Ideia]] e [[Ideia]]\n"));
        var view = vault.View;
        // the note that links is open, with text not saved yet
        view.Editor.Open(new CityNote("Índice.md", "[[Pasta/Ideia]] e [[Ideia]]\n"));
        view.Editor.SetMode(EditorMode.Fonte);
        await Settle(vault.Window, view);
        view.Editor.Editor.Text = "[[Pasta/Ideia]] e [[Ideia]]\n\nnovo parágrafo\n";
        await AnsweringAsync(vault, () => view.RenameFolderAsync("Pasta", "Projetos"), _ => "Ok");
        Assert.Equal("[[Projetos/Ideia]] e [[Ideia]]\n\nnovo parágrafo\n", vault.Read("Índice.md"));
        Assert.Equal("[[Projetos/Ideia]] e [[Ideia]]\n\nnovo parágrafo\n", view.Editor.Current!.Content);
        Assert.False(view.Editor.HasChanges);
        return true;
    }, CancellationToken.None));
}
