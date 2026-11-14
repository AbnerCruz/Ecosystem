using System.IO.Compression;
using Lunet.Core;

namespace Lunet.Tests;

public class ProjectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-proj-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Create_WritesManifestAndEntryPointAsPlainFiles()
    {
        var project = new ProjectStore(_root).Create("Space_Shooter");
        Assert.True(File.Exists(Path.Combine(_root, "Space_Shooter", "lunet.json")));
        var source = project.ReadText("Game.cs");
        Assert.Contains("class SpaceShooter : Game", source);
        Assert.DoesNotContain("Texture2D", source);
        Assert.Contains("SpriteBatch batch", source);
        Assert.Contains("protected override void Initialize()", source);
        Assert.Contains("protected override void LoadContent()", source);
        Assert.Contains("protected override void Update(GameTime time)", source);
        Assert.Contains("protected override void Draw(GameTime time)", source);
        Assert.Contains("protected override void UnloadContent()", source);
        Assert.Contains("batch.Begin()", source);
        Assert.Contains("batch.End()", source);
        Assert.DoesNotContain("CreateCircle", source);
        Assert.DoesNotContain("TryGetPointer", source);
        Assert.DoesNotContain("ball", source, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("com.lunet.games.spaceshooter.g", project.Manifest.PackageId);
        Assert.True(OfflineGameApkBuilder.ValidPackage(project.Manifest.PackageId));
        Assert.True(Guid.TryParse(project.Manifest.GameId, out _));
    }

    [Fact]
    public void Create_TwoDistinctNamesWithSameSanitizedSlugHaveDifferentAndroidPackageIds()
    {
        var store = new ProjectStore(_root);
        var first = store.Create("Bosque-Lumi");
        var second = store.Create("BosqueLumi");
        Assert.NotEqual(first.Manifest.GameId, second.Manifest.GameId);
        Assert.NotEqual(first.Manifest.PackageId, second.Manifest.PackageId);
        Assert.True(OfflineGameApkBuilder.ValidPackage(first.Manifest.PackageId));
        Assert.True(OfflineGameApkBuilder.ValidPackage(second.Manifest.PackageId));
        Assert.Equal(first.Manifest.PackageId, store.Open("Bosque-Lumi").Manifest.PackageId);
    }

    [Fact]
    public void Create_LandscapePersistsOrientationAndDimensionsOnReopen()
    {
        var store = new ProjectStore(_root);
        var game = store.Create("WideGame", ProjectTemplate.Blank, "landscape");
        Assert.Equal("landscape", game.Manifest.Orientation);
        Assert.True(game.Manifest.VirtualWidth > game.Manifest.VirtualHeight);
        Assert.Equal("landscape", store.Open(game.Name).Manifest.Orientation);
    }

    [Fact]
    public void Create_FullSettingsRoundTripAndGenerateMatchingGameConfiguration()
    {
        var store = new ProjectStore(_root);
        var settings = new ProjectCreationSettings("MeuJogo", "landscape", 800, 450, 120, "Fill",
            "1.2.0-beta", "com.exemplo.meujogo");
        var project = store.Create(settings);
        var reopened = store.Open("MeuJogo");
        Assert.Equal("landscape", reopened.Manifest.Orientation);
        Assert.Equal(800, reopened.Manifest.VirtualWidth);
        Assert.Equal(450, reopened.Manifest.VirtualHeight);
        Assert.Equal(120, reopened.Manifest.UpdatesPerSecond);
        Assert.Equal("Fill", reopened.Manifest.ViewportScaling);
        Assert.Equal("1.2.0-beta", reopened.Manifest.Version);
        Assert.Equal("com.exemplo.meujogo", reopened.Manifest.PackageId);
        Assert.True(Guid.TryParse(reopened.Manifest.GameId, out _));
        string source = reopened.ReadText("Game.cs");
        Assert.Contains("Configuration.VirtualWidth = 800;", source);
        Assert.Contains("Configuration.VirtualHeight = 450;", source);
        Assert.Contains("Configuration.UpdatesPerSecond = 120;", source);
        Assert.Contains("Configuration.ViewportScaling = ViewportScalingMode.Fill;", source);
        Assert.DoesNotContain("CreateCircle", source);
    }

    [Fact]
    public void Create_CustomPackageCollisionIsRejectedWithoutCreatingDirectory()
    {
        var store = new ProjectStore(_root);
        store.Create(new ProjectCreationSettings("Primeiro", PackageId: "com.exemplo.jogo"));
        Assert.Throws<ProjectException>(() => store.Create(
            new ProjectCreationSettings("Segundo", PackageId: "com.exemplo.jogo")));
        Assert.False(Directory.Exists(Path.Combine(_root, "Segundo")));
    }

    [Fact]
    public void Create_InvalidFullSettingsNeverLeavePartialProjects()
    {
        var store = new ProjectStore(_root);
        var invalid = new[]
        {
            new ProjectCreationSettings("BrokenWidth", VirtualWidth: 0),
            new ProjectCreationSettings("BrokenHeight", VirtualHeight: 99999),
            new ProjectCreationSettings("BrokenUps", UpdatesPerSecond: 0),
            new ProjectCreationSettings("BrokenScaling", ViewportScaling: "Stretch"),
            new ProjectCreationSettings("BrokenVersion", Version: "../"),
            new ProjectCreationSettings("BrokenPackage", PackageId: "bad-id"),
            new ProjectCreationSettings("BrokenOrientation", Orientation: "diagonal")
        };
        foreach (var settings in invalid)
        {
            Assert.Throws<ProjectException>(() => store.Create(settings));
            Assert.DoesNotContain(settings.Name, store.List());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("../evil")]
    [InlineData("..")]
    [InlineData("ação")]
    public void Create_RejectsInvalidNames(string name) =>
        Assert.Throws<ProjectException>(() => new ProjectStore(_root).Create(name));

    [Fact]
    public void Create_RejectsDuplicate_AndListIsSorted()
    {
        var store = new ProjectStore(_root);
        store.Create("b");
        store.Create("A");
        Assert.Throws<ProjectException>(() => store.Create("b"));
        Assert.Equal(["A", "b"], store.List());
    }

    [Fact]
    public void Files_CannotEscapeProjectDirectory()
    {
        var project = new ProjectStore(_root).Create("p");
        Assert.Throws<ProjectException>(() => project.ReadText("../p2/lunet.json"));
        Assert.Throws<ProjectException>(() => project.WriteText("/etc/passwd", "x"));
        Assert.Throws<ProjectException>(() => project.DeleteFile("lunet.json"));
    }

    [Fact]
    public void WriteText_IsAtomicAndLeavesNoTemporaryFiles()
    {
        var project = new ProjectStore(_root).Create("p");
        project.WriteText("Code/Player.cs", "class Player { }");
        project.WriteText("Code/Player.cs", "class Player2 { }");
        Assert.Equal("class Player2 { }", project.ReadText("Code/Player.cs"));
        Assert.DoesNotContain(project.ListFiles(), f => f.EndsWith(".lunet-tmp"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "p"), "*.lunet-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void LoadSources_IncludesNestedCodeAndSkipsTestsAndCache()
    {
        var project = new ProjectStore(_root).Create("p");
        project.WriteText("Code/Player.cs", "class P { }");
        project.WriteText("Tests/T.cs", "class T { }");
        project.WriteText(".lunet/cache/x.cs", "class X { }");
        Assert.Equal(["Code/Player.cs", "Game.cs"], project.LoadSources().Select(s => s.Path).Order().ToArray());
    }

    [Fact]
    public void Import_CopiesBinaryFileIntoProjectAndSanitizesName()
    {
        var project = new ProjectStore(_root).Create("p");
        var path = project.Import("Content/Textures", "../../evil/hero.png", new MemoryStream([1, 2, 3]));
        Assert.Equal("Content/Textures/hero.png", path);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(_root, "p", "Content", "Textures", "hero.png")));
        Assert.Throws<ProjectException>(() => project.Import("Content", ".hidden", new MemoryStream()));
        Assert.DoesNotContain(project.ListFiles(), f => f.EndsWith(".lunet-tmp"));
    }

    [Fact]
    public void ExportZip_ContainsProjectFilesButNotCache()
    {
        var project = new ProjectStore(_root).Create("p");
        project.WriteText(".lunet/cache/big.bin", "x");
        using var stream = new MemoryStream();
        project.ExportZip(stream);
        stream.Position = 0;
        using var zip = new ZipArchive(stream);
        var names = zip.Entries.Select(e => e.FullName).ToArray();
        Assert.Contains("p/lunet.json", names);
        Assert.Contains("p/Game.cs", names);
        Assert.DoesNotContain(names, n => n.Contains(".lunet"));
    }

    [Fact]
    public void Manifest_RejectsFutureFormatAndCorruptJson_AndKeepsUnknownFields()
    {
        Assert.Throws<ProjectException>(() => ProjectManifest.Parse("{ nope"));
        Assert.Throws<ProjectException>(() => ProjectManifest.Parse("{\"name\":\"x\",\"formatVersion\":99}"));
        var manifest = ProjectManifest.Parse("{\"name\":\"x\",\"plugins\":[\"a\"]}");
        Assert.Contains("plugins", manifest.ToJson());
    }
}

public class ProjectDeletionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-del-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Delete_RemovesOnlyTheNamedProject()
    {
        var store = new ProjectStore(_root);
        store.Create("a");
        store.Create("b");
        store.Delete("a");
        Assert.Equal(["b"], store.List());
        Assert.Throws<ProjectException>(() => store.Delete("a"));
    }

    [Fact]
    public void Delete_RefusesNamesThatEscapeAndFoldersThatAreNotProjects()
    {
        var store = new ProjectStore(_root);
        Directory.CreateDirectory(Path.Combine(_root, "not-a-project"));
        File.WriteAllText(Path.Combine(_root, "not-a-project", "keep.txt"), "x");
        Assert.Throws<ProjectException>(() => store.Delete("not-a-project"));
        Assert.Throws<ProjectException>(() => store.Delete("../"));
        Assert.Throws<ProjectException>(() => store.Delete(".."));
        Assert.True(File.Exists(Path.Combine(_root, "not-a-project", "keep.txt")));
    }
}
