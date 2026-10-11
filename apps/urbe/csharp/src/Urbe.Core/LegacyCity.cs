namespace Urbe.Core;

/// <summary>Terrain questions the 1.8.4 city asks (terrain.js INFO: build, road, cost).</summary>
public interface ILegacyCityTerrain
{
    bool Buildable(int x, int y);
    bool Roadable(int x, int y);
    double RoadCost(int x, int y);
}

/// <summary>The original terrain answers, from the ported terrain.js world.</summary>
public sealed class LegacyCityTerrain(Func<int, int, LegacyBiome> biomeAt) : ILegacyCityTerrain
{
    // terrain.js INFO[].cost, in LegacyBiome order.
    private static readonly double[] Costs =
        [99, 99, 4, 6, 1.3, 1, 1, 1.6, 2.2, 3, 1.7, 1.4, 99, 2, 99, 99, 1.4, 1.1, 1.1];

    public bool Buildable(int x, int y) => LegacyBiomeRules.Buildable(biomeAt(x, y));
    public bool Roadable(int x, int y) => LegacyBiomeRules.Roadable(biomeAt(x, y));
    public double RoadCost(int x, int y) => Costs[(int)biomeAt(x, y)];
}

/// <summary>A folder drawn as a neighbourhood (app.js world.regions).</summary>
public sealed class LegacyCityRegion
{
    internal LegacyCityRegion(string id, string name, string? parentId, string color)
    {
        Id = id;
        Name = name;
        ParentId = parentId;
        Color = color;
    }

    public string Id { get; }
    public string Name { get; }
    public string? ParentId { get; }
    public string Color { get; internal set; }
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int W { get; internal set; }
    public int H { get; internal set; }

    internal readonly List<(int X, int Y)> CellList = [];
    internal readonly HashSet<(int X, int Y)> CellSet = [];
    internal (string Key, List<(int X, int Y, double S)> List)? Lots;

    /// <summary>
    /// JS r.cells === null: a neighbourhood restored from .urbe/mapa.json without cells is its
    /// whole rectangle (regionHasTile) until something calls urbeCelulas on it.
    /// </summary>
    internal bool CellsNull;

    /// <summary>True while the neighbourhood has no cell list (JS r.cells null): it is its rectangle.</summary>
    public bool IsRectangle => CellsNull;

    /// <summary>The tiles of the neighbourhood (a null-cells region is its rectangle, row by row).</summary>
    public IReadOnlyList<(int X, int Y)> Cells => CellsNull ? Rectangle() : CellList;
    public bool Has(int x, int y) => CellsNull ? x >= X && x < X + W && y >= Y && y < Y + H : CellSet.Contains((x, y));

    private List<(int X, int Y)> Rectangle()
    {
        var cells = new List<(int X, int Y)>();
        for (int y = Y; y < Y + H; y++)
        for (int x = X; x < X + W; x++) cells.Add((x, y));
        return cells;
    }

    /// <summary>urbeCelulas: a null-cells region becomes its rectangle, row by row.</summary>
    internal List<(int X, int Y)> Celulas()
    {
        if (CellsNull)
        {
            CellsNull = false;
            foreach (var c in Rectangle()) Add(c);
        }
        return CellList;
    }

    internal void Add((int X, int Y) cell)
    {
        CellList.Add(cell);
        CellSet.Add(cell);
    }
}

/// <summary>A note (or other file) drawn as a 3×3 building (app.js world.buildings).</summary>
public sealed class LegacyCityBuilding
{
    internal LegacyCityBuilding(string id, string path, string name, string? regionId, int x, int y, string sprite,
        bool isNote = true, int w = 3, int h = 3)
    {
        Id = id;
        Path = path;
        Name = name;
        RegionId = regionId;
        X = x;
        Y = y;
        Sprite = sprite;
        IsNote = isNote;
        W = w;
        H = h;
        IndexedX = x;
        IndexedY = y;
    }

    public string Id { get; }
    public string Path { get; }
    public string Name { get; }
    public string? RegionId { get; }
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int W { get; }
    public int H { get; }
    public string Sprite { get; }

    /// <summary>b.tipo==='nota'. False for a file building restored from mapa.json construcoes.</summary>
    public bool IsNote { get; }

    /// <summary>b.documentId: the document this house shows (seeds urbeCasasNosBairros).</summary>
    public string? DocumentId { get; internal set; }

    /// <summary>
    /// Where app.js idxB last saw this building (indexar/indexarUm). bAt, tileBlockedByBuilding,
    /// isHouseAccessGap and v22LoteConflita look buildings up through idxB, which is not rebuilt
    /// when urbeCasasNosBairros moves a house; this reproduces that.
    /// </summary>
    internal int IndexedX, IndexedY;

    /// <summary>urbeCasaParaDocumento b.ext: the file extension, '.md' when there is none.</summary>
    public string Ext => System.Text.RegularExpressions.Regex.Match(Path, @"\.[^.]+$") is { Success: true } m ? m.Value : ".md";

