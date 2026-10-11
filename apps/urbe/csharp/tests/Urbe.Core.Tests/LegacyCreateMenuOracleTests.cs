using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>The "Criar" menu rules (unique folder names, the drawn region mask and territory
/// transfer, root houses under a region) against the unmodified 1.8.4 app.js run by Node
/// (tools/create-oracle.mjs).</summary>
public sealed class LegacyCreateMenuOracleTests
{
    private const string Scenario = """
    {"water":[[5,5],[6,5],[7,5],[8,5],[9,5],[10,2],[10,3],[10,4],[14,8],[15,8]],
     "regions":[
       {"id":"r1","name":"Projetos","x":0,"y":0,"w":6,"h":6,"cells":[[0,0],[1,0],[2,0],[3,0],[0,1],[1,1],[2,1],[3,1],[0,2],[1,2]]},
       {"id":"r2","name":"Sub","parentId":"r1","x":0,"y":0,"w":2,"h":2,"cells":[[0,0],[1,0],[0,1],[1,1]]},
       {"id":"r3","name":"Rect","x":20,"y":20,"w":4,"h":4},
       {"id":"r4","name":"Ilha","x":-6,"y":-6,"w":5,"h":5,"cells":[[-6,-6],[-5,-6],[-4,-6],[-6,-5],[-5,-5],[-4,-5],[-6,-4],[-5,-4]]}],
     "buildings":[{"id":"b1","regionId":"r1","x":1,"y":1,"w":3,"h":3},{"id":"b2","regionId":null,"x":13,"y":8,"w":3,"h":3},{"id":"b3","regionId":null,"x":30,"y":30,"w":3,"h":3}],
     "names":["Projetos","projetos","  ","Sub","Nova","Projetos (2)","Ilha","","Rect (2)"],
     "rects":[[2,0,8,8],[12,6,6,6],[3,3,3,3],[40,40,5,5],[5,0,8,12],[-3,-3,12,12],[-8,-8,6,6],[18,18,8,8],[28,28,6,6],[-5,-5,3,3]]}
    """;

    private sealed class Water(HashSet<(int, int)> cells) : ILegacyCityTerrain
    {
        public bool Buildable(int x, int y) => !cells.Contains((x, y));
        public bool Roadable(int x, int y) => true;
        public double RoadCost(int x, int y) => 1;
    }

    private static LegacyCity Build(JsonElement sc)
    {
        var water = sc.GetProperty("water").EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32())).ToHashSet();
        var city = new LegacyCity(new Water(water), p => p);
        foreach (var r in sc.GetProperty("regions").EnumerateArray())
        {
            var region = new LegacyCityRegion(r.GetProperty("id").GetString()!, r.GetProperty("name").GetString()!,
                r.TryGetProperty("parentId", out var pid) ? pid.GetString() : null, "#fff")
            { X = r.GetProperty("x").GetInt32(), Y = r.GetProperty("y").GetInt32(), W = r.GetProperty("w").GetInt32(), H = r.GetProperty("h").GetInt32() };
            if (r.TryGetProperty("cells", out var cells)) foreach (var c in cells.EnumerateArray()) region.Add((c[0].GetInt32(), c[1].GetInt32()));
            else region.CellsNull = true;
            city.Regions.Add(region);
        }
        foreach (var b in sc.GetProperty("buildings").EnumerateArray())
            city.Buildings.Add(new LegacyCityBuilding(b.GetProperty("id").GetString()!, "x/" + b.GetProperty("id").GetString() + ".md", "n",
                b.GetProperty("regionId").GetString(), b.GetProperty("x").GetInt32(), b.GetProperty("y").GetInt32(), "house1"));
        return city;
    }

    private static (int, int)[] Cells(JsonElement e) => e.EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetInt32())).ToArray();

    [Fact]
    public async Task CreateMenuRulesMatchTheOriginal()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "create-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        start.ArgumentList.Add(Scenario);
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var oracle = JsonDocument.Parse(await stdout);
        using var sc = JsonDocument.Parse(Scenario);

        var cityNames = Build(sc.RootElement);
        var typed = sc.RootElement.GetProperty("names").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var expectedNames = oracle.RootElement.GetProperty("names").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(expectedNames, typed.Select(cityNames.UniqueRegionName).ToArray());

        int n = 0, withGround = 0;
        foreach (var draw in oracle.RootElement.GetProperty("draws").EnumerateArray())
        {
            var rect = draw.GetProperty("rect").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            var city = Build(sc.RootElement);
            var mask = city.DrawRegionMask(rect[0], rect[1], rect[2], rect[3]);
            var m = draw.GetProperty("mask");
            Assert.Equal(Cells(m.GetProperty("cells")), mask.Cells.ToArray());
            Assert.Equal(m.GetProperty("dry").GetInt32(), mask.Dry);
            Assert.Equal(m.GetProperty("total").GetInt32(), mask.Total);
            var drawn = city.CreateDrawnRegion("n", "", "#fff", rect[0], rect[1], rect[2], rect[3]);
            var made = drawn?.Region;
            Assert.Equal(mask.Cells.Count > 0, made is not null);
            if (made is not null)
            {
                withGround++;
                var after = draw.GetProperty("regions").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetString()!);
                foreach (var r in city.Regions)
                {
                    var js = after[r.Id == made.Id ? "NEW" : r.Id];
                    Assert.Equal(js.GetProperty("x").GetInt32(), r.X);
                    Assert.Equal(js.GetProperty("y").GetInt32(), r.Y);
                    Assert.Equal(js.GetProperty("w").GetInt32(), r.W);
                    Assert.Equal(js.GetProperty("h").GetInt32(), r.H);
                    if (js.GetProperty("cells").ValueKind == JsonValueKind.Null) Assert.True(r.IsRectangle);
                    else Assert.Equal(Cells(js.GetProperty("cells")), r.Cells.ToArray());
                }
                Assert.Equal(draw.GetProperty("root").EnumerateArray().Select(e => e.GetString()).ToArray(),
                    city.RootBuildingsUnder(made).Select(b => b.Id).ToArray());
            }
            if (drawn is not null)
            {   // the way back restores every neighbourhood exactly
                var before = Build(sc.RootElement);
                drawn.Undo();
                Assert.Equal(before.Regions.Select(r => (r.Id, r.X, r.Y, r.W, r.H, r.IsRectangle ? 0 : r.Cells.Count)).ToArray(),
                    city.Regions.Select(r => (r.Id, r.X, r.Y, r.W, r.H, r.IsRectangle ? 0 : r.Cells.Count)).ToArray());
            }
            n++;
        }
        Assert.Equal(10, n);
        Assert.True(withGround >= 6, "o cenário precisa exercitar regiões criadas: " + withGround);
    }
}
