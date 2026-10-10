namespace Urbe.Core;

/// <summary>What the villagers of app.js v25 see of the city.</summary>
public interface ILegacyTownMap
{
    /// <summary>world.buildings (every 1.8.4 document house is tipo 'nota').</summary>
    IReadOnlyList<LegacyCityBuilding> Buildings { get; }
    /// <summary>world.links between building ids, in order.</summary>
    IReadOnlyList<(string From, string To)> Links { get; }
    bool Road(int x, int y);
    /// <summary>MUNDO.isWater.</summary>
    bool Water(int x, int y);
    LegacyCityBuilding? BuildingAt(int x, int y);
}

/// <summary>pixel-art.js villager look: {skin,hair,pants,style,shirt,dress,hood?,cap?,acc}.</summary>
public sealed record LegacyVillagerLook(int Skin, int Hair, int Pants, string Style, string Shirt, bool Dress, string? Hood, string? Cap, string? Acc);

/// <summary>A villager of v25Povo: a wanderer between linked notes, or idle at the door.</summary>
public sealed class LegacyVillager : ILegacyLifePerson
{
    public string Kind { get; init; } = "andarilho";   // tipo: andarilho | ocioso
    public LegacyCityBuilding From { get; init; } = null!; // de (ocioso: casa)
    public LegacyCityBuilding? To { get; init; }        // para
    public List<(int X, int Y)>? Route { get; init; }   // rota
    public double S { get; set; }
    public int Sense { get; set; } = 1;                 // sentido
    public double Speed { get; init; }                  // vel
    public double PauseTime { get; set; }               // pausa
    public double Step { get; set; }                    // passo
    public int Lane { get; init; }                      // faixa
    public LegacyVillagerLook Look { get; init; } = null!;
    public double PosX { get; set; }
    public double PosY { get; set; }
    public double OX { get; init; }
    public double OY { get; init; }
    public (double X, double Y)? Target { get; set; }  // alvo
    public int DirX { get; set; }
    public int DirY { get; set; } = 1;
    public double Conversation { get; set; }            // conversa

    bool ILegacyLifePerson.Placed => PosX != 0;
    double ILegacyLifePerson.X => PosX;
    double ILegacyLifePerson.Y => PosY;
    double ILegacyLifePerson.DX => DirX;
    double ILegacyLifePerson.DY => DirY;
    double ILegacyLifePerson.Pause => PauseTime;
    double ILegacyLifePerson.Talk => Conversation;
    bool ILegacyLifePerson.Wanderer => Kind == "andarilho";
    public bool Indoors => PauseTime > 0 && Route is not null && (S <= 0 || S >= Route.Count - 1);
    int? ILegacyLifePerson.ShirtLength => Look.Shirt.Length;
}

/// <summary>
/// app.js v25 (1.8.4-beta): who walks are notes linked to other notes, along the drawn road
/// network (BFS), from door to door; some neighbours idle at their door; walkers sometimes
/// stop to chat. Ported line by line; <see cref="Tick"/> is v25Ciclo().
/// </summary>
public sealed class LegacyVillagers
{
    public const int Max = 22;            // V25_MAX
    public const double Speed = 1.35;     // V25_VEL
    public const int FrameMs = 33;        // V25_FPS
    private const int CacheMax = 180;     // V25_CACHE_MAX
    private const int IdleMax = 8;        // V25_OCIOSOS
    private static readonly string[] Shirts = ["#b84a3a", "#3d6fb0", "#5f8f4a", "#c9a24a", "#7a5b8a", "#c7703c", "#4a8a8a", "#e0ddd0", "#9a4f6a"];
    private static readonly string[] Styles = ["short", "short", "long", "straw", "bald", "cap", "hood"];

    private readonly Func<double> _r;
    private readonly Dictionary<string, List<(int X, int Y)>?> _routes = new(StringComparer.Ordinal);
    private double _lastCast;

    public LegacyVillagers(Func<double>? random = null) => _r = random ?? Random.Shared.NextDouble;

    public List<LegacyVillager> People { get; private set; } = [];

    /// <summary>Mudou a geografia ou os links: rotas guardadas não valem mais (rebuildRoadNetwork).</summary>
    public void Reset()
    {
        _routes.Clear();
        People = [];
    }