    public bool Contains(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// Urbe 1.8.4-beta city layout, ported from the definitions that are in effect in
/// src/app.js (the file redefines several functions; the last definition wins):
/// neighbourhood masks (urbeMascara/urbeCrescer), colours, organic lots, house
/// placement, folder creation (urbeGarantirPasta) and the road network
/// (rebuildRoadNetwork with the v22 A*). No rule here is new.
/// </summary>
public sealed partial class LegacyCity
{
    public const int Tile = 32;
    private const int LotGap = 2; // app.js LOTE_GAP
    private static readonly string[] BaseColors = ["#5bc2ff", "#e38eff", "#7ee3a0", "#ffd567", "#ff9a88"];
    private static readonly string[] DistrictColors =
        ["#5bc2ff", "#e38eff", "#7ee3a0", "#ffd567", "#ff9a88", "#8fa8ff", "#5fd4c4", "#f7a95c", "#c9a0ff", "#b5d96a"];
    private static readonly (int X, int Y)[] Dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly ILegacyCityTerrain _terrain;
    private readonly Func<string, string> _newId;
    private readonly List<(int X, int Y)> _roadOrder = [];
    private readonly HashSet<(int X, int Y)> _roads = [];
    private HashSet<(int X, int Y)>? _occupied, _accessGaps;
    private int _componentSize = -1;
    private Dictionary<(int X, int Y), int>? _componentId;
    private List<List<(int X, int Y)>>? _componentNodes;

    /// <param name="terrain">terrain.js answers for (x, y).</param>
    /// <param name="newId">app.js id(prefix); ids seed lot order and growth, so they must be stable.</param>
    public LegacyCity(ILegacyCityTerrain terrain, Func<string, string> newId)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _newId = newId ?? throw new ArgumentNullException(nameof(newId));
    }

    public List<LegacyCityRegion> Regions { get; } = [];
    public List<LegacyCityBuilding> Buildings { get; } = [];
    public IReadOnlyCollection<(int X, int Y)> Roads => _roadOrder;
    public List<(string From, string To)> Links { get; } = [];
    public bool HasRoad(int x, int y) => _roads.Contains((x, y));

    /// <summary>app.js camera centre in world units (x, y); the default is the 1.8.4 start.</summary>
    public double CameraX { get; set; } = 72 * Tile / 2.0;
    public double CameraY { get; set; } = 50 * Tile / 2.0;

    // ------------------------------------------------------------------ terrain

    private bool Settled(int x, int y) => _roads.Contains((x, y)) || BuildingAt(x, y) is not null;
    /// <summary>ehAgua (final): not buildable, unless the city already settled there.</summary>
    public bool IsWater(int x, int y) => !_terrain.Buildable(x, y) && !Settled(x, y);
    private bool BlocksRoad(int x, int y) => !_terrain.Roadable(x, y) && !Settled(x, y);
    private double RoadCost(int x, int y) => Settled(x, y) ? 1 : _terrain.RoadCost(x, y);

    // ------------------------------------------------------------------ lookups

    private const int Chunk = 16; // app.js CH

    private static int ChunkOf(int v) => (int)Math.Floor(v / (double)Chunk);

    /// <summary>idxB: is the building listed under chunk (cx, cy)?</summary>
    private static bool Indexed(LegacyCityBuilding b, int cx, int cy) =>
        cx >= ChunkOf(b.IndexedX) && cx <= ChunkOf(b.IndexedX + b.W - 1) &&
        cy >= ChunkOf(b.IndexedY) && cy <= ChunkOf(b.IndexedY + b.H - 1);

    /// <summary>indexar: idxB sees every building where it is now.</summary>
    private void Reindex()
    {
        foreach (var b in Buildings) { b.IndexedX = b.X; b.IndexedY = b.Y; }
    }

    /// <summary>bAt: the last building (idxB order) on the tile.</summary>
    public LegacyCityBuilding? BuildingAt(int x, int y)
    {
        int cx = ChunkOf(x), cy = ChunkOf(y);
        for (int i = Buildings.Count - 1; i >= 0; i--)
            if (Indexed(Buildings[i], cx, cy) && Buildings[i].Contains(x, y)) return Buildings[i];
        return null;
    }

    private bool TileBlockedByBuilding(int x, int y) =>
        _occupied is not null ? _occupied.Contains((x, y)) : BuildingAt(x, y) is not null;

    private bool IsHouseAccessGap(int x, int y)
    {
        if (_accessGaps is not null) return _accessGaps.Contains((x, y));
        // the gap is just below the lot and may fall in the next chunk
        int cx = ChunkOf(x);
        foreach (var cy in new[] { ChunkOf(y), ChunkOf(y - 1) })
        foreach (var b in Buildings)
            if (Indexed(b, cx, cy) && x == (int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0) && y == b.Y + b.H) return true;
        return false;
    }

    private static bool RegionHasTile(LegacyCityRegion r, int x, int y) =>
        x >= r.X && x < r.X + r.W && y >= r.Y && y < r.Y + r.H && (r.CellsNull || r.CellSet.Contains((x, y)));

    /// <summary>regAt: the innermost (smallest bounding box) neighbourhood holding the tile.</summary>
    public LegacyCityRegion? RegionAt(int x, int y)
    {
        LegacyCityRegion? m = null;
        foreach (var r in Regions)
            if (RegionHasTile(r, x, y) && (m is null || r.W * r.H < m.W * m.H)) m = r;
        return m;
    }

    private LegacyCityRegion? Parent(LegacyCityRegion? r) =>
        r?.ParentId is { } pid ? Regions.Find(o => o.Id == pid) : null;

    private List<LegacyCityRegion> PathOf(LegacyCityRegion r)
    {
        var p = new List<LegacyCityRegion>();
        for (var c = r; c is not null; c = Parent(c)) p.Insert(0, c);
        return p;
    }

    private int Level(LegacyCityRegion r) => PathOf(r).Count - 1;

    private LegacyCityRegion RootOf(LegacyCityRegion r)
    {
        int n = 0;
        while (r.ParentId is not null && n++ < 64)
        {
            var p = Parent(r);
            if (p is null) break;
            r = p;
        }
        return r;
    }

    /// <summary>caminhosRegioes: folder path of every neighbourhood, unique case-insensitively.</summary>
    public Dictionary<string, string> RegionPaths()
    {
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        string Calc(LegacyCityRegion r, int depth)
        {
            if (cache.TryGetValue(r.Id, out var known)) return known;
            cache[r.Id] = ArtifactModel.SafeName(r.Name);
            var parent = r.ParentId is not null && depth < 24 ? Regions.Find(x => x.Id == r.ParentId) : null;
            var basePath = (parent is not null ? Calc(parent, depth + 1) + "/" : "") + ArtifactModel.SafeName(r.Name);
            var p = basePath;
            int n = 2;
            while (used.Contains(p.ToLowerInvariant())) p = basePath + " (" + n++ + ")";
            used.Add(p.ToLowerInvariant());
            cache[r.Id] = p;
            return p;
        }
        foreach (var r in Regions) Calc(r, 0);
        return cache;
    }

    public LegacyCityRegion? RegionByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var paths = RegionPaths();
        foreach (var r in Regions)
            if (paths[r.Id] == path) return r;
        return null;
    }

    // ------------------------------------------------------------------ region geometry

    private static (double X, double Y) Centroid(LegacyCityRegion r)
    {
        r.Celulas();
        if (r.CellList.Count == 0) return (r.X + r.W / 2.0, r.Y + r.H / 2.0);
        double sx = 0, sy = 0;
        foreach (var (x, y) in r.CellList) { sx += x; sy += y; }
        return (sx / r.CellList.Count + .5, sy / r.CellList.Count + .5);
    }

