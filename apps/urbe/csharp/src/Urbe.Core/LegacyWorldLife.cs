namespace Urbe.Core;

/// <summary>
/// C# port of the Urbe 1.8.4-beta src/world/life.js ("o aquário"): continuous light,
/// wind, clouds, rain/snow with rainbow, fog, water sparkles, jumping fish, ripples,
/// butterflies, fireflies, leaves, gusts, chimney smoke, pigeons, the dog, the fox,
/// balloon, party (balloons, confetti, fireworks), sailing boat, shooting stars, villager
/// emotes and street lamps. Ported line by line from passo()/iniciar()/explodir(); the
/// random sequence is consumed in the same order, so an injected generator replays the
/// original (see LegacyWorldLifeOracleTests). Drawing lives in the native client.
/// </summary>
public sealed class LegacyWorldLife
{
    public readonly record struct Light(int Red, int Green, int Blue, double Darkness, double Hour);
    private readonly record struct Stop(double Hour, int R, int G, int B, double Dark);

    // Literal keyframes from src/world/life.js CHAVES (1.8.4-beta).
    private static readonly Stop[] Stops =
    [
        new(0,78,92,158,1), new(4.6,84,96,162,1),
        new(5.4,166,132,178,.72), new(6.2,246,186,166,.32),
        new(7.2,255,234,212,.06), new(8.2,255,255,255,0),
        new(15.8,255,255,255,0), new(16.6,255,244,222,0),
        new(17.3,255,214,160,.06), new(18.1,248,164,124,.3),
        new(18.9,180,120,158,.6), new(19.8,106,104,170,.88),
        new(20.6,80,94,160,1), new(24,78,92,158,1)
    ];

    public static readonly string[] ButterflyColors = ["#f7d046", "#f4f0e6", "#ee8a4a", "#8fb8ff", "#e59ad8", "#b7e36a"];
    public static readonly string[] PartyColors = ["#ff5d5d", "#ffd24a", "#5dd6ff", "#9d7bff", "#6dffa0", "#ff8ad8", "#ffffff"];
    public static readonly string[][] BalloonColors =
        [["#d9483b", "#f4d35e"], ["#3d7dd8", "#f2f2f2"], ["#6c4ab6", "#f4a259"], ["#2a9d8f", "#e9c46a"], ["#e76f51", "#264653"]];
    private static readonly string[] LeafColors = ["#d9a13b", "#c8622f", "#9bb54a", "#e3c15a", "#b04a2a"];
    private static readonly string[] FireworkKinds = ["peonia", "anel", "salgueiro", "peonia"];
    private static readonly (int X, int Y)[] Dirs4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly Func<double> _r;
    private readonly ILegacyLifeHost? _host;
    private readonly LegacyFauna? _fauna;

    // ---------------------------------------------------------------- state (life.js "estado")
    public double Time { get; private set; }          // T
    public double Quality { get; set; } = 1;          // q
    public Wind Breeze { get; } = new();              // vento
    public List<Cloud> Clouds { get; } = [];          // nuvens
    public List<Cloud> FogBanks { get; } = [];        // neblinas
    public List<Drop> Drops { get; } = [];            // gotas
    public List<Splash> Splashes { get; } = [];       // respingos
    public List<Ripple> Ripples { get; } = [];        // ondas
    public List<Sparkle> Sparkles { get; } = [];      // brilhos
    public List<Fish> Fishes { get; } = [];           // peixes
    public List<Butterfly> Butterflies { get; } = []; // borboletas
    public List<Firefly> Fireflies { get; } = [];     // vagalumes
    public List<Smoke> Smokes { get; } = [];          // fumaca
    public List<Pigeon> Pigeons { get; } = [];        // pombos
    public List<Leaf> Leaves { get; } = [];           // folhas
    public List<Gust> Gusts { get; } = [];            // rajadas
    public List<PartyBalloon> PartyBalloons { get; } = []; // baloes
    public List<Confetti> Confettis { get; } = [];    // confete
    public List<Rocket> Rockets { get; } = [];        // foguetes
    public List<Spark> Sparks { get; } = [];          // faiscas
    public List<Flash> Flashes { get; } = [];         // claroes
    public List<ShootingStar> Stars { get; } = [];    // estrelas
    public Dictionary<ILegacyLifePerson, Emote> Emotes { get; } = new(ReferenceEqualityComparer.Instance);
    public Weather Rain { get; } = new();             // chuva
    public Weather Fog { get; } = new();              // neblina
    public Rainbow? Arc { get; private set; }         // arco
    public HotAirBalloon? Balloon { get; private set; } // balao
    public Boat? SailBoat { get; private set; }       // barco
    public Fox? TheFox { get; private set; }          // raposa
    public Dog? TheDog { get; private set; }          // cao
    public Party? TheParty { get; private set; }      // festa
    public double Meteors { get; private set; }       // meteoros
    private double _nextEvent, _nextFish, _nextPigeons, _nextEmote, _nextStar, _smokeT;
    private List<Lamp>? _lamps;
    private int _lampKey = -1;

    public LegacyLifeOptions Options { get; }
    public Light CurrentLight { get; private set; }
    /// <summary>H.pressa: villagers walk faster in the rain (urbeVelPovo).</summary>
    public double Hurry { get; private set; } = 1;
    /// <summary>avisar(): the discreet event notice (shown for 4.5 s).</summary>
    public event Action<string>? Notice;

    /// <param name="random">Math.random; replaced by the oracle with the recorded sequence.</param>
    /// <param name="clock">Date.now/new Date() for the 'ciclo' and 'auto' hours.</param>
    public LegacyWorldLife(ILegacyLifeHost? host = null, LegacyFauna? fauna = null, LegacyLifeOptions? options = null,
        Func<double>? random = null, Func<DateTimeOffset>? clock = null)
    {
        _host = host;
        _fauna = fauna;
        Options = options ?? new LegacyLifeOptions();
        _r = random ?? Random.Shared.NextDouble;
        Clock = clock ?? (() => DateTimeOffset.Now);
        // module load order: vento, then the proximo* timers
        Breeze.A = Rnd(0, Math.PI * 2);
        Breeze.S = .7; Breeze.X = .5; Breeze.Y = .1;
        _nextEvent = Rnd(14, 26);
        _nextFish = Rnd(3, 7);
        _nextPigeons = Rnd(2, 5);
        _nextEmote = Rnd(4, 9);
        _nextStar = Rnd(15, 40);
        UpdateLight();
    }

    public Func<DateTimeOffset> Clock { get; set; }

