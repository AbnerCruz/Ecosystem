namespace Urbe.Core;

/// <summary>An animal of app.js urbeFauna (sheep, cow, deer, duck or bird).</summary>
public sealed class LegacyAnimal
{
    public string Kind { get; init; } = "";
    /// <summary>classe(): agua, campo or mata (null for birds).</summary>
    public string? Class { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double TX { get; set; }
    public double TY { get; set; }
    /// <summary>idle, graze or walk.</summary>
    public string State { get; set; } = "idle";
    public double T { get; set; }
    public int Frame { get; set; }
    public double FrameTime { get; set; }
    public bool Flip { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Alt { get; set; }
}

/// <summary>
/// app.js fauna (1.8.4-beta, 'fauna: ovelhas, vacas, cervos, patos e pássaros'): animals
/// only around what is on screen, only on generated chunks, away from roads, houses and
/// neighbourhoods. Ported line by line; <see cref="Tick"/> is the scheduler's ciclo().
/// </summary>
public sealed class LegacyFauna
{
    private const int Max = 18;
    private readonly Func<double> _random;
    private double _lastMs, _spawnMs, _birdsMs;

    public LegacyFauna(Func<double>? random = null) => _random = random ?? Random.Shared.NextDouble;

    public List<LegacyAnimal> Animals { get; private set; } = [];

    private static double Speed(string kind) => kind switch { "sheep" => .5, "cow" => .38, "deer" => .9, "duck" => .32, _ => 0 };

    private static string? Class(ILegacyLifeHost host, int x, int y)
    {
        int b = host.Biome(x, y);
        if (b < 0) return null;
        return (LegacyBiome)b switch
        {
            LegacyBiome.Lake or LegacyBiome.River => "agua",
            LegacyBiome.Grass or LegacyBiome.Meadow or LegacyBiome.Steppe or LegacyBiome.Savanna or LegacyBiome.Hills => "campo",
            LegacyBiome.Forest or LegacyBiome.Dense or LegacyBiome.Taiga => "mata",
            _ => null
        };
    }

    private static bool Free(ILegacyLifeHost host, double fx, double fy)
    {
        int x = (int)Math.Floor(fx), y = (int)Math.Floor(fy);
        if (host.Road(x, y)) return false;
        return !host.House(x, y) && !host.District(x, y);
    }

    private static string? ClassAt(ILegacyLifeHost host, double x, double y) =>
        Class(host, (int)Math.Floor(x), (int)Math.Floor(y));

    /// <summary>ciclo(): <paramref name="nowMs"/> is performance.now().</summary>
    /// <returns>True when there are animals (the original then asks for a frame).</returns>
    public bool Tick(double nowMs, LegacyLifeView view, bool enabled, ILegacyLifeHost host)
    {
        double dt = Math.Min(.25, (nowMs - (_lastMs != 0 ? _lastMs : nowMs)) / 1000);
        _lastMs = nowMs;
        if (view.Zoom < .3 || !enabled)
        {
            Animals.Clear();
            return false;
        }
        if (nowMs - _spawnMs > 900)
        {
            _spawnMs = nowMs;
            int ground = 0;
            foreach (var a in Animals) if (a.Kind != "bird") ground++;
            if (ground < Max) Spawn(view, host);
            if (nowMs - _birdsMs > 14000 && _random() < .25)
            {
                _birdsMs = nowMs;
                Birds(view);
            }
        }
        if (Animals.Count == 0) return false;
        Step(dt, view, host);
        return true;
    }

    private void Spawn(LegacyLifeView f, ILegacyLifeHost host)
    {
        double w = f.X1 - f.X0, h = f.Y1 - f.Y0;
        for (int tent = 0; tent < 14; tent++)
        {
            double x = f.X0 - 3 + _random() * (w + 6), y = f.Y0 - 3 + _random() * (h + 6);
            var c = ClassAt(host, x, y);
            if (c is null || !Free(host, x, y)) continue;
            string? kind = c == "agua" ? "duck" : c == "mata" ? (_random() < .45 ? "deer" : null) : (_random() < .72 ? "sheep" : "cow");
            if (kind is null) continue;
            int n = kind == "deer" ? 1 + (_random() < .4 ? 1 : 0) : kind == "duck" ? 2 + (int)Math.Floor(_random() * 2) : 2 + (int)Math.Floor(_random() * 3);
            for (int i = 0; i < n; i++)
            {
                double ax = x + (_random() - .5) * 2.4, ay = y + (_random() - .5) * 1.8;
                if (ClassAt(host, ax, ay) != c || !Free(host, ax, ay)) continue;
                Animals.Add(new LegacyAnimal
                {
                    Kind = kind, Class = c, X = ax, Y = ay, TX = ax, TY = ay, State = "idle",
                    T = _random() * 3, Frame = 0, FrameTime = 0, Flip = _random() < .5
                });
            }
            return;
        }
    }

    private void Birds(LegacyLifeView f)
    {
        int n = 3 + (int)Math.Floor(_random() * 3);
        bool fromLeft = _random() < .5;
        double y = f.Y0 + _random() * (f.Y1 - f.Y0), x = fromLeft ? f.X0 - 4 : f.X1 + 4;
        double vx = (fromLeft ? 1 : -1) * (3 + _random() * 1.5), vy = (_random() - .5) * 1.2;
        for (int i = 0; i < n; i++)
            Animals.Add(new LegacyAnimal
            {
                Kind = "bird", X = x - (fromLeft ? 1 : -1) * i * .9, Y = y + (i % 2 != 0 ? .6 : -.6) * Math.Ceiling(i / 2.0),
                VX = vx, VY = vy, Frame = i % 2, FrameTime = 0, Flip = !fromLeft, Alt = 1.6 + _random() * .8
            });
    }

    private void Step(double dt, LegacyLifeView f, ILegacyLifeHost host)
    {
        double cx = (f.X0 + f.X1) / 2.0, cy = (f.Y0 + f.Y1) / 2.0, lim = Math.Max(f.X1 - f.X0, f.Y1 - f.Y0) * 1.2 + 8;
        Animals = Animals.Where(a => Math.Abs(a.X - cx) < lim && Math.Abs(a.Y - cy) < lim).ToList();
        foreach (var a in Animals)
        {
            a.FrameTime += dt;
            if (a.Kind == "bird")
            {
                a.X += a.VX * dt;
                a.Y += a.VY * dt;
                if (a.FrameTime > .16) { a.FrameTime = 0; a.Frame ^= 1; }
                continue;
            }
            if (a.State == "walk")
            {
                double dx = a.TX - a.X, dy = a.TY - a.Y, d = LegacyJsMath.Hypot(dx, dy), v = Speed(a.Kind) * dt;
                if (d <= v)
                {
                    a.X = a.TX;
                    a.Y = a.TY;
                    a.State = _random() < .6 && a.Kind != "duck" ? "graze" : "idle";
                    a.T = 1.5 + _random() * 4;
                }
                else
                {
                    a.X += dx / d * v;
                    a.Y += dy / d * v;
                    a.Flip = dx < 0;
                }
                if (a.FrameTime > .22) { a.FrameTime = 0; a.Frame = a.Frame == 0 ? 1 : 0; }
            }
            else
            {
                a.T -= dt;
                if (a.State == "graze") a.Frame = 2;
                else if (a.Kind == "duck") { if (a.FrameTime > .6) { a.FrameTime = 0; a.Frame ^= 1; } }
                else a.Frame = 0;
                if (a.T <= 0)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        double nx = a.X + (_random() - .5) * 6, ny = a.Y + (_random() - .5) * 4;
                        if (ClassAt(host, nx, ny) == a.Class && Free(host, nx, ny))
                        {
                            a.TX = nx;
                            a.TY = ny;
                            a.State = "walk";
                            break;
                        }
                    }
                    if (a.State != "walk") a.T = 1 + _random() * 2;
                }
            }
        }
    }
}