    private static void Bounds(LegacyCityRegion r)
    {
        r.Celulas();
        if (r.CellList.Count == 0) { r.W = 0; r.H = 0; return; }
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var (x, y) in r.CellList)
        {
            if (x < x0) x0 = x;
            if (x > x1) x1 = x;
            if (y < y0) y0 = y;
            if (y > y1) y1 = y;
        }
        r.X = x0; r.Y = y0; r.W = x1 - x0 + 1; r.H = y1 - y0 + 1;
    }

    private static int DistrictArea(double q) => (int)LegacyJsMath.Round(26 + Math.Max(1, q) * 36);

    private sealed class Grid(int x0, int y0, int w, int h)
    {
        public readonly int X0 = x0, Y0 = y0, W = w, H = h;
        public readonly byte[] Blocked = new byte[w * h];
    }

    /// <summary>urbeGradeBairro: 1 where the neighbourhood cannot grow.</summary>
    private Grid DistrictGrid(LegacyCityRegion? parent, LegacyCityRegion? self, int x0, int y0, int x1, int y1)
    {
        var g = new Grid(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        var bl = g.Blocked;
        int W = g.W;
        void Mark(int cx, int cy, int radius)
        {
            for (int yy = Math.Max(y0, cy - radius); yy <= Math.Min(y1, cy + radius); yy++)
            for (int xx = Math.Max(x0, cx - radius); xx <= Math.Min(x1, cx + radius); xx++)
                bl[(yy - y0) * W + (xx - x0)] = 1;
        }
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (IsWater(x, y)) bl[(y - y0) * W + (x - x0)] = 1;
        if (parent is not null)
        {
            parent.Celulas();
            var ps = parent.CellSet;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = (y - y0) * W + (x - x0);
                if (bl[i] != 0) continue;
                // one tile of margin inside the parent's border
                for (int dy = -1; dy <= 1 && bl[i] == 0; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!ps.Contains((x + dx, y + dy))) { bl[i] = 1; break; }
            }
        }
        string? pid = parent?.Id;
        int raio = parent is not null ? 1 : 2;
        foreach (var o in Regions)
        {
            if (o == self || o == parent || o.ParentId != pid) continue;
            if (o.X > x1 + raio || o.Y > y1 + raio || o.X + o.W < x0 - raio || o.Y + o.H < y0 - raio) continue;
            foreach (var (cx, cy) in o.Celulas()) Mark(cx, cy, raio);
        }
        // loose houses of the same level are obstacles: the new neighbourhood goes around them
        foreach (var b in Buildings)
        {
            if (b.RegionId != pid) continue;
            for (int yy = b.Y - 1; yy < b.Y + b.H + 1; yy++)
            for (int xx = b.X - 1; xx < b.X + b.W + 1; xx++)
                if (xx >= x0 && yy >= y0 && xx <= x1 && yy <= y1) bl[(yy - y0) * W + (xx - x0)] = 1;
        }
        return g;
    }

    /// <summary>
    /// urbeFolga: distance to the closest obstacle (8 neighbours). The JS queue is an
    /// Int32Array of W*H entries: writes past the end are silently dropped and reading them
    /// yields undefined (a no-op step). Reproduced as is, since it shapes the result.
    /// </summary>
    private static short[] Clearance(Grid g)
    {
        int W = g.W, H = g.H, n = W * H, head = 0, tail = 0;
        var d = new short[n];
        Array.Fill(d, short.MaxValue);
        var q = new int[n];
        void Enqueue(int i) { if (tail < n) q[tail] = i; tail++; }
        for (int i = 0; i < n; i++)
        {
            int x = i % W, y = i / W;
            if (g.Blocked[i] != 0) { d[i] = 0; Enqueue(i); }
            else if (x == 0 || y == 0 || x == W - 1 || y == H - 1) { d[i] = 1; Enqueue(i); }
        }
        while (head < tail)
        {
            if (head >= n) { head++; continue; }
            int i = q[head++], cx = i % W, cy = i / W;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = cx + dx, yy = cy + dy;
                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                int j = yy * W + xx;
                if (d[j] > d[i] + 1)
                {
                    d[j] = (short)(d[i] + 1);
                    Enqueue(j);
                }
            }
        }
        return d;
    }

    /// <summary>urbeMelhorPonto: the place closest to the centre with enough room.</summary>
    private static (int X, int Y, int Clearance)? BestPoint(Grid g, double cx, double cy, double raio)
    {
        var d = Clearance(g);
        int best = -1;
        double bs = -1e18;
        for (int i = 0; i < d.Length; i++)
        {
            if (g.Blocked[i] != 0) continue;
            double x = g.X0 + i % g.W + .5, y = g.Y0 + i / g.W + .5;
            double s = Math.Min(d[i], raio) * 1000 - LegacyJsMath.Hypot(x - cx, y - cy);
            if (s > bs) { bs = s; best = i; }
        }
        return best < 0 ? null : (g.X0 + best % g.W, g.Y0 + best / g.W, d[best]);
    }

    /// <summary>urbeCrescer: priority growth (distance to the centre with a light wave).</summary>
    private static List<(int X, int Y)> Grow(Grid g, double cx, double cy, int target, double seed, HashSet<(int X, int Y)>? existing)
    {
        int W = g.W, H = g.H, N = W * H;
        var rand = LegacyJsMath.Rng(seed);
        double a1 = rand() * 6.3, a2 = rand() * 6.3, a3 = rand() * 6.3;
        var inside = new byte[N];
        var seen = new byte[N];
        var hk = new List<double>();
        var hv = new List<int>();
        int total = 0;
        double Cost(int i)
        {
            double x = g.X0 + i % W + .5 - cx, y = g.Y0 + i / W + .5 - cy, t = Math.Atan2(y, x);
            return LegacyJsMath.Hypot(x, y) * (1 + .14 * Math.Sin(2 * t + a1) + .09 * Math.Sin(3 * t + a2) + .05 * Math.Sin(5 * t + a3));
        }
        void Push(int i)
        {
            double c = Cost(i);
            int n = hk.Count;
            hk.Add(c); hv.Add(i);
            while (n > 0)
            {
                int p = (n - 1) >> 1;
                if (hk[p] <= hk[n]) break;
                (hk[p], hk[n]) = (hk[n], hk[p]);
                (hv[p], hv[n]) = (hv[n], hv[p]);
                n = p;
            }
        }
        int Pop()
        {
            int v = hv[0];
            double lk = hk[^1];
            int lv = hv[^1];
            hk.RemoveAt(hk.Count - 1);
            hv.RemoveAt(hv.Count - 1);
            if (hk.Count > 0)
            {
                hk[0] = lk; hv[0] = lv;
                int n = 0, L = hk.Count;
                for (; ; )
                {
                    int a = 2 * n + 1, b = a + 1, m = n;
                    if (a < L && hk[a] < hk[m]) m = a;
                    if (b < L && hk[b] < hk[m]) m = b;
                    if (m == n) break;
                    (hk[m], hk[n]) = (hk[n], hk[m]);
                    (hv[m], hv[n]) = (hv[n], hv[m]);
                    n = m;
                }
            }
            return v;
        }
        void Neighbours(int i, Action<int> f)
        {
            int x = i % W, y = i / W;
            if (x > 0) f(i - 1);
            if (x < W - 1) f(i + 1);
            if (y > 0) f(i - W);
            if (y < H - 1) f(i + W);
        }
        bool In(int x, int y) => x >= g.X0 && y >= g.Y0 && x < g.X0 + W && y < g.Y0 + H;

        if (existing is not null)
        {
            foreach (var (x, y) in existing)
            {
                if (!In(x, y)) continue;
                int i = (y - g.Y0) * W + (x - g.X0);
                inside[i] = 2; seen[i] = 1;
            }
            for (int i0 = 0; i0 < N; i0++)
                if (inside[i0] == 2) Neighbours(i0, j => { if (seen[j] == 0) Push(j); });
            total = existing.Count;
        }
        else
        {
            int s = (int)cy - g.Y0, s2 = (int)cx - g.X0;
            if (s < 0 || s2 < 0 || s >= H || s2 >= W) return [];
            Push(s * W + s2);
        }
        while (hk.Count > 0 && total < target)
        {
            int i = Pop();
            if (seen[i] != 0) continue;
            seen[i] = 1;
            if (g.Blocked[i] != 0) continue;
            inside[i] = 1; total++;
            Neighbours(i, j => { if (seen[j] == 0) Push(j); });
        }
        // finishing: close holes and remove loose tips
        for (int pass = 0; pass < 3; pass++)
        for (int k = 0; k < N; k++)
        {
            int n = 0;
            Neighbours(k, j => { if (inside[j] != 0) n++; });
            if (inside[k] == 0 && g.Blocked[k] == 0 && n >= 3) inside[k] = 1;
            else if (inside[k] == 1 && n <= 1) inside[k] = 0;
        }
        var output = new List<(int X, int Y)>();
        for (int k = 0; k < N; k++)
            if (inside[k] == 1) output.Add((g.X0 + k % W, g.Y0 + k / W));
        return output;
    }

    /// <summary>urbeCentroCidade: weighted centre of root neighbourhoods and loose houses.</summary>
    private (double X, double Y) CityCentre()
    {
        double sx = 0, sy = 0, n = 0;
        foreach (var r in Regions)
        {
            if (r.ParentId is not null || r.W == 0) continue;
            var c = Centroid(r);
            double p = Math.Max(1, r.CellList.Count);
            sx += c.X * p; sy += c.Y * p; n += p;
        }
        foreach (var b in Buildings)
        {
            if (b.RegionId is not null) continue;
            sx += (b.X + 1.5) * 9; sy += (b.Y + 1.5) * 9; n += 9;
        }
        return n != 0 ? (sx / n, sy / n) : (Math.Floor(CameraX / Tile), Math.Floor(CameraY / Tile));
    }

    /// <summary>urbeInicioDoMundo.</summary>
    private static (double X, double Y) WorldStart() => (36 + .5, 25 + .5);

    /// <summary>urbeCentroEmTerra.</summary>
    private (double X, double Y) CentreOnLand((double X, double Y) c)
    {
        int ok = 0, n = 0;
        for (int dy = -12; dy <= 12; dy += 4)
        for (int dx = -12; dx <= 12; dx += 4)
        {
            n++;
            if (!IsWater((int)LegacyJsMath.Round(c.X + dx), (int)LegacyJsMath.Round(c.Y + dy))) ok++;
        }
        return ok >= n * .4 ? c : WorldStart();
    }

    private sealed record Mask(List<(int X, int Y)> Cells, int X, int Y, int W, int H);

    /// <summary>urbeMascara.</summary>
    private Mask? MaskFor(double seed, int target, (double X, double Y)? origin, LegacyCityRegion? parent)
    {
        double raio = Math.Max(3, Math.Sqrt(target / Math.PI) * .8);
        (int X, int Y, int Clearance)? p = null;
        Grid? g = null;
        double cx, cy;
        if (parent is not null)
        {
            (cx, cy) = Centroid(parent);
            g = DistrictGrid(parent, null, parent.X, parent.Y, parent.X + parent.W - 1, parent.Y + parent.H - 1);
            p = BestPoint(g, cx, cy, raio);
        }
        else
        {
            (cx, cy) = CentreOnLand(origin ?? CityCentre());
            double m = Math.Ceiling(Math.Sqrt(target)) * 2 + 14;
            for (int t = 0; t < 4; t++, m *= 2)
            {
                g = DistrictGrid(null, null, (int)Math.Floor(cx - m), (int)Math.Floor(cy - m), (int)Math.Ceiling(cx + m), (int)Math.Ceiling(cy + m));
                p = BestPoint(g, cx, cy, raio);
                if (p is { } q && q.Clearance >= Math.Min(raio, 6)) break;
            }
        }
        if (p is not { } point || g is null) return null;
        var cells = Grow(g, point.X + .5, point.Y + .5, target, seed, null);
        if (cells.Count == 0) return null;
        var tmp = new LegacyCityRegion("", "", null, "");
        foreach (var c in cells) tmp.Add(c);
        Bounds(tmp);
        return new Mask(cells, tmp.X, tmp.Y, tmp.W, tmp.H);
    }

    /// <summary>construirMascaraRegiao (final).</summary>
    private Mask? RegionMask(double seed, double quantity, (double X, double Y)? origin, LegacyCityRegion? parent)
    {
        int target = DistrictArea(quantity);
        Mask? res = null;
        for (int t = 0; t < 4; t++)
        {
            res = MaskFor(seed + t, target, t != 0 ? null : origin, parent);
            if (res is not null && res.Cells.Count >= target * .85) return res;
            if (parent is null) break;
            // did not fit: the parent grows and tries again
            if (ExpandRegion(parent, LegacyJsMath.Round(target * 1.3) + 40) == 0 && res is not null) break;
        }
        return res is not null && res.Cells.Count >= 12 ? res : null;
    }

    /// <summary>expandirRegiao (final).</summary>
    public int ExpandRegion(LegacyCityRegion r, double extra)
    {
        r.Celulas();
        var parent = Parent(r);
        int target = r.CellList.Count + (int)Math.Max(1, LegacyJsMath.Round(extra));
        int m = (int)Math.Ceiling(Math.Sqrt(target) * .7) + 6;
        var g = DistrictGrid(parent, r, r.X - m, r.Y - m, r.X + r.W - 1 + m, r.Y + r.H - 1 + m);
        var c = Centroid(r);
        double seed = LegacyJsMath.Seed(r.Id.Length > 0 ? r.Id : r.Name);
        var added = Grow(g, c.X, c.Y, target, seed, r.CellSet);
        if (added.Count < extra * .5 && parent is not null && ExpandRegion(parent, LegacyJsMath.Round(extra * 1.4) + 30) != 0)
        {
            g = DistrictGrid(parent, r, r.X - m, r.Y - m, r.X + r.W - 1 + m, r.Y + r.H - 1 + m);
            var more = Grow(g, c.X, c.Y, target, seed + 1, r.CellSet);
            if (more.Count > added.Count) added = more;
        }
        if (added.Count == 0) return 0;
        foreach (var k in added) r.Add(k);
        Bounds(r);
        r.Lots = null;
        Reindex();
        return added.Count;
    }

    // ------------------------------------------------------------------ colours

    private static string Mix(string h, string target, double f)
    {
        int a = Convert.ToInt32(h[1..], 16), b = Convert.ToInt32(target[1..], 16);
        var o = "#";
        for (int s = 16; s >= 0; s -= 8)
        {
            int c = (int)LegacyJsMath.Round(((a >> s) & 255) * (1 - f) + ((b >> s) & 255) * f);
            o += c.ToString("x2");
        }
        return o;
    }

    private string ChildColor(LegacyCityRegion r)
    {
        var root = RootOf(r);
        int n = Level(r);
        var baseColor = System.Text.RegularExpressions.Regex.IsMatch(root.Color ?? "", "^#[0-9a-f]{6}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? root.Color! : "#5bc2ff";
        return n != 0 ? Mix(baseColor, "#ffffff", Math.Min(.55, .22 * n)) : baseColor;
    }

    private string RootColor(LegacyCityRegion r)
    {
        var c = Centroid(r);
        var use = DistrictColors.ToDictionary(k => k, _ => 0.0, StringComparer.Ordinal);
        foreach (var o in Regions)
        {
            if (o == r || o.ParentId is not null || o.W == 0 || !use.ContainsKey(o.Color)) continue;
            var oc = Centroid(o);
            double d = LegacyJsMath.Hypot(oc.X - c.X, oc.Y - c.Y);
            use[o.Color] += 1 + 200 / (20 + d);
        }
        return DistrictColors
            .Select((k, i) => (k, i))
            .OrderBy(p => use[p.k]).ThenBy(p => p.i)
            .First().k;
    }

    // ------------------------------------------------------------------ neighbourhoods

    /// <summary>criarRegiaoOrganica (base + colour wrapper).</summary>
    public LegacyCityRegion? CreateRegion(string name, double quantity, double seed, string? parentId, (double X, double Y)? origin)
    {
        var parent = parentId is not null ? Regions.Find(r => r.Id == parentId) : null;
        var m = RegionMask(seed, quantity, origin, parent);
        if (m is null) return null;
        var r = new LegacyCityRegion(_newId("r"), name, parent?.Id, BaseColors[Regions.Count % BaseColors.Length])
        {
            X = m.X, Y = m.Y, W = m.W, H = m.H
        };
        foreach (var c in m.Cells) r.Add(c);
        Regions.Add(r);
        r.Color = r.ParentId is not null ? ChildColor(r) : RootColor(r);
        return r;
    }

    /// <summary>urbeGarantirPasta: the chain of neighbourhoods of a folder.</summary>
    public LegacyCityRegion? EnsureFolder(string folder, IReadOnlyDictionary<string, int>? quantities = null)
    {
        if (string.IsNullOrEmpty(folder)) return null;
        var segs = folder.Split('/').Where(s => s.Length > 0).ToArray();
        LegacyCityRegion? parent = null;
        var path = "";
        foreach (var seg in segs)
        {
            path = path.Length > 0 ? path + "/" + seg : seg;
            var r = RegionByPath(path);
            if (r is null)
            {
                int q = Math.Max(1, quantities is not null && quantities.TryGetValue(path, out var v) && v != 0 ? v : 1);
                for (int t = 0; t < 4 && r is null; t++)
                {
                    r = CreateRegion(seg, q, (double)LegacyJsMath.Seed("pasta:" + path) + t, parent?.Id, null);
                    if (r is null && parent is not null) ExpandRegion(parent, Math.Max(80, q * 36));
                }
                if (r is null) return parent;
            }
            parent = r;
        }
        return parent;
    }

    /// <summary>legacy.runtime.ensureFolders: shallow folders first.</summary>
    public void EnsureFolders(IReadOnlyList<KeyValuePair<string, int>> quantities)
    {
        var lookup = quantities.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var p in quantities.Select((p, i) => (p.Key, i))
                     .OrderBy(p => p.Key.Split('/').Length).ThenBy(p => p.i))
            EnsureFolder(p.Key, lookup);
    }

    /// <summary>tutorial.js prepararBairros: how many houses each folder must hold.</summary>
    public static List<KeyValuePair<string, int>> FolderQuantities(IEnumerable<string> paths)
    {
        var order = new List<string>();
        var qtd = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in paths)
        {
            var parts = p.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                var k = string.Join('/', parts.Take(i));
                if (!qtd.ContainsKey(k)) { qtd[k] = 0; order.Add(k); }
                qtd[k]++;
            }
        }
        foreach (var k in order)
        {
            int depth = k.Split('/').Length;
            int children = order.Count(x => x.StartsWith(k + "/", StringComparison.Ordinal) && x.Split('/').Length == depth + 1);
            qtd[k] += children * 3;
        }
        return order.Select(k => new KeyValuePair<string, int>(k, qtd[k])).ToList();
    }

    // ------------------------------------------------------------------ lots and houses

    private static double LotDraw(double x, double y, uint s)
    {
        unchecked
        {
            uint ix = (uint)LegacyJsMath.ToInt32(x), iy = (uint)LegacyJsMath.ToInt32(y);
            uint h = ((ix ^ 0x27d4eb2d) * 0x165667b1) ^ ((iy ^ s) * 0x9e3779b1);
            h ^= h >> 15;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            return h / 4294967296.0;
        }
    }

    private static Func<double, double, double> OrganicOrder(double cx, double cy, uint seed)
    {
        var rand = LegacyJsMath.Rng(seed);
        double a1 = rand() * 6.3, a2 = rand() * 6.3;
        return (x, y) =>
        {
            double dx = x - cx, dy = y - cy, t = Math.Atan2(dy, dx);
            return LegacyJsMath.Hypot(dx, dy) * (1 + .18 * Math.Sin(2 * t + a1) + .1 * Math.Sin(3 * t + a2)) + LotDraw(x, y, seed) * 2.6;
        };
    }

    /// <summary>urbeLotes: 3×3 lots inside the neighbourhood, in organic order around its centre.</summary>
    private List<(int X, int Y, double S)> Lots(LegacyCityRegion r)
    {
        r.Celulas();
        var key = r.CellList.Count + ":" + r.X + ":" + r.Y + ":" + r.W + ":" + r.H;
        if (r.Lots is { } cached && cached.Key == key) return cached.List;
        var c = Centroid(r);
        var order = OrganicOrder(c.X, c.Y, LegacyJsMath.Seed("lotes:" + (r.Id.Length > 0 ? r.Id : r.Name)));
        var list = new List<(int X, int Y, double S)>();
        for (int y = r.Y; y <= r.Y + r.H - 3; y++)
        for (int x = r.X; x <= r.X + r.W - 3; x++)
        {
            if (!r.Has(x, y) || !r.Has(x + 2, y + 2) || !r.Has(x + 2, y) || !r.Has(x, y + 2)) continue;
            list.Add((x, y, order(x + 1.5, y + 1.5)));
        }
        list = list.Select((l, i) => (l, i)).OrderBy(p => p.l.S).ThenBy(p => p.i).Select(p => p.l).ToList();
        r.Lots = (key, list);
        return list;
    }

    /// <summary>acessosLote: at least one side of the lot reaches dry, free ground.</summary>
    private bool HasAccess(int x, int y)
    {
        (int, int)[][] candidates =
        [
            [(x + 1, y - 1), (x + 1, y - 2)],
            [(x + 1, y + 3), (x + 1, y + 4)],
            [(x - 1, y + 1), (x - 2, y + 1)],
            [(x + 3, y + 1), (x + 4, y + 1)]
        ];
        return candidates.Any(cells => cells.All(c => !IsWater(c.Item1, c.Item2) && !TileBlockedByBuilding(c.Item1, c.Item2)));
    }

    /// <summary>v22LoteConflita: 2 tiles of spacing from every other building.</summary>
    private bool LotConflicts(int x, int y, ISet<string> ignore)
    {
        // idxB chunks of the 8-tile window around the lot
        int cx0 = ChunkOf(x - 8), cx1 = ChunkOf(x + 8), cy0 = ChunkOf(y - 8), cy1 = ChunkOf(y + 8);
        foreach (var b in Buildings)
        {
            if (ignore.Contains(b.Id)) continue;
            if (ChunkOf(b.IndexedX + b.W - 1) < cx0 || ChunkOf(b.IndexedX) > cx1 ||
                ChunkOf(b.IndexedY + b.H - 1) < cy0 || ChunkOf(b.IndexedY) > cy1) continue;
            if (x < b.X + b.W + LotGap && x + 3 + LotGap > b.X && y < b.Y + b.H + LotGap && y + 3 + LotGap > b.Y) return true;
        }
        return false;
    }

    /// <summary>loteValido (final): 3×3 dry lot, inside r when given, with access and spacing.</summary>
    public bool LotIsValid(int x, int y, ISet<string>? ignore = null, LegacyCityRegion? r = null)
    {
        ignore ??= new HashSet<string>();
        for (int yy = y; yy < y + 3; yy++)
        for (int xx = x; xx < x + 3; xx++)
        {
            if (IsWater(xx, yy)) return false;
            if (r is not null)
            {
                if (!RegionHasTile(r, xx, yy)) return false;
                var inner = RegionAt(xx, yy);
                if (inner is not null && inner.Id != r.Id) return false;
            }
        }
        if (!HasAccess(x, y)) return false;
        return !LotConflicts(x, y, ignore);
    }

    /// <summary>posicaoAleatoriaNaRegiao (final): first free lot in organic order.</summary>
    private (int X, int Y)? PlaceInRegion(LegacyCityRegion r, ISet<string> ignore)
    {
        foreach (var l in Lots(r))
            if (LotIsValid(l.X, l.Y, ignore, r)) return (l.X, l.Y);
        return null;
    }

    /// <summary>vagaNaRegiao: the neighbourhood grows when it is full.</summary>
    public (int X, int Y)? PlaceInRegion(LegacyCityRegion r, double seed, ISet<string>? ignore = null)
    {
        ignore ??= new HashSet<string>();
        var pos = PlaceInRegion(r, ignore);
        for (int t = 0; t < 4 && pos is null; t++)
        {
            if (ExpandRegion(r, 81) == 0) break;
            pos = PlaceInRegion(r, ignore);
        }
        return pos;
    }

    private bool LotOutsideRegions(int x, int y, int w, int h)
    {
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++)
            if (RegionAt(xx, yy) is not null) return false;
        return true;
    }

    /// <summary>vagaAleatoria (final, with the original random search as fallback): loose notes.</summary>
    public (int X, int Y)? PlaceLoose(double seed, int w = 3, int h = 3, (double X, double Y)? area = null)
    {
        // centroBuscaRaiz (final): where the camera is looking.
        var c = area ?? (Math.Floor(CameraX / Tile) - 1, Math.Floor(CameraY / Tile) - 1);
        int cx = (int)LegacyJsMath.Round(c.X), cy = (int)LegacyJsMath.Round(c.Y);
        var order = OrganicOrder(cx, cy, LegacyJsMath.Seed("raiz"));
        for (int R = 12, done = -1; R <= 192; done = R, R *= 2)
        {
            var cand = new List<(int X, int Y, double S, int I)>();
            for (int dy = -R; dy <= R; dy++)
            for (int dx = -R; dx <= R; dx++)
            {
                double d = LegacyJsMath.Hypot(dx, dy);
                if (d > R || d <= done) continue;
                cand.Add((cx + dx, cy + dy, order(cx + dx + 1.5, cy + dy + 1.5), cand.Count));
            }
            foreach (var p in cand.OrderBy(p => p.S).ThenBy(p => p.I))
                if (LotOutsideRegions(p.X - 1, p.Y - 1, w + 2, h + 2) && LotIsValid(p.X, p.Y)) return (p.X, p.Y);
        }
        var rand = LegacyJsMath.Rng(seed);
        for (int band = 0; band < 14; band++)
        {
            double radius = 12 * Math.Pow(2, band);
            for (int i = 0; i < 260; i++)
            {
                double ang = rand() * Math.PI * 2, dist = Math.Sqrt(rand()) * radius;
                int x = (int)LegacyJsMath.Round(c.X + Math.Cos(ang) * dist), y = (int)LegacyJsMath.Round(c.Y + Math.Sin(ang) * dist);
                if (LotOutsideRegions(x, y, w, h) && LotIsValid(x, y)) return (x, y);
            }
        }
        return null;
    }

    /// <summary>urbeCasaParaDocumento: every document becomes a house in its folder's neighbourhood.</summary>
    public LegacyCityBuilding AddDocument(string path, string name)
    {
        var slash = path.LastIndexOf('/');
        var folder = slash < 0 ? "" : path[..slash];
        var region = RegionByPath(folder);
        if (region is null && folder.Length > 0) region = EnsureFolder(folder);
        double seed = LegacyJsMath.Seed(path);
        var pos = region is not null ? PlaceInRegion(region, seed) : PlaceLoose(seed);
        string[] sprites = ["house1", "house2", "house3"];
        var b = new LegacyCityBuilding(_newId("b"), path, name, region?.Id, pos?.X ?? 0, pos?.Y ?? 0,
            sprites[LegacyJsMath.Seed(path) % 3]);
        Buildings.Add(b);
        return b;
    }

    // ------------------------------------------------------------------ roads

    private static int Manhattan((int X, int Y) a, (int X, int Y) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static (int X, int Y) LowerRoadStart(LegacyCityBuilding b) =>
        ((int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0), b.Y + b.H + 1);

    private void AddRoad((int X, int Y) p)
    {
        if (_roads.Add(p)) _roadOrder.Add(p);
    }

    private void LayPath(List<(int X, int Y)> path)
    {
        foreach (var p in path) AddRoad(p);
    }

    /// <summary>v22Componentes: road components, recomputed when the network size changes.</summary>
    private void Components()
    {
        if (_componentId is not null && _componentSize == _roads.Count) return;
        _componentId = [];
        _componentNodes = [];
        foreach (var s in _roadOrder)
        {
            if (_componentId.ContainsKey(s)) continue;
            int idc = _componentNodes.Count;
            var list = new List<(int X, int Y)>();
            var stack = new List<(int X, int Y)> { s };
            _componentId[s] = idc;
            while (stack.Count > 0)
            {
                var k = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                list.Add(k);
                (int, int)[] near = [(k.X + 1, k.Y), (k.X - 1, k.Y), (k.X, k.Y + 1), (k.X, k.Y - 1)];
                foreach (var nk in near)
                    if (_roads.Contains(nk) && !_componentId.ContainsKey(nk)) { _componentId[nk] = idc; stack.Add(nk); }
            }
            _componentNodes.Add(list);
        }
        _componentSize = _roads.Count;
    }

    private List<(int X, int Y)>? ComponentFrom((int X, int Y) start)
    {
        if (!_roads.Contains(start)) return null;
        Components();
        return _componentId!.TryGetValue(start, out var idc) ? _componentNodes![idc] : null;
    }

    private bool ConnectedBetween(LegacyCityBuilding a, LegacyCityBuilding b)
    {
        var sa = LowerRoadStart(a);
        var sb = LowerRoadStart(b);
        if (!_roads.Contains(sa) || !_roads.Contains(sb)) return false;
        Components();
        return _componentId!.TryGetValue(sa, out var ia) && _componentId.TryGetValue(sb, out var ib) && ia == ib;
    }

    private (bool H, bool V) Axes(int x, int y) =>
        (_roads.Contains((x - 1, y)) || _roads.Contains((x + 1, y)), _roads.Contains((x, y - 1)) || _roads.Contains((x, y + 1)));

    private bool IsRoadNode(int x, int y)
    {
        if (!_roads.Contains((x, y))) return false;
        var a = Axes(x, y);
        return a.H && a.V;
    }

    private (int X, int Y)? NearestRoadNode((int X, int Y) from, List<(int X, int Y)> nodes)
    {
        (int X, int Y)? best = null;
        int bestD = int.MaxValue;
        foreach (var n in nodes)
        {
            if (!IsRoadNode(n.X, n.Y)) continue;
            int d = Manhattan(from, n);
            if (d < bestD) { best = n; bestD = d; }
        }
        return best;
    }

    private ((int X, int Y) Join, (int X, int Y) Approach, int D)? NearestCreatableRoadNode((int X, int Y) from, List<(int X, int Y)> component)
    {
        ((int X, int Y) Join, (int X, int Y) Approach, int D)? best = null;
        foreach (var join in component)
        {
            var axes = Axes(join.X, join.Y);
            (int X, int Y)[] approaches;
            if (axes.H && !axes.V) approaches = [(join.X, join.Y - 1), (join.X, join.Y + 1)];
            else if (axes.V && !axes.H) approaches = [(join.X - 1, join.Y), (join.X + 1, join.Y)];
            else continue;
            foreach (var ap in approaches)
            {
                if (IsWater(ap.X, ap.Y) || IsHouseAccessGap(ap.X, ap.Y) || TileBlockedByBuilding(ap.X, ap.Y)) continue;
                int d = Manhattan(from, ap) + 1;
                if (best is null || d < best.Value.D) best = (join, ap, d);
            }
        }
        return best;
    }

    private (int X, int Y)? ConnectToExistingNetwork((int X, int Y) from, List<(int X, int Y)> component)
    {
        var existing = NearestRoadNode(from, component);
        var creatable = NearestCreatableRoadNode(from, component);
        double de = existing is { } e ? Manhattan(from, e) : double.PositiveInfinity;
        double dc = creatable is { } c ? c.D : double.PositiveInfinity;
        if (creatable is { } c1 && dc < de)
        {
            var path = Route(from, c1.Approach);
            if (path.Count > 0) { LayPath(path); AddRoad(c1.Join); return c1.Join; }
        }
        if (existing is { } e1)
        {
            var path = Route(from, e1);
            if (path.Count > 0) { LayPath(path); return e1; }
        }
        if (creatable is { } c2)
        {
            var path = Route(from, c2.Approach);
            if (path.Count > 0) { LayPath(path); AddRoad(c2.Join); return c2.Join; }
        }
        return null;
    }

    private sealed class Node(int x, int y, int d, double g, double f)
    {
        public readonly int X = x, Y = y, D = d;
        public readonly double G = g, F = f;
    }

    /// <summary>routeAStar (v22): binary heap, island test and growing windows.</summary>
    private List<(int X, int Y)> Route((int X, int Y) start, (int X, int Y) goal)
    {
        int distBase = Manhattan(start, goal);
        int[] envelopes = [24, 48, 96, 192, 384];
        if (BlocksRoad(start.X, start.Y) || BlocksRoad(goal.X, goal.Y)) return [];
        bool Isolated((int X, int Y) a, (int X, int Y) b)
        {
            var vis = new HashSet<(int, int)> { a };
            var q = new List<(int X, int Y)> { a };
            int h = 0;
            while (h < q.Count)
            {
                if (q.Count > 2500) return false;
                var c = q[h++];
                foreach (var (dx, dy) in Dirs)
                {
                    int x = c.X + dx, y = c.Y + dy;
                    if (!vis.Add((x, y))) continue;
                    if (x == b.X && y == b.Y) return false;
                    if (BlocksRoad(x, y) || IsHouseAccessGap(x, y) || TileBlockedByBuilding(x, y)) continue;
                    q.Add((x, y));
                }
            }
            return true;
        }
        if (Isolated(goal, start) || Isolated(start, goal)) return [];
        int budget = 150000;
        for (int e = 0; e < envelopes.Length && budget > 0; e++)
        {
            bool touchedEdge = false;
            int limit = Math.Min(120000, budget), extra = envelopes[e];
            int minX = Math.Min(start.X, goal.X) - extra, maxX = Math.Max(start.X, goal.X) + extra;
            int minY = Math.Min(start.Y, goal.Y) - extra, maxY = Math.Max(start.Y, goal.Y) + extra;
            var heap = new List<Node>();
            var best = new Dictionary<(int, int, int), double>();
            var came = new Dictionary<(int, int, int), Node>();
            Node? target = null;
            int steps = 0;
            void Push(Node no)
            {
                heap.Add(no);
                int i = heap.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (heap[p].F <= heap[i].F) break;
                    (heap[p], heap[i]) = (heap[i], heap[p]);
                    i = p;
                }
            }
            Node Pop()
            {
                var top = heap[0];
                var last = heap[^1];
                heap.RemoveAt(heap.Count - 1);
                if (heap.Count > 0)
                {
                    heap[0] = last;
                    int i = 0;
                    for (; ; )
                    {
                        int l = 2 * i + 1, r = l + 1, m = i;
                        if (l < heap.Count && heap[l].F < heap[m].F) m = l;
                        if (r < heap.Count && heap[r].F < heap[m].F) m = r;
                        if (m == i) break;
                        (heap[m], heap[i]) = (heap[i], heap[m]);
                        i = m;
                    }
                }
                return top;
            }
            Push(new Node(start.X, start.Y, -1, 0, distBase));
            best[(start.X, start.Y, -1)] = 0;
            while (heap.Count > 0 && steps++ < limit)
            {
                var cur = Pop();
                if (cur.G > (best.TryGetValue((cur.X, cur.Y, cur.D), out var bg) ? bg : double.PositiveInfinity)) continue;
                if (cur.X == goal.X && cur.Y == goal.Y) { target = cur; break; }
                for (int nd = 0; nd < 4; nd++)
                {
                    int x = cur.X + Dirs[nd].X, y = cur.Y + Dirs[nd].Y;
                    if (x < minX || x > maxX || y < minY || y > maxY) { touchedEdge = true; continue; }
                    if (BlocksRoad(x, y) || IsHouseAccessGap(x, y)) continue;
                    if (TileBlockedByBuilding(x, y) && !(x == goal.X && y == goal.Y)) continue;
                    bool reuse = _roads.Contains((x, y));
                    double turn = cur.D != -1 && cur.D != nd ? 1.35 : 0, step = reuse ? .06 : RoadCost(x, y);
                    double g = cur.G + step + turn;
                    var nk = (x, y, nd);
                    if (g < (best.TryGetValue(nk, out var old) ? old : double.PositiveInfinity))
                    {
                        best[nk] = g;
                        var no = new Node(x, y, nd, g, g + Manhattan((x, y), goal));
                        came[nk] = cur;
                        Push(no);
                    }
                }
            }
            if (target is not null)
            {
                var path = new List<(int X, int Y)>();
                Node? n = target;
                while (n is not null)
                {
                    path.Add((n.X, n.Y));
                    n = came.TryGetValue((n.X, n.Y, n.D), out var prev) ? prev : null;
                }
                path.Reverse();
                return path;
            }
            budget -= steps;
            if (!touchedEdge) break;
        }
        return [];
    }

    private void AddSemanticRoute(LegacyCityBuilding a, LegacyCityBuilding b)
    {
        var sa = LowerRoadStart(a);
        var sb = LowerRoadStart(b);
        var netB = ComponentFrom(sb);
        if (netB is { Count: > 0 } && ConnectToExistingNetwork(sa, netB) is not null) return;
        var netA = ComponentFrom(sa);
        if (netA is { Count: > 0 } && ConnectToExistingNetwork(sb, netA) is not null) return;
        LayPath(Route(sa, sb));
    }

    /// <summary>
    /// rebuildRoadNetwork: every [[link]] between two houses becomes a street; shorter links
    /// first so longer ones reuse the network. <paramref name="links"/> gives, for each building
    /// in order, the buildings it links to (knowledge.links order).
    /// </summary>
    public void RebuildRoads(Func<LegacyCityBuilding, IEnumerable<LegacyCityBuilding?>> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        _occupied = [];
        _accessGaps = [];
        foreach (var b in Buildings)
        {
            for (int y = b.Y; y < b.Y + b.H; y++)
            for (int x = b.X; x < b.X + b.W; x++) _occupied.Add((x, y));
            _accessGaps.Add(((int)LegacyJsMath.Round(b.X + (b.W - 1) / 2.0), b.Y + b.H));
        }
        try
        {
            _roads.Clear();
            _roadOrder.Clear();
            _componentId = null;
            Links.Clear();
            var pairs = new List<(LegacyCityBuilding A, LegacyCityBuilding B)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in Buildings)
            foreach (var t in links(b))
            {
                if (t is null || t == b) continue;
                var ids = new[] { b.Id, t.Id };
                Array.Sort(ids, StringComparer.Ordinal);
                if (!seen.Add(string.Join('|', ids))) continue;
                pairs.Add((b, t));
                Links.Add((b.Id, t.Id));
            }
            pairs = pairs.Select((p, i) => (p, i))
                .OrderBy(p => Math.Abs(p.p.A.X - p.p.B.X) + Math.Abs(p.p.A.Y - p.p.B.Y)).ThenBy(p => p.i)
                .Select(p => p.p).ToList();
            foreach (var (a, b) in pairs) AddSemanticRoute(a, b);
            // every resolved [[link]] must end in the same road component
            foreach (var (a, b) in pairs)
                if (!ConnectedBetween(a, b)) AddSemanticRoute(a, b);
        }
        finally
        {
            _occupied = null;
            _accessGaps = null;
        }
    }

    /// <summary>
    /// linksFromContent + buildingByLinkName: each house's [[links]] (KnowledgeIndex order),
    /// resolved by normalised title like app.js normName.
    /// </summary>
    public void RebuildRoads(DocumentStore store, KnowledgeIndex index)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        static string NormName(string s) => LegacyJsMath.Slug(System.Text.RegularExpressions.Regex.Replace(s, @"\.md$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToLowerInvariant();
        var all = store.List();
        var byPath = Buildings.GroupBy(b => b.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        LegacyCityBuilding? ByLinkName(string name)
        {
            var doc = all.FirstOrDefault(d => NormName(d.Title) == name || NormName(d.Path) == name);
            return doc is not null && byPath.TryGetValue(doc.Path, out var b) ? b : null;
        }
        RebuildRoads(b =>
        {
            var doc = store.Get(b.Path);
            return doc is null ? [] : index.Links(doc.Id).Select(d => ByLinkName(NormName(d.Title)));
        });
    }

    /// <summary>
    /// First open of a vault with these files (tutorial.js semear): neighbourhoods sized
    /// for their contents (prepararBairros + ensureFolders), one house per file in order,
    /// then the streets. Returns the document store used for the links.
    /// </summary>
    public DocumentStore OpenFirstTime(IReadOnlyList<(string Path, string Content)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        EnsureFolders(FolderQuantities(files.Select(f => f.Path)));
        var store = new DocumentStore();
        foreach (var (path, content) in files)
        {
            var doc = store.Upsert(new DocumentInput { Path = path, Content = content });
            AddDocument(path, NoteName(doc.Title, path));
        }
        using var index = new KnowledgeIndex(store);
        RebuildRoads(store, index);
        return store;
    }

    /// <summary>urbeNomeDoc: the title without the file extension.</summary>
    public static string NoteName(string title, string path)
    {
        var m = System.Text.RegularExpressions.Regex.Match(path, @"\.[^./]+$");
        var ext = m.Success ? m.Value : ".md";
        return title.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? title[..^ext.Length] : title;
    }

    /// <summary>urbeEnquadrarNotas(true): camera framing all houses (returns centre and zoom).</summary>
    public (double X, double Y, double Zoom)? FitNotes(double viewWidth, double viewHeight)
    {
        if (Buildings.Count == 0) return null;
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        foreach (var b in Buildings)
        {
            x0 = Math.Min(x0, b.X); y0 = Math.Min(y0, b.Y - 1);
            x1 = Math.Max(x1, b.X + b.W); y1 = Math.Max(y1, b.Y + b.H + 1);
        }
        const int margin = 3;
        double width = (x1 - x0 + margin * 2) * Tile, height = (y1 - y0 + margin * 2) * Tile;
        double usable = Math.Max(200, viewHeight - 170), ideal = Math.Min(viewWidth / width, usable / height);
        return ((x0 + x1) / 2 * Tile, (y0 + y1) / 2 * Tile, Math.Clamp(ideal, .45, 1.3));
    }
}
