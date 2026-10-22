using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-40/UC-64 (DEC-0045): the Tutorial is a panel of its own, only to read. The 1.8.4 (tutorial.js) created a Tutorial
/// folder in the vault the first time it opened; now nothing is created unless asked, Configurações → Tutorial
/// opens the panel, and its text is the native app's documentation (AppDocs).
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Tutorial_IsAPanelAndTheVaultIsLeftAlone() =>
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

            // an empty vault stays empty: no Tutorial folder, no mark, no notes, no editor
            Assert.False(Directory.Exists(Path.Combine(empty, "Tutorial")));
            Assert.False(File.Exists(Path.Combine(empty, ".urbe", "tutorial.json")));
            Assert.Empty(view.Documents!.List());
            Assert.False(view.Editor.IsVisible);

            // Configurações → Tutorial opens the panel on its index: every article, "Comece aqui" first
            _ = view.ShowSettingsAsync();
            Dispatcher.UIThread.RunJobs();
            ClickButtonWithText(view.Dialogs, "Tutorial");
            for (int i = 0; i < 20; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var panel = view.Tutorial;
            Assert.True(panel.IsVisible);
            var all = AppDocs.Load();
            // the articles are the .md notes (the example page of Páginas is not one)
            Assert.Equal(all.Count(a => a.Path.EndsWith(".md", StringComparison.Ordinal)), panel.ListedPaths.Count);
            Assert.Equal(LegacyTutorial.Start, panel.ListedPaths[0]);
            Snapshot(window, "native-tutorial-index.png");

            // search filters the index by title and text
            panel.SearchText = "lixeira";
            Assert.Contains("Tutorial/Escrever/Explorer.md", panel.ListedPaths);
            Assert.True(panel.ListedPaths.Count < all.Count);
            panel.SearchText = "";

            // an article: rendered, with Próximo, and its [[links]] move inside the tutorial
            Assert.True(panel.ShowArticle(LegacyTutorial.Start));
            Assert.Equal(LegacyTutorial.Start, panel.Article!.Path);
            Snapshot(window, "native-tutorial-article.png");
            ClickButtonWithText(panel, "Próximo ›");
            Assert.NotEqual(LegacyTutorial.Start, panel.Article!.Path);
            Assert.True(panel.FollowLink("Editor"));
            Assert.Equal("Tutorial/Escrever/Editor.md", panel.Article!.Path);
            Assert.False(panel.FollowLink("Nota que não existe"));
            panel.ShowIndex();
            Assert.Null(panel.Article);
            panel.Close();
            Assert.False(panel.IsVisible);

            // copying it into the vault is still possible, only when asked: the notes and the mark
            await view.RestoreTutorialAsync(false);
            foreach (var f in all)
                Assert.Equal(f.Content, File.ReadAllText(Path.Combine(empty, f.Path)));
            Assert.StartsWith("{\"versao\":\"" + MainView.TutorialVersion + "\",\"em\":\"", File.ReadAllText(Path.Combine(empty, ".urbe", "tutorial.json")));
            Assert.Equal(all.Count, view.Documents.List().Count);
            await view.RestoreTutorialAsync(false);
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "O Tutorial já está completo");
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    /// <summary>UC-64: an article is documentation — rendered, never in the note editor, with no text box to type in.</summary>
    [Fact]
    public async Task Tutorial_ArticleIsReadOnlyAndSearchAndLinksStayInside() =>
        Assert.True(await Session.Dispatch(TutorialReadOnlyScenario, CancellationToken.None));

    private static async Task<bool> TutorialReadOnlyScenario()
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.InitializeAsync();
        for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

        // no folder open: no notes, never the Tutorial created by itself
        Assert.Empty(view.Documents!.List());

        var panel = view.Tutorial;
        panel.Open();
        Dispatcher.UIThread.RunJobs();
        // the index: every section in order, "Comece aqui" first
        Assert.Equal(AppDocs.Load().Select(a => a.Path), panel.ListedPaths);

        // search across titles and text, accents ignored
        panel.SearchText = "voz";
        Assert.Contains("Tutorial/Escrever/Explorer.md", panel.ListedPaths);
        Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Nota rápida", StringComparison.Ordinal) == true);
        panel.SearchText = "formulas";
        Assert.Contains("Tutorial/Escrever/Editor.md", panel.ListedPaths);
        panel.SearchText = "nada disso existe aqui";
        Assert.Empty(panel.ListedPaths);
        panel.SearchText = "";

        // an article: rendered, read only, the note editor stays closed
        ClickButtonWithText(panel, "Editor");
        Assert.Equal("Tutorial/Escrever/Editor.md", panel.Article!.Path);
        Assert.False(view.Editor.IsVisible);
        Assert.DoesNotContain(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.IsEffectivelyVisible);
        Assert.Contains(panel.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("urbe-box"));   // the :::dica box
        Snapshot(window, "native-tutorial-editor-article.png");

        // links move inside the tutorial, with [[Nome|texto]] and [[Nome#seção]]
        Assert.True(panel.FollowLink("Modelos|os modelos"));
        Assert.Equal("Tutorial/Escrever/Modelos.md", panel.Article!.Path);
        Assert.True(panel.FollowLink("Backups, exportar e restaurar#Exportar"));
        Assert.Equal("Tutorial/Ajustes/Backups, exportar e restaurar.md", panel.Article!.Path);
        ClickButtonWithText(panel, "‹ Anterior");
        Assert.Equal("Tutorial/Ajustes/Personalização.md", panel.Article!.Path);
        Assert.False(view.Editor.IsVisible);
        panel.Close();
        window.Close();
        return true;
    }

    /// <summary>UC-64: every [[link]] of the documentation opens another article of it.</summary>
    [Fact]
    public async Task Tutorial_EveryLinkOfTheDocsOpensAnArticle() => Assert.True(await Session.Dispatch(() =>
    {
        var docs = AppDocs.Load();
        var panel = new TutorialPanel(docs);
        foreach (var doc in docs)
        {
            // links written as examples inside code are not links
            var text = System.Text.RegularExpressions.Regex.Replace(doc.Content, @"```.*?```|`[^`\n]*`", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\[\[([^\[\]\n]+)\]\]"))
                Assert.True(panel.FollowLink(m.Groups[1].Value), doc.Path + ": [[" + m.Groups[1].Value + "]]");
        }
        Assert.All(docs, d => Assert.StartsWith(LegacyTutorial.Folder, d.Path, StringComparison.Ordinal));
        Assert.Equal(LegacyTutorial.Start, docs[0].Path);
        return true;
    }, CancellationToken.None));
}
