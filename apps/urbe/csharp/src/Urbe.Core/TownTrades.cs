namespace Urbe.Core;

/// <summary>
/// Residents with trades (owner, DEC-0045, UC-42: "buscar água, cortar lenha, cuidar da roça"): for every well, field
/// and woodpile in sight, someone from the nearest house leaves the door, walks across the land to it, works there for
/// a while and goes back home, then rests indoors before going out again. Not part of the 1.8.4's town (v25Povo,
/// <see cref="LegacyVillagers"/>, whose oracle stays as it was): the decorations only exist in the native app
/// (.urbe/mundo.json). The workers are <see cref="LegacyVillager"/>s of kind "oficio", drawn with the 1.8.4's villager
/// sprite and carrying their trade's tool.
/// </summary>
public sealed class TownTrades
{
    /// <summary>The trades and where they happen: decoration kind → (tool carried, what is done there).</summary>
    public static IReadOnlyDictionary<string, (string Tool, string Work)> Trades { get; } = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["poco"] = ("bucket", "agua"),
        ["roca"] = ("basket", "roca"),
        ["lenha"] = ("sack", "lenha"),
    };

    /// <summary>How far (in tiles) a house may be from the decoration for its resident to take the trade.</summary>
    public const int Reach = 16;
    /// <summary>The longest walk (in tiles) searched between the door and the work spot.</summary>
    private const int MaxPath = 900;

    public enum Phase { Home, Going, Working, Returning }

    public sealed class Worker
    {
        public required WorldDecoration Place { get; init; }
        public required string Work { get; init; }
        public required LegacyVillager Person { get; init; }
        public required List<(double X, double Y)> Path { get; init; }
        public Phase Phase { get; set; }
        /// <summary>Seconds left in the current phase (home and working).</summary>
        public double Left { get; set; }
        /// <summary>The position along <see cref="Path"/> (index plus fraction).</summary>
        public double S { get; set; }
    }

    private readonly Func<double> _r;
    private readonly Dictionary<string, Worker> _workers = new(StringComparer.Ordinal);

    public TownTrades(Func<double>? random = null) => _r = random ?? Random.Shared.NextDouble;

    /// <summary>Everyone at a trade now (indoors ones too).</summary>
    public IReadOnlyCollection<Worker> Workers => _workers.Values;

    /// <summary>The workers outside, as villagers to draw.</summary>
    public List<LegacyVillager> People { get; private set; } = [];

    public void Reset()
    {
        _workers.Clear();
        People = [];
    }

    /// <summary>One step of <paramref name="dt"/> seconds; workers appear for the trade decorations in view and leave with them.</summary>
    public bool Tick(double dt, LegacyLifeView view, bool enabled, double hurry, IReadOnlyList<WorldDecoration> decorations, ILegacyTownMap map)
    {
        if (!enabled) { Reset(); return false; }
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in decorations)
        {
            if (!Trades.ContainsKey(d.Kind) || WorldDecorations.Find(d.Kind) is not { } k) continue;
            if (d.X + k.W < view.X0 - 4 || d.X > view.X1 + 4 || d.Y + k.H < view.Y0 - 4 || d.Y > view.Y1 + 4) continue;
            wanted.Add(d.Id);
            if (_workers.TryGetValue(d.Id, out var w) && w.Place == d && map.Buildings.Contains(w.Person.From)) continue;
            _workers.Remove(d.Id);
            if (Hire(d, k, map) is { } hired) _workers[d.Id] = hired;
        }
        foreach (var id in _workers.Keys.Where(id => !wanted.Contains(id)).ToList()) _workers.Remove(id);
        foreach (var w in _workers.Values) Step(w, dt, hurry);
        People = _workers.Values.Where(w => w.Phase != Phase.Home).Select(w => w.Person).ToList();
        return People.Count > 0;
    }

    private Worker? Hire(WorldDecoration d, WorldDecorations.Kind k, ILegacyTownMap map)
    {
        var (tool, work) = Trades[d.Kind];
        // the work spot: the free tile just below the decoration (or beside it)
        (int X, int Y)? spot = null;
        foreach (var c in new[] { (d.X, d.Y + k.H), (d.X + k.W, d.Y), (d.X - 1, d.Y), (d.X, d.Y - 1) })
            if (Walkable(c.Item1, c.Item2, map)) { spot = c; break; }
        if (spot is not { } s) return null;
        LegacyCityBuilding? home = null;
        double best = double.MaxValue;
        foreach (var b in map.Buildings)
        {
            if (!b.IsNote) continue;
            double dist = Math.Abs(b.X + b.W / 2.0 - s.X) + Math.Abs(b.Y + b.H - s.Y);
            if (dist <= Reach && dist < best) { best = dist; home = b; }
        }
        if (home is null) return null;
        var door = ((int)LegacyJsMath.Round(home.X + (home.W - 1) / 2.0), home.Y + home.H);
        if (FindPath(door, s, map) is not { } tiles) return null;
        var look = LegacyVillagers.Look(home, "oficio:" + d.Kind) with { Acc = tool };
        var path = tiles.Select(t => (t.X + .5, t.Y + .62)).ToList();
        var person = new LegacyVillager
        {
            Kind = "oficio", From = home, Speed = LegacyVillagers.Speed * (.7 + _r() * .3), Look = look,
            PosX = path[0].Item1, PosY = path[0].Item2, DirX = 0, DirY = 1
        };
        // the first one goes out soon, so the trade is seen
        return new Worker { Place = d, Work = work, Person = person, Path = path, Phase = Phase.Home, Left = .5 + _r() * 2.5 };
    }

    private static bool Walkable(int x, int y, ILegacyTownMap map) => map.BuildingAt(x, y) is null && !map.Water(x, y);

    /// <summary>A walk over land from the door to the spot (BFS, four directions, around houses and water).</summary>
    private static List<(int X, int Y)>? FindPath((int X, int Y) from, (int X, int Y) to, ILegacyTownMap map)
    {
        var came = new Dictionary<(int, int), (int, int)?> { [from] = null };
        var queue = new List<(int X, int Y)> { from };
        for (int head = 0; head < queue.Count && queue.Count < MaxPath * 4; head++)
        {
            var k = queue[head];
            if (k == to) break;
            foreach (var n in new[] { (k.X + 1, k.Y), (k.X - 1, k.Y), (k.X, k.Y + 1), (k.X, k.Y - 1) })
                if (!came.ContainsKey(n) && Math.Abs(n.Item1 - from.X) + Math.Abs(n.Item2 - from.Y) <= Reach * 2 && (n == to || Walkable(n.Item1, n.Item2, map)))
                {
                    came[n] = k;
                    queue.Add(n);
                }
        }
        if (!came.ContainsKey(to)) return null;
        var path = new List<(int X, int Y)>();
        for ((int, int)? cur = to; cur is { } c; cur = came[c]) path.Add(c);
        path.Reverse();
        return path;
    }

    private void Step(Worker w, double dt, double hurry)
    {
        var a = w.Person;
        switch (w.Phase)
        {
            case Phase.Home:
                w.Left -= dt;
                if (w.Left > 0) return;
                w.Phase = Phase.Going;
                w.S = 0;
                Place(w, 1);
                return;
            case Phase.Working:
                w.Left -= dt;
                a.PauseTime = Math.Max(.01, w.Left);
                a.Step += dt * 3;   // the work's rhythm (drawn by the renderer)
                if (w.Left > 0) return;
                a.PauseTime = 0;
                w.Phase = Phase.Returning;
                return;
        }
        int n = w.Path.Count - 1;
        double av = a.Speed * dt * hurry;
        a.Step += av * 4;
        if (w.Phase == Phase.Going)
        {
            w.S = Math.Min(n, w.S + av);
            Place(w, 1);
            if (w.S >= n)
            {
                w.Phase = Phase.Working;
                w.Left = 5 + _r() * 5;
                a.DirX = 0;
                a.DirY = -1;   // facing the work
            }
        }
        else
        {
            w.S = Math.Max(0, w.S - av);
            Place(w, -1);
            if (w.S <= 0)
            {
                w.Phase = Phase.Home;
                w.Left = 8 + _r() * 14;
            }
        }
    }

    private static void Place(Worker w, int sense)
    {
        var a = w.Person;
        int n = w.Path.Count - 1;
        if (n <= 0) { (a.PosX, a.PosY) = w.Path[0]; return; }
        int i = Math.Min(n - 1, (int)Math.Floor(w.S));
        double f = w.S - i;
        var (x0, y0) = w.Path[i];
        var (x1, y1) = w.Path[i + 1];
        a.PosX = x0 + (x1 - x0) * f;
        a.PosY = y0 + (y1 - y0) * f;
        double dx = (x1 - x0) * sense, dy = (y1 - y0) * sense;
        if (dx != 0 || dy != 0) { a.DirX = Math.Sign(dx); a.DirY = dx != 0 ? 0 : Math.Sign(dy); }
    }
}
