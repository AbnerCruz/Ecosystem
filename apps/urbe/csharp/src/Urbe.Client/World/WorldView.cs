using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// The city in full screen (1.8.4 #game canvas), drawn natively with the drawing code of
/// app.js ported step by step, in the original order: ground (near/far), neighbourhoods,
/// roads and bridges, trees, houses, house labels, neighbourhood labels.
/// World and city come from Urbe.Core (LegacyCity, LegacyChunkPixels, LegacyChunkFarPixels).
/// </summary>
public sealed class WorldView : Control
{
    private const double Far = .5;           // app.js URBE_LONGE
    private const int MaxChunks = 96;        // app.js URBE_CHUNKS_MAX (desktop)
    private const double TapSlop = 8;
    private const int T = LegacyWorld.Tile;
    private static readonly string[] Noted = ["md", "markdown", "txt", "html", "htm", "js", "mjs", "css", "json", "yaml", "yml", "csv"];
    private static readonly Dictionary<string, string> Trade = new(StringComparer.Ordinal)
    {
        ["md"] = "house", ["markdown"] = "house", ["txt"] = "house", ["html"] = "hall", ["htm"] = "hall",
        ["js"] = "workshop", ["mjs"] = "workshop", ["css"] = "dyer", ["json"] = "tower", ["yaml"] = "tower",
        ["yml"] = "tower", ["csv"] = "market"
    };

    private readonly Dictionary<(int, int), ChunkImage> _chunks = [];
    private readonly Queue<(int, int)> _chunkOrder = new();
    private readonly HashSet<(int, int)> _pending = [];
    private readonly SpriteCache _sprites = new();
    private readonly Dictionary<IPointer, Point> _pointers = [];
    private readonly Dictionary<string, RegionShape> _shapes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FormattedText> _texts = new(StringComparer.Ordinal);
    private Point? _pressAt;
    private bool _dragged;
    private double _pinchDistance;
    private int _worldGeneration;

    private sealed record ChunkImage(Bitmap Near, Bitmap Far, (int X, int Y, LegacyVegetation.Tree Tree)[] Trees);

    public WorldView()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public LegacyWorld? World { get; private set; }
    public LegacyCity? City { get; private set; }
    public Camera Camera { get; } = new();
    public LegacyCityBuilding? Selected { get; private set; }

    /// <summary>Raised when a tap selects a house or clears the selection.</summary>
    public event EventHandler<LegacyCityBuilding?>? SelectionChanged;
    /// <summary>Raised when the camera moves (biome pill refresh).</summary>
    public event EventHandler? CameraChanged;

    public void Load(LegacyWorld world, LegacyCity city)
    {
        _worldGeneration++;
        foreach (var image in _chunks.Values) { image.Near.Dispose(); image.Far.Dispose(); }
        _chunks.Clear();
        _chunkOrder.Clear();
        _pending.Clear();
        _texts.Clear();
        Selected = null;
        World = world;
        City = city;
        _shapes.Clear();
        InvalidateVisual();
    }

    public string BiomeNameAtCentre()
    {
        if (World is null) return "";
        var t = World.At((int)Math.Floor(Camera.X / T), (int)Math.Floor(Camera.Y / T));
        return BiomeNames.Of(t.Biome);
    }

    public void Select(LegacyCityBuilding? house)
    {
        Selected = house;
        InvalidateVisual();
        SelectionChanged?.Invoke(this, house);
    }

