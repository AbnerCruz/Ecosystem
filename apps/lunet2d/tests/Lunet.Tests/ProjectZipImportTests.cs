using System.IO.Compression;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class ProjectZipImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-zip-" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void ExportThenImport_PreservesCodeBinaryMetadataAndSourceStream()
    {
        var original = new ProjectStore(Path.Combine(_root, "source")).Create("Game", ProjectTemplate.CoinCatcher);
        original.WriteText("Code/Player.cs", "class Player { }");
        original.Manifest.Extra = new() { ["custom"] = System.Text.Json.JsonSerializer.SerializeToElement(new { value = 42 }) };
        original.WriteText("lunet.json", original.Manifest.ToJson());
        using var zip = new MemoryStream();
        original.ExportZip(zip);
        zip.Position = 0;
        var store = new ProjectStore(Path.Combine(_root, "target"));
        var imported = store.ImportZip(new NonSeekable(zip));
        Assert.Equal(original.ListFiles(), imported.ListFiles());
        foreach (var path in original.ListFiles())
            Assert.Equal(File.ReadAllBytes(Path.Combine(original.Directory, path)), File.ReadAllBytes(Path.Combine(imported.Directory, path)));
        Assert.Equal(original.Manifest.GameId, imported.Manifest.GameId);
        Assert.Equal(42, imported.Manifest.Extra!["custom"].GetProperty("value").GetInt32());
        Assert.True(zip.CanRead);
        Assert.Equal(new[] { "Game" }, store.List());
    }

    [Fact]
    public void DuplicateName_CreatesCopyWithoutChangingExistingProjectOrGameIdentity()
    {
        var store = new ProjectStore(_root);
        var original = store.Create("Game");
        var json = original.ReadText("lunet.json");
        using var zip = new MemoryStream();
        original.ExportZip(zip);
        zip.Position = 0;
        var copy = store.ImportZip(zip);
        Assert.Equal("Game_2", copy.Name);
        Assert.Equal(original.Manifest.GameId, copy.Manifest.GameId);
        Assert.Equal(json, original.ReadText("lunet.json"));
        zip.Position = 0;
        Assert.Equal("Game_3", store.ImportZip(zip).Name);
    }

    [Fact]
    public void RootManifest_IsSupported()
    {
        using var zip = Archive(("lunet.json", "{\"name\":\"Root\"}"), ("Game.cs", "class G { }"));
        Assert.Equal("class G { }", new ProjectStore(_root).ImportZip(zip).ReadText("Game.cs"));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("Game/../../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("Game\\escape.txt")]
    [InlineData("Game//file.txt")]
    [InlineData("Game/./file.txt")]
    [InlineData("Other/file.txt")]
    [InlineData("Game/Code/../file.txt")]
    public void UnsafePaths_AreRejectedWithoutPublishingProject(string path)
    {
        using var zip = Archive(("Game/lunet.json", "{\"name\":\"Game\"}"), ("Game/Game.cs", "x"), (path, "x"));
        AssertFailedCleanly(zip);
        Assert.False(File.Exists(Path.Combine(_root, "escape.txt")));
    }

    [Theory]
    [InlineData("Game/lunet.json", "{ nope", "Game/Game.cs")]
    [InlineData("Game/lunet.json", "{\"name\":\"Game\",\"formatVersion\":99}", "Game/Game.cs")]
    [InlineData("Game/lunet.json", "{\"name\":\"../Game\"}", "Game/Game.cs")]
    [InlineData("Game/lunet.json", "{\"name\":\"Game\",\"entryPoint\":\"../outside.cs\"}", "Game/Game.cs")]
    [InlineData("Game/lunet.json", "{\"name\":\"Game\",\"entryPoint\":\"Missing.cs\"}", "Game/Game.cs")]
    [InlineData("a/b/lunet.json", "{\"name\":\"Game\"}", "a/b/Game.cs")]
    [InlineData("README.txt", "not a project", "Game.cs")]
    public void InvalidProjects_AreRejected(string manifestPath, string manifest, string sourcePath)
    {
        using var zip = Archive((manifestPath, manifest), (sourcePath, "x"));
        AssertFailedCleanly(zip);
    }

    [Fact]
    public void DuplicatePathsAndMultipleProjects_AreRejected()
    {
        using var duplicate = Archive(("lunet.json", "{\"name\":\"Game\"}"), ("Game.cs", "x"), ("game.cs", "y"));
        AssertFailedCleanly(duplicate);
        using var multiple = Archive(("a/lunet.json", "{\"name\":\"a\"}"), ("b/lunet.json", "{\"name\":\"b\"}"));
        AssertFailedCleanly(multiple);
    }

    [Fact]
    public void Symlink_IsRejected()
    {
        using var zip = Archive(("lunet.json", "{\"name\":\"Game\"}"), ("Game.cs", "x"));
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Update, true))
            archive.GetEntry("Game.cs")!.ExternalAttributes = 0xA000 << 16;
        zip.Position = 0;
        AssertFailedCleanly(zip);
    }

    [Fact]
    public void TooManyEntries_AreRejected()
    {
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
            for (int i = 0; i < 10_001; i++) archive.CreateEntry("files/" + i);
        zip.Position = 0;
        AssertFailedCleanly(zip);
    }

    [Fact]
    public void ExpandedSizeLimit_IsCheckedBeforeExtraction()
    {
        using var zip = Archive(("lunet.json", "{\"name\":\"Game\"}"), ("Game.cs", "x"));
        // Forge the central-directory length rather than allocating a huge fixture.
        var bytes = zip.ToArray();
        var header = Enumerable.Range(0, bytes.Length - 46).First(i =>
            bytes[i] == 0x50 && bytes[i + 1] == 0x4b && bytes[i + 2] == 0x01 && bytes[i + 3] == 0x02);
        BitConverter.GetBytes(512u * 1024 * 1024 + 1).CopyTo(bytes, header + 24);
        using var oversized = new MemoryStream(bytes);
        AssertFailedCleanly(oversized);
    }

    [Fact]
    public void InvalidZipAndInterruptedRead_LeaveNoPartialProject()
    {
        using var invalid = new MemoryStream([1, 2, 3]);
        AssertFailedCleanly(invalid);
        var store = new ProjectStore(_root);
        using var broken = new BrokenStream();
        Assert.Throws<IOException>(() => store.ImportZip(broken));
        Assert.Empty(store.List());
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_root, ".lunet-imports")));
    }

    private void AssertFailedCleanly(Stream zip)
    {
        var store = new ProjectStore(_root);
        Assert.Throws<ProjectException>(() => store.ImportZip(zip));
        Assert.Empty(store.List());
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_root, ".lunet-imports")));
    }

    private static MemoryStream Archive(params (string Path, string Text)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (path, text) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(text);
            }
        stream.Position = 0;
        return stream;
    }

    private sealed class NonSeekable(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BrokenStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Leitura interrompida.");
    }
}
