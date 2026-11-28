using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>G3: the editor's tabs like VS Code's — close others/right/all, reorder, next tab, reveal in the Explorer.</summary>
public sealed class IdeTabsTests
{
    [Fact]
    public void TheSession_ClosesOthersRightAll_MovesAndWraps()
    {
        var s = new EditorDocuments();
        foreach (var f in new[] { "A.cs", "B.cs", "C.cs", "D.cs" }) s.Open(f, additional: true);
        Assert.Equal(["A.cs", "B.cs", "C.cs", "D.cs"], s.Tabs);
        Assert.True(s.Move("C.cs", -1));
        Assert.Equal(["A.cs", "C.cs", "B.cs", "D.cs"], s.Tabs);
        Assert.False(s.Move("A.cs", -1));
        s.Current = "D.cs";
        Assert.Equal("A.cs", s.Neighbour(+1));                     // wraps
        Assert.Equal("B.cs", s.Neighbour(-1));
        s.CloseToRight("C.cs");
        Assert.Equal(["A.cs", "C.cs"], s.Tabs);
        Assert.Equal("C.cs", s.Current);                           // the current one was closed: the anchor
        s.CloseOthers("A.cs");
        Assert.Equal(["A.cs"], s.Tabs);
        s.CloseAll();
        Assert.Empty(s.Tabs);
        Assert.Null(s.Current);
    }

    [Fact]
    public void ALongPress_OpensTheTabActions_AndTheyWork()
    {
        var root = Directory.CreateTempSubdirectory("lunet-tabs-").FullName;
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
                project.CreateFile("Scripts/Player.cs", "class Player {}\n");
                project.CreateFile("Enemy.cs", "class Enemy {}\n");
                var code = shell.WorkspaceView.Code;
                code.Load("Scripts/Player.cs");
                code.Load("Enemy.cs");
                Assert.Equal(["Game.cs", "Scripts/Player.cs", "Enemy.cs"], code.OpenTabs);

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(w.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "EditorTab"), string.Join(",", w.GetVisualDescendants().OfType<Control>().Select(c => c.Name).Where(n => n is not null).Distinct()));
                var tab = w.GetVisualDescendants().OfType<Border>().First(b => b.Name == "EditorTab");
                tab.RaiseEvent(new Avalonia.Input.ContextRequestedEventArgs());                   // long press / right click
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("Game.cs", code.TabMenuFor);
                Assert.Contains(w.GetVisualDescendants().OfType<Control>(), c => c.Name == "TabMenu" && c.IsEffectivelyVisible);

                code.MoveTab("Enemy.cs", -1);
                Assert.Equal(["Game.cs", "Enemy.cs", "Scripts/Player.cs"], code.OpenTabs);
                Assert.True(code.NextTab(+1));
                Assert.Equal("Scripts/Player.cs", code.OpenPath);

                shell.WorkspaceView.RevealInExplorer("Scripts/Player.cs");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(SupportPanel.Explorer, shell.State.Panel);
                Assert.Equal("Scripts/Player.cs", shell.WorkspaceView.Explorer.Revealed);
                shell.State.ShowPanel(SupportPanel.None);

                code.CloseOthers("Enemy.cs");
                Assert.Equal(["Enemy.cs"], code.OpenTabs);
                Assert.Equal("Enemy.cs", code.OpenPath);
                code.CloseAll();
                Assert.Empty(code.OpenTabs);
                Assert.Null(code.OpenPath);
                w.Close();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
