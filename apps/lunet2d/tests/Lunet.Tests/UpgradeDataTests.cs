using Avalonia.Controls;
using Lunet.Client;

namespace Lunet.Tests;

/// <summary>
/// G14 (headless part): a person updating from a version whose Home saved pins and the last game by name (launcher
/// preferences v1) finds them in the shell's Home — pinned, and under Continuar — and the file is upgraded to GameIds
/// without losing a name that matches no game.
/// </summary>
public sealed class UpgradeDataTests
{
    [Fact]
    public void PinsAndLastGameSavedByName_SurviveTheUpdate_IntoTheShellHome()
    {
        var root = Directory.CreateTempSubdirectory("lunet-upgrade-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var prefsPath = Path.Combine(root, ".data", "launcher-preferences.json");
                var shell = new LunetShell(root) { LauncherPreferencesPath = prefsPath };
                shell.Workspace.Create("Plataforma"); shell.Workspace.Create("Nave-Espacial");
                Directory.CreateDirectory(Path.GetDirectoryName(prefsPath)!);
                // What the previous Home wrote (v1: names only), including a game that no longer exists.
                File.WriteAllText(prefsPath, """{"Version":1,"Section":"home","LastProject":"Nave-Espacial","PinnedProjects":["Plataforma","Apagado"]}""");

                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.GoHome();
                shell.Home.Refresh();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.True(shell.Home.IsPinned("Plataforma"), "o jogo fixado antes da atualização sumiu dos Fixados");
                Assert.Equal("Nave-Espacial", shell.Home.Arranged.Continue?.Name);   // Continuar keeps the last game
                var upgraded = new Lunet.Core.LauncherPreferences(prefsPath);
                Assert.Single(upgraded.PinnedProjectIds);                              // now by GameId
                Assert.Equal(["Apagado"], upgraded.PinnedProjects);                     // an unknown name is kept, not dropped
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
}
