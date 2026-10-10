using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Opening a real vault must give the 1.8.4 city. Fixtures are recorded from the unmodified
/// 1.8.4-beta running in Chromium (csharp/tools/legacy-open-oracle.mjs), which opens the vault
/// "Urbe" from its own IndexedDB adapter through the abrirCidade chain: "fresh" is a vault with
/// notes and no .urbe/mapa.json; "mapa" reopens it with the mapa.json the app produced, partly
/// forgotten (a region without geometry, a child region missing, a rectangle region without cells,
/// notes without a place, a new folder, a file building and a camera to clamp); "regra" reopens it
/// with the neighbourhood rule broken (a house far away, a house just outside, a closed hole, a
/// child region outside its parent), so urbeCasasNosBairros and urbeTaparTodos change the city.
/// The ids the original generated are fed back, since they seed lot order and growth.
/// </summary>
public sealed class LegacyCityOpenOracleTests
{
    private static readonly Lazy<LegacyTileChunks> World = new(() =>
    {
        var climate = LegacyElevationClimateField.Generate("urbe", 512);
        var hydrology = LegacyHydrologyField.Generate(climate);
        return new LegacyTileChunks(new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology)));
    });

    [Theory]
    [InlineData("open-vault-run-a.json", "fresh")]
    [InlineData("open-vault-run-a.json", "mapa")]
    [InlineData("open-vault-run-b.json", "fresh")]
    [InlineData("open-vault-run-b.json", "mapa")]
    [InlineData("open-vault-run-a.json", "regra")]
    [InlineData("open-vault-run-b.json", "regra")]
    public void OpeningARealVaultBuildsTheOriginalCity(string fixture, string phase)
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", fixture)));
        var o = json.RootElement.GetProperty(phase);
        var files = o.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();
        string? mapa = o.TryGetProperty("mapaJson", out var mj) ? mj.GetString() : null;
        Assert.Equal(phase != "fresh", mapa is not null);

        // id('r') runs once per region in world.regions order; id('b') once per text file in listing
        // order (houses with a saved place are pushed first, so the array order differs), then
        // once per file building of mapa.construcoes.
        var expectedBuildings = o.GetProperty("buildings").EnumerateArray().ToList();
        var regionIds = new Queue<string>(o.GetProperty("regions").EnumerateArray().Select(r => r.GetProperty("id").GetString()!));
        var noteIds = expectedBuildings.Where(b => b.GetProperty("tipo").GetString() == "nota")
            .ToDictionary(b => b.GetProperty("path").GetString()!, b => b.GetProperty("id").GetString()!);
        var buildingIds = new Queue<string>(files.Select(f => noteIds[f.Item1])
            .Concat(expectedBuildings.Where(b => b.GetProperty("tipo").GetString() != "nota").Select(b => b.GetProperty("id").GetString()!)));
        var documentIds = o.GetProperty("documents").EnumerateArray()
            .ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!);

        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome),
            prefix => prefix == "r" ? regionIds.Dequeue() : buildingIds.Dequeue());
        var store = city.Open(files, mapa, documentIds);

        Assert.Empty(regionIds);
        Assert.Empty(buildingIds);
        Assert.False(city.WorldReorganizationPending);
        Assert.Equal(documentIds.Keys.Order(StringComparer.Ordinal), store.List().Select(d => d.Path).Order(StringComparer.Ordinal));

        var expectedRegions = o.GetProperty("regions").EnumerateArray().ToList();
        Assert.Equal(expectedRegions.Count, city.Regions.Count);
        for (int i = 0; i < expectedRegions.Count; i++)
        {
            var e = expectedRegions[i];
            var r = city.Regions[i];
            Assert.Equal(e.GetProperty("id").GetString(), r.Id);
            Assert.Equal(e.GetProperty("name").GetString(), r.Name);
            Assert.Equal(e.GetProperty("parentId").ValueKind == JsonValueKind.Null ? null : e.GetProperty("parentId").GetString(), r.ParentId);
            Assert.Equal(e.GetProperty("color").GetString(), r.Color);
            Assert.Equal((e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32()),
                (r.X, r.Y, r.W, r.H));
            var cells = e.GetProperty("cells");
            Assert.Equal(cells.ValueKind == JsonValueKind.Null, r.IsRectangle);
            if (cells.ValueKind != JsonValueKind.Null)
                Assert.Equal(cells.EnumerateArray().Select(c => c.GetString()!).ToList(), r.Cells.Select(c => c.X + "," + c.Y).ToList());
        }

        Assert.Equal(expectedBuildings.Count, city.Buildings.Count);
        for (int i = 0; i < expectedBuildings.Count; i++)
        {
            var e = expectedBuildings[i];
            var b = city.Buildings[i];
            Assert.Equal(e.GetProperty("id").GetString(), b.Id);
            Assert.Equal(e.GetProperty("tipo").GetString() == "nota", b.IsNote);
            if (b.IsNote)
            {
                Assert.Equal(e.GetProperty("path").GetString(), b.Path);
                Assert.Equal(documentIds[b.Path], b.DocumentId);
            }
            Assert.Equal(e.GetProperty("name").GetString(), b.Name);
            Assert.Equal(e.GetProperty("regionId").ValueKind == JsonValueKind.Null ? null : e.GetProperty("regionId").GetString(), b.RegionId);
            Assert.Equal((e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32()),
                (b.X, b.Y, b.W, b.H));
            Assert.Equal(e.GetProperty("sprite").GetString(), b.Sprite);
        }

        Assert.Equal(o.GetProperty("links").EnumerateArray().Select(l => l[0].GetString() + ">" + l[1].GetString()),
            city.Links.Select(l => l.From + ">" + l.To));
        Assert.Equal(o.GetProperty("roads").EnumerateArray().Select(r => r.GetString()!).ToList(),
            city.Roads.Select(r => r.X + "," + r.Y).ToList());

        // the 1.8.4 camera after the open: mapa.camera merged, z clamped to [.22, 2.8]; no framing
        var camera = o.GetProperty("camera");
        Assert.Equal(camera.GetProperty("x").GetDouble(), city.CameraX);
        Assert.Equal(camera.GetProperty("y").GetDouble(), city.CameraY);
        Assert.Equal(camera.GetProperty("z").GetDouble(), city.CameraZoom);
    }

    [Fact]
    public void AMapaFromAnotherWorldIsFlaggedNotSilentlyReorganized()
    {
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p + Guid.NewGuid().ToString("N")[..6]);
        city.Open([("Solta.md", "# Solta\n")], """{"v":4,"mundo":"antigo","notas":{"Solta.md":{"x":40,"y":30}}}""");
        Assert.True(city.WorldReorganizationPending);
        Assert.Equal((40, 30), (city.Buildings[0].X, city.Buildings[0].Y));
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
