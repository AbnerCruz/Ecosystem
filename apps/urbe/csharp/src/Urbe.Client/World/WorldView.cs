using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Urbe.Client.City;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// The city in full screen (1.8.4 #game canvas), drawn natively: ground chunks,
/// trees, neighbourhoods, houses and labels, with drag/pinch/wheel camera and tap
/// selection. Terrain and art come from Urbe.Core; this control only draws.
/// </summary>
public sealed class WorldView : Control
{
    private const int MaxChunks = 160;       // app.js URBE_CHUNKS_MAX order of magnitude
    private const double TapSlop = 8;

    private readonly Dictionary<(int, int), ChunkImage> _chunks = [];
    private readonly Queue<(int, int)> _chunkOrder = new();
    private readonly HashSet<(int, int)> _pending = [];
    private readonly SpriteCache _sprites = new();
    private readonly Dictionary<IPointer, Point> _pointers = [];
    private Point? _pressAt;
    private bool _dragged;
    private double _pinchDistance;
    private CityModel _city = new();

    private sealed record ChunkImage(Bitmap Ground, (int X, int Y, LegacyVegetation.Tree Tree)[] Trees);

    public WorldView()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public LegacyWorld? World { get; private set; }
    public Camera Camera { get; } = new();
    public CityHouse? Selected { get; private set; }

    /// <summary>Raised when a tap selects a house or clears the selection.</summary>
    public event EventHandler<CityHouse?>? SelectionChanged;
    /// <summary>Raised when the camera moves (HUD/biome pill refresh).</summary>
    public event EventHandler? CameraChanged;

    public void Load(LegacyWorld world, CityModel city)
    {
        World = world;
        _city = city;
        InvalidateVisual();
    }

    public string BiomeNameAtCentre()
    {
        if (World is null) return "";
        var t = World.At((int)Math.Floor(Camera.X / LegacyWorld.Tile), (int)Math.Floor(Camera.Y / LegacyWorld.Tile));
        return BiomeNames.Of(t.Biome);
    }

    public void Select(CityHouse? house)
    {
        Selected = house;
        InvalidateVisual();
        SelectionChanged?.Invoke(this, house);
    }

    public void CenterOn(CityHouse house)
    {
        Camera.X = (house.X + house.W / 2.0) * LegacyWorld.Tile;
        Camera.Y = (house.Y + house.H / 2.0) * LegacyWorld.Tile;
        InvalidateVisual();
        CameraChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------------- drawing ----------------

    public override void Render(DrawingContext context)
    {
        Camera.Viewport = Bounds.Size;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#23466f")), new Rect(Bounds.Size));
        if (World is null) return;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            var missing = DrawGround(context);
            DrawDistricts(context);
            DrawTrees(context);
            DrawHouses(context);
            RequestChunks(missing);
        }
    }

    private List<(int, int)> DrawGround(DrawingContext context)
    {
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        const int ch = LegacyWorld.ChunkTiles;
        double side = ch * LegacyWorld.Tile * Camera.Zoom;
        var waiting = new SolidColorBrush(Color.Parse("#3d5a36"));
        var missing = new List<(int, int)>();
        for (int cy = FloorDiv(y0, ch); cy <= FloorDiv(y1, ch); cy++)
        for (int cx = FloorDiv(x0, ch); cx <= FloorDiv(x1, ch); cx++)
        {
            var p = Camera.WorldToScreen(cx * ch * LegacyWorld.Tile, cy * ch * LegacyWorld.Tile);
            var dest = new Rect(Math.Floor(p.X), Math.Floor(p.Y), Math.Ceiling(side) + 1, Math.Ceiling(side) + 1);
            if (_chunks.TryGetValue((cx, cy), out var chunk))
                context.DrawImage(chunk.Ground, new Rect(0, 0, ChunkGround.Side, ChunkGround.Side), dest);
            else
            {
                context.FillRectangle(waiting, dest);
                missing.Add((cx, cy));
            }
        }
        return missing;
    }

