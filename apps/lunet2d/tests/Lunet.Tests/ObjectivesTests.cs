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
