using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-196: ordinary Markdown links follow a note dragged in the Explorer and a folder renamed.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Links_MarkdownLinksFollowANoteDraggedInTheExplorerAndAFolderRenamed() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        const string index = "Veja [o plano](Pasta/Plano.md#Metas), ![](<Pasta/img/foto 1.png>) e [ref][p].\n\n[p]: Pasta/Plano.md \"Plano\"\n\n" +
                             "```\n[código](Pasta/Plano.md)\n```\n[site](https://exemplo.com/Pasta/Plano.md) `[c](Pasta/Plano.md)` [sumiu](Pasta/Nada.md)\n";
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Índice.md", index),
            ("Pasta/Plano.md", "# Metas\n\n[índice](../Índice.md) ![](img/foto%201.png)\n"), ("Outro/Leia.md", "# Leia\n"));
        Directory.CreateDirectory(Path.Combine(vault.Folder, "Pasta", "img"));
        File.WriteAllBytes(Path.Combine(vault.Folder, "Pasta", "img", "foto 1.png"), [0x89, 0x50, 0x4E, 0x47]);
        var view = vault.View;
        await view.OpenVaultAsync();
        await Settle(vault.Window, view);
        async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }

        // the note is dragged onto another folder in the Explorer
        view.ShowNotes();
        Assert.True(view.Explorer.TryEnterFolder("Pasta"));
        Assert.True(view.Explorer.HoldNote("Pasta/Plano.md"));
        Assert.True(view.Explorer.DropSelectionOn("Outro"));
        await Settled();
        await Settle(vault.Window, view);
        Assert.True(vault.Exists("Outro/Plano.md"));
        Assert.Equal("Veja [o plano](Outro/Plano.md#Metas), ![](<Pasta/img/foto 1.png>) e [ref][p].\n\n[p]: Outro/Plano.md \"Plano\"\n\n" +
                     "```\n[código](Pasta/Plano.md)\n```\n[site](https://exemplo.com/Pasta/Plano.md) `[c](Pasta/Plano.md)` [sumiu](Pasta/Nada.md)\n",
            vault.Read("Índice.md"));
        // the moved note's own relative links are written from where it is now
        Assert.Equal("# Metas\n\n[índice](../Índice.md) ![](../Pasta/img/foto%201.png)\n", vault.Read("Outro/Plano.md"));
        Assert.Equal("Links atualizados em 2 notas.", view.NoticeText);

        // the folder with the image is renamed: both notes follow it
        await AnsweringAsync(vault, () => view.RenameFolderAsync("Pasta", "Arquivo"), _ => "Ok");
        await Settled();
        Assert.True(File.Exists(Path.Combine(vault.Folder, "Arquivo", "img", "foto 1.png")));
        Assert.Contains("![](<Arquivo/img/foto 1.png>)", vault.Read("Índice.md"));
        Assert.Contains("[sumiu](Pasta/Nada.md)", vault.Read("Índice.md"));   // it found nothing before: left alone
        Assert.Equal("# Metas\n\n[índice](../Índice.md) ![](../Arquivo/img/foto%201.png)\n", vault.Read("Outro/Plano.md"));
        Assert.Equal("Pasta renomeada para “Arquivo”. Links atualizados em 2 notas.", view.NoticeText);
        return true;
    }, CancellationToken.None));
}
