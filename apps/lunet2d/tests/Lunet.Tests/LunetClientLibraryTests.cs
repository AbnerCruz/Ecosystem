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

/// <summary>P7: Ajustes in the shell, on the same settings.json.</summary>
public sealed class LunetClientSettingsTests
{
    [Fact]
    public void Settings_SaveAtOnce_KeepOtherFields_AndApplyToTheEditorAndRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                Directory.CreateDirectory(root);
                var file = Path.Combine(root, "settings.json");
                var previous = new EditorSettings { ShowMinimap = false, IsolatedPreview = true, FontSize = 14 };
                new SettingsStore(file).Save(previous);                       // written by the previous screens
                var shell = new LunetShell(Path.Combine(root, "Projects")) { SettingsPath = file };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var code = Assert.IsType<Lunet.Client.Views.CodeSurface>(shell.WorkspaceView.Code.Surface);
                Assert.Equal(14, code.Editor.FontSize);

                var next = shell.Settings.Clone();
                next.FontSize = 20; next.ShowLineNumbers = false; next.FormatOnRun = true;
                shell.SaveSettings(next);
                Assert.Equal(20, code.Editor.FontSize);
                Assert.False(code.Editor.ShowLineNumbers);
                var saved = new SettingsStore(file).Load();
                Assert.Equal(20, saved.FontSize);
                Assert.True(saved.IsolatedPreview);                          // what the shell does not show is kept
                Assert.False(saved.ShowMinimap);

                code.Text = "public sealed class G : Lunet.Game {\nprotected override void Draw(Lunet.GameTime t){\nGraphicsDevice.Clear(Lunet.Graphics.Color.Black);}}";
                shell.RunGame();                                              // Formatar ao rodar
                Assert.Contains("\n    protected override void Draw", code.Text);
                shell.StopGame();

                shell.GoHome();
                var view = shell.Home.ShowSettings();
                Assert.NotNull(view);
                Assert.True(shell.Back());
                Assert.Null(shell.Home.Sheet);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
