using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>UX09: from a compiler problem to the documentation of the name it is about, without leaving the code.</summary>
public sealed class ProblemDocsTests
{
    [Theory]
    [InlineData("'SpriteBatch' does not contain a definition for 'Desenhar'", "search:SpriteBatch")]
    [InlineData("The name 'velocidade' does not exist in the current context", "search:velocidade")]
    [InlineData("O nome 'Lunet.Graphics.Texture2D' não existe", "search:Texture2D")]
    [InlineData("Cannot convert 'List<Vector2>' to 'int'", "search:List")]
    [InlineData("; expected", null)]
    [InlineData("Unexpected character '#'", null)]
    public void TheTarget_IsTheFirstNameTheCompilerQuotes(string message, string? expected) =>
        Assert.Equal(expected, SupportPanelView.DocsTarget(message));

    [Fact]
    public void OpeningDocsFromAProblem_ShowsThatPage_NextToTheCode()
    {
        var root = Directory.CreateTempSubdirectory("lunet-problem-docs-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                shell.WorkspaceView.Support.OpenDocs(SupportPanelView.DocsTarget("'SpriteBatch' does not contain a definition for 'X'")!);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(SupportPanel.Docs, shell.State.Panel);
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);            // still in the game
                var page = shell.WorkspaceView.Support.Docs.Page!;
                Assert.Equal("search:SpriteBatch", page.Target);
                Assert.NotEmpty(page.Blocks);                                     // the search found the Framework type
                Assert.True(shell.Back());                                        // Back returns to the code
                Assert.Equal(SupportPanel.None, shell.State.Panel);
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
}
