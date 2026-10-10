using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The life of the world against the unmodified 1.8.4-beta world/life.js and the app.js
/// fauna, run by Node only in the test process (tools/life-oracle.mjs) with the same
/// seeded Math.random: 1500 steps of 33 ms with scripted events and villagers, and every
/// particle, animal, event and lamp compared at each checkpoint.
/// </summary>
public sealed class LegacyWorldLifeOracleTests
{
    private const double Eps = 1e-7;

    [Theory]
    [InlineData("dia", 7)]
    [InlineData("noite", 11)]
    public async Task LifeAndFaunaReplayTheOriginalSimulation(string mode, int seed)
    {
        using var json = await Oracle(mode, 1500, seed);
        var root = json.RootElement;
        var host = new ScriptedHost(root);
        var random = LegacyJsMath.Rng(seed);
        var fauna = new LegacyFauna(random);
        var life = new LegacyWorldLife(host, fauna, new LegacyLifeOptions { TimeMode = mode }, random,
            () => DateTimeOffset.FromUnixTimeMilliseconds(1760000000000));
        var cam = root.GetProperty("camera");
        var f = root.GetProperty("view");
        var view = new LegacyLifeView(cam.GetProperty("x").GetDouble(), cam.GetProperty("y").GetDouble(), cam.GetProperty("z").GetDouble(),
            root.GetProperty("width").GetDouble(), root.GetProperty("height").GetDouble(),
            f.GetProperty("x0").GetInt32(), f.GetProperty("y0").GetInt32(), f.GetProperty("x1").GetInt32(), f.GetProperty("y1").GetInt32());
        var events = root.GetProperty("events").EnumerateArray().Select(e => (At: e[0].GetInt32(), Kind: e[1].GetString()!)).ToList();
        var checkpoints = root.GetProperty("checkpoints").EnumerateArray().ToDictionary(c => c.GetProperty("k").GetInt32());
        double dt = root.GetProperty("dt").GetDouble(), now = root.GetProperty("startMs").GetDouble();
        int steps = root.GetProperty("steps").GetInt32(), compared = 0;
        for (int k = 1; k <= steps; k++)
        {
            host.Move(k);
            foreach (var e in events) if (e.At == k) life.Start(e.Kind, view);
            now += 33;
            fauna.Tick(now, view, true, host);
            life.Tick(dt, view);
            if (!checkpoints.TryGetValue(k, out var c)) continue;
            Compare(c, life, fauna, host, $"{mode} k={k}");
            compared++;
        }
        Assert.Equal(checkpoints.Count, compared);
    }

