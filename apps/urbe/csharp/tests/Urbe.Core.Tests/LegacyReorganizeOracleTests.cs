using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// "Organizar os bairros" (city.reorganize → urbeReorganizarCidade) and "Desfazer reorganização" (layout-guard.js
/// undo), recorded from the UNMODIFIED 1.8.4 by csharp/tools/legacy-reorganize-oracle.mjs on a vault with folders, a
/// subfolder and loose notes, opened from the .urbe/mapa.json the 1.8.4 wrote, with the 1.8.4's runtime ids.
/// </summary>
public sealed class LegacyReorganizeOracleTests
{
    private static readonly Lazy<LegacyTileChunks> World = new(() =>
    {
        var climate = LegacyElevationClimateField.Generate("urbe", 512);
        var hydrology = LegacyHydrologyField.Generate(climate);
        return new LegacyTileChunks(new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology)));
    });

    private static (LegacyCity City, DocumentStore Store, JsonDocument Json) Open(string step = "open")
    {
        var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", "reorganize-run-a.json")));
        var open = json.RootElement.GetProperty(step);
        var files = json.RootElement.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();
        var documentIds = open.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!);
        // the 1.8.4's runtime ids (Math.random) in the order this open creates them: a first pass learns which
        // neighbourhood and house each new id goes to, then each gets the id the 1.8.4 gave it
        int n = 0;
        var probe = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p + "#" + n++);
        probe.Open(files, open.GetProperty("mapa").GetString(), documentIds, open.GetProperty("now").GetInt64());
        var regionPaths = probe.RegionPaths();
        var jsRegionPath = new Dictionary<string, string>(StringComparer.Ordinal);
        {
            var byId = open.GetProperty("regions").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetString()!, r => r);
            string PathOf(JsonElement r) => (r.GetProperty("parentId").GetString() is { } pid ? PathOf(byId[pid]) + "/" : "") + r.GetProperty("name").GetString();
            foreach (var r in byId.Values) jsRegionPath[PathOf(r)] = r.GetProperty("id").GetString()!;
        }
        var jsBuilding = open.GetProperty("buildings").EnumerateArray().ToDictionary(b => b.GetProperty("path").GetString()!, b => b.GetProperty("id").GetString()!);
        var order = probe.Regions.Select(r => (r.Id, jsRegionPath[regionPaths[r.Id]]))
            .Concat(probe.Buildings.Select(b => (b.Id, jsBuilding[b.Path]))).ToDictionary(p => p.Item1, p => p.Item2);
        var regionIds = new Queue<string>(Enumerable.Range(0, n).Select(i => "r#" + i).Where(order.ContainsKey).Select(k => order[k]));
        var buildingIds = new Queue<string>(Enumerable.Range(0, n).Select(i => "b#" + i).Where(order.ContainsKey).Select(k => order[k]));
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p == "r" ? regionIds.Dequeue() : buildingIds.Dequeue());
        var store = city.Open(files, open.GetProperty("mapa").GetString(),
            open.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!),
            open.GetProperty("now").GetInt64());
        return (city, store, json);
    }

    private static void SameLayout(JsonElement expected, LegacyCity city, string step)
    {
        var regions = expected.GetProperty("regions").EnumerateArray().ToList();
        Assert.Equal(regions.Count, city.Regions.Count);
        for (int i = 0; i < regions.Count; i++)
        {
            var e = regions[i];
            var r = city.Regions[i];
            Assert.True(e.GetProperty("id").GetString() == r.Id, step + ": region " + i);
            Assert.True((e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32()) == (r.X, r.Y, r.W, r.H),
                $"{step}: {r.Name} at {r.X},{r.Y} {r.W}x{r.H}");
            Assert.Equal(e.GetProperty("color").GetString(), r.Color);
            Assert.Equal(e.GetProperty("parentId").GetString(), r.ParentId);
            if (e.GetProperty("cells").ValueKind == JsonValueKind.Array)
                Assert.Equal(e.GetProperty("cells").EnumerateArray().Select(c => c.GetString()!).Order(StringComparer.Ordinal).ToList(),
                    r.Cells.Select(c => c.X + "," + c.Y).Order(StringComparer.Ordinal).ToList());
        }
        var buildings = expected.GetProperty("buildings").EnumerateArray()
            .Select(b => b.GetProperty("id").GetString() + " " + b.GetProperty("x").GetInt32() + "," + b.GetProperty("y").GetInt32() + " " + b.GetProperty("regionId").GetString()).ToList();
        Assert.Equal(buildings, city.Buildings.Select(b => b.Id + " " + b.X + "," + b.Y + " " + b.RegionId).ToList());
    }

    [Fact]
    public void OrganizingTheDistrictsDrawsThemAndPlacesTheHousesAsTheOriginal()
    {
        var (city, store, json) = Open();
        using var _ = json;
        var root = json.RootElement;
        SameLayout(root.GetProperty("open"), city, "open");
        var before = city.SnapshotLayout();

        var result = city.Reorganize();
        var reorganized = root.GetProperty("reorganized");
        var expected = reorganized.GetProperty("result");
        Assert.Equal((expected.GetProperty("bairros").GetInt32(), expected.GetProperty("casas").GetInt32(), expected.GetProperty("falhas").GetInt32()),
            (result.Districts, result.Houses, result.Failures));
        SameLayout(reorganized, city, "reorganized");
        Assert.Equal(reorganized.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList(), city.ValidateDistricts());
        city.RebuildRoadsAfterChange(store);
        Assert.Equal(reorganized.GetProperty("roads").EnumerateArray().Select(t => t.GetString()!).Order(StringComparer.Ordinal).ToList(),
            city.Roads.Select(t => t.X + "," + t.Y).Order(StringComparer.Ordinal).ToList());

        city.RestoreLayout(before);
        SameLayout(root.GetProperty("restored"), city, "restored");
    }

    /// <summary>abrirCidade with a map saved for another world: "Adaptando a cidade ao mundo novo" reorganizes it from the
    /// world's start before the houses are fitted into their neighbourhoods; the layout is the original's.</summary>
    [Fact]
    public void AMapOfAnotherWorldIsReorganizedOnOpenAsTheOriginal()
    {
        var (city, _, json) = Open("migrated");
        using var _ = json;
        var migrated = json.RootElement.GetProperty("migrated");
        Assert.Equal("antigo", city.MigratedFromWorld);
        SameLayout(migrated, city, "migrated");
        Assert.Equal(migrated.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList(), city.ValidateDistricts());
        Assert.Contains("Cidade reorganizada", migrated.GetProperty("dialog").GetString());
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
