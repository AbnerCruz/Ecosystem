using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>G1: search in the whole game from the Explorer (VS Code's Search view, on a phone).</summary>
public sealed class IdeSearchTests
{
    private static (Window, LunetShell) Open(string root)
    {
        var shell = new LunetShell(root) { HardwareKeyboard = false };
        var window = new Window { Width = 412, Height = 915, Content = shell };
        window.Show();
        shell.CreateProject();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    private static void Wait(Task task)
    {
        while (!task.IsCompleted) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        task.GetAwaiter().GetResult();
    }

    [Fact]
    public void TheSearch_FindsInEveryFile_AndATapGoesToTheLine()
    {
        var root = Directory.CreateTempSubdirectory("lunet-search-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var (w, shell) = Open(root);
                var project = shell.Workspace.Project!;
                project.CreateFile("Enemy.cs", "public sealed class Enemy\n{\n    int vida = 3;\n    int Vida2 = vida;\n}\n");
                var explorer = shell.WorkspaceView.Explorer;
                shell.State.ShowPanel(SupportPanel.Explorer);
                explorer.ShowSearch(true, "vida");
                Assert.True(explorer.Searching);
                var search = explorer.Search;
                Wait(search.RunAsync());
                Assert.True(search.Matches.Count(m => m.Path == "Enemy.cs") == 3, search.Query + "|" + search.Summary + "|" + string.Join(",", project.ListFiles()));      // ignores case by default

                search.Query = "vida";
                w.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().First(t => t.Name == "SearchWord").IsChecked = true;
                Wait(search.RunAsync());
                Assert.Equal(2, search.Matches.Count);                               // "Vida2" is not the word

                var second = search.Matches.Single(m => m.Line == 4);
                search.Go(second);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("Enemy.cs", shell.WorkspaceView.Code.OpenPath);
                Assert.Equal(SupportPanel.None, shell.State.Panel);                   // the phone's drawer closed
                w.Close();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ReplaceInAll_AsksFirst_ThenWritesEveryFile()
    {
        var root = Directory.CreateTempSubdirectory("lunet-replace-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var (w, shell) = Open(root);
                var project = shell.Workspace.Project!;
                project.CreateFile("A.cs", "// velocidade\nint velocidade = 1;\n");
                project.CreateFile("B.cs", "int velocidade = 2;\n");
                var search = shell.WorkspaceView.Explorer.Search;
                shell.WorkspaceView.Explorer.ShowSearch(true, "velocidade");
                search.Replacement = "speed";
                Wait(search.RunAsync());
                Assert.Equal(3, search.Matches.Count);

                search.ReplaceAll();                                                   // first tap: only shows the count
                Assert.Contains("velocidade", project.ReadText("A.cs"));
                search.ReplaceAll();                                                   // second tap: writes
                Assert.Equal("// speed\nint speed = 1;\n", project.ReadText("A.cs"));
                Assert.Equal("int speed = 2;\n", project.ReadText("B.cs"));
                w.Close();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TheEngine_StopsWhenCancelled_AndRejectsABadRegex()
    {
        var root = Directory.CreateTempSubdirectory("lunet-engine-").FullName;
        try
        {
            var project = new ProjectStore(root).Create("Jogo");
            project.WriteText("C.cs", "x x x\n");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Empty(ProjectSearch.SearchCancellable(project, "x", null, cancelled.Token));
            Assert.Null(ProjectSearch.PatternFor("(", new ProjectSearchOptions(UseRegex: true)));
            var (files, count) = ProjectSearch.ReplaceAll(project, @"(x)", "[$1]", new ProjectSearchOptions(UseRegex: true));
            Assert.Contains("C.cs", files);
            
            Assert.Equal("[x] [x] [x]\n", project.ReadText("C.cs"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