    /// <summary>city.fit (urbeEnquadrarNotas(true)).</summary>
    public void FitNotes()
    {
        if (City?.FitNotes(Bounds.Width, Bounds.Height) is not { } fit) return;
        Camera.X = fit.X;
        Camera.Y = fit.Y;
        Camera.SetZoom(fit.Zoom);
        InvalidateVisual();
        CameraChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CenterOn(LegacyCityBuilding house)
    {
        Camera.X = (house.X + house.W / 2.0) * T;
        Camera.Y = (house.Y + house.H / 2.0) * T;
        InvalidateVisual();
        CameraChanged?.Invoke(this, EventArgs.Empty);
    }

    // ================================================================ drawing

    public override void Render(DrawingContext context)
    {
        Camera.Viewport = Bounds.Size;
        context.FillRectangle(UrbeTheme.Brush("#23466f"), new Rect(Bounds.Size));
        if (World is null || City is null) return;
        List<(int, int)> missing;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            missing = DrawGround(context);
        DrawRegions(context);
        DrawRoads(context);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            DrawTrees(context);
        DrawBuildings(context);
        RequestChunks(missing);
    }

    /// <summary>app.js drawGround (v0.41): near chunk, or far chunk with neighbourhoods cut from the near one.</summary>
    private List<(int, int)> DrawGround(DrawingContext context)
    {
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        const int ch = LegacyWorld.ChunkTiles;
        double z = Camera.Zoom, side = ch * T * z;
        var missing = new List<(int, int)>();
        for (int cy = FloorDiv(y0, ch); cy <= FloorDiv(y1, ch); cy++)
        for (int cx = FloorDiv(x0, ch); cx <= FloorDiv(x1, ch); cx++)
        {
            var p = Camera.WorldToScreen(cx * ch * T, cy * ch * T);
            double x = Math.Floor(p.X), y = Math.Floor(p.Y), l = Math.Ceiling(side) + 1;
            if (!_chunks.TryGetValue((cx, cy), out var img))
            {
                context.FillRectangle(UrbeTheme.Brush("#3d5a36"), new Rect(x, y, l, l));
                missing.Add((cx, cy));
                continue;
            }
            if (z < Far)
            {
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = z < .2 ? BitmapInterpolationMode.LowQuality : BitmapInterpolationMode.None }))
                    context.DrawImage(img.Far, new Rect(0, 0, LegacyChunkFarPixels.Side, LegacyChunkFarPixels.Side), new Rect(x, y, l, l));
                int cx0 = cx * ch, cy0 = cy * ch;
                const int px2 = LegacyChunkPixels.Side / ch;
                foreach (var rg in City!.Regions)
                {
                    int ax = Math.Max(rg.X, cx0), ay = Math.Max(rg.Y, cy0), bx = Math.Min(rg.X + rg.W, cx0 + ch), by = Math.Min(rg.Y + rg.H, cy0 + ch);
                    if (ax >= bx || ay >= by) continue;
                    context.DrawImage(img.Near,
                        new Rect((ax - cx0) * px2, (ay - cy0) * px2, (bx - ax) * px2, (by - ay) * px2),
                        new Rect(x + (ax - cx0) * T * z, y + (ay - cy0) * T * z, Math.Ceiling((bx - ax) * T * z) + 1, Math.Ceiling((by - ay) * T * z) + 1));
                }
            }
            else
                context.DrawImage(img.Near, new Rect(0, 0, LegacyChunkPixels.Side, LegacyChunkPixels.Side), new Rect(x, y, l, l));
        }
        return missing;
    }

    private RegionShape Shape(LegacyCityRegion r)
    {
        var key = r.Id + ":" + r.Cells.Count;
        if (!_shapes.TryGetValue(key, out var s)) _shapes[key] = s = RegionShape.Build(r);
        return s;
    }

    private int Level(LegacyCityRegion r)
    {
        int n = 0;
        for (var p = r; p.ParentId is not null && n < 24; n++)
        {
            var parent = City!.Regions.Find(o => o.Id == p.ParentId);
            if (parent is null) break;
            p = parent;
        }
        return n;
    }

    private List<LegacyCityRegion> VisibleRegions(int margin)
    {
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        return City!.Regions.Where(r => r.W > 0 && r.X < x1 + margin && r.X + r.W > x0 - margin && r.Y < y1 + margin && r.Y + r.H > y0 - margin).ToList();
    }

    /// <summary>app.js urbeDesenharBairros: fill per level, then outlines (halo on root, dashed from level 2).</summary>
    private void DrawRegions(DrawingContext context)
    {
        var vis = VisibleRegions(1);
        if (vis.Count == 0) return;
        double s = T * Camera.Zoom;
        var o = Camera.WorldToScreen(0, 0);
        var ordered = vis.Select(r => (r, n: Level(r))).OrderBy(p => p.n).ToList();
        using (context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(o.X, o.Y)))
        {
            foreach (var (r, n) in ordered)
                context.DrawGeometry(UrbeTheme.Brush(r.Color, n != 0 ? .10 : .13), null, Shape(r).Geometry);
            foreach (var (r, n) in ordered)
            {
                double w = n == 0 ? 3 : n == 1 ? 2 : 1.5;
                var g = Shape(r).Geometry;
                if (n == 0)
                    context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(140, 8, 10, 14)), (w + 2.5) / s,
                        lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), g);
                // JS dash [5/px, 4/px] in tile units = 5 and 4 screen px; Avalonia dashes are in pen widths.
                var dash = n >= 2 ? new DashStyle([5 / w, 4 / w], 0) : null;
                context.DrawGeometry(null, new Pen(UrbeTheme.Brush(r.Color, n != 0 ? .85 : .95), w / s, dash,
                    PenLineCap.Round, PenLineJoin.Round), g);
            }
        }
    }

    /// <summary>app.js drawRoads (two passes with neighbour links, pebbles at z ≥ .7) and bridges over water.</summary>
    private void DrawRoads(DrawingContext context)
    {
        var city = City!;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        double z = Camera.Zoom, s = T * z, lg = s * .56, mg = (s - lg) / 2, edge = Math.Max(1, s * .07);
        var roads = city.Roads.ToList();
        var dark = UrbeTheme.Brush("#6f6250");
        var light = UrbeTheme.Brush("#b8a684");
        for (int pass = 0; pass < 2; pass++)
        {
            var fill = pass == 0 ? dark : light;
            double extra = pass == 0 ? edge : 0;
            foreach (var (x, y) in roads)
            {
                if (x < x0 - 1 || x > x1 + 1 || y < y0 - 1 || y > y1 + 1) continue;
                int v = (city.HasRoad(x + 1, y) ? 1 : 0) | (city.HasRoad(x - 1, y) ? 2 : 0) | (city.HasRoad(x, y + 1) ? 4 : 0) | (city.HasRoad(x, y - 1) ? 8 : 0);
                var p = Camera.WorldToScreen(x * T, y * T);
                double cx = p.X + mg - extra, cy = p.Y + mg - extra, l = lg + extra * 2;
                context.FillRectangle(fill, new Rect(cx, cy, l, l));
                if ((v & 1) != 0) context.FillRectangle(fill, new Rect(cx + l - extra * 2, cy, s - mg - l + extra * 3 + mg, l));
                if ((v & 2) != 0) context.FillRectangle(fill, new Rect(p.X - .5, cy, mg + extra + .5, l));
                if ((v & 4) != 0) context.FillRectangle(fill, new Rect(cx, cy + l - extra * 2, l, s - mg - l + extra * 3 + mg));
                if ((v & 8) != 0) context.FillRectangle(fill, new Rect(cx, p.Y - .5, l, mg + extra + .5));
            }
        }
        if (z >= .7)
        {
            var pebble = new SolidColorBrush(Color.FromArgb(89, 111, 98, 80));
            foreach (var (x, y) in roads)
            {
                if (x < x0 || x > x1 || y < y0 || y > y1) continue;
                var q = Camera.WorldToScreen(x * T, y * T);
                double u = s / 8;
                context.FillRectangle(pebble, new Rect(q.X + s * .38, q.Y + s * .34, u, u));
                context.FillRectangle(pebble, new Rect(q.X + s * .56, q.Y + s * .58, u, u));
            }
        }
        // bridges: road tiles over water (MUNDO.isWater: biome ≤ lake)
        foreach (var (x, y) in roads)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1 || World!.At(x, y).Biome > LegacyBiome.Lake) continue;
            var p = Camera.WorldToScreen(x * T, y * T);
            bool hor = city.HasRoad(x - 1, y) || city.HasRoad(x + 1, y), ver = city.HasRoad(x, y - 1) || city.HasRoad(x, y + 1);
            var deck = UrbeTheme.Brush("#8a6440");
            var plank = UrbeTheme.Brush("#6b4a2e");
            var rail = UrbeTheme.Brush("#4f3520");
            context.FillRectangle(deck, new Rect(p.X, p.Y + s * .14, s, s * .72));
            if (ver && !hor)
            {
                context.FillRectangle(deck, new Rect(p.X + s * .14, p.Y, s * .72, s));
                for (int k = 1; k < 5; k++) context.FillRectangle(plank, new Rect(p.X + s * .14, p.Y + k * s / 5, s * .72, Math.Max(1, s * .04)));
                context.FillRectangle(rail, new Rect(p.X + s * .1, p.Y, Math.Max(1, s * .06), s));
                context.FillRectangle(rail, new Rect(p.X + s * .84, p.Y, Math.Max(1, s * .06), s));
            }
            else
            {
                for (int k = 1; k < 5; k++) context.FillRectangle(plank, new Rect(p.X + k * s / 5, p.Y + s * .14, Math.Max(1, s * .04), s * .72));
                context.FillRectangle(rail, new Rect(p.X, p.Y + s * .1, s, Math.Max(1, s * .06)));
                context.FillRectangle(rail, new Rect(p.X, p.Y + s * .84, s, Math.Max(1, s * .06)));
            }
        }
    }

    /// <summary>app.js drawTrees (v0.41): only from zoom .5; streets get a free shoulder; none on houses or neighbourhoods.</summary>
    private void DrawTrees(DrawingContext context)
    {
        double z = Camera.Zoom;
        if (z < Far) return;
        var city = City!;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        const int ch = LegacyWorld.ChunkTiles;
        double s = T * z / LegacyWorld.TilePixels * 1.3;
        var visible = new List<(int X, int Y, LegacyVegetation.Tree Tree)>();
        for (int cy = FloorDiv(y0, ch); cy <= FloorDiv(y1, ch); cy++)
        for (int cx = FloorDiv(x0, ch); cx <= FloorDiv(x1, ch); cx++)
        {
            if (!_chunks.TryGetValue((cx, cy), out var chunk)) continue; // trees arrive with the ground
            foreach (var t in chunk.Trees)
            {
                if (t.X < x0 || t.X > x1 || t.Y < y0 || t.Y > y1 + 2) continue;
                if (city.HasRoad(t.X, t.Y) || city.HasRoad(t.X + 1, t.Y) || city.HasRoad(t.X - 1, t.Y) || city.HasRoad(t.X, t.Y + 1)) continue;
                if (city.BuildingAt(t.X, t.Y) is not null || city.RegionAt(t.X, t.Y) is not null) continue;
                if (city.BuildingAt(t.X, t.Y + 1) is not null) continue;
                visible.Add(t);
            }
        }
        visible = visible.Select((t, i) => (t, i)).OrderBy(p => p.t.Y).ThenBy(p => p.t.X).ThenBy(p => p.i).Select(p => p.t).ToList();
        foreach (var (tx, ty, tree) in visible)
        {
            var q = Camera.WorldToScreen((tx + .5) * T, (ty + 1) * T);
            double jx = (LegacyWorld.Hash(tx, ty, 31) - .5) * 8 * s, jy = (LegacyWorld.Hash(tx, ty, 32) - .5) * 5 * s;
            var img = _sprites.Tree(tree.Kind, tree.Variant, tree.Snow);
            context.DrawImage(img, new Rect(0, 0, LegacyWorldSprites.TreeWidth, LegacyWorldSprites.TreeHeight),
                new Rect(Math.Round(q.X - 12 * s + jx), Math.Round(q.Y - 30 * s + jy), Math.Ceiling(24 * s), Math.Ceiling(32 * s)));
        }
    }

    /// <summary>The house art as drawn in the city (house panel preview).</summary>
    public Bitmap? ArtOf(LegacyCityBuilding b) => World is null ? null : Art(b);

    /// <summary>urbeArteDaConstrucao: trade by extension, style by biome, variant and flowers by name.</summary>
    private Bitmap Art(LegacyCityBuilding b)
    {
        var ext = b.Ext.TrimStart('.').ToLowerInvariant();
        if (!Noted.Contains(ext)) ext = "md";
        var kind = Trade.GetValueOrDefault(ext, "house");
        var biome = World!.At((int)Math.Floor(b.X + b.W / 2.0), (int)Math.Floor(b.Y + b.H / 2.0)).Biome;
        int variant = b.Sprite == "house2" ? 1 : b.Sprite == "house3" ? 2 : (int)(LegacyJsMath.Seed(b.Id.Length > 0 ? b.Id : b.Name) % 3);
        bool flowers = LegacyJsMath.Seed(b.Name) % 3 == 0;
        return _sprites.Building(kind, LegacyWorldBuildings.StyleForBiome(LegacyBiomeRules.Id(biome)), variant, flowers);
    }

    /// <summary>urbeNomeCasa: name with its extension, Markdown hidden, at most 30 characters.</summary>
    public static string HouseLabel(LegacyCityBuilding b)
    {
        var ext = b.Ext.ToLowerInvariant();
        if (!Noted.Contains(ext.TrimStart('.'))) ext = ".md";
        var raw = System.Text.RegularExpressions.Regex.Replace(b.Name + ext, @"\.(md|markdown)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return raw.Length > 30 ? raw[..28] + "…" : raw;
    }

    /// <summary>app.js drawBuildings (final): halo, art, then labels that do not collide, neighbourhood names last.</summary>
    private void DrawBuildings(DrawingContext context)
    {
        var city = City!;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        double z = Camera.Zoom, s = T * z / LegacyWorld.TilePixels;
        var labels = new List<LegacyCityBuilding>();
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            foreach (var b in city.Buildings)
            {
                if (b.X > x1 || b.X + b.W < x0 || b.Y > y1 || b.Y + b.H < y0) continue;
                if (b == Selected)
                {
                    var sp = Camera.WorldToScreen((b.X - .15) * T, (b.Y - .45) * T);
                    var halo = new Rect(sp.X, sp.Y, (b.W + .3) * T * z, (b.H + .6) * T * z);
                    double rr = Math.Min(10 * z, Math.Min(halo.Height / 2, halo.Width / 2));
                    context.DrawRectangle(new SolidColorBrush(Color.FromArgb(41, 143, 179, 255)), new Pen(UrbeTheme.Brush("#8fb3ff"), 2), halo, rr, rr);
                }
                var p = Camera.WorldToScreen(b.X * T, b.Y * T);
                double w = 48 * s * (b.W / 3.0), h = 56 * s * (b.H / 3.0);
                context.DrawImage(Art(b), new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                    new Rect(Math.Round(p.X), Math.Round(p.Y + b.H * T * z - h), Math.Ceiling(w), Math.Ceiling(h)));
                if (z >= .42 || b == Selected) labels.Add(b);
            }
        var used = DistrictLabelLayout();
        var districts = used.ToList();
        labels = labels.Select((b, i) => (b, i)).OrderBy(p => p.b == Selected ? 0 : 1).ThenBy(p => p.i).Select(p => p.b).ToList();
        foreach (var r in labels)
        {
            var q = Camera.WorldToScreen((r.X + r.W / 2.0) * T, (r.Y + r.H) * T);
            var name = HouseLabel(r);
            var box = HouseLabelBox(name, q.X, q.Y + 4 * z);
            if (r != Selected && used.Any(u => box.X < u.Box.X + u.Box.Width + 3 && box.X + box.Width + 3 > u.Box.X && box.Y < u.Box.Y + u.Box.Height + 2 && box.Y + box.Height + 2 > u.Box.Y)) continue;
            used.Add((box, null, 0, ""));
            DrawHouseLabel(context, name, q.X, q.Y + 4 * z, r == Selected);
        }
        foreach (var d in districts) DrawDistrictPill(context, d);
    }

    // ---------------- labels (urbeRotulo, urbeCaixaBairro, urbePilulaBairro, urbeLayoutNomes)

    private FormattedText Text(string text, double size, FontWeight weight, IBrush brush)
    {
        var key = text + "|" + size + "|" + (int)weight + "|" + brush.GetHashCode();
        if (!_texts.TryGetValue(key, out var ft))
        {
            if (_texts.Count > 4000) _texts.Clear();
            _texts[key] = ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                UrbeTheme.UiTypeface(weight), size, brush);
        }
        return ft;
    }

    private double LabelSize => Math.Round(Math.Max(11, Math.Min(15, 12 * Camera.Zoom)));

    private Rect HouseLabelBox(string text, double cx, double top)
    {
        double size = LabelSize, padX = Math.Round(size * .65), alt = Math.Round(size * 1.75);
        double width = Text(text, size, FontWeight.SemiBold, UrbeTheme.Brush("#ececf0")).Width;
        return new Rect(cx - width / 2 - padX, top, width + padX * 2, alt);
    }

    private void DrawHouseLabel(DrawingContext context, string text, double cx, double top, bool selected)
    {
        var box = HouseLabelBox(text, cx, top);
        var ft = Text(text, LabelSize, FontWeight.SemiBold, UrbeTheme.Brush(selected ? "#0b1325" : "#ececf0"));
        context.DrawRectangle(selected ? UrbeTheme.Brush("#8fb3ff") : new SolidColorBrush(Color.FromArgb(209, 14, 15, 17)), null, box, box.Height / 2, box.Height / 2);
        context.DrawText(ft, new Point(cx - ft.Width / 2, top + box.Height / 2 + .5 - ft.Height / 2));
    }

    private (Rect Box, LegacyCityRegion? Region, int Level, string Name) DistrictBox(LegacyCityRegion r, int n, double x, double y)
    {
        var name = r.Name.Length > 26 ? r.Name[..24] + "…" : r.Name;
        double size = n != 0 ? 11 : 13;
        double tw = Text(name, size, n != 0 ? FontWeight.SemiBold : FontWeight.Bold, Brushes.White).Width;
        double alt = n != 0 ? 22 : 26, w = tw + (n != 0 ? 30 : 36);
        return (new Rect(x - w / 2, y - alt / 2, w, alt), r, n, name);
    }

    private List<(Rect Box, LegacyCityRegion? Region, int Level, string Name)> DistrictLabelLayout()
    {
        var used = new List<(Rect Box, LegacyCityRegion? Region, int Level, string Name)>();
        if (Camera.Zoom < .28) return used;
        double px = T * Camera.Zoom;
        var vis = VisibleRegions(0);
        foreach (var (r, n) in vis.Select(r => (r, n: Level(r)))
                     .Where(o => o.n == 0 || px >= (o.n == 1 ? 7 : 12))
                     .Select((o, i) => (o, i)).OrderBy(p => p.o.n).ThenByDescending(p => p.o.r.Cells.Count).ThenBy(p => p.i).Select(p => p.o))
        {
            var g = Shape(r);
            if ((g.X1 - g.X0) * px < (n != 0 ? 60 : 34)) continue;
            var p = Camera.WorldToScreen(g.Label.X * T, g.Label.Y * T);
            var box = DistrictBox(r, n, p.X, p.Y);
            bool Hits(Rect b) => used.Any(u => b.X < u.Box.X + u.Box.Width + 4 && b.X + b.Width + 4 > u.Box.X && b.Y < u.Box.Y + u.Box.Height + 3 && b.Y + b.Height + 3 > u.Box.Y);
            if (Hits(box.Box))
            {
                var a0 = Camera.WorldToScreen(g.X0 * T, g.Y0 * T);
                var a1 = Camera.WorldToScreen(g.X1 * T, g.Y1 * T);
                double step = box.Box.Height + 6;
                (Rect Box, LegacyCityRegion? Region, int Level, string Name)? found = null;
                foreach (var (dx, dy) in new (double, double)[] { (0, 1), (0, 2), (-.5, 1), (.5, 1), (-.5, 2), (.5, 2), (0, 3) })
                {
                    var bx = DistrictBox(r, n, p.X + dx * box.Box.Width, p.Y + dy * step);
                    if (bx.Box.X < a0.X - 4 || bx.Box.X + bx.Box.Width > a1.X + 4 || bx.Box.Y + bx.Box.Height > a1.Y) continue;
                    if (Hits(bx.Box)) continue;
                    found = bx;
                    break;
                }
                if (found is null) continue;
                box = found.Value;
            }
            used.Add(box);
        }
        return used;
    }

    private void DrawDistrictPill(DrawingContext context, (Rect Box, LegacyCityRegion? Region, int Level, string Name) d)
    {
        if (d.Region is null) return;
        var r = d.Region;
        int n = d.Level;
        var box = d.Box;
        double y = box.Y + box.Height / 2;
        context.DrawRectangle(new SolidColorBrush(n != 0 ? Color.FromArgb(219, 20, 22, 27) : Color.FromArgb(235, 12, 13, 16)),
            new Pen(UrbeTheme.Brush(r.Color, n != 0 ? .7 : .95), n != 0 ? 1.5 : 2), box, box.Height / 2, box.Height / 2);
        if (n != 0) context.DrawEllipse(UrbeTheme.Brush(r.Color), null, new Point(box.X + 12, y), 3.5, 3.5);
        else context.FillRectangle(UrbeTheme.Brush(r.Color), new Rect(box.X + 10, y - 4.5, 9, 9));
        var ft = Text(d.Name, n != 0 ? 11 : 13, n != 0 ? FontWeight.SemiBold : FontWeight.Bold, UrbeTheme.Brush(n != 0 ? "#dfe3ea" : "#ffffff"));
        context.DrawText(ft, new Point(box.X + (n != 0 ? 21 : 25), y + .5 - ft.Height / 2));
    }

    // ================================================================ chunks (off the UI thread)

    private void RequestChunks(List<(int X, int Y)> missing)
    {
        if (World is null || missing.Count == 0) return;
        var centre = Camera.ScreenToWorld(new Point(Bounds.Width / 2, Bounds.Height / 2));
        double ccx = centre.X / T / LegacyWorld.ChunkTiles, ccy = centre.Y / T / LegacyWorld.ChunkTiles;
        // app.js: closest to the centre of the screen first; at most 6 requests in flight
        foreach (var key in missing.OrderBy(m => LegacyJsMath.Hypot(m.X + .5 - ccx, m.Y + .5 - ccy)))
        {
            if (_pending.Count >= 6) break;
            if (!_pending.Add(key)) continue;
            var world = World;
            var generation = _worldGeneration;
            Task.Run(() => GenerateChunk(world, key.X, key.Y)).ContinueWith(task =>
                Dispatcher.UIThread.Post(() => AcceptChunk(key, generation, task)), TaskScheduler.Default);
        }
    }

    private static (byte[] Near, byte[] Far, (int, int, LegacyVegetation.Tree)[] Trees) GenerateChunk(LegacyWorld world, int cx, int cy)
    {
        var near = LegacyChunkPixels.Render(world.At, world.DecorAt, cx, cy);
        var far = LegacyChunkFarPixels.Render(near, world.TreeAt, cx, cy);
        var trees = new List<(int, int, LegacyVegetation.Tree)>();
        const int ch = LegacyWorld.ChunkTiles;
        for (int j = 0; j < ch; j++)
        for (int i = 0; i < ch; i++)
        {
            int x = cx * ch + i, y = cy * ch + j;
            if (world.TreeAt(x, y) is { } tree) trees.Add((x, y, tree));
        }
        return (near, far, trees.ToArray());
    }

    private void AcceptChunk((int, int) key, int generation, Task<(byte[] Near, byte[] Far, (int, int, LegacyVegetation.Tree)[] Trees)> task)
    {
        if (generation != _worldGeneration) return;
        _pending.Remove(key);
        if (task.IsCompletedSuccessfully && !_chunks.ContainsKey(key))
        {
            var (near, far, trees) = task.Result;
            _chunks[key] = new ChunkImage(Pixels.ToBitmap(near, LegacyChunkPixels.Side, LegacyChunkPixels.Side),
                Pixels.ToBitmap(far, LegacyChunkFarPixels.Side, LegacyChunkFarPixels.Side), trees);
            _chunkOrder.Enqueue(key);
            // app.js evicts the first inserted chunk (urbeChunks.keys().next())
            while (_chunks.Count > MaxChunks && _chunkOrder.TryDequeue(out var old))
                if (_chunks.Remove(old, out var gone)) { gone.Near.Dispose(); gone.Far.Dispose(); }
        }
        InvalidateVisual();
    }

    public int ReadyChunks => _chunks.Count;
    public int PendingChunks => _pending.Count;

    // ================================================================ input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        _pointers[e.Pointer] = p;
        e.Pointer.Capture(this);
        if (_pointers.Count == 1) { _pressAt = p; _dragged = false; }
        else { _pressAt = null; _pinchDistance = PinchDistance(); }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointers.TryGetValue(e.Pointer, out var last)) return;
        var p = e.GetPosition(this);
        _pointers[e.Pointer] = p;
        if (_pointers.Count >= 2)
        {
            double d = PinchDistance();
            if (_pinchDistance > 0 && d > 0) Camera.ZoomAt(PinchCentre(), d / _pinchDistance);
            _pinchDistance = d;
        }
        else
        {
            if (_pressAt is { } start && Distance(start, p) > TapSlop) _dragged = true;
            if (_dragged) Camera.Pan(p - last);
        }
        InvalidateVisual();
        CameraChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pointers.Remove(e.Pointer);
        e.Pointer.Capture(null);
        if (_pointers.Count == 0 && _pressAt is { } start && !_dragged && City is not null)
        {
            var (tx, ty) = Camera.TileAt(start);
            Select(City.BuildingAt(tx, ty));
        }
        if (_pointers.Count < 2) _pinchDistance = 0;
        if (_pointers.Count == 0) _pressAt = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pointers.Remove(e.Pointer);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Camera.ZoomAt(e.GetPosition(this), Math.Pow(1.12, e.Delta.Y));
        InvalidateVisual();
        CameraChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private double PinchDistance()
    {
        if (_pointers.Count < 2) return 0;
        return Distance(_pointers.Values.ElementAt(0), _pointers.Values.ElementAt(1));
    }

    private Point PinchCentre()
    {
        var a = _pointers.Values.ElementAt(0);
        var b = _pointers.Values.ElementAt(1);
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static int FloorDiv(int v, int d) => (int)Math.Floor(v / (double)d);
}

/// <summary>Biome names shown in the top pill (terrain.js INFO[].nome).</summary>
public static class BiomeNames
{
    private static readonly string[] Names =
    [
        "Mar profundo", "Mar", "Rio", "Lago", "Praia", "Campo", "Prado florido", "Floresta",
        "Mata fechada", "Pântano", "Taiga", "Tundra", "Neve eterna", "Colinas", "Montanha",
        "Pico", "Deserto", "Savana", "Estepe"
    ];

    public static string Of(LegacyBiome biome) => Names[(int)biome];
}
