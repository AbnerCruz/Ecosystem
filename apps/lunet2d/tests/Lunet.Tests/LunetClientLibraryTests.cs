using Avalonia.Controls;
using Lunet.Client;
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