    private void DrawDistricts(DrawingContext context)
    {
        double z = Camera.Zoom;
        var fill = new SolidColorBrush(Color.Parse("#8fd0ff"), .10);
        var line = new Pen(new SolidColorBrush(Color.Parse("#8fd0ff"), .85), Math.Max(1.5, 2 * z));
        foreach (var d in _city.Districts)
        {
            var a = Camera.WorldToScreen(d.X * LegacyWorld.Tile, d.Y * LegacyWorld.Tile);
            var r = new Rect(a.X, a.Y, d.W * LegacyWorld.Tile * z, d.H * LegacyWorld.Tile * z);
            if (!r.Intersects(new Rect(Bounds.Size))) continue;
            context.DrawRectangle(fill, line, r, 18 * z, 18 * z);
        }
        if (z < .42) return;
        foreach (var d in _city.Districts)
        {
            var top = Camera.WorldToScreen((d.X + d.W / 2.0) * LegacyWorld.Tile, d.Y * LegacyWorld.Tile);
            Label(context, d.Name, top.X, top.Y - 11, district: true);
        }
    }

    private void DrawTrees(DrawingContext context)
    {
        double z = Camera.Zoom;
        if (z < .3) return;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        const int ch = LegacyWorld.ChunkTiles;
        double s = LegacyWorld.Tile * z / LegacyWorld.TilePixels * 1.3;
        var visible = new List<(int X, int Y, LegacyVegetation.Tree Tree)>();
        for (int cy = FloorDiv(y0, ch); cy <= FloorDiv(y1, ch); cy++)
        for (int cx = FloorDiv(x0, ch); cx <= FloorDiv(x1, ch); cx++)
        {
            if (!_chunks.TryGetValue((cx, cy), out var chunk)) continue; // trees arrive with the ground
            foreach (var t in chunk.Trees)
            {
                if (t.X < x0 || t.X > x1 || t.Y < y0 || t.Y > y1 + 2) continue;
                if (_city.Occupied(t.X, t.Y) || InDistrict(t.X, t.Y)) continue;
                visible.Add(t);
            }
        }
        // app.js: back to front, the southern tree covers the one behind it.
        visible.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        foreach (var (tx, ty, tree) in visible)
        {
            var q = Camera.WorldToScreen((tx + .5) * LegacyWorld.Tile, (ty + 1) * LegacyWorld.Tile);
            double jx = (LegacyWorld.Hash(tx, ty, 31) - .5) * 8 * s, jy = (LegacyWorld.Hash(tx, ty, 32) - .5) * 5 * s;
            var img = _sprites.Tree(tree.Kind, tree.Variant, tree.Snow);
            context.DrawImage(img, new Rect(0, 0, LegacyWorldSprites.TreeWidth, LegacyWorldSprites.TreeHeight),
                new Rect(Math.Round(q.X - 12 * s + jx), Math.Round(q.Y - 30 * s + jy), Math.Ceiling(24 * s), Math.Ceiling(32 * s)));
        }
    }

    private void DrawHouses(DrawingContext context)
    {
        if (World is null) return;
        double z = Camera.Zoom, s = LegacyWorld.Tile * z / LegacyWorld.TilePixels;
        var view = new Rect(Bounds.Size).Inflate(80 * z);
        foreach (var b in _city.Houses.OrderBy(h => h.Y))
        {
            var p = Camera.WorldToScreen(b.X * LegacyWorld.Tile, b.Y * LegacyWorld.Tile);
            if (!view.Contains(p)) continue;
            if (b == Selected)
            {
                var sp = Camera.WorldToScreen((b.X - .15) * LegacyWorld.Tile, (b.Y - .45) * LegacyWorld.Tile);
                var halo = new Rect(sp.X, sp.Y, (b.W + .3) * LegacyWorld.Tile * z, (b.H + .6) * LegacyWorld.Tile * z);
                context.DrawRectangle(new SolidColorBrush(Color.Parse("#8fb3ff"), .16),
                    new Pen(new SolidColorBrush(Color.Parse("#8fb3ff")), 2), halo, 10 * z, 10 * z);
            }
            var (kind, style, variant, flowers) = CityModel.Art(b, World);
            var img = _sprites.Building(kind, style, variant, flowers);
            double w = 48 * s * (b.W / 3.0), h = 56 * s * (b.H / 3.0);
            context.DrawImage(img, new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                new Rect(Math.Round(p.X), Math.Round(p.Y + b.H * LegacyWorld.Tile * z - h), Math.Ceiling(w), Math.Ceiling(h)));
        }
        if (z < .42) return;
        foreach (var b in _city.Houses)
        {
            var q = Camera.WorldToScreen((b.X + b.W / 2.0) * LegacyWorld.Tile, (b.Y + b.H) * LegacyWorld.Tile);
            if (!view.Contains(q)) continue;
            Label(context, b.Note.Name, q.X, q.Y + 4 * z, selected: b == Selected);
        }
    }

