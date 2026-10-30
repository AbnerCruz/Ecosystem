using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-039: "Mover para" — the folder itself, its subfolders and where it already is are not offered; a long list has a
/// search that ignores accents and says when nothing matches; the folder moves with what is inside it.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Move_ThePickerSearchesExcludesInvalidPlacesAndMovesTheFolder() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Projetos/Plano.md", "plano\n"), ("Projetos/Sub/Ideia.md", "ideia\n"),
            ("Ação/A.md", "a\n"), ("Bairro/B.md", "b\n"), ("Cozinha/C.md", "c\n"), ("Diário/D.md", "d\n"), ("Estudos/E.md", "e\n"), ("Finanças/F.md", "f\n"), ("Galeria/G.md", "g\n"), ("Hábitos/H.md", "h\n"));
        var view = vault.View;
        var moving = view.MoveFolderAsync("Projetos");
        for (int i = 0; i < 300 && !view.Dialogs.IsOpen; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(view.Dialogs.IsOpen);
        List<string> Shown() => [.. view.Dialogs.GetLogicalDescendants().OfType<Button>().Where(b => b.IsVisible && b.Tag is string)
            .SelectMany(b => b.GetLogicalDescendants().OfType<TextBlock>().Take(1)).Select(t => t.Text ?? "")];
        var all = Shown();
        Assert.Contains("Ação", all);
        Assert.DoesNotContain("Raiz", all);   // it is already at the root
        Assert.DoesNotContain("Projetos", all);
        Assert.DoesNotContain("Sub", all);

        var search = view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Single(t => t.PlaceholderText == "Buscar");
        search.Text = "acao";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Ação"], Shown());
        search.Text = "zzz";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(Shown());
        Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.IsVisible && t.Text == "Nada com “zzz”.");

        search.Text = "acao";
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.Dialogs, "Ação");
        for (int i = 0; i < 300 && !moving.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); if (view.Dialogs.IsOpen) ClickButtonWithText(view.Dialogs, "Ok"); await Task.Delay(10); }
        Assert.True(await moving);
        Assert.Equal("ideia\n", vault.Read("Ação/Projetos/Sub/Ideia.md"));
        Assert.Equal("plano\n", vault.Read("Ação/Projetos/Plano.md"));
        Assert.False(vault.Exists("Projetos/Plano.md"));
        return true;
    }, CancellationToken.None));
}
