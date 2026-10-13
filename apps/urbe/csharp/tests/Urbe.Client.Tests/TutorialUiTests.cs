using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-40 (DEC-0045): the Tutorial is a panel of its own. The 1.8.4 (tutorial.js) created a Tutorial neighbourhood in the
/// city the first time it opened; now nothing is created unless asked, and Configurações → Tutorial opens the panel.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Tutorial_IsAPanelAndTheCityIsLeftAlone() =>
        Assert.True(await Session.Dispatch(TutorialScenario, CancellationToken.None));

    private static async Task<bool> TutorialScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-tutorial-" + Guid.NewGuid().ToString("N"));
        var empty = Path.Combine(dir, "Vazia");
        Directory.CreateDirectory(empty);
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(empty), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            for (int i = 0; i < 80; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

            // an empty city stays empty: no Tutorial folder, no mark, no houses, no editor
            Assert.False(Directory.Exists(Path.Combine(empty, "Tutorial")));
            Assert.False(File.Exists(Path.Combine(empty, ".urbe", "tutorial.json")));
            Assert.DoesNotContain(view.World.City!.Buildings, b => b.IsNote);
            Assert.False(view.Editor.IsVisible);

            // Configurações → Tutorial opens the panel on its index: every article, "Comece aqui" first
            _ = view.ShowSettingsAsync();
            Dispatcher.UIThread.RunJobs();
            ClickButtonWithText(view.Dialogs, "Tutorial");
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var panel = view.Tutorial;
            Assert.True(panel.IsVisible);
            var all = TutorialNotes.Load();
            // the articles are the .md notes (the example page of Páginas is not one)
            Assert.Equal(all.Count(a => a.Path.EndsWith(".md", StringComparison.Ordinal)), panel.ListedPaths.Count);
            Assert.Equal(LegacyTutorial.Start, panel.ListedPaths[0]);
            Snapshot(window, "native-tutorial-index.png");

            // search filters the index by title and text
            panel.SearchText = "lixeira";
            Assert.Contains("Tutorial/Notas/Lixeira, versões e desfazer.md", panel.ListedPaths);
            Assert.True(panel.ListedPaths.Count < all.Count);
            panel.SearchText = "";

            // an article: rendered, with Próximo, and its [[links]] move inside the tutorial
            Assert.True(panel.ShowArticle(LegacyTutorial.Start));
            Assert.Equal(LegacyTutorial.Start, panel.Article!.Path);
            Snapshot(window, "native-tutorial-article.png");
            ClickButtonWithText(panel, "Próximo ›");
            Assert.NotEqual(LegacyTutorial.Start, panel.Article!.Path);
            Assert.True(panel.FollowLink("Glossário"));
            Assert.Equal("Tutorial/Glossário.md", panel.Article!.Path);
            Assert.False(panel.FollowLink("Nota que não existe"));
            panel.ShowIndex();
            Assert.Null(panel.Article);
            panel.Close();
            Assert.False(panel.IsVisible);

            // copying it into the city is still possible, only when asked: the notes, their houses and the mark
            await view.RestoreTutorialAsync(false);
            foreach (var f in all)
                Assert.Equal(f.Content, File.ReadAllText(Path.Combine(empty, f.Path)));
            Assert.StartsWith("{\"versao\":\"" + MainView.TutorialVersion + "\",\"em\":\"", File.ReadAllText(Path.Combine(empty, ".urbe", "tutorial.json")));
            Assert.Equal(all.Count, view.World.City.Buildings.Count(b => b.IsNote));
            await view.RestoreTutorialAsync(false);
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "O Tutorial já está completo");
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
