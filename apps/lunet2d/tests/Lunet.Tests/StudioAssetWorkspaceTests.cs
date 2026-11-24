using Lunet.Core;

namespace Lunet.Tests;

public sealed class StudioAssetWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-standalone-" + Guid.NewGuid().ToString("N"));
    private StudioAssetWorkspace Workspace => new(Path.Combine(_root, "Studio"));
    private ReusableAssetLibrary Library => new(Path.Combine(_root, "Library"));
    private ProjectStore Projects => new(Path.Combine(_root, "Projects"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void StandaloneStudio_WritesAndReopensEditableSpriteWithoutProject()
    {
        var workspace = Workspace;
        var path = workspace.NewPath("Assets", "hero", ".sprite.json");
        workspace.WriteText(path, "{\"frames\":[1]}");
        workspace.WriteBytes(path[..^".sprite.json".Length] + ".png", [137, 80, 78, 71]);

        var reopened = Workspace;
        Assert.Equal("{\"frames\":[1]}", reopened.ReadText(path));
        Assert.Equal(2, reopened.ListFiles().Count);
        Assert.False(Directory.Exists(Path.Combine(_root, "Projects")));
    }

    [Fact]
    public void StandaloneLibrary_PublishesAndCopiesIntoTwoDifferentProjects()
    {
        var workspace = Workspace;
        workspace.WriteText("Assets/hero.sprite.json", "editable-source");
        workspace.WriteBytes("Assets/hero.png", [1, 2, 3, 4]);
        var library = Library;
        var published = library.PublishWorkspace(workspace.RootDirectory, "Assets/hero.sprite.json",
            "Hero", "me", "Uso próprio");
        Assert.Equal(2, published.Files.Length);
        var p1 = Projects.Create("First");
        var p2 = Projects.Create("Second");
        library.ImportToProject(published.Id, published.Version, p1, TestContext.Current.CancellationToken);
        library.ImportToProject(published.Id, published.Version, p2, TestContext.Current.CancellationToken);
        Assert.Equal("editable-source", p1.ReadText("Assets/hero.sprite.json"));
        Assert.Equal("editable-source", p2.ReadText("Assets/hero.sprite.json"));

        workspace.WriteText("Assets/hero.sprite.json", "new-draft");
        Assert.Equal("editable-source", p1.ReadText("Assets/hero.sprite.json"));
        Assert.Equal("editable-source", p2.ReadText("Assets/hero.sprite.json"));
        Assert.Equal("editable-source", File.ReadAllText(Path.Combine(_root, "Library",
            published.Id, "v1", "files", "Assets", "hero.sprite.json")));
    }

    [Fact]
    public void StandaloneMusic_ExportsSourceAndWavWithoutProject()
    {
        var w = Workspace;
        w.WriteText("Content/Audio/theme.song.json", "{}");
        w.WriteBytes("Content/Audio/theme.wav", [82, 73, 70, 70]);
        var published = Library.PublishWorkspace(w.RootDirectory,
            "Content/Audio/theme.song.json", "Theme", "", "Uso próprio");
        Assert.Equal(2, published.Files.Length);
        Assert.Contains(published.Files, x => x.Path.EndsWith(".wav"));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("Assets/../../outside.txt")]
    [InlineData("/outside.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("NotAnAsset/file.txt")]
    [InlineData("Assets//sprite.png")]
    public void StandaloneWorkspace_RejectsTraversalAndNonAssetPaths(string path)
    {
        Assert.Throws<ArgumentException>(() => Workspace.WriteText(path, "malicious"));
    }

    [Fact]
    public void ExistingDestinationFiles_AreNotOverwrittenByStudioLibraryImport()
    {
        var w = Workspace;
        w.WriteText("Assets/heroi.sprite.json", "from library");
        w.WriteBytes("Assets/heroi.png", [1, 2, 3]);
        var pub = Library.PublishWorkspace(w.RootDirectory, "Assets/heroi.sprite.json",
            "heroi", "me", "Uso próprio");
        var game = Projects.Create("Game");
        game.WriteText("Assets/heroi.sprite.json", "manual edits");
        Assert.Throws<IOException>(() => Library.ImportToProject(pub.Id, 1, game, TestContext.Current.CancellationToken));
        Assert.Equal("manual edits", game.ReadText("Assets/heroi.sprite.json"));
        Assert.False(File.Exists(Path.Combine(game.Directory, "Assets", "heroi.png")));
    }
}
