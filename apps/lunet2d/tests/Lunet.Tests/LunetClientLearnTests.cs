using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>P3: Aprender in the shell, on the same progress file as the previous screens.</summary>
public sealed class LunetClientLearnTests
{
    [Fact]
    public void Learn_CourseChallengeObjectives_CreateTheGameAndKeepTheProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-learn-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var learn = shell.Home.ShowLearn();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("catalog", learn.Showing);
                Assert.Null(Find<Button>(learn, "Course-LRN-006")); // not available yet: listed, not tappable

                Click(Find<Button>(learn, "Course-LRN-001")!);
                Assert.Equal("LRN-001", learn.Showing);
                Click(Find<Button>(learn, "Project-LRN-001-P1")!);
                Assert.Equal("LRN-001-P1", learn.Showing);

                var step = LearningCatalog.FindProject("LRN-001-P1")!.Steps[0];
                Find<CheckBox>(learn, "Step-" + step.Id)!.IsChecked = true;
                Assert.True(new LearningProgressStore(shell.LearningProgressPath).Completed("LRN-001-P1", step.Id));

                Assert.True(shell.Back()); // back to the course
                Assert.Equal("LRN-001", learn.Showing);
                Click(Find<Button>(learn, "Project-LRN-001-P1")!);
                Click(Find<Button>(learn, "CreateChallenge")!);
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                Assert.Equal("AprenderLRN001P1", shell.State.Project);
                Assert.Equal(SupportPanel.Objectives, shell.State.Panel); // the mission's objectives beside the code (anexo A §6.4)
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Click(Find<Button>(shell, "ObjectiveReference")!);              // its Referência: the exact offline page
                Assert.Equal(SupportPanel.Docs, shell.State.Panel);
                Assert.StartsWith("guide:responder-ao-toque", shell.WorkspaceView.Support.Docs.Page!.Target);
                Assert.Null(shell.Home.Sheet);

                var progress = new LearningProgressStore(shell.LearningProgressPath);
                Assert.Equal(shell.Workspace.Project!.Manifest.GameId, progress.ProjectGameIdFor("LRN-001-P1"));

                shell.GoHome();
                learn = shell.Home.ShowLearn();
                Click(Find<Button>(learn, "Resume")!);
                Assert.Equal("LRN-001-P1", learn.Showing);
                Assert.NotNull(Find<Button>(learn, "OpenChallenge")); // linked: open, not create again
                Assert.True(shell.Back());
                Assert.True(shell.Back()); // catalog → closes Aprender
                Assert.Null(shell.Home.Sheet);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static T? Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    private static void Click(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
