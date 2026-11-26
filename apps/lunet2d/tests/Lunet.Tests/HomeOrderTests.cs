using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>H2 / anexo A §6.1: Home with Continuar, Fixados (by GameId), recent first and accent-insensitive search.</summary>
public sealed class HomeOrderTests
{
    private static readonly DateTime T = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Arrange_ContinueIsTheLastEdited_PinnedComeFirst_OthersByRecency()
    {
        var games = new[] { new HomeGame("Água", "a", T.AddHours(-5)), new HomeGame("Nave", "n", T), new HomeGame("Bola", "b", T.AddHours(-1)) };
        var r = HomeOrder.Arrange(games, ["a"], null);
        Assert.Equal("Nave", r.Continue!.Name);
        Assert.Equal(["Água"], r.Pinned.Select(g => g.Name));
        Assert.Equal(["Nave", "Bola"], r.Others.Select(g => g.Name));
    }

    [Fact]
    public void Continue_PrefersTheGameOpenedLast()
    {
        var games = new[] { new HomeGame("Velho", "v", T.AddDays(-3)), new HomeGame("Novo", "n", T) };
        Assert.Equal("Velho", HomeOrder.Arrange(games, [], null, lastOpenedId: "V").Continue!.Name); // ids compare ignoring case
        Assert.Equal("Novo", HomeOrder.Arrange(games, [], null, lastOpenedId: "apagado").Continue!.Name);
    }

    [Fact]
    public void Search_IgnoresCaseAndAccents_AndHidesContinue()
    {
        var games = new[] { new HomeGame("Fase Água", "a", T), new HomeGame("Nave", "n", T.AddHours(-1)) };
        var r = HomeOrder.Arrange(games, [], "AGUA");
        Assert.Null(r.Continue);
        Assert.Equal(["Fase Água"], r.Others.Select(g => g.Name));
        Assert.Empty(HomeOrder.Arrange(games, [], "zzz").Others);
    }

    [Fact]
    public void ASingleGame_HasNoContinueCard()
    {
        Assert.Null(HomeOrder.Arrange([new HomeGame("Só", "s", T)], [], null).Continue);
    }

    [Fact]
    public void Home_PinsByGameId_SurvivesRename_AndSearchFilters()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-home-order-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.Workspace.Create("Moedas"); shell.Workspace.Create("Nave"); shell.Workspace.Create("Fase-Água");
                var home = shell.Home;
                home.Refresh();
                home.TogglePin("Moedas");
                Assert.Equal(["Moedas"], home.Arranged.Pinned.Select(g => g.Name));
                Assert.Single(new Lunet.Core.LauncherPreferences(shell.LauncherPreferencesPath).PinnedProjectIds); // the previous screens' file
                shell.Workspace.Store.Rename("Moedas", "Moedas2");
                home.Refresh();
                Assert.Equal(["Moedas2"], home.Arranged.Pinned.Select(g => g.Name));                        // same GameId
                home.Query = "agua";
                Assert.Equal(["Fase-Água"], home.Shown);
                home.Query = "";
                Assert.Equal(3, home.Shown.Count);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