    /// <summary>v25Ciclo(): <paramref name="nowMs"/> is Date.now(); hurry is urbeVelPovo.</summary>
    public bool Tick(double nowMs, LegacyLifeView view, bool enabled, double hurry, ILegacyTownMap map)
    {
        if (!enabled) { People = []; return false; }
        if (nowMs - _lastCast > 2500) { _lastCast = nowMs; Repopulate(view, map); }
        if (People.Count == 0) return false;
        Step(FrameMs / 1000.0, hurry, map);
        return true;
    }

    // ---------------------------------------------------------------- cast
    private static List<string> Neighbours(LegacyCityBuilding b, ILegacyTownMap map)
    {
        var o = new List<string>();
        foreach (var (from, to) in map.Links)
        {
            if (from == b.Id) o.Add(to);
            else if (to == b.Id) o.Add(from);
        }
        return o;
    }

    private static bool InView(LegacyCityBuilding b, LegacyLifeView f) =>
        !(b.X + b.W < f.X0 || b.X > f.X1 || b.Y + b.H < f.Y0 || b.Y > f.Y1);

    private static List<(LegacyCityBuilding B, List<string> Viz)> Cast(LegacyLifeView f, ILegacyTownMap map)
    {
        var cand = new List<(LegacyCityBuilding B, List<string> Viz, int Weight)>();
        foreach (var b in map.Buildings)
        {
            if (!InView(b, f)) continue;
            var viz = Neighbours(b, map);
            if (viz.Count == 0) continue;
            cand.Add((b, viz, Math.Min(4, viz.Count)));
        }
        return cand.OrderByDescending(c => c.Weight).Take(Max).Select(c => (c.B, c.Viz)).ToList();
    }

    private static (int X, int Y) Door(LegacyCityBuilding b) => ((int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0), b.Y + b.H);