    private static void Compare(JsonElement c, LegacyWorldLife life, LegacyFauna fauna, ScriptedHost host, string at)
    {
        void Num(double expected, double actual, string what)
        {
            if (Math.Abs(expected - actual) > Eps * Math.Max(1, Math.Abs(expected)))
                Assert.Fail($"{at} {what}: esperado {expected:R}, obtido {actual:R}");
        }
        void List<T>(string key, IReadOnlyList<T> actual, params (string Key, Func<T, double> Get)[] fields)
        {
            var e = c.GetProperty(key);
            Assert.True(e.GetArrayLength() == actual.Count, $"{at} {key}: esperado {e.GetArrayLength()}, obtido {actual.Count}");
            int i = 0;
            foreach (var item in e.EnumerateArray())
            {
                foreach (var (k, get) in fields)
                {
                    var p = item.GetProperty(k);
                    Num(p.ValueKind is JsonValueKind.True or JsonValueKind.False ? B(p.GetBoolean()) : p.GetDouble(), get(actual[i]), $"{key}[{i}].{k}");
                }
                i++;
            }
        }
        static double B(bool b) => b ? 1 : 0;

        Num(c.GetProperty("T").GetDouble(), life.Time, "T");
        var v = c.GetProperty("vento");
        Num(v.GetProperty("a").GetDouble(), life.Breeze.A, "vento.a");
        Num(v.GetProperty("x").GetDouble(), life.Breeze.X, "vento.x");
        Num(v.GetProperty("y").GetDouble(), life.Breeze.Y, "vento.y");
        var ch = c.GetProperty("chuva");
        Num(ch.GetProperty("k").GetDouble(), life.Rain.K, "chuva.k");
        Num(ch.GetProperty("alvo").GetDouble(), life.Rain.Target, "chuva.alvo");
        Assert.Equal(ch.GetProperty("neve").GetBoolean(), life.Rain.Snow);
        Num(c.GetProperty("neblina").GetProperty("k").GetDouble(), life.Fog.K, "neblina.k");
        Num(c.GetProperty("pressa").GetDouble(), life.Hurry, "pressa");
        var luz = c.GetProperty("luz");
        var cor = luz.GetProperty("cor").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(cor, new[] { life.CurrentLight.Red, life.CurrentLight.Green, life.CurrentLight.Blue });
        Num(luz.GetProperty("escuro").GetDouble(), life.CurrentLight.Darkness, "luz.escuro");
        Num(c.GetProperty("meteoros").GetDouble(), life.Meteors, "meteoros");

        void Optional<T>(string key, T? actual, Action<JsonElement, T> fields) where T : class
        {
            var e = c.GetProperty(key);
            Assert.True((e.ValueKind != JsonValueKind.Null) == (actual is not null), $"{at} {key}: presença difere");
            if (actual is not null) fields(e, actual);
        }
        Optional("arco", life.Arc is { } a0 ? new[] { a0.T0, a0.End, a0.CX } : null, (e, a) =>
        { Num(e.GetProperty("t0").GetDouble(), a[0], "arco.t0"); Num(e.GetProperty("cx").GetDouble(), a[2], "arco.cx"); });
        Optional("balao", life.Balloon, (e, b) =>
        { Num(e.GetProperty("x").GetDouble(), b.X, "balao.x"); Num(e.GetProperty("y").GetDouble(), b.Y, "balao.y"); Num(e.GetProperty("fim").GetDouble(), b.End, "balao.fim");
          Assert.Equal(e.GetProperty("cs")[0].GetString(), b.Colors[0]); });
        Optional("barco", life.SailBoat, (e, b) =>
        { Num(e.GetProperty("x").GetDouble(), b.X, "barco.x"); Num(e.GetProperty("y").GetDouble(), b.Y, "barco.y");
          Assert.Equal(e.GetProperty("d")[0].GetInt32(), b.DX); Assert.Equal(e.GetProperty("d")[1].GetInt32(), b.DY); });
        Optional("raposa", life.TheFox, (e, r) =>
        { Num(e.GetProperty("x").GetDouble(), r.X, "raposa.x"); Num(e.GetProperty("y").GetDouble(), r.Y, "raposa.y"); Assert.Equal(e.GetProperty("fr").GetInt32(), r.Frame); });
        Optional("cao", life.TheDog, (e, d) =>
        { Num(e.GetProperty("x").GetDouble(), d.X, "cao.x"); Num(e.GetProperty("y").GetDouble(), d.Y, "cao.y"); Num(e.GetProperty("troca").GetDouble(), d.Swap, "cao.troca");
          Assert.Equal(e.GetProperty("senta").GetBoolean(), d.Sitting); Assert.Equal(e.GetProperty("flip").GetBoolean(), d.Flip); Assert.Equal(e.GetProperty("fr").GetInt32(), d.Frame); });
        Optional("festa", life.TheParty, (e, p) =>
        { Num(e.GetProperty("x").GetDouble(), p.X, "festa.x"); Num(e.GetProperty("prox").GetDouble(), p.Next, "festa.prox"); Assert.Equal(e.GetProperty("nome").GetString(), p.Name); });

        List("nuvens", life.Clouds, ("x", n => n.X), ("y", n => n.Y), ("r", n => n.R), ("v", n => n.V));
        List("neblinas", life.FogBanks, ("x", n => n.X), ("y", n => n.Y), ("r", n => n.R), ("v", n => n.V));
        List("gotas", life.Drops, ("x", g => g.X), ("y", g => g.Y), ("v", g => g.V), ("l", g => g.L));
        List("respingos", life.Splashes, ("x", g => g.X), ("y", g => g.Y), ("t", g => g.T));
        List("ondas", life.Ripples, ("x", g => g.X), ("y", g => g.Y), ("t", g => g.T), ("d", g => g.D), ("r", g => g.R));
        List("brilhos", life.Sparkles, ("x", g => g.X), ("y", g => g.Y), ("t", g => g.T), ("d", g => g.D));
        List("peixes", life.Fishes, ("x", g => g.X), ("y", g => g.Y), ("t", g => g.T), ("dir", g => g.Dir), ("alt", g => g.Alt));
        List("borboletas", life.Butterflies, ("x", g => g.X), ("y", g => g.Y), ("alt", g => g.Alt), ("vida", g => g.Life));
        List("vagalumes", life.Fireflies, ("x", g => g.X), ("y", g => g.Y), ("alt", g => g.Alt), ("vx", g => g.VX), ("ph", g => g.Ph));
        List("fumaca", life.Smokes, ("x", g => g.X), ("y", g => g.Y), ("t", g => g.T), ("r0", g => g.R0));
        List("pombos", life.Pigeons, ("x", g => g.X), ("y", g => g.Y), ("t", g => g.T), ("fr", g => g.Frame), ("alt", g => g.Alt));
        Assert.Equal(c.GetProperty("pombos").EnumerateArray().Select(p => p.GetProperty("st").GetString() == "voo"), life.Pigeons.Select(p => p.Flying));
        List("folhas", life.Leaves, ("x", g => g.X), ("y", g => g.Y), ("alt", g => g.Alt));
        List("rajadas", life.Gusts, ("x", g => g.X), ("y", g => g.Y), ("l", g => g.L));
        List("baloes", life.PartyBalloons, ("x", g => g.X), ("alt", g => g.Alt), ("ph", g => g.Ph));
        List("confete", life.Confettis, ("x", g => g.X), ("y", g => g.Y), ("alt", g => g.Alt), ("valt", g => g.VAlt));
        List("foguetes", life.Rockets, ("x", g => g.X), ("y", g => g.Y), ("alt", g => g.Alt), ("topo", g => g.Top));
        List("faiscas", life.Sparks, ("x", g => g.X), ("y", g => g.Y), ("alt", g => g.Alt), ("valt", g => g.VAlt), ("d", g => g.D), ("pesada", g => B(g.Heavy)));
        List("claroes", life.Flashes, ("x", g => g.X), ("alt", g => g.Alt), ("t", g => g.T));
        List("estrelas", life.Stars, ("x", g => g.X), ("y", g => g.Y), ("vx", g => g.VX), ("d", g => g.D));
        List("lampioes", life.Lamps(), ("x", g => g.X), ("y", g => g.Y), ("ph", g => g.Ph));
        var emotes = c.GetProperty("emotes").EnumerateArray().Select(e => (e[0].GetInt32(), e[1].GetString(), e[2].GetDouble())).ToList();
        Assert.Equal(emotes.Count, life.Emotes.Count);
        foreach (var (who, text, end) in emotes)
        {
            Assert.True(life.Emotes.TryGetValue(host.People[who], out var e), $"{at} emote do morador {who}");
            Assert.Equal(text, e.Text);
            Num(end, e.End, "emote.fim");
        }
        var fa = c.GetProperty("fauna");
        Assert.True(fa.GetArrayLength() == fauna.Animals.Count, $"{at} fauna: esperado {fa.GetArrayLength()}, obtido {fauna.Animals.Count}");
        int i = 0;
        foreach (var e in fa.EnumerateArray())
        {
            var an = fauna.Animals[i++];
            Assert.Equal(e.GetProperty("kind").GetString(), an.Kind);
            Num(e.GetProperty("x").GetDouble(), an.X, $"fauna[{i - 1}].x");
            Num(e.GetProperty("y").GetDouble(), an.Y, $"fauna[{i - 1}].y");
            Num(e.GetProperty("fr").GetDouble(), an.Frame, $"fauna[{i - 1}].fr");
            Assert.Equal(e.GetProperty("flip").GetBoolean(), an.Flip);
            if (an.Kind != "bird")
            {
                Assert.Equal(e.GetProperty("st").GetString(), an.State);
                Num(e.GetProperty("t").GetDouble(), an.T, $"fauna[{i - 1}].t");
                Num(e.GetProperty("tx").GetDouble(), an.TX, $"fauna[{i - 1}].tx");
            }
            else Num(e.GetProperty("alt").GetDouble(), an.Alt, $"fauna[{i - 1}].alt");
        }
    }

