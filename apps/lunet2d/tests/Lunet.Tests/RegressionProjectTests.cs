using System.IO.Compression;
using Lunet;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Pathfinding;
using Lunet.Runtime;
using Lunet.Storage;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>
/// Golden source projects: actual Lunet folders, compiled and executed by the
/// production compiler/runtime, not mock screenshots or reimplemented engines.
/// </summary>
public sealed class RegressionProjectTests
{
    private static string SamplesRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        for (int i = 0; i < 12 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "samples", "regression");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("samples/regression não encontrado");
    }

    [Theory]
    [InlineData("portrait", "portrait", 360, 640)]
    [InlineData("landscape", "landscape", 640, 360)]
    [InlineData("full-stack", "portrait", 360, 640)]
    public void ReferenceProject_CompilesRunsExportsAndReimports(
        string name, string orientation, int width, int height)
    {
        var samples = new ProjectStore(SamplesRoot());
        var project = samples.Open(name);
        Assert.Equal(orientation, project.Manifest.Orientation);
        Assert.Equal(width, project.Manifest.VirtualWidth);
        Assert.Equal(height, project.Manifest.VirtualHeight);
        Assert.True(Guid.TryParse(project.Manifest.GameId, out _));
        Assert.True(OfflineGameApkBuilder.ValidPackage(project.Manifest.PackageId));

        var sourceFiles = project.LoadSources()
            .Select(s => new SourceFile(s.Path, s.Text)).ToList();
        Assert.NotEmpty(sourceFiles);
        var compilation = new GameCompiler(
            new LoadedAssembliesReferenceProvider(typeof(Game).Assembly))
            .Compile("regression_" + name.Replace('-', '_'), sourceFiles);
        Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Diagnostics));

        var backend = new RecordingBackend();
        var saves = new MemorySaveStore();
        using (var loaded = GameLoader.Load(compilation.Assembly!, compilation.Symbols))
        {
            var logs = new List<string>();
            loaded.Game.Log.Written += (_, line) => logs.Add(line);
            var host = new GameHost(loaded.Game, backend,
                new DirectoryContentSource(Path.Combine(project.Directory, "Content")),
                saveStore: saves);
            Assert.True(host.Start(width, height), host.Fault?.ToString());
            for (int i = 0; i < 90; i++) host.Tick(1d / 60);
            Assert.False(host.IsFaulted, host.Fault?.ToString());
            Assert.NotEmpty(backend.Clears);
            Assert.NotEmpty(backend.Batches);
            Assert.Contains(logs, line => line.Contains("REGRESSION:" + name,
                StringComparison.Ordinal));
            host.Stop();
        }

        if (name == "full-stack")
        {
            var sprite = SpriteDocument.Parse(project.ReadText("Content/Textures/tiles.sprite.json"));
            Assert.Equal(8, sprite.Width);
            Assert.Equal(8, sprite.Height);
            Assert.NotEmpty(sprite.Frames);
            var map = TileMap.Parse(project.ReadText("Content/Data/arena.tilemap.json"));
            Assert.Equal("Textures/tiles.png", map.TexturePath);
            Assert.True(map.IsBlocked(new GridPoint(1, 1)));
            Assert.False(map.IsBlocked(new GridPoint(2, 2)));
            Assert.True(WaveAudioDocument.Parse(
                File.ReadAllBytes(Path.Combine(project.Directory, "Content/Audio/trigger.wav")))
                .FrameCount > 0);
            Assert.Contains(ProjectAssetCatalog.Build(project.ListFiles()),
                asset => asset.Kind == ProjectAssetKind.Sprite &&
                    asset.RuntimePath == "Textures/tiles.png");
            Assert.Equal(1, new SaveData(saves).Load("launches", 0));
            Assert.Contains(backend.Pixels.Values, image =>
                image.Width == 8 && image.Height == 8 && image.Rgba.Length == 8 * 8 * 4);
        }

        // A sample must also be a portable user project: ZIP round-trip must
        // preserve every authored byte, including binary audio and PNG assets.
        using var zipBytes = new MemoryStream();
        project.ExportZip(zipBytes);
        zipBytes.Position = 0;
        string temp = Path.Combine(Path.GetTempPath(), "lunet-regression-" + Guid.NewGuid().ToString("N"));
        try
        {
            var imported = new ProjectStore(temp).ImportZip(zipBytes);
            Assert.Equal(project.Manifest.GameId, imported.Manifest.GameId);
            Assert.Equal(project.Manifest.PackageId, imported.Manifest.PackageId);
            foreach (string path in Directory.GetFiles(project.Directory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(project.Directory, path);
                Assert.Equal(File.ReadAllBytes(path),
                    File.ReadAllBytes(Path.Combine(imported.Directory, relative)));
            }
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void ReferenceProjects_HaveDistinctStableAndroidIdentities()
    {
        var store = new ProjectStore(SamplesRoot());
        Assert.Equal(new[] { "full-stack", "landscape", "portrait" }, store.List());
        var manifests = store.List().Select(name => store.Open(name).Manifest).ToList();
        Assert.Equal(3, manifests.Select(m => m.GameId).Distinct().Count());
        Assert.Equal(3, manifests.Select(m => m.PackageId).Distinct().Count());
    }
}
