using System.IO.Compression;
using System.Numerics;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Pathfinding;
using Lunet.Runtime;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>
/// Reproduces the same source-first journey as the Android editor using the real
/// ProjectStore, Studio documents, Roslyn compiler, Preview host and project ZIP.
/// LUNET_JOURNEY_OUTPUT additionally publishes the *same* edited project to CI,
/// where the offline packager installs and launches it on Android.
/// No Android UI interaction is claimed by this portable test.
/// </summary>
public sealed class ProjectJourneyTests
{
    // Stable, valid 1x1 RGBA PNG with an opaque green pixel; corresponds to the
    // edited SpriteDocument below. Fixture only, not a substitute for testing
    // the Sprite Studio's Android PNG exporter.
    private const string GreenPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNg+M/wHwAEAQH/cetH5QAAAABJRU5ErkJggg==";

    [Fact]
    public void CreateEditAssetsPreviewExportAndReopen()
    {
        string? artifactRoot = Environment.GetEnvironmentVariable("LUNET_JOURNEY_OUTPUT");
        bool keepArtifacts = !string.IsNullOrWhiteSpace(artifactRoot);
        string root = keepArtifacts
            ? Path.GetFullPath(artifactRoot!)
            : Path.Combine(Path.GetTempPath(), "lunet-journey-" + Guid.NewGuid().ToString("N"));
        string projectsRoot = Path.Combine(root, "projects");
        Directory.CreateDirectory(projectsRoot);

        try
        {
            var store = new ProjectStore(projectsRoot);
            var project = store.Create("JourneyPortrait", ProjectTemplate.Blank, "portrait");
            Assert.Equal("portrait", project.Manifest.Orientation);
            Assert.True(OfflineGameApkBuilder.ValidPackage(project.Manifest.PackageId));

            // Edit an actual Studio source document and its PNG derivative.
            var sprite = SpriteDocument.Create(1);
            sprite.Stroke(0, 0, 0, 0, 0, 0xff00ff00);
            project.WriteText("Content/Textures/hero.sprite.json", sprite.ToJson());
            using (var image = new MemoryStream(Convert.FromBase64String(GreenPng)))
                project.Import("Content/Textures", "hero.png", image);
            Assert.Equal(0xff00ff00u,
                SpriteDocument.Parse(project.ReadText("Content/Textures/hero.sprite.json")).Frames[0][0]);

            var tiles = new TileStudioSession(
                TileMap.Create("Textures/hero.png", 2, 2, 1, 1), maxTileId: 1);
            tiles.BeginStroke();
            Assert.True(tiles.Paint(1, 0));
            Assert.True(tiles.EndStroke());
            project.WriteText("Content/Data/level.tilemap.json", tiles.Map.ToJson());
            Assert.Equal(1, TileMap.Parse(project.ReadText("Content/Data/level.tilemap.json"))
                .GetTile(0, new GridPoint(1, 0)));

            byte[] wav = WavGenerator.Beep(440, 0.12);
            using (var audio = new MemoryStream(wav))
                project.Import("Content/Audio", "beep.wav", audio);
            Assert.True(WaveAudioDocument.Parse(wav).FrameCount > 0);

            // Modify the *created* Game.cs: the preview must use the edited
            // code and the imported asset, never a hard-coded sample assembly.
            string original = project.ReadText("Game.cs");
            const string fromBall = "ball = Texture2D.CreateCircle(GraphicsDevice, 48, Color.Yellow);";
            const string fromLog = "Log.Info(\"Jogo iniciado. Toque na tela.\");";
            Assert.Contains(fromBall, original);
            Assert.Contains(fromLog, original);
            Assert.Contains("GraphicsDevice.Clear(Color.CornflowerBlue);", original);
            string edited = original
                .Replace(fromBall, "ball = Content.LoadTexture(\"Textures/hero.png\", TextureFilter.Point);")
                .Replace(fromLog,
                    "if (Content.LoadTileMap(\"Data/level.tilemap.json\").Width != 2) throw new System.Exception(\"Mapa não carregado\"); " +
                    "Log.Info(\"Jornada editada iniciou\");")
                .Replace("GraphicsDevice.Clear(Color.CornflowerBlue);", "GraphicsDevice.Clear(Color.Green);");
            project.WriteText("Game.cs", edited);
            Assert.Equal(edited, project.ReadText("Game.cs"));

            var catalog = ProjectAssetCatalog.Build(project.ListFiles());
            Assert.Contains(catalog, a =>
                a.Kind == ProjectAssetKind.Sprite &&
                a.Path == "Content/Textures/hero.sprite.json" &&
                a.PreviewPath == "Content/Textures/hero.png" &&
                a.RuntimePath == "Textures/hero.png");
            Assert.Contains(catalog, a => a.Kind == ProjectAssetKind.TileMap);
            Assert.Contains(catalog, a => a.Kind == ProjectAssetKind.Audio);

            var reopened = store.Open(project.Name);
            var sources = reopened.LoadSources()
                .Select(s => new SourceFile(s.Path, s.Text)).ToList();
            var compiler = new GameCompiler(
                new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var compiled = compiler.Compile("journey_preview", sources);
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));

            using (var loaded = GameLoader.Load(compiled.Assembly!, compiled.Symbols))
            {
                var backend = new RecordingBackend();
                var logs = new List<string>();
                loaded.Game.Log.Written += (_, message) => logs.Add(message);
                var host = new GameHost(loaded.Game, backend,
                    new DirectoryContentSource(Path.Combine(reopened.Directory, "Content")));
                Assert.True(host.Start(360, 640), host.Fault?.ToString());
                host.Tick(1.0 / 60);
                Assert.False(host.IsFaulted, host.Fault?.ToString());
                Assert.Contains(Color.Green, backend.Clears);
                Assert.Contains(logs, line => line.Contains("Jornada editada iniciou"));
                Assert.Contains(backend.Pixels.Values, p =>
                    p.Width == 1 && p.Height == 1 &&
                    p.Rgba.Length == 4 && p.Rgba[1] == 255 && p.Rgba[3] == 255);
            }

            using var zipStream = new MemoryStream();
            reopened.ExportZip(zipStream);
            zipStream.Position = 0;
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            string prefix = reopened.Name + "/";
            foreach (var path in new[] {
                "lunet.json", "Game.cs", "Content/Textures/hero.sprite.json",
                "Content/Textures/hero.png", "Content/Data/level.tilemap.json",
                "Content/Audio/beep.wav" })
                Assert.NotNull(zip.GetEntry(prefix + path));
            Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("/.lunet/"));

            if (keepArtifacts)
            {
                File.WriteAllText(Path.Combine(root, "journey-package-id.txt"),
                    reopened.Manifest.PackageId);
                File.WriteAllText(Path.Combine(root, "journey-orientation.txt"),
                    reopened.Manifest.Orientation);
                File.WriteAllBytes(Path.Combine(root, "journey-export.zip"), zipStream.ToArray());
            }
        }
        finally
        {
            if (!keepArtifacts && Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
