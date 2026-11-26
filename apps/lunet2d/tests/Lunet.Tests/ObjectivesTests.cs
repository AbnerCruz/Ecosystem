using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>H6: Objetivos beside the code — the game's mission, its checklist (same progress as Aprender), the reference and a hint in Estudo.</summary>
public sealed class ObjectivesTests
{
    [Fact]
    public void ALinkedGame_ShowsItsObjectivesInTheWorkspace_SavesTheChecklist_AndAsksForAHintWithoutWriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-objectives-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var ai = Path.Combine(root, ".ai");
                var shell = new LunetShell(root) { AiHost = new Lunet.Client.Views.AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(ai, "s.json"), new NullVault()), ai, new NoModel()) };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var gameId = shell.Workspace.Project!.Manifest.GameId;
                var challenge = LearningCatalog.FindProject(LearningCatalog.FirstProjectId)!;
                new LearningProgressStore(shell.LearningProgressPath).LinkProject(challenge.Id, shell.Workspace.Project.Name, gameId);

                shell.State.ShowPanel(SupportPanel.More);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Objetivos"); // offered in Painéis
                shell.State.ShowPanel(SupportPanel.Objectives);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var first = challenge.Steps[0];
                var box = window.GetVisualDescendants().OfType<CheckBox>().First(c => c.Name == "Objective-" + first.Id);
                box.IsChecked = true;
                Assert.True(new LearningProgressStore(shell.LearningProgressPath).Completed(challenge.Id, first.Id)); // saved, same file as Aprender

                var before = shell.WorkspaceView.Code.Surface.Text;
                shell.WorkspaceView.Support.Objectives.Hint(challenge, first);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(SupportPanel.Ai, shell.State.Panel);
                Assert.Equal(Lunet.Ai.LunetAiMode.Teach, shell.WorkspaceView.Support.Ai.Mode);
                Assert.Contains(first.Objective, shell.WorkspaceView.Support.Ai.Input.Text);
                Assert.Equal(before, shell.WorkspaceView.Code.Surface.Text);                                        // nothing written
                Assert.Null(shell.WorkspaceView.Support.Ai.LastReply);                                              // nothing sent by itself
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class NullVault : Lunet.Ai.ISecretVault
    {
        public string? Get(string name) => null;
        public void Set(string name, string value) { }
        public void Delete(string name) { }
    }

    private sealed class NoModel : AgentRuntime.IModelProvider
    {
        public string ProviderId => "chat-completions";
        public ValueTask<AgentRuntime.ModelResponse> CompleteAsync(AgentRuntime.ModelRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("not used");
    }
}

public sealed class RunButtonLayoutTests
{
    [Fact]
    public void Rodar_NeverCoversTheCodeBar()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-runfab-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.WorkspaceView.Code.ShowKeyboard();
                shell.State.ShowPanel(SupportPanel.Objectives); // the state of capture 9a: a panel open…
                shell.WorkspaceView.Code.ShowKeyboard();         // …and the editor taking focus again brings the bar back
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var run = shell.WorkspaceView.RunButton!; var bar = shell.WorkspaceView.Code.Keyboard!;
                Console.WriteLine($"bar visible={bar.IsVisible} attached={TopLevel.GetTopLevel(run) is not null} keyboardOpen={shell.KeyboardOpen}");
                Assert.True(bar.IsEffectivelyVisible && TopLevel.GetTopLevel(run) is not null, $"bar={bar.IsEffectivelyVisible} fab={TopLevel.GetTopLevel(run) is not null} kb={shell.KeyboardOpen}");
                var runBox = new Avalonia.Rect(run.TranslatePoint(new(0, 0), window)!.Value, run.Bounds.Size);
                var barBox = new Avalonia.Rect(bar.TranslatePoint(new(0, 0), window)!.Value, bar.Bounds.Size);
                Assert.False(runBox.Intersects(barBox), $"Rodar {runBox} cobre a barra {barBox}");
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

public sealed class WorkspaceTabsTests
{
    [Fact]
    public void TheCodeBar_HidesTheTabsOnlyInCodigo_SoJogoAlwaysHasAWayBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-tabs-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.WorkspaceView.Code.ShowKeyboard();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(shell.WorkspaceView.TabBarVisible);           // typing code: the bar gives way
                shell.State.SelectTab(WorkspaceTab.Game);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(shell.WorkspaceView.TabBarVisible);            // Jogo: the tabs are there
                shell.State.SelectTab(WorkspaceTab.Code);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(shell.WorkspaceView.TabBarVisible);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

public sealed class DirtyTabTests
{
    [Fact]
    public void TheOpenFileTab_ShowsUnsavedChanges_AndClearsAfterSaving()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-dirty-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var code = shell.WorkspaceView.Code;
                Assert.DoesNotContain("•", code.OpenTabLabel);
                code.Surface.Text += "\n// mudança";
                code.UpdateDirtyMark();                                  // what the pause after typing does
                Assert.EndsWith(" •", code.OpenTabLabel);
                Assert.True(code.Save());
                Assert.DoesNotContain("•", code.OpenTabLabel);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
