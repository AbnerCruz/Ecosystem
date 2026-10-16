using Avalonia;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// A modal tool of the city canvas (app.js tool = 'region' / 'house' + v21Placement): while it is
/// active a one-finger drag does not pan the camera, it drives the tool. Pinch and wheel still zoom.
/// </summary>
public abstract class MapTool
{
    public abstract void Down(int tx, int ty);
    public abstract void Move(int tx, int ty);
    public abstract void Up(int tx, int ty);
    /// <summary>A second finger arrived (pinch) or the gesture was lost: forget the draft.</summary>
    public virtual void Cancel() { }
    public abstract void Render(DrawingContext context, WorldView view);
}

/// <summary>tool 'region': drag a rectangle (regionDraft), reported when the finger lifts.</summary>
public sealed class RegionDrawTool(Action<int, int, int, int> finished) : MapTool
{
    private (int X, int Y)? _a, _b;

    public (int X, int Y, int W, int H)? Draft => _a is { } a && _b is { } b
        ? (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1) : null;

    public override void Down(int tx, int ty) { _a = _b = (tx, ty); }
    public override void Move(int tx, int ty) { if (_a is not null) _b = (tx, ty); }
    public override void Up(int tx, int ty)
    {
        if (_a is null) return;
        _b = (tx, ty);
        var d = Draft!.Value;
        _a = _b = null;
        finished(d.X, d.Y, d.W, d.H);
    }
    public override void Cancel() { _a = _b = null; }

    public override void Render(DrawingContext context, WorldView view)
    {
        if (Draft is not { } d) return;
        var p = view.Camera.WorldToScreen(d.X * LegacyWorld.Tile, d.Y * LegacyWorld.Tile);
        double z = view.Camera.Zoom, size = LegacyWorld.Tile * z;
        var pen = new Pen(UrbeTheme.Brush("#9fe4ff"), 1, new DashStyle([6, 4], 0));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x22, 0x61, 0xc8, 0xff)), pen, new Rect(p.X, p.Y, d.W * size, d.H * size));
    }
}

/// <summary>v21Placement 'new': a 3×3 ghost house follows the finger; the candidate tile is read by the dock.</summary>
public sealed class PlacementTool(Func<int, int, bool> valid, Action<(int X, int Y)?> changed) : MapTool
{
    public (int X, int Y)? Candidate { get; private set; }

    /// <summary>The trade of the ghost (the type chosen in "+" → Construção; the 1.8.4 always shows the house).</summary>
    public string GhostKind { get; init; } = "house";

    private void Set(int tx, int ty) { Candidate = (tx, ty); changed(Candidate); }
    public override void Down(int tx, int ty) => Set(tx, ty);
    public override void Move(int tx, int ty) => Set(tx, ty);
    public override void Up(int tx, int ty) => Set(tx, ty);

    public override void Render(DrawingContext context, WorldView view)
    {
        if (Candidate is not { } c) return;
        var p = view.Camera.WorldToScreen(c.X * LegacyWorld.Tile, c.Y * LegacyWorld.Tile);
        double z = view.Camera.Zoom, size = LegacyWorld.Tile * z;
        bool ok = valid(c.X, c.Y);
        if (view.GhostArt(c.X, c.Y, GhostKind) is { } art)
        {
            double s = LegacyWorld.Tile * z / LegacyWorld.TilePixels, w = 48 * s, h = 56 * s;
            using (context.PushOpacity(.62))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode.None }))
                context.DrawImage(art, new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                    new Rect(Math.Round(p.X), Math.Round(p.Y + 3 * size - h), Math.Ceiling(w), Math.Ceiling(h)));
        }
        context.DrawRectangle(null, new Pen(UrbeTheme.Brush(ok ? "#7ef29a" : "#ef766f"), 3), new Rect(p.X, p.Y, 3 * size, 3 * size));
    }
}

/// <summary>
/// Placing a decoration (DEC-0045, UC-41): its sprite follows the finger, half transparent, framed in green where it fits
/// and in red where it does not (water, a building, a street or another decoration). UC-53: it carries the look to place
/// (variation, mirrored, turned) and has a brush mode, where a drag paints one per tile along the stroke and the host is
/// told the stroke when the finger lifts.
/// </summary>
public sealed class DecorationTool(WorldDecorations.Kind kind, Func<int, int, bool> valid, Action<(int X, int Y)?> changed) : MapTool
{
    private readonly List<(int X, int Y)> _stroke = [];

    public WorldDecorations.Kind Kind { get; } = kind;
    public (int X, int Y)? Candidate { get; private set; }
    public int Variant { get; set; }
    public bool Flipped { get; set; }
    public bool Turned { get; set; }

    /// <summary>Brush mode: a drag paints along its stroke instead of moving one ghost.</summary>
    public bool Brush { get; set; }
    /// <summary>The finished stroke of the brush (the tiles the finger crossed, in order).</summary>
    public Action<IReadOnlyList<(int X, int Y)>>? Painted { get; init; }
    /// <summary>The tiles of the stroke being drawn.</summary>
    public IReadOnlyList<(int X, int Y)> Stroke => _stroke;

    private void Set(int tx, int ty) { Candidate = (tx, ty); changed(Candidate); }
    /// <summary>No ghost until the next tap (after one was placed).</summary>
    public void Clear() => Candidate = null;

    private void Extend(int tx, int ty)
    {
        if (_stroke.Count == 0) { _stroke.Add((tx, ty)); return; }
        var (lx, ly) = _stroke[^1];
        if ((lx, ly) == (tx, ty)) return;
        _stroke.AddRange(WorldDecorations.Line(lx, ly, tx, ty).Skip(1));
    }

