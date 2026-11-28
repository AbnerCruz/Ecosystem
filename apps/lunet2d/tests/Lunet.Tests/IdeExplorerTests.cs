using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>G4: the Explorer — long press for actions, move to a folder, duplicate, filter by name.</summary>
public sealed class IdeExplorerTests
{
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