    private static async Task<JsonDocument> Oracle(string mode, int steps, int seed)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "life-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        start.ArgumentList.Add(mode);
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

    /// <summary>The oracle's scripted world, read from its JSON.</summary>
    private sealed class ScriptedHost : ILegacyLifeHost
    {
        private readonly int _x0, _y0, _x1, _y1, _default;
        private readonly byte[] _biomes;
        private readonly HashSet<(int, int)> _roadSet = [];
        private readonly List<(int X, int Y)> _roads = [];
        private readonly List<(int X, int Y, int W, int H)> _regions = [];
        private readonly Person[] _people = [new(), new(), new(), new()];

        public ScriptedHost(JsonElement root)
        {
            var a = root.GetProperty("area");
            (_x0, _y0, _x1, _y1) = (a.GetProperty("x0").GetInt32(), a.GetProperty("y0").GetInt32(), a.GetProperty("x1").GetInt32(), a.GetProperty("y1").GetInt32());
            _default = root.GetProperty("defaultBiome").GetInt32();
            _biomes = Convert.FromBase64String(root.GetProperty("biomes").GetString()!);
            foreach (var r in root.GetProperty("roads").EnumerateArray())
            {
                var t = (r[0].GetInt32(), r[1].GetInt32());
                _roads.Add(t);
                _roadSet.Add(t);
            }
            var districts = new List<LegacyLifeDistrict>();
            foreach (var r in root.GetProperty("regions").EnumerateArray())
            {
                int w = r.GetProperty("w").GetInt32();
                _regions.Add((r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(), w, r.GetProperty("h").GetInt32()));
                if (w != 0)
                    districts.Add(new LegacyLifeDistrict(r.GetProperty("name").GetString()!, r.GetProperty("parentId").ValueKind != JsonValueKind.Null,
                        r.GetProperty("cx").GetDouble(), r.GetProperty("cy").GetDouble()));
            }
            Districts = districts;
            var store = new List<LegacyCityBuilding>();
            foreach (var b in root.GetProperty("buildings").EnumerateArray())
                store.Add(new LegacyCityBuilding("b" + store.Count, "n" + store.Count + ".md", "n" + store.Count, null, b.GetProperty("x").GetInt32(), b.GetProperty("y").GetInt32(), "house1"));
            Buildings = store;
        }

