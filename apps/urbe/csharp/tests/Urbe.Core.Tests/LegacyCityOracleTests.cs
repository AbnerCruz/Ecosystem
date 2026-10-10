using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The C# city must be the 1.8.4 city. Fixtures are recorded from the unmodified
/// 1.8.4-beta running in Chromium on first open (csharp/tools/legacy-city-oracle.mjs):
/// neighbourhood cells, colours and bounds, every house position and every road tile.
/// The ids the original generated are fed back, since they seed lot order and growth.
/// </summary>
public sealed class LegacyCityOracleTests
{
    private static readonly Lazy<LegacyTileChunks> World = new(() =>
    {
        var climate = LegacyElevationClimateField.Generate("urbe", 512);
        var hydrology = LegacyHydrologyField.Generate(climate);
        return new LegacyTileChunks(new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology)));
    });

    [Theory]
    [InlineData("tutorial-run-a.json")]
    [InlineData("tutorial-run-b.json")]
    public void FirstOpenOfTheTutorialBuildsTheOriginalCity(string fixture)
    {
        var root = Root();
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root.FullName, "csharp", "tests", "fixtures", "legacy-city", fixture)));
        var o = json.RootElement;

        var regionIds = new Queue<string>(o.GetProperty("regions").EnumerateArray().Select(r => r.GetProperty("id").GetString()!));
        var buildingIds = new Queue<string>(o.GetProperty("buildings").EnumerateArray().Select(b => b.GetProperty("id").GetString()!));
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome),
            prefix => prefix == "r" ? regionIds.Dequeue() : buildingIds.Dequeue());

        // tutorial.js: C.files order (build-tutorial.mjs: sorted walk of apps/urbe/tutorial).
        var files = Directory.EnumerateFiles(Path.Combine(root.FullName, "tutorial"), "*", SearchOption.AllDirectories)
            .Select(f => "Tutorial/" + Path.GetRelativePath(Path.Combine(root.FullName, "tutorial"), f).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => (p, File.ReadAllText(Path.Combine(root.FullName, "tutorial", p["Tutorial/".Length..])).Replace("\r\n", "\n")))
            .ToList();
        Assert.Equal(o.GetProperty("documents").EnumerateArray().Select(d => d.GetString()), files.Select(f => f.p));

        city.OpenFirstTime(files);

        var expectedRegions = o.GetProperty("regions").EnumerateArray().ToList();
        Assert.Equal(expectedRegions.Count, city.Regions.Count);
        for (int i = 0; i < expectedRegions.Count; i++)
        {
            var e = expectedRegions[i];
            var r = city.Regions[i];
            Assert.Equal(e.GetProperty("name").GetString(), r.Name);
            Assert.Equal(e.GetProperty("id").GetString(), r.Id);
            Assert.Equal(e.GetProperty("parentId").ValueKind == JsonValueKind.Null ? null : e.GetProperty("parentId").GetString(), r.ParentId);
            var cells = e.GetProperty("cells").EnumerateArray().Select(c => c.GetString()!).ToList();
            Assert.Equal(cells, r.Cells.Select(c => c.X + "," + c.Y).ToList());
            Assert.Equal((e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32()),
                (r.X, r.Y, r.W, r.H));
            Assert.Equal(e.GetProperty("color").GetString(), r.Color);
        }

        var expectedBuildings = o.GetProperty("buildings").EnumerateArray().ToList();
        Assert.Equal(expectedBuildings.Count, city.Buildings.Count);
        for (int i = 0; i < expectedBuildings.Count; i++)
        {
            var e = expectedBuildings[i];
            var b = city.Buildings[i];
            Assert.Equal(e.GetProperty("path").GetString(), b.Path);
            Assert.Equal((e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32()), (b.X, b.Y));
            Assert.Equal(e.GetProperty("regionId").GetString(), b.RegionId);
        }

        Assert.Equal(o.GetProperty("links").EnumerateArray().Select(l => l[0].GetString() + ">" + l[1].GetString()),
            city.Links.Select(l => l.From + ">" + l.To));
        Assert.Equal(o.GetProperty("roads").EnumerateArray().Select(r => r.GetString()!).ToList(),
            city.Roads.Select(r => r.X + "," + r.Y).ToList());
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
