using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>A6: the Biblioteca in the shell over the existing library.</summary>
public sealed class LunetClientLibraryTests
{
    [Fact]
    public void Library_PublishesFromAGame_SearchesAndUsesItInAnother_WithoutOverwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-library-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var source = shell.Workspace.Store.Create("Origem");
                source.WriteText("Content/dados/fase.json", "{\"nivel\":1}");
                shell.Workspace.Store.Create("Destino");
                shell.Home.Refresh();

                var library = shell.Home.ShowLibrary();
                Assert.Equal("list", library.Showing);
                Assert.Empty(library.Listed);

                new ReusableAssetLibrary(shell.LibraryPath).Publish(shell.Workspace.Store.Open("Origem"), "Content/dados/fase.json",
                    "Fase um", "Eu", "CC0", tags: ["nivel", "fase"]);
                library.FillResults();
                var asset = Assert.Single(library.Listed);
                Assert.Equal("Fase um", asset.Title);

                library.ShowAsset(asset.Id, asset.Version);
                Assert.Equal(asset.Id, library.Showing);
                library.Use(asset, "Destino");
                Assert.Equal("{\"nivel\":1}", shell.Workspace.Store.Open("Destino").ReadText("Content/dados/fase.json"));

                shell.Workspace.Store.Open("Destino").WriteText("Content/dados/fase.json", "{\"nivel\":99}");
                library.Use(asset, "Destino"); // the name is used now: nothing is overwritten
                Assert.Equal("{\"nivel\":99}", shell.Workspace.Store.Open("Destino").ReadText("Content/dados/fase.json"));

                Assert.True(shell.Back());                 // asset page → list
                Assert.Equal("list", library.Showing);
                Assert.True(shell.Back());                 // list → closes the Biblioteca
                Assert.Null(shell.Home.Sheet);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>P4: Jogar in the shell.</summary>
public sealed class LunetClientPlayTests
{
    [Fact]
    public void Play_ListsTheGames_AndPlaysThroughTheHead_OrInTheWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-play-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.Workspace.Store.Create("Nave");
                var played = new List<string>();
                shell.PlayGame = played.Add;
                var play = shell.Home.ShowPlay();
                Assert.Equal(["Nave"], play.Games);
                play.Play("Nave");
                Assert.Equal(["Nave"], played);                       // Android: full screen in its own process
                Assert.Equal(ShellLevel.Home, shell.State.Level);

                shell.PlayGame = null;                                 // desktop: runs in the workspace
                play.Play("Nave");
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                Assert.Null(shell.Home.Sheet);
                shell.StopGame();
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
