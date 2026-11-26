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
                Assert.Equal(SupportPanel.Objectives, shell.State.Panel);
                Assert.Equal("LRN-001-P1", shell.ActiveChallenge!.Id);
                var support = shell.WorkspaceView.Support;
                var second = shell.ActiveChallenge.Steps[1];
                Find<CheckBox>(support, "Step-" + second.Id)!.IsChecked = true;
                var progress = new LearningProgressStore(shell.LearningProgressPath);
                Assert.Equal(shell.Workspace.Project!.Manifest.GameId, progress.ProjectGameIdFor("LRN-001-P1"));
                Assert.True(progress.Completed("LRN-001-P1", second.Id));

                var reference = support.GetLogicalDescendants().OfType<Button>()
                    .Single(button => button.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Referência"));
                Click(reference);
                Assert.Equal(SupportPanel.Docs, shell.State.Panel);
                Assert.StartsWith("guide:responder-ao-toque", support.Docs.Page!.Target);
                Assert.True(shell.Back());
                Assert.Equal(SupportPanel.Objectives, shell.State.Panel);
                shell.GoHome();
                Assert.Same(learn, shell.Home.Sheet);
                Assert.Equal("LRN-001-P1", learn.Showing);
                Assert.True(Find<CheckBox>(learn, "Step-" + second.Id)!.IsChecked);
                Assert.NotNull(Find<Button>(learn, "OpenChallenge"));
                Assert.True(shell.Back());
                Assert.Equal("LRN-001", learn.Showing);
                Assert.True(shell.Back());
                Assert.Equal("catalog", learn.Showing);
                Assert.True(shell.Back());
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