    /// <summary>ambiente (urbeOpcoes): auto, ciclo, dia, entardecer or noite.</summary>
    public string TimeMode
    {
        get => Options.TimeMode;
        set { Options.TimeMode = value; UpdateLight(); }
    }

    // ---------------------------------------------------------------- utilities
    private double Rnd(double a, double b) => a + _r() * (b - a);
    private T Pick<T>(IReadOnlyList<T> a) => a[(int)Math.Floor(_r() * a.Count)];
    private static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    private static int Floor(double v) => (int)Math.Floor(v);

    /// <summary>life.js hash(x,y).</summary>
    public static uint Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (int)((uint)h >> 13)) * 1274126177;
            return (uint)(h ^ (int)((uint)h >> 16));
        }
    }

    private bool Water(double x, double y) => _host!.Water(Floor(x), Floor(y));
    private int Biome(double x, double y) => _host!.Biome(Floor(x), Floor(y));
    private bool Road(double x, double y) => _host!.Road(Floor(x), Floor(y));
    private bool House(double x, double y) => _host!.House(Floor(x), Floor(y));
    private static bool IsField(int b) => b is (int)LegacyBiome.Grass or (int)LegacyBiome.Meadow or (int)LegacyBiome.Steppe or (int)LegacyBiome.Savanna or (int)LegacyBiome.Hills;
    private static bool IsWood(int b) => b is (int)LegacyBiome.Forest or (int)LegacyBiome.Dense or (int)LegacyBiome.Taiga or (int)LegacyBiome.Swamp;
    public static bool IsCold(int b) => b is (int)LegacyBiome.Snow or (int)LegacyBiome.Tundra or (int)LegacyBiome.Peak or (int)LegacyBiome.Taiga;
    private bool Field(double x, double y) => IsField(Biome(x, y));
    private bool Wood(double x, double y) => IsWood(Biome(x, y));
    private bool Free(double x, double y) => !Water(x, y) && !Road(x, y) && !House(x, y);

    private (int X, int Y)? TileInView(LegacyLifeView f, Func<double, double, bool> pred, int tries = 8)
    {
        for (int i = 0; i < tries; i++)
        {
            int x = Floor(Rnd(f.X0, f.X1 + 1)), y = Floor(Rnd(f.Y0, f.Y1 + 1));
            if (pred(x, y)) return (x, y);
        }
        return null;
    }

    // ---------------------------------------------------------------- hour and light
    private static int JsRound(double value) => (int)Math.Floor(value + .5);

    public static Light LightAt(double hour)
    {
        if (!double.IsFinite(hour)) throw new ArgumentOutOfRangeException(nameof(hour));
        hour = ((hour % 24) + 24) % 24;
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            var a = Stops[i];
            var b = Stops[i + 1];
            if (hour < a.Hour || hour > b.Hour) continue;
            double k = (hour - a.Hour) / (b.Hour - a.Hour), e = k * k * (3 - 2 * k);
            return new Light(JsRound(a.R + (b.R - a.R) * e), JsRound(a.G + (b.G - a.G) * e), JsRound(a.B + (b.B - a.B) * e),
                a.Dark + (b.Dark - a.Dark) * e, hour);
        }
        return new Light(255, 255, 255, 0, hour);
    }

    /// <summary>life.js hora(): fixed modes, 'ciclo' (a day every 24 min) or the device clock.</summary>
    public static double HourAt(DateTimeOffset localTime, string mode = "auto") => mode switch
    {
        "dia" => 12.5,
        "entardecer" => 18.2,
        "noite" => 23,
        "ciclo" => ((localTime.ToUnixTimeMilliseconds() / 1000d / (24 * 60)) % 1) * 24,
        _ => localTime.Hour + localTime.Minute / 60d + localTime.Second / 3600d
    };

    /// <summary>atualizarLuz(): rain darkens and cools the light.</summary>
    public void UpdateLight()
    {
        double h = HourAt(Clock(), Options.TimeMode);
        var l = LightAt(h);
        double k = Rain.K;
        if (k > 0)
            l = l with
            {
                Red = JsRound(l.Red * (1 - k * .26)), Green = JsRound(l.Green * (1 - k * .22)), Blue = JsRound(l.Blue * (1 - k * .14)),
                Darkness = Math.Min(1, l.Darkness + k * .12)
            };
        CurrentLight = l with { Hour = h };
    }

    // ---------------------------------------------------------------- clock (tick)
    /// <summary>tick(): one step of <paramref name="dt"/> seconds (clamped to .1 as the original).</summary>
    public void Tick(double dt, LegacyLifeView view)
    {
        if (!double.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        dt = Math.Min(.1, dt);
        Time += dt;
        UpdateLight();
        if (view.Zoom < .12 || _host is null) return;
        Step(dt, view);
    }

    /// <summary>evento(tipo): the 'Cidade: …' commands of the command palette.</summary>
    public void Start(string kind, LegacyLifeView view)
    {
        if (_host is null) return;
        Begin(kind, view);
    }

    // ---------------------------------------------------------------- events
    private LegacyLifeDistrict? DistrictInView(LegacyLifeView f)
    {
        var cand = new List<LegacyLifeDistrict>();
        var sub = new List<LegacyLifeDistrict>();
        foreach (var d in _host!.Districts)
        {
            if (!f.Inside(d.X, d.Y, -2)) continue;
            cand.Add(d);
            if (d.HasParent) sub.Add(d);
        }
        return sub.Count > 0 ? Pick(sub) : cand.Count > 0 ? Pick(cand) : null;
    }

    private (int X, int Y)? BigWater(LegacyLifeView f) => TileInView(f, (x, y) =>
    {
        if (!Water(x, y)) return false;
        for (int d = 0; d < 8; d++)
        {
            double a = d * Math.PI / 4;
            if (!Water(x + LegacyJsMath.Round(Math.Cos(a) * 3), y + LegacyJsMath.Round(Math.Sin(a) * 3))) return false;
        }
        return true;
    }, 30);

    private void RollEvent(LegacyLifeView v)  // sortearEvento
    {
        var o = Options;
        var L = CurrentLight;
        double h = L.Hour;
        var op = new List<(string Kind, double W)>();
        if (o.Weather)
        {
            if (Rain.Target == 0 && Rain.K < .05) op.Add(("chuva", 2.2));
            if (Fog.K < .05) op.Add(("neblina", h > 4.5 && h < 9.5 ? 4 : .4));
        }
        if (o.Events)
        {
            if (L.Darkness < .2 && Rain.K < .1 && Balloon is null) op.Add(("balao", 1.6));
            if (TheParty is null && v.Zoom >= .3) op.Add(("festa", L.Darkness > .55 ? 3 : 1.4));
            if (TheFox is null && (h >= 17.3 || h < 6.3) && v.Zoom >= .45) op.Add(("raposa", 1.8));
            if (L.Darkness > .75 && Meteors == 0) op.Add(("meteoros", .8));
            if (o.Fauna) op.Add(("revoada", 1.1));
            if (SailBoat is null && v.Zoom >= .35) op.Add(("barco", 1.8));
        }
        if (op.Count == 0) return;
        double tot = 0;
        foreach (var e in op) tot += e.W;
        double r = _r() * tot;
        foreach (var e in op)
        {
            r -= e.W;
            if (r <= 0) { Begin(e.Kind, v); return; }
        }
    }

    private void Say(string text)
    {
        if (Options.Events) Notice?.Invoke(text);
    }

    private void Begin(string kind, LegacyLifeView v)
    {
        UpdateLight();
        var f = v;
        double cx = (f.X0 + f.X1) / 2.0, cy = (f.Y0 + f.Y1) / 2.0;
        switch (kind)
        {
            case "chuva":
                Rain.Target = Rnd(.55, 1);
                Rain.End = Time + Rnd(40, 85);
                Rain.Snow = IsCold(Biome(cx, cy));
                Say(Rain.Snow ? "❄ Começou a nevar" : "🌧 Chuva passageira");
                break;
            case "neblina":
                Fog.Target = Rnd(.6, 1);
                Fog.End = Time + Rnd(45, 90);
                Say("🌫 Neblina");
                break;
            case "balao":
            {
                double ang = Math.Atan2(Breeze.Y, Breeze.X), vx = Math.Cos(ang), vy = Math.Sin(ang) * .5;
                double lx3 = (f.X1 - f.X0) / 2.0 + 3, ly3 = (f.Y1 - f.Y0) / 2.0 + 3;
                double kk = Math.Min(lx3 / Math.Max(.01, Math.Abs(vx)), ly3 / Math.Max(.01, Math.Abs(vy)));
                double bx = cx - vx * kk, by = cy - vy * kk + Rnd(-2, 2);
                Balloon = new HotAirBalloon { X = bx, Y = by, VX = vx * .9, VY = vy * .9, Alt = 3.4, Colors = Pick(BalloonColors), End = Time + kk * 2 / .9 + 4 };
                Say("🎈 Um balão cruza o céu");
                break;
            }
            case "festa":
            {
                if (DistrictInView(f) is not { } b) return;
                bool night = CurrentLight.Darkness > .55;
                TheParty = new Party { X = b.X, Y = b.Y, Name = b.Name, Night = night, End = Time + (night ? 38 : 30), Next = Time + .5 };
                Say(night ? "🎆 Fogos sobre " + b.Name : "🎉 Festa em " + b.Name);
                foreach (var a in _host!.People)
                    if (a.Placed && Math.Abs(a.X - b.X) < 7 && Math.Abs(a.Y - b.Y) < 6)
                    {
                        var c = Pick(new[] { "♪", "♥", "♪", "!" });
                        Emotes[a] = new Emote(c, Time + Rnd(3, 6));
                    }
                break;
            }
            case "raposa":
            {
                if (TileInView(f, (x, y) => Field(x, y) && Free(x, y), 40) is not { } t) return;
                int dir = _r() < .5 ? 1 : -1;
                TheFox = new Fox { X = dir > 0 ? f.X0 - 2 : f.X1 + 2, Y = t.Y + .5, Dir = dir, End = Time + 60 };
                Say("🦊 Uma raposa atravessa os campos");
                break;
            }
            case "meteoros":
                Meteors = Time + 25;
                Say("✦ Chuva de estrelas cadentes");
                break;
            case "revoada":
            {
                var fa = _fauna?.Animals;
                bool dl = _r() < .5;
                double y0 = Rnd(f.Y0 + 2, f.Y1 - 2), x0 = dl ? f.X0 - 6 : f.X1 + 6, vx = (dl ? 1 : -1) * Rnd(3.2, 4);
                int n = 9 + Floor(_r() * 7);
                for (int i = 0; i < n; i++)
                {
                    int side = i % 2 != 0 ? 1 : -1;
                    double k = Math.Ceiling(i / 2.0);
                    double bx = x0 - (dl ? 1 : -1) * k * .85, by = y0 + side * k * .55;
                    double vy = Rnd(-.2, .2), ft = _r() * .2, alt = 2.6 + _r() * .4;
                    fa?.Add(new LegacyAnimal { Kind = "bird", X = bx, Y = by, VX = vx, VY = vy, Frame = i % 2, FrameTime = ft, Flip = !dl, Alt = alt });
                }
                Say("🕊 Uma revoada passa pela cidade");
                break;
            }
            case "barco":
            {
                if (BigWater(f) is not { } w) return;
                var d = Pick(Dirs4);
                SailBoat = new Boat { X = w.X + .5, Y = w.Y + .5, DX = d.X, DY = d.Y, End = Time + Rnd(70, 120) };
                break;
            }
        }
    }

    // ---------------------------------------------------------------- simulation step (passo)
    private void Step(double dt, LegacyLifeView v)
    {
        var f = v;
        var o = Options;
        var L = CurrentLight;
        bool day = L.Darkness < .3, night = L.Darkness > .55;
        double T = Time, q = Quality;
        var host = _host!;

        // wind: changes slowly, with gusts
        Breeze.A += Rnd(-.03, .03) * dt * 10;
        Breeze.S = Clamp(.75 + .45 * Math.Sin(T * .07) + .25 * Math.Sin(T * .31), .2, 1.5);
        Breeze.X = Math.Cos(Breeze.A) * Breeze.S;
        Breeze.Y = Math.Sin(Breeze.A) * Breeze.S * .45;

        // events
        if (T > _nextEvent) { _nextEvent = T + Rnd(40, 95); RollEvent(v); }
        L = CurrentLight;
        if (Rain.Target != 0 && T > Rain.End)
        {
            Rain.Target = 0;
            if (!Rain.Snow && CurrentLight.Darkness < .3 && _r() < .6) Arc = new Rainbow(T + 6, T + 46, Rnd(.25, .75));
        }
        Rain.K += (Rain.Target - Rain.K) * Math.Min(1, dt * .35);
        if (Rain.K < .004 && Rain.Target == 0) Rain.K = 0;
        Hurry = 1 + Rain.K * .7;
        if (Fog.Target != 0 && T > Fog.End) Fog.Target = 0;
        Fog.K += (Fog.Target - Fog.K) * Math.Min(1, dt * .25);
        if (Arc is { } arc && T > arc.End) Arc = null;
        if (TheParty is { } party && T > party.End) TheParty = null;
        if (Meteors != 0 && T > Meteors) Meteors = 0;

        double cx = (f.X0 + f.X1) / 2.0, cy = (f.Y0 + f.Y1) / 2.0, lx = f.X1 - f.X0, ly = f.Y1 - f.Y0, area = lx * ly;

        // clouds (shadows passing over everything)
        if (o.Weather)
        {
            int target = (int)LegacyJsMath.Round(Clamp(area / 520, 3, 11) * (q * .5 + .5) * (1 + Rain.K));
            while (Clouds.Count < target)
            {
                double x = cx + Rnd(-lx, lx), y = cy + Rnd(-ly, ly), r = Rnd(5, 11);
                Clouds.Add(new Cloud { X = x, Y = y, R = r, V = Floor(_r() * 3) });
            }
            for (int i = Clouds.Count - 1; i >= 0; i--)
            {
                var nu = Clouds[i];
                nu.X += Breeze.X * .55 * dt;
                nu.Y += Breeze.Y * .55 * dt;
                if (Math.Abs(nu.X - cx) > lx * 1.3 + nu.R * 2 || Math.Abs(nu.Y - cy) > ly * 1.3 + nu.R * 2 || Clouds.Count > target + 2)
                {
                    if (Clouds.Count > target) { Clouds.RemoveAt(i); continue; }
                    nu.X = cx - Math.Sign(Breeze.X != 0 ? Breeze.X : 1) * (lx * .9 + nu.R * 1.5) + Rnd(-2, 2);
                    nu.Y = cy + Rnd(-ly * .8, ly * .8);
                }
            }
        }
        else Clouds.Clear();
        if (Fog.K > .02)
        {
            while (FogBanks.Count < LegacyJsMath.Round(8 * q))
            {
                double x = cx + Rnd(-lx, lx), y = cy + Rnd(-ly, ly), r = Rnd(6, 12);
                FogBanks.Add(new Cloud { X = x, Y = y, R = r, V = Floor(_r() * 3) });
            }
            foreach (var n in FogBanks)
            {
                n.X += Breeze.X * .25 * dt;
                if (Math.Abs(n.X - cx) > lx * 1.2 + n.R) n.X = cx - Math.Sign(Breeze.X != 0 ? Breeze.X : 1) * (lx * .9 + n.R);
            }
        }
        else FogBanks.Clear();

        // rain/snow (on the screen)
        double W = v.Width, Hh = v.Height;
        int nG = (int)LegacyJsMath.Round(Rain.K * 170 * q * Math.Min(2.2, W * Hh / (420 * 860)));
        while (Drops.Count < nG)
        {
            double x = Rnd(-40, W + 40), y = Rnd(-Hh, Hh), vv = Rnd(520, 760), l = Rnd(9, 16), ph = _r() * 6;
            Drops.Add(new Drop { X = x, Y = y, V = vv, L = l, Ph = ph });
        }
        if (Drops.Count > nG) Drops.RemoveRange(nG, Drops.Count - nG);
        foreach (var g in Drops)
        {
            if (Rain.Snow) { g.Y += g.V * .12 * dt; g.X += (Breeze.X * 40 + Math.Sin(T * 1.7 + g.Ph) * 18) * dt; }
            else { g.Y += g.V * dt; g.X += Breeze.X * 140 * dt; }
            if (g.Y > Hh + 10)
            {
                g.Y = Rnd(-40, -5);
                g.X = Rnd(-40, W + 40);
                if (!Rain.Snow && _r() < .35)
                {
                    double sx = Rnd(0, W), sy = Rnd(0, Hh);
                    Splashes.Add(new Splash { X = sx, Y = sy });
                }
            }
            if (g.X > W + 60) g.X -= W + 100;
            if (g.X < -60) g.X += W + 100;
        }
        for (int i = Splashes.Count - 1; i >= 0; i--)
        {
            Splashes[i].T += dt;
            if (Splashes[i].T > .25) Splashes.RemoveAt(i);
        }
        if (Rain.K > .15 && !Rain.Snow)
            for (int r = 0; r < LegacyJsMath.Round(Rain.K * 5 * q); r++)
                if (TileInView(f, Water, 2) is { } tw)
                {
                    double x = tw.X + _r(), y = tw.Y + _r(), d = Rnd(.7, 1.1), rr = Rnd(.25, .5);
                    Ripples.Add(new Ripple { X = x, Y = y, D = d, R = rr });
                }

        // water: sparkles, fish, duck wakes
        int nb = (int)LegacyJsMath.Round((day ? 9 : night ? 3 : 5) * q * dt * 10);
        for (int i = 0; i < nb; i++)
            if (TileInView(f, Water, 2) is { } tb)
            {
                double x = tb.X + _r(), y = tb.Y + _r(), d = Rnd(.5, 1.2);
                Sparkles.Add(new Sparkle { X = x, Y = y, D = d });
            }
        for (int i = Sparkles.Count - 1; i >= 0; i--)
        {
            Sparkles[i].T += dt;
            if (Sparkles[i].T > Sparkles[i].D) Sparkles.RemoveAt(i);
        }
        if (o.Fauna && v.Zoom >= .35 && T > _nextFish)
        {
            _nextFish = T + Rnd(3.5, 9);
            if (TileInView(f, (x, y) => Water(x, y) && Water(x + 1, y) && Water(x - 1, y) && Water(x, y + 1), 14) is { } tp)
            {
                int dir = _r() < .5 ? 1 : -1;
                double d = Rnd(.8, 1.1), alt = Rnd(.5, .9);
                Fishes.Add(new Fish { X = tp.X + .5, Y = tp.Y + .5, Dir = dir, D = d, Alt = alt });
                Ripples.Add(new Ripple { X = tp.X + .5, Y = tp.Y + .5, D = .9, R = .45 });
            }
        }
        for (int i = Fishes.Count - 1; i >= 0; i--)
        {
            var pe = Fishes[i];
            pe.T += dt;
            if (pe.T > pe.D)
            {
                Ripples.Add(new Ripple { X = pe.X + pe.Dir * .9, Y = pe.Y, D = 1, R = .55 });
                Ripples.Add(new Ripple { X = pe.X + pe.Dir * .9, Y = pe.Y, T = -.15, D = 1.2, R = .8 });
                Fishes.RemoveAt(i);
            }
        }
        var fa = _fauna?.Animals ?? [];
        foreach (var an in fa)
            if (an.Kind == "duck" && an.State == "walk" && _r() < dt * 1.6)
                Ripples.Add(new Ripple { X = an.X, Y = an.Y + .1, D = .9, R = .35 });
        for (int i = Ripples.Count - 1; i >= 0; i--)
        {
            Ripples[i].T += dt;
            if (Ripples[i].T > Ripples[i].D) Ripples.RemoveAt(i);
        }
        if (Ripples.Count > 140) Ripples.RemoveRange(0, Ripples.Count - 140);

        // butterflies (by day, in fields and gardens)
        int nBorb = o.Fauna && day && Rain.K < .2 && v.Zoom >= .38 ? (int)LegacyJsMath.Round(8 * q) : 0;
        if (Butterflies.Count < nBorb && _r() < dt * 2)
            if (TileInView(f, (x, y) => (Field(x, y) || House(x + 2, y)) && !Water(x, y), 14) is { } tc)
            {
                double ph = _r() * 9;
                var cor = Pick(ButterflyColors);
                double life = Rnd(18, 40), alt = Rnd(.3, .8);
                Butterflies.Add(new Butterfly { CX = tc.X + .5, CY = tc.Y + .5, X = tc.X + .5, Y = tc.Y + .5, Ph = ph, Color = cor, Life = life, Alt = alt });
            }
        for (int i = Butterflies.Count - 1; i >= 0; i--)
        {
            var bo = Butterflies[i];
            bo.T += dt;
            bo.Ph += dt;
            bo.CX += (Math.Sin(bo.Ph * .37) * .5 + Breeze.X * .15) * dt;
            bo.CY += Math.Cos(bo.Ph * .29) * .35 * dt;
            bo.X = bo.CX + Math.Sin(bo.Ph * 1.9) * .55;
            bo.Y = bo.CY + Math.Sin(bo.Ph * 2.6) * .3;
            bo.Alt = .45 + Math.Sin(bo.Ph * 3.1) * .2;
            if (bo.T > bo.Life || !f.Inside(bo.X, bo.Y, 4) || nBorb == 0 && bo.T > 2) Butterflies.RemoveAt(i);
        }

        // fireflies (at night, over the grass)
        int nVag = o.Fauna && L.Darkness > .45 && Rain.K < .3 && v.Zoom >= .4 ? (int)LegacyJsMath.Round(30 * q) : 0;
        if (Fireflies.Count < nVag)
            if (TileInView(f, (x, y) => (Field(x, y) || Wood(x, y)) && !Water(x, y), 10) is { } tv)
            {
                double x = tv.X + _r(), y = tv.Y + _r(), ph = _r() * 9, alt = Rnd(.2, .9), life = Rnd(12, 30);
                Fireflies.Add(new Firefly { X = x, Y = y, Ph = ph, Alt = alt, Life = life });
            }
        for (int i = Fireflies.Count - 1; i >= 0; i--)
        {
            var va = Fireflies[i];
            va.T += dt;
            va.VX += Rnd(-.8, .8) * dt;
            va.VY += Rnd(-.8, .8) * dt;
            va.VX *= .97;
            va.VY *= .97;
            va.X += va.VX * dt * .6;
            va.Y += va.VY * dt * .6;
            va.Alt = Clamp(va.Alt + Rnd(-.3, .3) * dt, .15, 1.1);
            if (va.T > va.Life || !f.Inside(va.X, va.Y, 3) || Fireflies.Count > nVag + 4) Fireflies.RemoveAt(i);
        }

        // leaves falling from the woods and gusts of wind over the fields
        if (o.Weather && v.Zoom >= .4)
        {
            if (_r() < dt * (2.5 + Breeze.S * 3) * q)
                if (TileInView(f, Wood, 6) is { } tf)
                {
                    double x = tf.X + _r(), y = tf.Y + _r(), alt = Rnd(1, 1.8), ph = _r() * 9;
                    Leaves.Add(new Leaf { X = x, Y = y, Alt = alt, Ph = ph, Color = Pick(LeafColors) });
                }
            if (Breeze.S > 1.05 && _r() < dt * 6 * q)
                if (TileInView(f, Field, 6) is { } tr)
                {
                    double x = tr.X + _r(), y = tr.Y + _r(), d = Rnd(.8, 1.4), l = Rnd(1.2, 2.2);
                    Gusts.Add(new Gust { X = x, Y = y, D = d, L = l });
                }
        }
        for (int i = Leaves.Count - 1; i >= 0; i--)
        {
            var fo = Leaves[i];
            fo.T += dt;
            fo.Alt -= dt * .32;
            fo.X += (Breeze.X * .9 + Math.Sin(fo.Ph + T * 3) * .35) * dt;
            fo.Y += Breeze.Y * .5 * dt;
            if (fo.Alt < -.8 || Leaves.Count > 80) Leaves.RemoveAt(i);
        }
        for (int i = Gusts.Count - 1; i >= 0; i--)
        {
            var ra = Gusts[i];
            ra.T += dt;
            ra.X += Breeze.X * 2.2 * dt;
            ra.Y += Breeze.Y * 2.2 * dt;
            if (ra.T > ra.D) Gusts.RemoveAt(i);
        }

        // chimney smoke
        _smokeT -= dt;
        if (_smokeT <= 0 && v.Zoom >= .35)
        {
            _smokeT = .12;
            double h = L.Hour;
            int slot = Floor(h * 2);
            double @base = h >= 5.5 && h < 9.5 ? .85 : h >= 16.5 && h < 23 ? .75 : h >= 23 || h < 5.5 ? .4 : .22;
            foreach (var ca in host.Buildings)
            {
                if (!f.Inside(ca.X + ca.W / 2.0, ca.Y, 1)) continue;
                uint sem = Hash(ca.X * 31 + ca.Y, slot);
                double cold = IsCold(Biome(ca.X, ca.Y)) ? .3 : 0;
                if ((sem % 100) / 100.0 > @base + cold) continue;
                if (_r() > .3) continue;
                double x = ca.X + ca.W * .72 + Rnd(-.05, .05), d = Rnd(2.6, 4), r0 = Rnd(.06, .1);
                double vx = Breeze.X * .35 + Rnd(-.05, .05), vy = -Rnd(.32, .45);
                Smokes.Add(new Smoke { X = x, Y = ca.Y + .25, D = d, R0 = r0, VX = vx, VY = vy });
            }
        }
        for (int i = Smokes.Count - 1; i >= 0; i--)
        {
            var fu = Smokes[i];
            fu.T += dt;
            fu.X += (fu.VX + Breeze.X * .15 * fu.T) * dt;
            fu.Y += fu.VY * dt;
            if (fu.T > fu.D) Smokes.RemoveAt(i);
        }
        if (Smokes.Count > 110 * q) Smokes.RemoveRange(0, Smokes.Count - (int)LegacyJsMath.Round(110 * q));

        // pigeons: peck near the streets and fly off when someone comes close
        var people = host.People;
        if (o.Fauna && v.Zoom >= .4 && L.Darkness < .6 && Rain.K < .4 && T > _nextPigeons)
        {
            _nextPigeons = T + Rnd(5, 11);
            int ground = Pigeons.Count(p => !p.Flying);
            if (ground < LegacyJsMath.Round(9 * q))
            {
                var tr2 = TileInView(f, (x, y) => Road(x, y) && (House(x + 1, y) || House(x - 1, y) || House(x, y + 1) || House(x, y - 1) || House(x + 2, y) || House(x, y + 2)), 120)
                    ?? TileInView(f, Road, 120);
                if (tr2 is { } t2)
                    for (int n = 3 + Floor(_r() * 3), k2 = 0; k2 < n; k2++)
                    {
                        double x = t2.X + .5 + Rnd(-.6, .6), y = t2.Y + .5 + Rnd(-.5, .5), t = Rnd(0, 1);
                        bool flip = _r() < .5;
                        Pigeons.Add(new Pigeon { X = x, Y = y, T = t, Flip = flip });
                    }
            }
        }
        for (int i = Pigeons.Count - 1; i >= 0; i--)
        {
            var po = Pigeons[i];
            po.T -= dt;
            if (!po.Flying)
            {
                if (po.T <= 0)
                {
                    po.T = Rnd(.3, 1.1);
                    po.Frame = po.Frame != 0 ? 0 : 1;
                    if (_r() < .25)
                    {
                        double nx = po.X + Rnd(-.3, .3), ny = po.Y + Rnd(-.2, .2);
                        if (!Water(nx, ny)) { po.Flip = nx < po.X; po.X = nx; po.Y = ny; }
                    }
                }
                (double X, double Y)? scare = null;
                foreach (var pv in people)
                    if (pv.Placed && Math.Abs(pv.X - po.X) < 1.3 && Math.Abs(pv.Y - po.Y) < 1) { scare = (pv.X, pv.Y); break; }
                if (scare is null && TheDog is { } dog && Math.Abs(dog.X - po.X) < 1.6 && Math.Abs(dog.Y - po.Y) < 1.2) scare = (dog.X, dog.Y);
                if (scare is null && TheFox is { } fox && Math.Abs(fox.X - po.X) < 2.5 && Math.Abs(fox.Y - po.Y) < 2) scare = (fox.X, fox.Y);
                if (scare is { } s)
                {
                    double ddx = po.X - s.X, ddy = po.Y - s.Y, dd = LegacyJsMath.Hypot(ddx, ddy);
                    if (dd == 0) dd = 1;
                    po.Flying = true;
                    po.VX = ddx / dd * Rnd(2.6, 3.6) + Rnd(-.5, .5);
                    po.VY = ddy / dd * Rnd(1.5, 2.5) + Rnd(-.5, .5);
                    po.T = Rnd(2.5, 3.5);
                    po.Flip = po.VX < 0;
                }
            }
            else
            {
                po.X += po.VX * dt;
                po.Y += po.VY * dt;
                po.Alt = Math.Min(2.4, po.Alt + dt * 1.6);
                po.Frame = (int)(Math.Floor(T * 10 + i) % 2);
                if (po.T <= 0 || !f.Inside(po.X, po.Y, 4)) Pigeons.RemoveAt(i);
            }
        }

        // the dog that follows a villager
        UpdateDog(dt, v, people);

        // fox: crosses the fields; sheep, cows and pigeons move away
        if (TheFox is { } rf)
        {
            rf.X += rf.Dir * 1.35 * dt;
            rf.Frame = (int)(Math.Floor(T * 6) % 2);
            rf.Y += Math.Sin(T * .8) * .1 * dt;
            foreach (var ov in fa)
            {
                if (ov.Kind != "sheep" && ov.Kind != "cow") continue;
                double dx2 = ov.X - rf.X, dy2 = ov.Y - rf.Y, d2 = LegacyJsMath.Hypot(dx2, dy2);
                if (d2 < 3.2 && d2 > 0)
                {
                    double ax = ov.X + dx2 / d2 * 3, ay = ov.Y + dy2 / d2 * 2;
                    if (!Water(ax, ay)) { ov.TX = ax; ov.TY = ay; ov.State = "walk"; }
                }
            }
            if ((rf.Dir > 0 && rf.X > f.X1 + 3) || (rf.Dir < 0 && rf.X < f.X0 - 3) || T > rf.End) TheFox = null;
        }
        // deer move away from passers-by
        foreach (var ce in fa)
        {
            if (ce.Kind != "deer" || ce.State == "walk") continue;
            foreach (var pw in people)
            {
                if (!pw.Placed) continue;
                double dx3 = ce.X - pw.X, dy3 = ce.Y - pw.Y, d3 = LegacyJsMath.Hypot(dx3, dy3);
                if (d3 < 1.9 && d3 > 0)
                {
                    double bx = ce.X + dx3 / d3 * 3.2, by = ce.Y + dy3 / d3 * 2;
                    if (!Water(bx, by) && !Road(bx, by)) { ce.TX = bx; ce.TY = by; ce.State = "walk"; }
                    break;
                }
            }
        }

        // hot-air balloon
        if (Balloon is { } bal)
        {
            bal.X += bal.VX * dt;
            bal.Y += bal.VY * dt;
            if (T > bal.End) Balloon = null;
        }
        // sailing boat
        if (SailBoat is { } bt)
        {
            double vb = .55 * dt, nx2 = bt.X + bt.DX * vb, ny2 = bt.Y + bt.DY * vb;
            if (!Water(bt.X + bt.DX * .9, bt.Y + bt.DY * .9))
            {
                var opts = Dirs4.Where(d => !(d.X == -bt.DX && d.Y == -bt.DY) && Water(bt.X + d.X * 1.4, bt.Y + d.Y * 1.4)).ToList();
                if (opts.Count > 0) { var d = Pick(opts); bt.DX = d.X; bt.DY = d.Y; }
                else { bt.DX = -bt.DX; bt.DY = -bt.DY; }
            }
            else { bt.X = nx2; bt.Y = ny2; }
            bt.Wake -= dt;
            if (bt.Wake <= 0)
            {
                bt.Wake = .3;
                Ripples.Add(new Ripple { X = bt.X - bt.DX * .7, Y = bt.Y - bt.DY * .5 + .15, D = 1.4, R = .5 });
            }
            if (T > bt.End || !f.Inside(bt.X, bt.Y, 12)) SailBoat = null;
        }

        // party: balloons and confetti by day, fireworks at night
        if (TheParty is { } fe && T > fe.Next)
        {
            if (fe.Night)
            {
                fe.Next = T + Rnd(.6, 1.5);
                double lx2 = f.X1 - f.X0, ly2 = f.Y1 - f.Y0;
                double fx = Clamp(fe.X + Rnd(-2.5, 2.5), f.X0 + lx2 * .12, f.X1 - lx2 * .12);
                double fy = Clamp(fe.Y + Rnd(-1.5, 1.5), f.Y0 + ly2 * .35, f.Y1 - ly2 * .1);
                double ground = v.P(fx, fy).Y, maxAlt = Math.Max(2.2, (ground - v.Height * .14) / v.T);
                double valt = Rnd(7.5, 9.5), top = Math.Min(Rnd(5, 8.5), maxAlt);
                var cor = Pick(PartyColors);
                var kind = Pick(FireworkKinds);
                Rockets.Add(new Rocket { X = fx, Y = fy, VAlt = valt, Top = top, Color = cor, Kind = kind });
            }
            else
            {
                fe.Next = T + Rnd(.25, .6);
                if (PartyBalloons.Count < 26)
                {
                    double x = fe.X + Rnd(-1.8, 1.8), y = fe.Y + Rnd(-1, 1), alt = Rnd(0, .4), vy = Rnd(.55, .95);
                    var cor = Pick(PartyColors[..6]);
                    double ph = _r() * 9;
                    PartyBalloons.Add(new PartyBalloon { X = x, Y = y, Alt = alt, VY = vy, Color = cor, Ph = ph });
                }
                if (_r() < .25)
                    for (int cf = 0; cf < LegacyJsMath.Round(26 * q); cf++)
                    {
                        double x = fe.X + Rnd(-.5, .5), y = fe.Y + Rnd(-.4, .4), alt = Rnd(.5, 1), vx = Rnd(-1.6, 1.6), vy = Rnd(-1, 1), valt = Rnd(2, 4);
                        var cor = Pick(PartyColors);
                        double ph = _r() * 9;
                        Confettis.Add(new Confetti { X = x, Y = y, Alt = alt, VX = vx, VY = vy, VAlt = valt, Color = cor, Ph = ph });
                    }
            }
        }
        for (int i = PartyBalloons.Count - 1; i >= 0; i--)
        {
            var bl = PartyBalloons[i];
            bl.Alt += bl.VY * dt;
            bl.Ph += dt;
            bl.X += (Math.Sin(bl.Ph * 1.3) * .25 + Breeze.X * .35) * dt;
            if (bl.Alt > 11) PartyBalloons.RemoveAt(i);
        }
        for (int i = Confettis.Count - 1; i >= 0; i--)
        {
            var co = Confettis[i];
            co.T += dt;
            co.VAlt -= 4.5 * dt;
            co.VAlt = Math.Max(co.VAlt, -.7);
            co.Alt += co.VAlt * dt;
            co.X += (co.VX + Math.Sin(co.Ph + T * 5) * .3) * dt;
            co.Y += co.VY * dt * .4;
            co.VX *= .98;
            if (co.Alt < 0 || co.T > 4) Confettis.RemoveAt(i);
        }
        for (int i = Rockets.Count - 1; i >= 0; i--)
        {
            var fg = Rockets[i];
            fg.Alt += fg.VAlt * dt;
            fg.Trail.Add((fg.X, fg.Y, fg.Alt));
            if (fg.Trail.Count > 8) fg.Trail.RemoveAt(0);
            if (fg.Alt >= fg.Top) { Explode(fg); Rockets.RemoveAt(i); }
        }
        for (int i = Sparks.Count - 1; i >= 0; i--)
        {
            var fs = Sparks[i];
            fs.T += dt;
            fs.VAlt -= (fs.Heavy ? 2.6 : 1.5) * dt;
            fs.VX *= .985;
            fs.VY *= .985;
            fs.X += fs.VX * dt;
            fs.Y += fs.VY * dt;
            fs.Alt += fs.VAlt * dt;
            if (fs.T > fs.D) Sparks.RemoveAt(i);
        }
        for (int i = Flashes.Count - 1; i >= 0; i--)
        {
            Flashes[i].T += dt;
            if (Flashes[i].T > .45) Flashes.RemoveAt(i);
        }

        // shooting stars (on the screen)
        if (L.Darkness > .7 && o.Events && (T > _nextStar || (Meteors != 0 && _r() < dt * 2.5)))
        {
            _nextStar = T + Rnd(18, 55);
            bool dl2 = _r() < .5;
            double x = Rnd(W * .1, W * .9), y = Rnd(0, Hh * .45), vx = (dl2 ? -1 : 1) * Rnd(W * .6, W * 1.1), vy = Rnd(Hh * .2, Hh * .4), d = Rnd(.6, 1.1);
            Stars.Add(new ShootingStar { X = x, Y = y, VX = vx, VY = vy, D = d });
        }
        for (int i = Stars.Count - 1; i >= 0; i--)
        {
            Stars[i].T += dt;
            if (Stars[i].T > Stars[i].D) Stars.RemoveAt(i);
        }

        // villagers: a whistle here, a heart there
        if (o.People && v.Zoom >= .45 && T > _nextEmote)
        {
            _nextEmote = T + Rnd(5, 11);
            var vis = people.Where(a => a.Placed && f.Inside(a.X, a.Y, -1) && !a.Indoors).ToList();
            if (vis.Count > 0)
            {
                var chosen = Pick(vis);
                string c = night ? Pick(new[] { "z", "♪", "…" }) : Rain.K > .3 ? Pick(new[] { "!", "☂" }) : Pick(new[] { "♪", "♥", "♪", "?", "!", "☀" });
                Emotes[chosen] = new Emote(c, T + Rnd(2.2, 3.5));
            }
        }
        foreach (var a in Emotes.Where(e => T > e.Value.End).Select(e => e.Key).ToList()) Emotes.Remove(a);
    }

    private void Explode(Rocket fg)
    {
        double q = Quality;
        int n = (int)LegacyJsMath.Round((fg.Kind == "salgueiro" ? 46 : 60) * (q * .6 + .4));
        var cor2 = Pick(PartyColors);
        Flashes.Add(new Flash { X = fg.X, Y = fg.Y, Alt = fg.Alt, Color = fg.Color });
        for (int i = 0; i < n; i++)
        {
            double a = (double)i / n * Math.PI * 2 + Rnd(-.05, .05);
            double sp = fg.Kind == "anel" ? 3.2 : Rnd(1.2, 3.6);
            double elev = fg.Kind == "anel" ? 0 : Rnd(-1, 1);
            double d = fg.Kind == "salgueiro" ? Rnd(2, 2.8) : Rnd(1.1, 1.8);
            Sparks.Add(new Spark
            {
                X = fg.X + Math.Cos(a) * .15, Y = fg.Y + Math.Sin(a) * .07, Alt = fg.Alt + Math.Sin(a) * .1,
                VX = Math.Cos(a) * sp, VY = Math.Sin(a) * sp * .45, VAlt = Math.Sin(a) * sp * .55 + elev,
                Color = i % 3 == 0 && fg.Kind != "salgueiro" ? cor2 : fg.Color, D = d, Heavy = fg.Kind == "salgueiro"
            });
        }
    }

    private void UpdateDog(double dt, LegacyLifeView v, IReadOnlyList<ILegacyLifePerson> people)
    {
        if (!Options.People || v.Zoom < .4) { TheDog = null; return; }
        var dog = TheDog;
        if (dog is null || dog.Owner is null || Time > dog.Swap && !dog.Owner.Indoors && _r() < .02)
        {
            var cand = people.Where(a => a.Wanderer && a.Placed && v.Inside(a.X, a.Y, -1) && !a.Indoors).ToList();
            if (cand.Count == 0) { TheDog = null; return; }
            var d0 = Pick(cand);
            dog ??= new Dog { X = d0.X - .8, Y = d0.Y + .2 };
            dog.Owner = d0;
            dog.Swap = Time + Rnd(40, 80);
            TheDog = dog;
        }
        var a = dog.Owner!;
        if (!a.Placed) { TheDog = null; return; }
        double tx = a.X - a.DX * .75 - .25, ty = a.Y - a.DY * .55 + .18, dx = tx - dog.X, dy = ty - dog.Y, d = LegacyJsMath.Hypot(dx, dy);
        if (a.Indoors) dog.Sitting = true;
        else if (d > .28)
        {
            double vel = Math.Min(d * 2.4, 2.6) * dt;
            dog.X += dx / d * vel;
            dog.Y += dy / d * vel;
            dog.Sitting = false;
            if (Math.Abs(dx) > .05) dog.Flip = dx < 0;
            dog.Frame = (int)(Math.Floor(Time * 8) % 2);
        }
        else dog.Sitting = a.Pause > 0;
        if (!v.Inside(dog.X, dog.Y, 6)) TheDog = null;
    }

    // ---------------------------------------------------------------- street lamps
    /// <summary>listaLampioes(): one lamp on some road tiles, on a free side.</summary>
    public IReadOnlyList<Lamp> Lamps()
    {
        var host = _host;
        if (host is null) return [];
        if (_lamps is not null && _lampKey == host.Roads.Count) return _lamps;
        _lampKey = host.Roads.Count;
        _lamps = [];
        foreach (var (x, y) in host.Roads)
        {
            if (Hash(x, y) % 7 != 0) continue;
            for (int j = 0; j < 4; j++)
            {
                var d = Dirs4[(int)((Hash(y, x) + (uint)j) % 4)];
                int nx = x + d.X, ny = y + d.Y;
                if (host.Road(nx, ny) || host.Water(nx, ny) || host.House(nx, ny)) continue;
                _lamps.Add(new Lamp(x + .5 + d.X * .62, y + .5 + d.Y * .62, Hash(x, y) % 100));
                break;
            }
        }
        return _lamps;
    }

    // ---------------------------------------------------------------- particle types
    public sealed class Wind { public double A, S, X, Y; }
    public sealed class Weather { public double K, Target, End; public bool Snow; }
    public sealed class Cloud { public double X, Y, R; public int V; }
    public sealed class Drop { public double X, Y, V, L, Ph; }
    public sealed class Splash { public double X, Y, T; }
    public sealed class Ripple { public double X, Y, T, D, R; }
    public sealed class Sparkle { public double X, Y, T, D; }
    public sealed class Fish { public double X, Y, T, D, Alt; public int Dir; }
    public sealed class Butterfly { public double CX, CY, X, Y, Ph, Life, T, Alt; public string Color = ""; }
    public sealed class Firefly { public double X, Y, VX, VY, Ph, Alt, Life, T; }
    public sealed class Smoke { public double X, Y, T, D, R0, VX, VY; }
    public sealed class Pigeon { public double X, Y, T, Alt, VX, VY; public bool Flying, Flip; public int Frame; }
    public sealed class Leaf { public double X, Y, Alt, T, Ph; public string Color = ""; }
    public sealed class Gust { public double X, Y, T, D, L; }
    public sealed class PartyBalloon { public double X, Y, Alt, VY, Ph; public string Color = ""; }
    public sealed class Confetti { public double X, Y, Alt, VX, VY, VAlt, T, Ph; public string Color = ""; }
    public sealed class Rocket { public double X, Y, Alt, VAlt, Top; public string Color = "", Kind = ""; public List<(double X, double Y, double Alt)> Trail { get; } = []; }
    public sealed class Spark { public double X, Y, Alt, VX, VY, VAlt, T, D; public string Color = ""; public bool Heavy; }
    public sealed class Flash { public double X, Y, Alt, T; public string Color = ""; }
    public sealed class ShootingStar { public double X, Y, VX, VY, T, D; }
    public sealed class HotAirBalloon { public double X, Y, VX, VY, Alt, End; public string[] Colors = []; }
    public sealed class Boat { public double X, Y, End, Wake; public int DX, DY, Frame; }
    public sealed class Fox { public double X, Y, End; public int Dir, Frame; }
    public sealed class Dog { public double X, Y, Swap; public int Frame; public bool Flip, Sitting; public ILegacyLifePerson? Owner; }
    public sealed class Party { public double X, Y, End, Next; public string Name = ""; public bool Night; }
    public readonly record struct Rainbow(double T0, double End, double CX);
    public readonly record struct Emote(string Text, double End);
    public readonly record struct Lamp(double X, double Y, uint Ph);
}