        public IReadOnlyList<LegacyCityBuilding> Buildings { get; }
        public IReadOnlyCollection<(int X, int Y)> Roads => _roads;
        public IEnumerable<LegacyLifeDistrict> Districts { get; }
        public IReadOnlyList<ILegacyLifePerson> People => _people;

        public int Biome(int x, int y) =>
            x < _x0 || x >= _x1 || y < _y0 || y >= _y1 ? _default : _biomes[(y - _y0) * (_x1 - _x0) + (x - _x0)];
        public bool Water(int x, int y) => Biome(x, y) <= 3;
        public bool Road(int x, int y) => _roadSet.Contains((x, y));
        public bool House(int x, int y) => Buildings.Any(b => b.Contains(x, y));
        public bool District(int x, int y) => _regions.Any(r => r.W != 0 && x >= r.X && x < r.X + r.W && y >= r.Y && y < r.Y + r.H);

        /// <summary>The same scripted villagers as people(k) in the oracle.</summary>
        public void Move(int k)
        {
            _people[0].Set(true, 4 + ((k * .07) % 30), 10.4, 1, 0, 0, true, false);
            bool home = k % 400 < 120;
            _people[1].Set(true, 12.5, 2 + ((k * .05) % 20), 0, 1, home ? 1 : 0, true, home);
            _people[2].Set(true, 25, 20, 0, 0, 1, false, true);
            _people[3].Set(false, 0, 0, 0, 0, 0, true, false);
        }

        private sealed class Person : ILegacyLifePerson
        {
            public bool Placed { get; private set; }
            public double X { get; private set; }
            public double Y { get; private set; }
            public double DX { get; private set; }
            public double DY { get; private set; }
            public double Pause { get; private set; }
            public double Talk => -5;
            public bool Wanderer { get; private set; }
            public bool Indoors { get; private set; }
            public int? ShirtLength => 7;

            public void Set(bool placed, double x, double y, double dx, double dy, double pause, bool wanderer, bool indoors) =>
                (Placed, X, Y, DX, DY, Pause, Wanderer, Indoors) = (placed, x, y, dx, dy, pause, wanderer, indoors);
        }
    }
}
