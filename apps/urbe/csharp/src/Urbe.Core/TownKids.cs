namespace Urbe.Core;

/// <summary>
/// Children of the city (owner, UC-52: "adicionar crianças ao mundo"): some houses in view have a child — or two — who
/// play out by the door: they run to a nearby spot, stop, run again, and sometimes run to each other. They never cross
/// water or houses and stay close to home. Drawn as smaller villagers. Not in the 1.8.4: its oracle is untouched.
/// </summary>
public sealed class TownKids
{
    /// <summary>How far (in tiles) from the door a child runs.</summary>
    public const int Reach = 3;
    /// <summary>The most children out at once in view.</summary>
    public const int Max = 10;
    public const double Size = .62;

    public sealed class Kid
    {
        public required LegacyCityBuilding Home { get; init; }
        public required LegacyVillager Person { get; init; }
        public required (int X, int Y) Door { get; init; }
        public (double X, double Y)? Target { get; set; }
        public double Wait { get; set; }
    }

    private readonly Func<double> _r;
    private readonly Dictionary<string, Kid> _kids = new(StringComparer.Ordinal);

    public TownKids(Func<double>? random = null) => _r = random ?? Random.Shared.NextDouble;

    public IReadOnlyCollection<Kid> Kids => _kids.Values;

    /// <summary>The children, as villagers to draw.</summary>
    public List<LegacyVillager> People { get; private set; } = [];

    public void Reset()
    {
        _kids.Clear();
        People = [];
    }

    /// <summary>Whether a house has children: about one house in four, always the same ones (by the house's id).</summary>
    public static int ChildrenOf(LegacyCityBuilding house)
    {
        uint h = 2166136261;
        foreach (var ch in house.Id) h = (h ^ ch) * 16777619;
        return (h % 8) switch { 0 => 2, 1 => 1, _ => 0 };
    }

    /// <summary>One step of <paramref name="dt"/> seconds for the houses in view; children of houses out of view go home.</summary>
    public bool Tick(double dt, LegacyLifeView view, bool enabled, ILegacyTownMap map)
    {
        if (!enabled) { Reset(); return false; }
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in map.Buildings)
        {
            if (wanted.Count >= Max) break;
            if (!b.IsNote || b.X + b.W < view.X0 - 2 || b.X > view.X1 + 2 || b.Y + b.H < view.Y0 - 2 || b.Y > view.Y1 + 3) continue;
            int n = ChildrenOf(b);
            for (int i = 0; i < n && wanted.Count < Max; i++)
            {
                var key = b.Id + "#" + i;
                wanted.Add(key);
                if (_kids.TryGetValue(key, out var k) && k.Home == b) continue;
                var door = ((int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0), b.Y + b.H);
                if (!Free(door.Item1, door.Item2, map)) { wanted.Remove(key); continue; }
                var person = new LegacyVillager
                {
                    Kind = "crianca", From = b, Speed = LegacyVillagers.Speed * (1.5 + _r() * .5), Scale = Size,
                    Look = LegacyVillagers.Look(b, "crianca:" + i), PosX = door.Item1 + .5 + (i == 0 ? -.3 : .3), PosY = door.Item2 + .62, DirY = 1
                };
                _kids[key] = new Kid { Home = b, Person = person, Door = door, Wait = _r() * 1.5 };
            }
        }
        foreach (var id in _kids.Keys.Where(id => !wanted.Contains(id)).ToList()) _kids.Remove(id);
        foreach (var k in _kids.Values) Step(k, dt, map);
        People = _kids.Values.Select(k => k.Person).ToList();
        return People.Count > 0;
    }

    private static bool Free(int x, int y, ILegacyTownMap map) => map.BuildingAt(x, y) is null && !map.Water(x, y);

    private void Step(Kid k, double dt, ILegacyTownMap map)
    {
        var a = k.Person;
        if (k.Target is not { } t)
        {
            k.Wait -= dt;
            a.PauseTime = Math.Max(0, k.Wait);
            if (k.Wait > 0) return;
            k.Target = Pick(k, map);
            if (k.Target is null) { k.Wait = 1; return; }
            t = k.Target.Value;
        }
        double dx = t.X - a.PosX, dy = t.Y - a.PosY, d = Math.Sqrt(dx * dx + dy * dy), step = a.Speed * dt * 1.4;
        a.PauseTime = 0;
        a.Step += step * 5;
        if (Math.Abs(dx) > Math.Abs(dy)) { a.DirX = Math.Sign(dx); a.DirY = 0; } else { a.DirX = 0; a.DirY = Math.Sign(dy); }
        if (d <= step)
        {
            (a.PosX, a.PosY) = t;
            k.Target = null;
            k.Wait = .4 + _r() * 2.2;
            a.PauseTime = k.Wait;
            return;
        }
        double nx = a.PosX + dx / d * step, ny = a.PosY + dy / d * step;
        if (!Free((int)Math.Floor(nx), (int)Math.Floor(ny), map)) { k.Target = null; k.Wait = .3; return; }
        a.PosX = nx;
        a.PosY = ny;
    }

    /// <summary>Where to run: now and then to another child of the same house, else a free tile near the door.</summary>
    private (double X, double Y)? Pick(Kid k, ILegacyTownMap map)
    {
        if (_r() < .3 && _kids.Values.FirstOrDefault(o => o != k && o.Home == k.Home) is { } other)
            return (other.Person.PosX + (_r() - .5) * .6, other.Person.PosY + (_r() - .5) * .4);
        for (int tries = 0; tries < 8; tries++)
        {
            int x = k.Door.X + (int)Math.Round((_r() * 2 - 1) * Reach), y = k.Door.Y + (int)Math.Round(_r() * Reach);
            if (Free(x, y, map)) return (x + .2 + _r() * .6, y + .4 + _r() * .4);
        }
        return null;
    }
}