    private void Label(DrawingContext context, string text, double centreX, double top,
        bool selected = false, bool district = false)
    {
        if (text.Length > 34) text = text[..31] + "…";
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            UrbeTheme.UiTypeface(FontWeight.SemiBold), 11.5, selected ? UrbeTheme.Brush("#0b1325") : UrbeTheme.Brush("#eef5f8"));
        double dot = district ? 12 : 0, padX = 8, h = ft.Height + 6;
        var r = new Rect(centreX - (ft.Width + dot) / 2 - padX, top, ft.Width + dot + padX * 2, h);
        context.DrawRectangle(selected ? UrbeTheme.Brush("#8fb3ff") : new SolidColorBrush(Color.Parse("#0e151b"), .86),
            null, r, h / 2, h / 2);
        if (district)
            context.DrawEllipse(UrbeTheme.Brush("#5aa9ff"), null, new Point(r.X + padX + 3, r.Y + h / 2), 3.5, 3.5);
        context.DrawText(ft, new Point(r.X + padX + dot, r.Y + 3));
    }

    private bool InDistrict(int x, int y)
    {
        foreach (var d in _city.Districts)
            if (x >= d.X && x < d.X + d.W && y >= d.Y && y < d.Y + d.H) return true;
        return false;
    }

    // ---------------- chunk generation (off the UI thread) ----------------

    private void RequestChunks(List<(int X, int Y)> missing)
    {
        if (World is null || missing.Count == 0) return;
        var centre = Camera.ScreenToWorld(new Point(Bounds.Width / 2, Bounds.Height / 2));
        double ccx = centre.X / LegacyWorld.Tile / LegacyWorld.ChunkTiles, ccy = centre.Y / LegacyWorld.Tile / LegacyWorld.ChunkTiles;
        // app.js: what is closest to the centre of the screen comes first; at most a few in flight.
        foreach (var key in missing.OrderBy(m => Math.Pow(m.X + .5 - ccx, 2) + Math.Pow(m.Y + .5 - ccy, 2)))
        {
            if (_pending.Count >= Math.Max(2, Environment.ProcessorCount - 1)) break;
            if (!_pending.Add(key)) continue;
            var world = World;
            Task.Run(() => GenerateChunk(world, key.X, key.Y)).ContinueWith(task =>
                Dispatcher.UIThread.Post(() => AcceptChunk(key, task)), TaskScheduler.Default);
        }
    }

    private static (byte[] Rgba, (int, int, LegacyVegetation.Tree)[] Trees) GenerateChunk(LegacyWorld world, int cx, int cy)
    {
        var rgba = ChunkGround.Paint(world, cx, cy);
        var trees = new List<(int, int, LegacyVegetation.Tree)>();
        const int ch = LegacyWorld.ChunkTiles;
        for (int j = 0; j < ch; j++)
        for (int i = 0; i < ch; i++)
        {
            int x = cx * ch + i, y = cy * ch + j;
            if (world.TreeAt(x, y) is { } tree) trees.Add((x, y, tree));
        }
        return (rgba, trees.ToArray());
    }

    private void AcceptChunk((int, int) key, Task<(byte[] Rgba, (int, int, LegacyVegetation.Tree)[] Trees)> task)
    {
        _pending.Remove(key);
        if (task.IsCompletedSuccessfully && !_chunks.ContainsKey(key))
        {
            var (rgba, trees) = task.Result;
            _chunks[key] = new ChunkImage(Pixels.ToBitmap(rgba, ChunkGround.Side, ChunkGround.Side), trees);
            _chunkOrder.Enqueue(key);
            // terrain.js/app.js evict the first inserted chunk, not LRU.
            while (_chunks.Count > MaxChunks && _chunkOrder.TryDequeue(out var old))
                if (_chunks.Remove(old, out var gone)) gone.Ground.Dispose();
        }
        InvalidateVisual();
    }

    /// <summary>Number of chunks drawn from generated pixels (diagnostics/tests).</summary>
    public int ReadyChunks => _chunks.Count;
    public int PendingChunks => _pending.Count;

    // ---------------- input ----------------

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
        if (_pointers.Count == 0 && _pressAt is { } start && !_dragged)
        {
            var (tx, ty) = Camera.TileAt(start);
            Select(_city.HouseAt(tx, ty));
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
        var a = _pointers.Values.ElementAt(0);
        var b = _pointers.Values.ElementAt(1);
        return Distance(a, b);
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
