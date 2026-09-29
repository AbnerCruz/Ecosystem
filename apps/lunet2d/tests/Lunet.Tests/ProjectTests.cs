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
        Assert.Contains("class SpaceShooter : Game", project.ReadText("Game.cs"));
        Assert.Equal("com.lunet.games.spaceshooter", project.Manifest.PackageId);
        Assert.True(Guid.TryParse(project.Manifest.GameId, out _));
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
