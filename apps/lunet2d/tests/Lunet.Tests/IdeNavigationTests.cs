using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>G5: back and forward between positions; breadcrumbs over the code show the folders and the outline.</summary>
public sealed class IdeNavigationTests
{
    [Fact]
    public void BackAndForward_AndTheBreadcrumbs()
    {
        var root = Directory.CreateTempSubdirectory("lunet-nav-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root) { HardwareKeyboard = false };
                var w = new Window { Width = 412, Height = 915, Content = shell };
                w.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                shell.Workspace.Project!.CreateFile("Scripts/Player.cs", "class Player\n{\n    int vida;\n}\n");
                var code = shell.WorkspaceView.Code;
                code.JumpTo("Scripts/Player.cs", 3, 5);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("Scripts/Player.cs", code.OpenPath);
                Assert.True(code.BackToPosition());
                Assert.Equal("Game.cs", code.OpenPath);
                Assert.Equal(1, code.PositionsForward);
                Assert.True(code.ForwardToPosition());
                Assert.Equal("Scripts/Player.cs", code.OpenPath);
                Assert.False(code.ForwardToPosition());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                var folder = w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "BreadcrumbFolder" && b.IsEffectivelyVisible);
                Assert.Equal("Mostrar a pasta Scripts", Avalonia.Automation.AutomationProperties.GetName(folder));
                folder.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(SupportPanel.Explorer, shell.State.Panel);
                Assert.Equal("Scripts", shell.WorkspaceView.Explorer.Revealed);
                w.Close();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
