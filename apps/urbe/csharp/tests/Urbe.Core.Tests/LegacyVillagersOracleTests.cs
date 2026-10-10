using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The villagers against the unmodified 1.8.4-beta app.js v25 block and pixel-art.js
/// villager(), run by Node only in the test process (tools/villagers-oracle.mjs) with the
/// same seeded Math.random and Date.now: routes, looks, walking, idling, chats and the
/// sprite pixels of every look that appears.
/// </summary>
public sealed class LegacyVillagersOracleTests
{
    private const double Eps = 1e-7;

    [Theory]
    [InlineData(5)]
    [InlineData(23)]
    public async Task VillagersReplayTheOriginalAndSpritesAreIdentical(int seed)
    {
        using var json = await Oracle(1500, seed);
        var root = json.RootElement;
        var town = new ScriptedTown(root);
        var villagers = new LegacyVillagers(LegacyJsMath.Rng(seed));
        var cam = root.GetProperty("camera");
        var f = root.GetProperty("view");
        var view = new LegacyLifeView(cam.GetProperty("x").GetDouble(), cam.GetProperty("y").GetDouble(), cam.GetProperty("z").GetDouble(),
            root.GetProperty("width").GetDouble(), root.GetProperty("height").GetDouble(),
            f.GetProperty("x0").GetInt32(), f.GetProperty("y0").GetInt32(), f.GetProperty("x1").GetInt32(), f.GetProperty("y1").GetInt32());
        var checkpoints = root.GetProperty("checkpoints").EnumerateArray().ToDictionary(c => c.GetProperty("k").GetInt32());
        double now = root.GetProperty("startMs").GetDouble();
        int steps = root.GetProperty("steps").GetInt32(), compared = 0;
        for (int k = 1; k <= steps; k++)
        {
            double hurry = k < 600 ? 1 : 1 + .7 * Math.Min(1, (k - 600) / 300.0);
            now += 33;
            villagers.Tick(now, view, true, hurry, town);
            if (!checkpoints.TryGetValue(k, out var c)) continue;
            Compare(c, villagers, town, $"seed {seed} k={k}");
            compared++;
        }
        Assert.Equal(checkpoints.Count, compared);

        int sprites = 0;
        foreach (var s in root.GetProperty("sprites").EnumerateArray())
        {
            var look = LookOf(s.GetProperty("look"));
            var expected = Convert.FromBase64String(s.GetProperty("rgba").GetString()!);
            var actual = LegacyVillagerSprite.Render(look, s.GetProperty("dir").GetString()!, s.GetProperty("frame").GetInt32());
            Assert.True(expected.SequenceEqual(actual), $"sprite {s.GetProperty("look")} {s.GetProperty("dir")} {s.GetProperty("frame")}");
            sprites++;
        }
        Assert.True(sprites > 40, $"poucos sprites ({sprites})");
    }

    private static LegacyVillagerLook LookOf(JsonElement l)
    {
        string? S(string k) => l.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return new LegacyVillagerLook(l.GetProperty("skin").GetInt32(), l.GetProperty("hair").GetInt32(), l.GetProperty("pants").GetInt32(),
            S("style")!, S("shirt")!, l.GetProperty("dress").GetInt32() != 0, S("hood"), S("cap"), S("acc"));
    }

    private static void Compare(JsonElement c, LegacyVillagers villagers, ScriptedTown town, string at)
    {
        void Num(double expected, double actual, string what)
        {
            if (Math.Abs(expected - actual) > Eps * Math.Max(1, Math.Abs(expected)))
                Assert.Fail($"{at} {what}: esperado {expected:R}, obtido {actual:R}");
        }
        var people = c.GetProperty("people");
        Assert.True(people.GetArrayLength() == villagers.People.Count, $"{at} moradores: esperado {people.GetArrayLength()}, obtido {villagers.People.Count}");
        int i = 0;
        foreach (var e in people.EnumerateArray())
        {
            var a = villagers.People[i];
            var who = $"morador[{i++}]";
            Assert.Equal(e.GetProperty("tipo").GetString(), a.Kind);
            Assert.Equal(town.Buildings[e.GetProperty("de").GetInt32()], a.From);
            Num(e.GetProperty("x").GetDouble(), a.PosX, who + ".x");
            Num(e.GetProperty("y").GetDouble(), a.PosY, who + ".y");
            Num(e.GetProperty("s").GetDouble(), a.S, who + ".s");
            Num(e.GetProperty("pausa").GetDouble(), a.PauseTime, who + ".pausa");
            Num(e.GetProperty("passo").GetDouble(), a.Step, who + ".passo");
            Num(e.GetProperty("conversa").GetDouble(), a.Conversation, who + ".conversa");
            Num(e.GetProperty("vel").GetDouble(), a.Speed, who + ".vel");
            Assert.Equal(e.GetProperty("dx").GetInt32(), a.DirX);
            Assert.Equal(e.GetProperty("dy").GetInt32(), a.DirY);
            Assert.Equal(e.GetProperty("sentido").GetInt32(), a.Sense);
            Assert.Equal(LookOf(e.GetProperty("look")), a.Look);
            var rota = e.GetProperty("rota");
            if (rota.ValueKind == JsonValueKind.Null) Assert.Null(a.Route);
            else Assert.Equal(rota.EnumerateArray().Select(p => (p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32())), a.Route!);
        }
    }

    private static async Task<JsonDocument> Oracle(int steps, int seed)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "villagers-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        start.ArgumentList.Add(steps.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        return JsonDocument.Parse(await stdout);
    }

    /// <summary>The oracle's scripted town.</summary>
    private sealed class ScriptedTown : ILegacyTownMap
    {
        private readonly HashSet<(int, int)> _roads = [];

        public ScriptedTown(JsonElement root)
        {
            foreach (var r in root.GetProperty("roads").EnumerateArray()) _roads.Add((r[0].GetInt32(), r[1].GetInt32()));
            var list = new List<LegacyCityBuilding>();
            foreach (var b in root.GetProperty("buildings").EnumerateArray())
                list.Add(new LegacyCityBuilding(b.GetProperty("id").GetString()!, b.GetProperty("name").GetString() + ".md", b.GetProperty("name").GetString()!, null,
                    b.GetProperty("x").GetInt32(), b.GetProperty("y").GetInt32(), "house1"));
            Buildings = list;
            Links = root.GetProperty("links").EnumerateArray().Select(l => (list[l[0].GetInt32()].Id, list[l[1].GetInt32()].Id)).ToList();
        }

        public IReadOnlyList<LegacyCityBuilding> Buildings { get; }
        public IReadOnlyList<(string From, string To)> Links { get; }
        public bool Road(int x, int y) => _roads.Contains((x, y));
        public bool Water(int x, int y) => x >= 10 && x <= 13 && y >= 22 && y <= 25;
        public LegacyCityBuilding? BuildingAt(int x, int y) => Buildings.LastOrDefault(b => b.Contains(x, y));
    }
}
