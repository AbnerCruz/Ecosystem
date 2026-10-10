using System.Diagnostics;
using Urbe.Client;
using Urbe.Client.City;
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

    [Fact]
    public void World_IsTheOriginalSeedAndSpawn()
    {
        // app.js: createWorld('urbe', {spawnX: 36, spawnY: 25}); the start area must be land.
        Assert.Equal("urbe", _world.Terrain.SeedText);
        Assert.True(_world.Buildable(36, 25));
    }

    [Fact]
    public void ChunkGround_IsOpaqueDeterministicAndUsesOriginalTextures()
    {
        var a = ChunkGround.Paint(_world, 2, 1);
        var b = ChunkGround.Paint(_world, 2, 1);
        Assert.Equal(ChunkGround.Side * ChunkGround.Side * 4, a.Length);
        Assert.Equal(a, b);
        for (int i = 3; i < a.Length; i += 4) Assert.Equal(255, a[i]);

        // A tile without decoration is exactly the 1.8.4 texture for its biome/variant.
        for (int ty = 0; ty < 16; ty++)
        for (int tx = 0; tx < 16; tx++)
        {
            int wx = 32 + tx, wy = 16 + ty;
            if (_world.DecorAt(wx, wy).Count > 0) continue;
            var tile = _world.At(wx, wy);
            int variant = (int)Math.Floor(LegacyTerrainMath.TileHash(wx, wy, 5) * 4);
            var texture = LegacyWorldPixelTextures.CreateTile(LegacyBiomeRules.Id(tile.Biome), variant);
            for (int row = 0; row < 16; row++)
                Assert.Equal(texture.AsSpan(row * 64, 64).ToArray(),
                    a.AsSpan(((ty * 16 + row) * ChunkGround.Side + tx * 16) * 4, 64).ToArray());
            return;
        }
        Assert.Fail("No undecorated tile found in the sampled chunk.");
    }

    [Fact]
    public void ChunkGround_FitsAMobileFrameBudget()
    {
        ChunkGround.Paint(_world, 5, 5); // warm-up (textures, terrain cache)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 8; i++) ChunkGround.Paint(_world, 10 + i, 7);
        sw.Stop();
        // Generated off the UI thread; generous bound only to catch pathological regressions.
        Assert.True(sw.ElapsedMilliseconds / 8.0 < 400, $"{sw.ElapsedMilliseconds / 8.0:F1} ms per chunk");
    }

    [Fact]
    public void TutorialNotes_AreThe184TutorialFolder()
    {
        var notes = TutorialNotes.Load();
        Assert.True(notes.Count >= 40, $"{notes.Count} notes");
        Assert.Contains(notes, n => n.Path == "Tutorial/Comece aqui.md" && n.Content.Contains("Bem-vindo ao **Urbe**"));
        Assert.All(notes, n => Assert.StartsWith("Tutorial/", n.Path));
    }

    [Fact]
    public void City_PlacesEveryNoteOnBuildableLotsWithoutOverlap()
    {
        var notes = TutorialNotes.Load();
        var city = CityModel.Build(notes, _world);
        Assert.Equal(notes.Count, city.Houses.Count);
        foreach (var h in city.Houses)
        {
            for (int y = h.Y; y < h.Y + h.H; y++)
            for (int x = h.X; x < h.X + h.W; x++)
                Assert.True(_world.Buildable(x, y), $"{h.Note.Path} on water/mountain at {x},{y}");
            Assert.DoesNotContain(city.Houses, o => o != h && o.X < h.X + h.W && h.X < o.X + o.W && o.Y < h.Y + h.H && h.Y < o.Y + o.H);
        }
        Assert.Contains(city.Districts, d => d.Name == "Matemática");
        var any = city.Houses[0];
        Assert.Same(any, city.HouseAt(any.X + 1, any.Y + 1));
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