    private static (int X, int Y)? RoadNear(LegacyCityBuilding b, ILegacyTownMap map)
    {
        int bx = (int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0), by = b.Y + b.H + 1;  // lowerRoadStart
        for (int radius = 0; radius <= 4; radius++)
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                if (map.Road(bx + dx, by + dy)) return (bx + dx, by + dy);
            }
        return null;
    }

    private List<(int X, int Y)>? Route(LegacyCityBuilding a, LegacyCityBuilding b, ILegacyTownMap map)
    {
        var key = a.Id + ">" + b.Id;
        if (_routes.TryGetValue(key, out var cached)) return cached;
        List<(int X, int Y)>? r = null;
        if (RoadNear(a, map) is { } ini && RoadNear(b, map) is { } end && ini != end)
        {
            var came = new Dictionary<(int, int), (int, int)?> { [ini] = null };
            var queue = new List<(int X, int Y)> { ini };
            for (int head = 0; head < queue.Count;)
            {
                var k = queue[head++];
                if (k == end) break;
                foreach (var nk in new[] { (k.X + 1, k.Y), (k.X - 1, k.Y), (k.X, k.Y + 1), (k.X, k.Y - 1) })
                    if (map.Road(nk.Item1, nk.Item2) && !came.ContainsKey(nk)) { came[nk] = k; queue.Add(nk); }
            }
            if (came.ContainsKey(end))
            {
                var path = new List<(int X, int Y)>();
                for ((int, int)? cur = end; cur is { } c; cur = came[c]) path.Add(c);
                path.Reverse();
                if (path.Count > 2) r = path;
            }
        }
        if (_routes.Count > CacheMax) _routes.Clear();
        _routes[key] = r;
        return r;
    }

    /// <summary>v25Visual(b, sal): the look, from a hash of the house id.</summary>
    public static LegacyVillagerLook Look(LegacyCityBuilding b, string salt)
    {
        uint h = LegacyJsMath.Seed(b.Id + "#" + salt);
        int R(int n)
        {
            int v = (int)(h % (uint)n);
            h = (uint)(int)((long)Math.Floor(h / (double)n) ^ (h >> 7));
            return v;
        }
        bool dress = R(3) == 0;
        int skin = R(5), hair = R(6), pants = R(5);
        string style = Styles[R(Styles.Length)], shirt = Shirts[R(Shirts.Length)];
        string? hood = null, cap = null;
        if (style == "long" && !dress && R(2) != 0) dress = true;
        if (style == "hood") hood = new[] { "#5b4a6a", "#6a5a3a", "#3f5a6a", "#7a3f3a" }[R(4)];
        if (style == "cap") cap = new[] { "#a33c32", "#3f5f8a", "#5a7a3a", "#8a6a3a" }[R(4)];
        int luck = R(10);
        string? acc = luck < 2 ? "basket" : luck < 4 ? "sack" : luck < 5 ? "bucket" : null;
        if (style == "hood" && acc == "sack") acc = null;
        return new LegacyVillagerLook(skin, hair, pants, style, shirt, dress, hood, cap, acc);
    }

    private LegacyVillager? Spawn((LegacyCityBuilding B, List<string> Viz) c, ILegacyTownMap map)
    {
        var targetId = c.Viz[(int)Math.Floor(_r() * c.Viz.Count)];
        var target = map.Buildings.FirstOrDefault(b => b.Id == targetId);
        if (target is null) return null;
        var route = Route(c.B, target, map);
        if (route is null) return null;
        var r = new List<(int X, int Y)> { Door(c.B) };
        r.AddRange(route);
        r.Add(Door(target));
        int salt = (int)Math.Floor(_r() * 3);
        double s = _r() * Math.Max(1, r.Count - 2) + .5;
        double vel = Speed * (.8 + _r() * .45), step = _r() * 4;
        int lane = _r() < .5 ? -1 : 1;
        return new LegacyVillager
        {
            Kind = "andarilho", From = c.B, To = target, Route = r, S = s, Sense = 1, Speed = vel, Step = step, Lane = lane,
            Look = Look(c.B, salt.ToString(System.Globalization.CultureInfo.InvariantCulture)), DirX = 0, DirY = 1
        };
    }

    private void Idle(List<LegacyVillager> alive, LegacyLifeView f, ILegacyTownMap map)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        int n = 0;
        foreach (var a in People)
        {
            var h = a.From;
            if (a.Kind == "ocioso" && h.X + h.W >= f.X0 && h.X <= f.X1 && h.Y + h.H >= f.Y0 && h.Y <= f.Y1 && map.Buildings.Contains(h) && n < IdleMax)
            {
                alive.Add(a);
                taken.Add(h.Id);
                n++;
            }
        }
        for (int j = 0; j < map.Buildings.Count && n < IdleMax; j++)
        {
            var b = map.Buildings[j];
            if (taken.Contains(b.Id)) continue;
            if (!InView(b, f)) continue;
            if (LegacyJsMath.Seed(b.Id + "ocioso") % 3 != 0) continue;
            var p = Door(b);
            int side = (LegacyJsMath.Seed(b.Id) & 1) != 0 ? 1 : -1;
            double x = p.X + .5 + side * Math.Max(1.3, b.W / 2.0 - .2), y = p.Y + .55;
            double vel = .55 + _r() * .3, pause = 1 + _r() * 3;
            alive.Add(new LegacyVillager
            {
                Kind = "ocioso", From = b, PosX = x, PosY = y, OX = x, OY = y, Speed = vel, PauseTime = pause,
                Look = Look(b, "porta"), DirX = 0, DirY = 1
            });
            taken.Add(b.Id);
            n++;
        }
    }

    private void Repopulate(LegacyLifeView f, ILegacyTownMap map)
    {
        var cast = Cast(f, map);
        var alive = new List<LegacyVillager>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        int newRoutes = 0;
        foreach (var a in People)
            if (a.Kind != "ocioso" && cast.Any(c => c.B == a.From)) { alive.Add(a); used.Add(a.From.Id); }
        for (int j = 0; j < cast.Count && alive.Count < cast.Count; j++)
        {
            var c = cast[j];
            if (used.Contains(c.B.Id)) continue;
            if (newRoutes >= 4) break;
            var created = Spawn(c, map);
            newRoutes++;
            if (created is not null) { alive.Add(created); used.Add(c.B.Id); }
        }
        Idle(alive, f, map);
        People = alive;
    }

    // ---------------------------------------------------------------- step
    private static bool Free(double x, double y, ILegacyTownMap map)
    {
        int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
        return map.BuildingAt(tx, ty) is null && !map.Water(tx, ty);
    }

    private static void Place(LegacyVillager a)
    {
        var r = a.Route!;
        int n = r.Count - 1;
        double s = Math.Max(0, Math.Min(n, a.S));
        int i = Math.Min(n - 1, (int)Math.Floor(s));
        double f = s - i;
        var p0 = r[i];
        var p1 = r[i + 1];
        int dx = p1.X - p0.X, dy = p1.Y - p0.Y;
        if (a.Sense < 0) { dx = -dx; dy = -dy; }
        if (a.PauseTime <= 0 && (dx != 0 || dy != 0)) { a.DirX = dx; a.DirY = dy; }
        // two-way: each walks on their own side of the street, except at the door
        double side = i == 0 || i == n - 1 ? 0 : .2 * a.Lane * a.Sense, ox = -dy * side, oy = dx * side;
        a.PosX = p0.X + (p1.X - p0.X) * f + .5 + ox;
        a.PosY = p0.Y + (p1.Y - p0.Y) * f + .62 + oy;
    }

    private void Step(double dt, double hurry, ILegacyTownMap map)
    {
        foreach (var a in People)
        {
            if (a.Conversation > 0) a.Conversation -= dt;
            if (a.PauseTime > 0)
            {
                a.PauseTime -= dt;
                if (a.Kind == "andarilho") Place(a);
                continue;
            }
            if (a.Kind == "ocioso")
            {
                if (a.Target is null)
                {
                    double tx = a.OX + (_r() * 1.6 - .8), ty = a.OY + (_r() * .6 - .1);
                    if (Free(tx, ty, map)) a.Target = (tx, ty);
                    else { a.PauseTime = 1; continue; }
                }
                var (ax, ay) = a.Target.Value;
                double ddx = ax - a.PosX, ddy = ay - a.PosY, d = LegacyJsMath.Hypot(ddx, ddy), st = a.Speed * dt * hurry;
                if (d <= st)
                {
                    a.PosX = ax;
                    a.PosY = ay;
                    a.Target = null;
                    a.PauseTime = 1.5 + _r() * 4;
                    a.DirX = 0;
                    a.DirY = _r() < .6 ? 1 : 0;
                    if (a.DirY == 0) a.DirX = _r() < .5 ? -1 : 1;
                }
                else
                {
                    a.PosX += ddx / d * st;
                    a.PosY += ddy / d * st;
                    a.Step += st * 4;
                    if (Math.Abs(ddx) > Math.Abs(ddy)) { a.DirX = ddx > 0 ? 1 : -1; a.DirY = 0; }
                    else { a.DirX = 0; a.DirY = ddy > 0 ? 1 : -1; }
                }
                continue;
            }
            int n = a.Route!.Count - 1;
            double av = a.Speed * dt * hurry;
            a.S += av * a.Sense;
            a.Step += av * 4;
            if (a.S >= n) { a.S = n; a.Sense = -1; a.PauseTime = 2 + _r() * 4; a.DirX = 0; a.DirY = 1; }
            else if (a.S <= 0) { a.S = 0; a.Sense = 1; a.PauseTime = 2 + _r() * 4; a.DirX = 0; a.DirY = 1; }
            Place(a);
        }
        // crossed someone on the street? sometimes they stop for a chat
        for (int p = 0; p < People.Count; p++)
        {
            var A = People[p];
            if (A.Kind != "andarilho" || A.PauseTime > 0 || A.Conversation > -4) continue;
            for (int q = p + 1; q < People.Count; q++)
            {
                var B = People[q];
                if (B.PauseTime > 0 || B.Conversation > -4) continue;
                if (Math.Abs(A.PosX - B.PosX) + Math.Abs(A.PosY - B.PosY) < .75 && A.DirX * B.DirX + A.DirY * B.DirY < 0)
                {
                    if (_r() < .45)
                    {
                        double t = 2.5 + _r() * 2.5;
                        A.PauseTime = B.PauseTime = t;
                        A.Conversation = B.Conversation = t;
                        double ddx2 = B.PosX - A.PosX, ddy2 = B.PosY - A.PosY;
                        if (Math.Abs(ddx2) >= Math.Abs(ddy2)) { A.DirX = ddx2 >= 0 ? 1 : -1; A.DirY = 0; }
                        else { A.DirX = 0; A.DirY = ddy2 >= 0 ? 1 : -1; }
                        B.DirX = -A.DirX;
                        B.DirY = -A.DirY;
                    }
                    else A.Conversation = B.Conversation = -.001;
                    break;
                }
            }
        }
        foreach (var a in People)
            if (a.Conversation <= 0 && a.Conversation > -10) a.Conversation -= dt;
    }
}