    public override void Down(int tx, int ty)
    {
        if (!Brush) { Set(tx, ty); return; }
        _stroke.Clear();
        Candidate = null;
        Extend(tx, ty);
    }

    public override void Move(int tx, int ty) { if (Brush) Extend(tx, ty); else Set(tx, ty); }

    public override void Up(int tx, int ty)
    {
        if (!Brush) { Set(tx, ty); return; }
        if (_stroke.Count == 0) return;
        Extend(tx, ty);
        var stroke = _stroke.ToList();
        _stroke.Clear();
        Painted?.Invoke(stroke);
    }

    public override void Cancel() => _stroke.Clear();

    /// <summary>The stroke's tiles that will take a decoration: valid and not covered by an earlier one of the stroke.</summary>
    public IEnumerable<(int X, int Y)> Planned()
    {
        var taken = new HashSet<(int, int)>();
        foreach (var t in _stroke.Distinct())
        {
            var tiles = WorldDecorations.Footprint(Kind, t.X, t.Y).ToList();
            if (!valid(t.X, t.Y) || tiles.Any(taken.Contains)) continue;
            foreach (var c in tiles) taken.Add(c);
            yield return t;
        }
    }

    public override void Render(DrawingContext context, WorldView view)
    {
        var art = view.DecorationArt(Kind.Id, Variant, Flipped, Turned);
        void Ghost(int x, int y, bool ok)
        {
            var rect = view.DecorationRect(Kind, x, y);
            if (art is not null)
                using (context.PushOpacity(.7))
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode.None }))
                    context.DrawImage(art, new Rect(0, 0, art.Size.Width, art.Size.Height), rect);
            context.DrawRectangle(null, new Pen(UrbeTheme.Brush(ok ? "#7ef29a" : "#ef766f"), 3), rect);
        }
        if (Brush) { foreach (var (x, y) in Planned()) Ghost(x, y, true); return; }
        if (Candidate is { } c) Ghost(c.X, c.Y, valid(c.X, c.Y));
    }
}

/// <summary>
/// v20RegionShape: a neighbourhood being reshaped. Its rectangle is drawn dashed with eight handles
/// (v20HandlePoints); dragging a handle moves that side or corner (v20ResizeDraft, at least 3×3). The new shape is
/// applied by the dock's Aplicar (v20SairShape(true)), with the owner's priority mask.
/// </summary>
public sealed class RegionShapeTool(LegacyCityRegion region, (int X, int Y, int W, int H) start, Func<double> tilePixels, Action changed) : MapTool
{
    public static readonly string[] Handles = ["nw", "n", "ne", "e", "se", "s", "sw", "w"];

    public LegacyCityRegion Region { get; } = region;
    public (int X, int Y, int W, int H) Start { get; } = start;
    public (int X, int Y, int W, int H) Draft { get; private set; } = start;
    public bool Changed => Draft != Start;

    private string? _handle;
    private (int X, int Y, int W, int H) _base;

    /// <summary>A handle's place in tiles (corners on the rectangle's edges, sides in their middle).</summary>
    public static (double X, double Y) HandleAt((int X, int Y, int W, int H) b, string k) => k switch
    {
        "nw" => (b.X, b.Y), "n" => (b.X + b.W / 2.0, b.Y), "ne" => (b.X + b.W, b.Y), "e" => (b.X + b.W, b.Y + b.H / 2.0),
        "se" => (b.X + b.W, b.Y + b.H), "s" => (b.X + b.W / 2.0, b.Y + b.H), "sw" => (b.X, b.Y + b.H), _ => (b.X, b.Y + b.H / 2.0)
    };

    public override void Down(int tx, int ty)
    {
        // v20HandleAt: within 18 px of a handle (at least the tile itself)
        double reach = Math.Max(1, 18 / Math.Max(1e-6, tilePixels()));
        _handle = null;
        double best = double.MaxValue;
        foreach (var k in Handles)
        {
            var (hx, hy) = HandleAt(Draft, k);
            double d = Math.Max(Math.Abs(tx + .5 - hx), Math.Abs(ty + .5 - hy));
            if (d <= reach + .5 && d < best) { best = d; _handle = k; }
        }
        _base = Draft;
    }

    public override void Move(int tx, int ty)
    {
        if (_handle is null) return;
        Draft = LegacyCity.ResizeDraft(_base, _handle, tx, ty);
        changed();
    }

    public override void Up(int tx, int ty)
    {
        Move(tx, ty);
        _handle = null;
    }

    public override void Cancel() => _handle = null;

    public override void Render(DrawingContext context, WorldView view)
    {
        var b = Draft;
        var p = view.Camera.WorldToScreen(b.X * LegacyWorld.Tile, b.Y * LegacyWorld.Tile);
        double size = LegacyWorld.Tile * view.Camera.Zoom;
        var line = UrbeTheme.Brush("#b9ecff");
        context.DrawRectangle(null, new Pen(line, 2, new DashStyle([7, 4], 0)), new Rect(p.X, p.Y, b.W * size, b.H * size));
        double radius = Math.Max(6, 7 * view.Camera.Zoom);
        foreach (var k in Handles)
        {
            var (hx, hy) = HandleAt(b, k);
            var q = view.Camera.WorldToScreen(hx * LegacyWorld.Tile, hy * LegacyWorld.Tile);
            context.DrawEllipse(UrbeTheme.Brush("#0d1b24"), new Pen(line, 2), q, radius, radius);
        }
    }
}
