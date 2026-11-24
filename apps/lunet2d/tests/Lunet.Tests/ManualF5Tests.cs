using System.Numerics;
using System.Reflection;
using System.Xml.Linq;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Docs;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

public sealed class ManualF5Tests
{
    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "docs", "manual", "catalog.json"))) return dir.FullName;
        throw new DirectoryNotFoundException("Manual do Lunet");
    }

    private static ManualCatalog Catalog() =>
        ManualCatalog.Read(File.ReadAllText(Path.Combine(Root(), "docs", "manual", "catalog.json")));

    [Fact]
    public void ReaderCatalogMatchesPackagedGuidesAndReferencesResolve()
    {
        var root = Root();
        var catalog = Catalog();
        var groups = Directory.GetFiles(Path.Combine(root, "docs", "guides"), "*.md");
        var manual = Directory.GetFiles(Path.Combine(root, "docs", "manual"), "*.md");
        var project = XDocument.Load(Path.Combine(root, "src", "Lunet.Android", "Lunet.Android.csproj"));
        var exclusions = project.Descendants("AndroidAsset").Single(e =>
            (string?)e.Attribute("Include") == @"..\..\docs\guides\*.md")
            .Attribute("Exclude")!.Value.Split(';').Select(p => Path.GetFileNameWithoutExtension(p.Replace('\\', '/')));
        Assert.Equal(catalog.Redirects.Keys.Order(), exclusions.Order());
        var guides = groups.Concat(manual).Where(p => !catalog.Redirects.ContainsKey(Path.GetFileNameWithoutExtension(p)))
            .ToDictionary(p => Path.GetFileNameWithoutExtension(p), File.ReadAllText);
        Assert.Equal(guides.Keys.Order(), catalog.Entries.Select(e => e.Id).Order());
        var browser = new DocumentationBrowser(DocsTests.Generate(), guides, catalog);
        Assert.Equal(new[] { "Tutoriais", "Tarefas", "Referência de API", "Conceitos" },
            browser.Resolve("home").Blocks.Select(block => block.Text));
        var targets = LearningCatalog.Projects.Select(p => p.ReferenceTarget)
            .Concat(guides.Values.SelectMany(g => MarkdownLite.Parse(g))
                .Where(b => b.Kind == MarkdownBlockKind.Link).Select(b => b.Target!));
        foreach (var target in targets)
            Assert.True(browser.Resolve(target).Title is not ("Não encontrado" or "Seção não encontrada"), target);

        Assert.Contains(browser.Resolve("search:diagnostico CS1002").Blocks, b => b.Target == "guide:ler-erros");
        Assert.Contains(browser.Resolve("search:tela azul").Blocks, b => b.Target == "guide:criar-e-rodar");
        Assert.Equal("guide:instalar-apk", browser.Resolve("guide:game-apk-signing-regression").Target);
        Assert.DoesNotContain(browser.Resolve("search:regression").Blocks, b => b.Target == "guide:game-apk-signing-regression");
        Assert.Equal("Seção não encontrada", browser.Resolve("guide:responder-ao-toque#inexistente").Title);
        browser.Navigate("home");
        browser.Navigate("search:CS1002");
        browser.Navigate("guide:ler-erros#erro-de-compilacao");
        Assert.Equal("search:CS1002", browser.Back()!.Target);
        Assert.Null(browser.NewNavigation().Current);
    }

    private static string SourceOf(string lesson) => MarkdownLite.Parse(File.ReadAllText(
        Path.Combine(Root(), "docs", "manual", lesson + ".md"))).Single(b => b.Language == "csharp full").Text;

    private static CompileResult Compile(string text) =>
        new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly))
            .Compile("Manual" + Guid.NewGuid().ToString("N"), [new SourceFile("Game.cs", text)]);

    private static Game Load(string source)
    {
        var result = Compile(source);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        var type = Assembly.Load(result.Assembly!).GetTypes().Single(t => t.IsSubclassOf(typeof(Game)));
        return (Game)Activator.CreateInstance(type)!;
    }

    [Fact]
    public void FirstTutorialRunsBlackThenBlueAndTeachesARealDiagnostic()
    {
        var source = SourceOf("criar-e-rodar");
        Assert.Contains(Compile(source.Replace("Clear(Color.Black);", "Clear(Color.Black)")).Diagnostics, d => d.Id == "CS1002");
        foreach (var (code, expected) in new[] { (source, Color.Black), (source.Replace("Color.Black", "Color.CornflowerBlue"), Color.CornflowerBlue) })
        {
            var backend = new RecordingBackend();
            var host = new GameHost(Load(code), backend);
            Assert.True(host.Start(360, 640));
            host.Tick(1.0 / 30);
            Assert.Equal(expected, backend.Clears[^1]);
        }
    }

    [Fact]
    public void TouchTutorialRunsAndOnlyNewPressChangesTheBackground()
    {
        var backend = new RecordingBackend();
        var host = new GameHost(Load(SourceOf("responder-ao-toque")), backend);
        Assert.True(host.Start(360, 640));
        host.Tick(1.0 / 30);
        Assert.Equal(Color.Black, backend.Clears[^1]);
        var target = new Vector2(90, 110);
        host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Pressed, target)]);
        host.Tick(1.0 / 30);
        Assert.Equal(Color.CornflowerBlue, backend.Clears[^1]);
        Assert.Equal(target, backend.LastQuadCenter());
        host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Moved, target)]);
        host.Tick(1.0 / 30);
        Assert.Equal(Color.CornflowerBlue, backend.Clears[^1]);
    }

    [Fact]
    public void SpriteTutorialLoadsTheRelativePngAndDrawsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Textures"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "Textures", "hero.png"), ContentTests.EncodePng(1, 1, 6, [0, 255, 0, 0, 255]));
            var backend = new RecordingBackend();
            var host = new GameHost(Load(SourceOf("desenhar-sprite")), backend, new DirectoryContentSource(root));
            Assert.True(host.Start(360, 640));
            host.Tick(1.0 / 30);
            var sprite = Assert.Single(backend.Batches);
            Assert.Equal(TextureFilter.Point, backend.Pixels[sprite.Texture].Filter);
            Assert.Equal(new Vector2(96, 112), backend.LastQuadCenter());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void AGameMovedOnDiskStillResolvesTheMissionByItsOriginalId()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var projects = new ProjectStore(Path.Combine(root, "games"));
            var game = projects.Create("Original");
            var progress = new LearningProgressStore(Path.Combine(root, "progress.json"));
            progress.LinkProject("Original", game.Manifest.GameId);
            progress.SetCompleted("LRN-001-P1-S1", true);
            Directory.Move(game.Directory, Path.Combine(Path.GetDirectoryName(game.Directory)!, "Movido"));
            var name = LauncherProjectCatalog.UniqueNameForId(LauncherProjectCatalog.Scan(projects), progress.ProjectGameId);
            Assert.Equal("Movido", name);
            Assert.Equal("Original", projects.Open(name!).Manifest.Name);
            Assert.True(progress.Completed("LRN-001-P1-S1"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void RebindingIsolatesGamesAndRecoveryPreservesTheDamagedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "progress.json");
            var store = new LearningProgressStore(path);
            store.LinkProject("Original", "game-a");
            store.SetCompleted("LRN-001-P1-S1", true);
            store.LinkProject("Novo", "game-b");
            Assert.Equal(0, store.CompletedCount);
            store.SetCompleted("LRN-001-P1-S2", true);
            store = new LearningProgressStore(path);
            store.LinkProject("Renomeado", "game-a");
            Assert.True(store.Completed("LRN-001-P1-S1"));
            Assert.False(store.Completed("LRN-001-P1-S2"));
            store.LinkProject("Novo", "game-b");
            Assert.True(store.Completed("LRN-001-P1-S2"));
            File.WriteAllText(path + ".lunet-tmp", "{interrompido");
            Assert.Equal("game-b", new LearningProgressStore(path).ProjectGameId);
            File.WriteAllText(path, "{danificado");
            Assert.True(LearningProgressStore.HasRecovery(path));
            LearningProgressStore.RestoreBackup(path);
            Assert.Equal("game-a", new LearningProgressStore(path).ProjectGameId);
            Assert.Equal("{danificado", File.ReadAllText(Assert.Single(Directory.GetFiles(root, "*.preserved-*"))));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
