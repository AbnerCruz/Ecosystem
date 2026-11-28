using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>G4: the Explorer — long press for actions, move to a folder, duplicate, filter by name.</summary>
public sealed class IdeExplorerTests
{
    [Fact]
    public void Create_OnlyWritesAfterConfirmation_UsesFinalClassName_AndCancelWritesNothing()
    {
        var root = Directory.CreateTempSubdirectory("lunet-create-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root) { HardwareKeyboard = false };
                var w = new Window { Width = 360, Height = 800, Content = shell };
                w.Show(); shell.CreateProject();
                shell.State.ShowPanel(SupportPanel.Explorer);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var project = shell.Workspace.Project!;
                var explorer = shell.WorkspaceView.Explorer;
                var before = project.ListFiles().ToArray();
                explorer.BeginCreate(false);
                Assert.Equal(before, project.ListFiles().ToArray());
                Assert.True(explorer.CancelCreate());
                Assert.Equal(before, project.ListFiles().ToArray());
                explorer.BeginCreate(false);
                var box = w.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "ExplorerCreateName");
                var confirm = w.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ExplorerConfirmCreate");
                box.Text = "class";
                confirm.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.True(explorer.CreationOpen);
                Assert.Equal(before, project.ListFiles().ToArray());
                box.Text = "Player";
                confirm.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.False(explorer.CreationOpen);
                Assert.Equal("Player.cs", shell.WorkspaceView.Code.OpenPath);
                Assert.Contains("class Player", project.ReadText("Player.cs"));
                Assert.False(project.Exists("Script.cs"));
                Assert.Throws<ArgumentException>(() => explorer.CreateNamed(false, "player.cs"));
                Assert.Throws<ArgumentException>(() => explorer.CreateNamed(false, "../Escape"));
                Assert.Equal("Actors", explorer.CreateNamed(true, "Actors"));
                Assert.Equal("Actors/Enemy.cs", explorer.CreateNamed(false, "Enemy.cs", "Actors"));
                Assert.Contains("class Enemy", project.ReadText("Actors/Enemy.cs"));
                w.Close();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TheExplorer_MovesDuplicatesAndFilters_TabsFollow()
    {
        var root = Directory.CreateTempSubdirectory("lunet-explorer-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root) { HardwareKeyboard = false };
                var w = new Window { Width = 412, Height = 915, Content = shell };
                w.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var project = shell.Workspace.Project!;
                project.CreateDirectory("Scripts");
                project.CreateFile("Enemy.cs", "class Enemy {}\n");
                var explorer = shell.WorkspaceView.Explorer;
                shell.State.ShowPanel(SupportPanel.Explorer);
                explorer.Fill();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // long press on a row opens its actions
                var row = w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ExplorerItem" && Avalonia.Automation.AutomationProperties.GetName(b) == "Abrir Enemy.cs");
                row.RaiseEvent(new Avalonia.Input.ContextRequestedEventArgs());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains(w.GetVisualDescendants().OfType<Button>(), b => b.Name == "ExplorerMove" && b.IsEffectivelyVisible);

                shell.WorkspaceView.Code.Load("Enemy.cs");
                var entry = project.ListTree().Single(e => e.Path == "Enemy.cs");
                Assert.Equal("Scripts/Enemy.cs", explorer.Move(entry, "Scripts"));
                Assert.True(project.Exists("Scripts/Enemy.cs"));
                Assert.False(project.Exists("Enemy.cs"));
                Assert.Equal("Scripts/Enemy.cs", shell.WorkspaceView.Code.OpenPath);       // the tab followed

                Assert.Equal("Scripts/Enemy cópia.cs", explorer.Duplicate("Scripts/Enemy.cs"));
                Assert.Equal("class Enemy {}\n", project.ReadText("Scripts/Enemy cópia.cs"));
                Assert.Equal("Scripts/Enemy cópia 2.cs", explorer.Duplicate("Scripts/Enemy.cs"));

                explorer.Filter = "cópia 2";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(["Scripts", "Scripts/Enemy cópia 2.cs"], explorer.Shown);    // the match and its folder
                explorer.Filter = "";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains("Game.cs", explorer.Shown);
                w.Close();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
