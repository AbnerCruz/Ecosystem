using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>P3: the offline documentation inside the shell.</summary>
public sealed class LunetClientDocsTests
{
    [Fact]
    public void DocsLibrary_LoadsTheApiTheManualAndTheGuidesFromTheClient()
    {
        var browser = DocsLibrary.NewBrowser();
        Assert.NotEmpty(browser.Api.Types);
        Assert.NotEmpty(browser.GuideNames);
        var home = browser.Navigate("home");
        Assert.Contains(home.Blocks, b => b.Target == "category:api");
        Assert.Contains(browser.Navigate("search:SpriteBatch").Blocks, b => b.Target?.Contains("SpriteBatch") == true);
        // Internal validation guides are not shipped, exactly as in the Android assets.
        Assert.DoesNotContain("game-apk-smoke", browser.GuideNames);
    }

    [Fact]
    public void DocsPanel_OpensFromTheWorkspace_NavigatesAndGoesBack_AndOpensASymbol()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-docs-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.State.ShowPanel(SupportPanel.Docs);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var docs = shell.WorkspaceView.Support.Docs;
                Assert.True(docs.IsEffectivelyVisible);
                Assert.Equal("home", docs.Page!.Target);

                docs.Navigate("category:api");
                Assert.Equal("category:api", docs.Page.Target);
                docs.GoBack();
                Assert.Equal("home", docs.Page.Target);

                docs.OpenSymbol("T:Lunet.Graphics.SpriteBatch", "SpriteBatch");
                Assert.Equal("id:T:Lunet.Graphics.SpriteBatch", docs.Page.Target);
                docs.OpenSymbol("T:Nao.Existe", "Existe");
                Assert.Equal("search:Existe", docs.Page.Target);

                shell.State.ShowPanel(SupportPanel.None);
                shell.State.ShowPanel(SupportPanel.Docs);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("search:Existe", docs.Page.Target); // the page survives closing the panel
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
