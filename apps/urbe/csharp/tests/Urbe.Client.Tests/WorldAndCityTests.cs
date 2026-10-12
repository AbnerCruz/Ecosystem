using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed class WorldFixture
{
    public LegacyWorld World { get; } = new();
}

public sealed class WorldAndCityTests(WorldFixture fixture) : IClassFixture<WorldFixture>
{
    private readonly LegacyWorld _world = fixture.World;

    /// <summary>
    /// A file building of mapa.construcoes (an image the 1.8.4 indexed) is drawn as a "store" and named by its file
    /// (nomeExibidoArquivo), as notes keep their trade and name.
    /// </summary>
    [Fact]
    public void FileBuildingsAreNamedByTheirFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir!.FullName, "csharp", "tests", "fixtures", "legacy-city", "open-vault-run-a.json")));
        var o = json.RootElement.GetProperty("mapa");
        var files = o.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();
        int n = 0;
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => _world.At(x, y).Biome), p => p + (++n));
        city.Open(files, o.GetProperty("mapaJson").GetString(),
            o.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!));
        var file = city.Buildings.Single(b => !b.IsNote);
        Assert.Equal("foto.png", WorldView.HouseLabel(file));
        Assert.Equal("image", file.FileKind);
        var note = city.Buildings.First(b => b.IsNote);
        Assert.DoesNotContain(".md", WorldView.HouseLabel(note));
    }

    [Fact]
    public void World_IsTheOriginalSeedAndSpawn()
    {
        // app.js: createWorld('urbe', {spawnX: 36, spawnY: 25}); the start area must be land.
        Assert.Equal("urbe", _world.Terrain.SeedText);
        Assert.True(_world.Buildable(36, 25));
    }

    [Fact]
    public void TutorialFiles_AreThe184TutorialInContentOrder()
    {
        var files = TutorialNotes.Load();
        Assert.Equal(48, files.Count);
        Assert.Equal(files.Select(f => f.Path).OrderBy(p => p, StringComparer.Ordinal), files.Select(f => f.Path));
        Assert.Contains(files, n => n.Path == "Tutorial/Comece aqui.md" && n.Content.Contains("Bem-vindo ao **Urbe**"));
        Assert.Contains(files, n => n.Path == "Tutorial/Páginas/Página de exemplo.page.json");
        Assert.DoesNotContain(files, n => n.Content.Contains('\r'));
    }

    [Fact]
    public void FirstOpen_PutsEveryFileInsideItsNeighbourhood()
    {
        int next = 0;
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => _world.At(x, y).Biome), p => p + ++next);
        city.OpenFirstTime(TutorialNotes.Load().Select(f => (f.Path, f.Content)).ToList());
        Assert.Equal(48, city.Buildings.Count);
        Assert.Equal(9, city.Regions.Count);
        var paths = city.RegionPaths();
        foreach (var b in city.Buildings)
        {
            var folder = b.Path[..b.Path.LastIndexOf('/')];
            var region = city.Regions.Single(r => r.Id == b.RegionId);
            Assert.Equal(folder, paths[region.Id]);
            for (int y = b.Y; y < b.Y + b.H; y++)
            for (int x = b.X; x < b.X + b.W; x++)
                Assert.Same(region, city.RegionAt(x, y));
        }
        Assert.NotEmpty(city.Roads);
        Assert.Equal("Página de exemplo.page.json", WorldView.HouseLabel(city.Buildings.Single(b => b.Path.EndsWith(".json"))));
        Assert.Equal("Comece aqui", WorldView.HouseLabel(city.Buildings.Single(b => b.Path == "Tutorial/Comece aqui.md")));
    }

    [Fact]
    public void Camera_ZoomKeepsTheAnchorAndRespectsTheOriginalLimits()
    {
        var cam = new Camera { Viewport = new Avalonia.Size(800, 600) };
        var anchor = new Avalonia.Point(200, 150);
        var before = cam.ScreenToWorld(anchor);
        cam.ZoomAt(anchor, 1.7);
        var after = cam.ScreenToWorld(anchor);
        Assert.InRange(Math.Abs(before.X - after.X) + Math.Abs(before.Y - after.Y), 0, 1e-6);
        cam.ZoomAt(anchor, 100);
        Assert.Equal(Camera.MaxZoom, cam.Zoom);
        cam.ZoomAt(anchor, 1e-6);
        Assert.Equal(Camera.MinZoom, cam.Zoom);
    }
}
