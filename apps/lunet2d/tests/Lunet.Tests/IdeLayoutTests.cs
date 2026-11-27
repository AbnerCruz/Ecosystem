using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// The IDE after the owner's review (27/11: "odiei a interface da IDE… o que mais me incomoda é a navbar"), on a phone:
/// no tab bar; ☰ opens the Explorer (the game's folder as a tree); the editor's tabs are the open files only, each
/// with ✕; ▶ opens the Preview in full-screen mode with Jogo, Console and Problemas. The status bar also shows the
/// problems, the caret's line and column, and the AI.
/// </summary>
public sealed class IdeLayoutTests
{
    private static T Named<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().First(c => c.Name == name && c.IsEffectivelyVisible);
    private static bool Shows(Window w, string name) => w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);
    private static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    private static (Window, LunetShell) Open(string root)
    {
        var shell = new LunetShell(root) { HardwareKeyboard = false };
        var window = new Window { Width = 412, Height = 915, Content = shell };
        window.Show();
        shell.CreateProject();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    [Fact]
    public void ThePhoneIde_HasNoTabBar_TheExplorerOpensFilesAsClosableTabs()
    {
        var root = Directory.CreateTempSubdirectory("lunet-ide-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var (w, shell) = Open(root);
                Assert.DoesNotContain(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Recursos" && t.IsEffectivelyVisible); // the old tab bar is gone
                Assert.True(Shows(w, "RunButton"));
                Assert.True(Shows(w, "StatusBar"));

                Click(Named<Button>(w, "ExplorerButton"));
                Assert.Equal(SupportPanel.Explorer, shell.State.Panel);
                Assert.True(Shows(w, "ExplorerDrawer"));
                var explorer = shell.WorkspaceView.Explorer;
                Assert.Contains("Game.cs", explorer.Shown);
                Assert.Contains("lunet.json", explorer.Shown);

                var created = explorer.Create(folder: false);                       // ＋ creates Script.cs and opens it
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(created, shell.WorkspaceView.Code.OpenPath);
                Assert.Contains(created, shell.WorkspaceView.Code.OpenTabs);
                Assert.Contains("Game.cs", shell.WorkspaceView.Code.OpenTabs);    // the game's entry stays a tab
                Assert.DoesNotContain("lunet.json", shell.WorkspaceView.Code.OpenTabs); // files not opened are not tabs

                shell.State.ShowPanel(SupportPanel.None);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                shell.WorkspaceView.Code.CloseTab(created);                          // ✕
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.DoesNotContain(created, shell.WorkspaceView.Code.OpenTabs);
                Assert.Equal("Game.cs", shell.WorkspaceView.Code.OpenPath);          // the neighbour opens
                Assert.True(File.Exists(Path.Combine(shell.Workspace.Project!.Directory, created))); // closing never deletes

                var renamed = explorer.Rename(new Lunet.Core.ExplorerEntry(created, Path.GetFileName(created), 0, false), "Jogador");
                Assert.Equal("Jogador.cs", renamed);
                Assert.Contains("Jogador.cs", explorer.Shown);
                w.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Running_StartsFullScreen_AndBackShowsEditorBeforeHome(bool systemBack)
    {
        var root = Directory.CreateTempSubdirectory("lunet-ide-run-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var (w, shell) = Open(root);
                var file = shell.WorkspaceView.Code.OpenPath;
                shell.WorkspaceView.Code.ShowKeyboard();
                Click(Named<Button>(w, "RunButton"));
                Assert.True(shell.WorkspaceView.PreviewFull);
                Assert.True(shell.WorkspaceView.BottomPanelVisible);
                Assert.False(shell.WorkspaceView.Code.IsAttachedToVisualTree());
                Assert.True(shell.WorkspaceView.Game.IsEffectivelyVisible);

                Click(Named<Button>(w, "PanelFull"));
                Assert.False(shell.WorkspaceView.PreviewFull);
                Assert.True(shell.WorkspaceView.Code.IsAttachedToVisualTree());
                Click(Named<Button>(w, "PanelFull"));
                Click(Named<Button>(w, "PanelConsole"));
                Assert.Equal(SupportPanel.Console, shell.State.Panel);
                Assert.True(shell.WorkspaceView.PreviewFull);

                if (systemBack) Assert.True(shell.Back());
                else Click(Named<Button>(w, "IdeBack"));
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                Assert.Equal(WorkspaceTab.Code, shell.State.Tab);
                Assert.Equal(SupportPanel.None, shell.State.Panel);
                Assert.Equal(GameState.Stopped, shell.Game.State);
                Assert.Equal(file, shell.WorkspaceView.Code.OpenPath);
                Assert.True(shell.WorkspaceView.Code.IsAttachedToVisualTree());
                Assert.False(shell.WorkspaceView.BottomPanelVisible);

                shell.WorkspaceView.Code.ShowKeyboard();
                Assert.Single(w.GetVisualDescendants().OfType<CodeBar>());
                shell.WorkspaceView.Code.HideKeyboard();
                Click(Named<Button>(w, "IdeBack"));
                Assert.Equal(ShellLevel.Home, shell.State.Level);
                w.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TheStatusBar_ShowsTheCaret_AndProblemsOpenTheirPanel()
    {
        var root = Directory.CreateTempSubdirectory("lunet-ide-status-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var (w, shell) = Open(root);
                var code = (CodeSurface)shell.WorkspaceView.Code.Surface;
                code.Caret = code.Text.IndexOf('{') + 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.StartsWith("Ln ", shell.WorkspaceView.CursorText);
                Click(Named<Button>(w, "StatusProblems"));
                Assert.Equal(SupportPanel.Problems, shell.State.Panel);
                Assert.True(shell.WorkspaceView.BottomPanelVisible);                 // Problemas lives in the bottom panel
                Click(Named<Button>(w, "AiButton"));
                Assert.Equal(SupportPanel.Ai, shell.State.Panel);
                w.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
}
