using System.Text.Json;
using AgentRuntime;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// Owner, 27/11: "usar a IA tá impossível de ruim, o Numa faz isso bem". Like the Numa, what the AI changes shows in the
/// file itself: the file opens with the changed lines tinted and a bar to keep the change or undo the whole run; on a
/// phone the AI panel steps aside so the code is seen.
/// </summary>
public sealed class AiReviewInFileTests
{
    private sealed class Writes(string path, string content) : IModelProvider
    {
        private int _step;
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_step++ % 2 == 0
                ? new ModelResponse([new ToolUseBlock("c" + _step, "lunet.files.write", JsonSerializer.SerializeToElement(new { path, content, expected_sha256 = "" }))],
                    StopReason.ToolUse, new Usage(1, 1, Money.Zero("USD")))
                : new ModelResponse([new AgentRuntime.TextBlock("Criei o jogador.")], StopReason.EndTurn, new Usage(1, 1, Money.Zero("USD"))));
    }

    private sealed class NullVault : Lunet.Ai.ISecretVault
    {
        public string? Get(string name) => null;
        public void Set(string name, string value) { }
        public void Delete(string name) { }
    }

    private static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    private static void Run(Window w, AiPanelView ai, string prompt)
    {
        var fazer = w.GetVisualDescendants().OfType<Button>().First(b => b.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Any(t => t.Text == "Fazer"));
        Click(fazer);
        ai.Input.Text = prompt;
        var run = ai.SendAsync();
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!run.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        Assert.True(run.IsCompleted);
        run.GetAwaiter().GetResult();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void TheAiChange_ShowsInTheFile_ToKeepOrUndo()
    {
        var root = Directory.CreateTempSubdirectory("lunet-ai-review-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var aiDir = Path.Combine(root, ".ai");
                var model = new Writes("Jogador.cs", "using Lunet;\n\npublic sealed class Jogador\n{\n    public int Vida = 3;\n}\n");
                var shell = new LunetShell(root)
                {
                    AiHost = new AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new NullVault()), aiDir, model),
                };
                var w = new Window { Width = 412, Height = 915, Content = shell };
                w.Show();
                shell.CreateProject();
                shell.State.ShowPanel(SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = shell.WorkspaceView.Support.Ai;
                var file = Path.Combine(shell.Workspace.Project!.Directory, "Jogador.cs");

                Run(w, ai, "crie um jogador com vida");
                var editor = shell.WorkspaceView.Code;
                Assert.Equal("Jogador.cs", editor.OpenPath);                                   // the changed file opens
                Assert.Equal(6, ((CodeSurface)editor.Surface).ChangedLines.Count);              // its new lines are tinted
                Assert.Contains(w.GetVisualDescendants().OfType<Control>(), c => c.Name == "AiReviewBar" && c.IsEffectivelyVisible);
                Assert.Equal(SupportPanel.None, shell.State.Panel);                             // the panel stepped aside
                Click(w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ReviewUndo"));
                Assert.False(File.Exists(file));                                                // undo: the new file is gone
                Assert.Empty(((CodeSurface)editor.Surface).ChangedLines);

                shell.State.ShowPanel(SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Run(w, ai, "crie de novo");
                Click(w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ReviewKeep"));
                Assert.True(File.Exists(file));                                                 // kept
                Assert.DoesNotContain(w.GetVisualDescendants().OfType<Control>(), c => c.Name == "AiReviewBar" && c.IsEffectivelyVisible);
                Assert.Empty(((CodeSurface)editor.Surface).ChangedLines);
                w.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(null, "a\nb\n", new[] { 1, 2 })]
    [InlineData("a\nb\nc\n", "a\nB\nc\n", new[] { 2 })]
    [InlineData("a\nb\nc\n", "a\nc\n", new int[0])]
    [InlineData("a\nc\n", "a\nb\nc\nd\n", new[] { 2, 4 })]
    public void ChangedLines_AreTheAddedOrRewrittenLinesOfTheNewText(string? before, string after, int[] expected) =>
        Assert.Equal(expected, DocumentEditor.ChangedLinesOf(before, after));
}
