using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    /// <summary>v21MoveHold: holding a building 430 ms picks it up (gesture.mode = 'moveBuilding').</summary>
    public const int MoveHoldMs = 430;
    private readonly DispatcherTimer _moveHold = new() { Interval = TimeSpan.FromMilliseconds(MoveHoldMs) };
    private LegacyCityBuilding? _pressBuilding;
    private (int X, int Y) _moveOffset;

    /// <summary>The building being carried by the finger, or null.</summary>
    public LegacyCityBuilding? Moving { get; private set; }

    /// <summary>Where the carried building would land (gesture.dragPos).</summary>
    public (int X, int Y) MovePos { get; private set; }

    /// <summary>validarPosicaoMovel for the drawing of the carried building (green or red frame).</summary>
    public Func<LegacyCityBuilding, int, int, bool>? MoveValid { get; set; }

    /// <summary>The carried building was let go at a new lot (finalizarMovimentoMundo).</summary>
    public event Action<LegacyCityBuilding, int, int>? BuildingMoveRequested;

    /// <summary>The hold that picks a building up (v21MoveHold), from tile (tx, ty) on it.</summary>
    public void PickUp(LegacyCityBuilding b, int tx, int ty)
    {
        Moving = b;
        _moveOffset = (tx - b.X, ty - b.Y);
        MovePos = (b.X, b.Y);
        InvalidateVisual();
    }

    /// <summary>The carried building follows the finger to tile (tx, ty).</summary>
    public void Carry(int tx, int ty)
    {
        if (Moving is null) return;
        MovePos = (tx - _moveOffset.X, ty - _moveOffset.Y);
        InvalidateVisual();
    }

    /// <summary>Lets the carried building go: a new lot asks to move it there.</summary>
    public void Drop()
    {
        if (Moving is not { } b) return;
        Moving = null;
        var (x, y) = MovePos;
        InvalidateVisual();
        // let go where it was: the hold was only a long tap, which selects it
        if (x == b.X && y == b.Y) { Select(b); return; }
        Select(null);
        BuildingMoveRequested?.Invoke(b, x, y);
    }
    private bool _dragged;
    private double _pinchDistance;
    private int _worldGeneration;
    private readonly LegacyFauna _fauna = new();
    private readonly LegacyWorldLife _life;
    private readonly LegacyVillagers _villagers = new();
    private readonly TownTrades _trades = new();
    // app.js v25Ciclo every V25_FPS (33 ms)
    private readonly DispatcherTimer _villagerTimer = new() { Interval = TimeSpan.FromMilliseconds(LegacyVillagers.FrameMs) };
    private readonly LifeRenderer _lifeArt = new();
    private readonly CityLifeHost _lifeHost;
    // life.js tick every 33 ms and the app.js fauna ciclo every 40 ms (scheduler intervals)
    private readonly DispatcherTimer _lifeTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _faunaTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _lastTick, _lagAverage = 33, _lagT, _faunaMs, _faunaLast, _villagerMs = 3000, _villagerLast;
    private Bitmap? _lightTint;
    private int _lightKey = -1;


    private sealed record ChunkImage(Bitmap Near, Bitmap Far, (int X, int Y, LegacyVegetation.Tree Tree)[] Trees);

    public WorldView()
    {
        ClipToBounds = true;
        Focusable = true;
        _lifeHost = new CityLifeHost(this);
        _life = new LegacyWorldLife(_lifeHost, _fauna);
        _life.Notice += text => Notice?.Invoke(this, text);
        _lifeTimer.Tick += (_, _) => LifeTick();
        _faunaTimer.Tick += (_, _) => FaunaTick();
        _villagerTimer.Tick += (_, _) => VillagerTick();
        _moveHold.Tick += (_, _) =>
        {
            _moveHold.Stop();
            if (_pointers.Count == 1 && !_dragged && Tool is null && _pressBuilding is { } b && _pressAt is { } at)
            {
                var (tx, ty) = Camera.TileAt(at);
                PickUp(b, tx, ty);
            }
        };
        RefreshLighting();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshLighting();
        _lastTick = 0;
        _lifeTimer.Start();
        _faunaTimer.Start();
        _villagerTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _lifeTimer.Stop();
        _faunaTimer.Stop();
        _villagerTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The 'ambiente' modes of the 1.8.4 (Personalização → Luz): ciclo, dia, entardecer, noite, auto.</summary>
    public string LightingMode
    {
        get => _life.TimeMode;
        set
        {
            if (value is not ("auto" or "ciclo" or "dia" or "entardecer" or "noite"))
                throw new ArgumentOutOfRangeException(nameof(value));
            _life.TimeMode = value;
            RefreshLighting();
        }
    }

    public LegacyWorldLife.Light CurrentLight => _life.CurrentLight;
    /// <summary>The life of the world (weather, water, animals, events, lamps).</summary>
    public LegacyWorldLife Life => _life;
    public LegacyFauna Fauna => _fauna;
    /// <summary>v25Povo: who walks between linked notes and who idles at the door.</summary>
    public LegacyVillagers Villagers => _villagers;

    /// <summary>The residents with trades at the well, the field and the woodpile (DEC-0045, UC-42).</summary>
    public TownTrades Trades => _trades;
    /// <summary>The discreet event notice of life.js avisar() ("🌧 Chuva passageira", …).</summary>
    public event EventHandler<string>? Notice;
    /// <summary>v25MapaVisivel(): false while the editor, explorer or a menu covers the city.</summary>
    public Func<bool> CityVisible { get; set; } = () => true;

    /// <summary>The camera as life.js view() sees it.</summary>
    public LegacyLifeView LifeView()
    {
        Camera.Viewport = Bounds.Size;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        return new LegacyLifeView(Camera.X, Camera.Y, Camera.Zoom, Bounds.Width, Bounds.Height, x0, y0, x1, y1);
    }

    /// <summary>life.js tick(): adaptive quality, light, then one simulation step.</summary>
    public void LifeTick()
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        if (!CityVisible() || World is null || City is null || Bounds.Width <= 0) { _lastTick = 0; return; }
        if (_lastTick == 0) _lastTick = now;
        double dt = Math.Min(.1, (now - _lastTick) / 1000), interval = now - _lastTick;
        _lastTick = now;
        _lagAverage = _lagAverage * .92 + interval * .08;
        _lagT += dt;
        if (_lagT > 2)
        {
            _lagT = 0;
            if (_lagAverage > 58) _life.Quality = Math.Max(.35, _life.Quality - .15);
            else if (_lagAverage < 40) _life.Quality = Math.Min(1, _life.Quality + .05);
        }
        _life.Tick(dt, LifeView());
        RefreshLighting();
        InvalidateVisual();
    }

    /// <summary>app.js fauna ciclo().</summary>
    public void FaunaTick()
    {
        if (!CityVisible() || World is null || City is null || Bounds.Width <= 0) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        _faunaMs += _faunaLast == 0 ? 0 : now - _faunaLast;
        _faunaLast = now;
        bool had = _fauna.Animals.Count > 0;
        if (_fauna.Tick(1000 + _faunaMs, LifeView(), _life.Options.Fauna, _lifeHost) || had) InvalidateVisual();
    }

    /// <summary>app.js v25Ciclo().</summary>
    public void VillagerTick()
    {
        if (World is null || City is null || Bounds.Width <= 0) return;
        if (!CityVisible()) { _villagers.People.Clear(); _trades.Reset(); _villagerLast = 0; return; }
        double now = _clock.Elapsed.TotalMilliseconds;
        double dt = _villagerLast == 0 ? 0 : now - _villagerLast;
        _villagerMs += dt;
        _villagerLast = now;
        var view = LifeView();
        bool moved = _villagers.Tick(_villagerMs, view, _life.Options.People, _life.Hurry, _lifeHost);
        if (_trades.Tick(Math.Min(.1, dt / 1000), view, _life.Options.People, _life.Hurry, Prefs.Decorations, _lifeHost) || moved) InvalidateVisual();
    }

    /// <summary>Runs the life and the fauna for <paramref name="seconds"/> of simulated time
    /// (33 ms life steps, 40 ms fauna cycles), independent of the wall clock.</summary>
    public void AdvanceLife(double seconds)
    {
        if (World is null || City is null) return;
        var v = LifeView();
        for (double t = 0, nextFauna = 0; t < seconds; t += .033)
        {
            _life.Tick(.033, v);
            _villagerMs += 33;
            _villagers.Tick(_villagerMs, v, _life.Options.People, _life.Hurry, _lifeHost);
            _trades.Tick(.033, v, _life.Options.People, _life.Hurry, Prefs.Decorations, _lifeHost);
            while (nextFauna <= t)
            {
                nextFauna += .04;
                _faunaMs += 40;
                _fauna.Tick(1000 + _faunaMs, v, _life.Options.Fauna, _lifeHost);
            }
        }
        RefreshLighting();
        InvalidateVisual();
    }

    private void RefreshLighting()
    {
        // The light comes from life.js luz: the clock (or the fixed 'ambiente') and the rain.
        _life.UpdateLight();
        var light = _life.CurrentLight;
        int key = (light.Red << 16) | (light.Green << 8) | light.Blue;
        if (key == _lightKey) return;
        _lightKey = key;
        _lightTint?.Dispose();
        _lightTint = key >= 0 && light.Red + light.Green + light.Blue < 762
            ? Pixels.ToBitmap([(byte)light.Red, (byte)light.Green, (byte)light.Blue, 255], 1, 1)
            : null;
        InvalidateVisual();
    }

    private void DrawWorldLighting(DrawingContext context)
    {
        if (_lightTint is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        // app.js: globalCompositeOperation='multiply', full world canvas, BEFORE
        // urbeRotulo/urbePilulaBairro. A 1x1 opaque bitmap is cached until RGB changes.
        using (context.PushRenderOptions(new RenderOptions
               {
                   BitmapBlendingMode = BitmapBlendingMode.Multiply,
                   BitmapInterpolationMode = BitmapInterpolationMode.None
               }))
            context.DrawImage(_lightTint, new Rect(0, 0, 1, 1), new Rect(Bounds.Size));
    }

    public LegacyWorld? World { get; private set; }
    public LegacyCity? City { get; private set; }
    public Camera Camera { get; } = new();
    public LegacyCityBuilding? Selected { get; private set; }
    /// <summary>urbeOpcoes.nomes: the house name signs.</summary>
    public bool ShowHouseNames { get; set; } = true;
    /// <summary>urbeOpcoes.bairros: the neighbourhood name pills.</summary>
    public bool ShowDistrictNames { get; set; } = true;

    /// <summary>Raised when a tap selects a house or clears the selection.</summary>
    public event EventHandler<LegacyCityBuilding?>? SelectionChanged;
    /// <summary>Raised when the camera moves (biome pill refresh).</summary>
    public event EventHandler? CameraChanged;

    private MapTool? _tool;
    private bool _toolGesture;
    /// <summary>The active modal tool (draw a region, place a building); null = the normal pan/select.</summary>
    public MapTool? Tool
    {
        get => _tool;
        set { _tool?.Cancel(); _tool = value; _toolGesture = false; InvalidateVisual(); }
    }

    /// <summary>The house art of the placement ghost (v21 overlay: A.house1 at the candidate tile).</summary>
    public Bitmap? GhostArt(int x, int y, string kind = "house")
    {
        if (World is null) return null;
        if (_ownArt.TryGetValue(kind, out var own)) return own;
        var biome = World.At(x + 1, y + 1).Biome;
        return _sprites.Building(kind, LegacyWorldBuildings.StyleForBiome(LegacyBiomeRules.Id(biome)), 0, false);
    }

    /// <summary>A trade's art in the temperate style (the type picker of "+" → Construção).</summary>
    public Bitmap KindArt(string kind) =>
        _ownArt.TryGetValue(kind, out var own) ? own : _sprites.Building(kind, "temperate", 0, false);

    private LegacyGroundStyle? _ground;
    /// <summary>The ground set by Personalização; null is the original art.</summary>
    public LegacyGroundStyle? Ground => _ground;
    private readonly Dictionary<string, Bitmap> _ownArt = new(StringComparer.Ordinal);

    /// <summary>
    /// world.custom terrain() and sprites(): the ground palette/textures of Personalização and the
    /// pack's own building images. The ground is painted again from scratch.
    /// </summary>
    public void SetGround(LegacyGroundStyle ground, IReadOnlyDictionary<string, LegacyTexturePack.Art> buildings)
    {
        _ground = ReferenceEquals(ground, LegacyGroundStyle.Original) ? null : ground;
        _ownArt.Clear();
        foreach (var (kind, art) in buildings) _ownArt[kind] = Pixels.ToBitmap(art.Rgba, art.Width, art.Height);
        _worldGeneration++;
        foreach (var image in _chunks.Values) { image.Near.Dispose(); image.Far.Dispose(); }
        _chunks.Clear();
        _chunkOrder.Clear();
        _pending.Clear();
        InvalidateVisual();
    }

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
        _fauna.Animals.Clear();
        _villagers.Reset();
        _trades.Reset();
        InvalidateVisual();
    }

    /// <summary>rebuildRoadNetwork just ran: the cached routes go; villagers whose way still exists keep walking (DEC-0045).</summary>
    public void RoadsChanged()
    {
        _villagers.RoadsChanged(_lifeHost);
        InvalidateVisual();
    }

    internal bool ChunkReady(int x, int y) =>
        _chunks.ContainsKey((FloorDiv(x, LegacyWorld.ChunkTiles), FloorDiv(y, LegacyWorld.ChunkTiles)));

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

    /// <summary>The camera the city opened with (abrirCidade: mapa.camera, z clamped).</summary>
    public void ShowCityCamera()
    {
        if (City is null) return;
        Camera.X = City.CameraX;
        Camera.Y = City.CameraY;
        Camera.SetZoom(City.CameraZoom);
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

    /// <summary>The camera on the middle of a neighbourhood (the Explorer's "Mostrar na cidade" of a folder, DEC-0045).</summary>
    public void CenterOnRegion(LegacyCityRegion region)
    {
        Camera.X = (region.X + region.W / 2.0) * T;
        Camera.Y = (region.Y + region.H / 2.0) * T;
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
            DrawDecorations(context);
        var life = LifeView();
        _lifeArt.Ground(context, _life, life);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            DrawTrees(context);
        _lifeArt.Villagers(context, _trades.People.Count == 0 ? _villagers.People : [.. _villagers.People, .. _trades.People], life, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _lifeArt.TradeWork(context, _trades, life);
        _lifeArt.Fauna(context, _fauna, life);
        DrawBuildings(context);
        // app.js urbeAntesDosRotulos: céu, multiply light, lit windows, brilho, then the names
        _lifeArt.Sky(context, _life, _lifeHost.People, life);
        DrawWorldLighting(context);
        var light = _life.CurrentLight;
        if (light.Darkness > .25 && Camera.Zoom >= .3)
            _lifeArt.Windows(context, City.Buildings, life, Math.Min(1, (light.Darkness - .25) / .6), _clock.Elapsed.TotalMilliseconds);
        _lifeArt.Glow(context, _life, _lifeHost.People, life);
        DrawWorldLabels(context);
        DrawRootDistrictName(context);
        RequestChunks(missing);
        Tool?.Render(context, this);
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

    /// <summary>Muralha, contornos (DEC-0045, UC-41): from .urbe/mundo.json; the defaults when there is none.</summary>
    public WorldPrefs Prefs { get; set; } = WorldPrefs.Default;

    /// <summary>
    /// app.js urbeDesenharBairros: fill per level, then outlines (halo on root, dashed from level 2). With DEC-0045 (the
    /// owner found the outlines too strong and asked for walls): a root neighbourhood can be walled in stone, and the
    /// sub-neighbourhoods drawn faint ("suave") or not at all ("oculto"); "forte" is the 1.8.4's drawing.
    /// </summary>
    private void DrawRegions(DrawingContext context)
    {
        var vis = VisibleRegions(1);
        if (vis.Count == 0) return;
        double s = T * Camera.Zoom;
        var o = Camera.WorldToScreen(0, 0);
        var ordered = vis.Select(r => (r, n: Level(r))).OrderBy(p => p.n).ToList();
        using (context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(o.X, o.Y)))
        {
            bool strong = Prefs.Outlines == WorldPrefs.Strong, hidden = Prefs.Outlines == WorldPrefs.Hidden;
            foreach (var (r, n) in ordered)
                context.DrawGeometry(UrbeTheme.Brush(r.Color, n != 0 ? (strong ? .10 : .05) : (strong ? .13 : .08)), null, Shape(r).Geometry);
            foreach (var (r, n) in ordered)
            {
                double w = n == 0 ? 3 : n == 1 ? 2 : 1.5;
                var g = Shape(r).Geometry;
                if (n == 0 && Prefs.Walls) { DrawWall(context, g); continue; }
                if (n != 0 && hidden) continue;
                double alpha = strong ? (n != 0 ? .85 : .95) : (n != 0 ? .35 : .6);
                if (n == 0)
                    context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(140, 8, 10, 14)), (w + 2.5) / s,
                        lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), g);
                // JS dash [5/px, 4/px] in tile units = 5 and 4 screen px; Avalonia dashes are in pen widths.
                var dash = n >= 2 ? new DashStyle([5 / w, 4 / w], 0) : null;
                context.DrawGeometry(null, new Pen(UrbeTheme.Brush(r.Color, alpha), w / s, dash,
                    PenLineCap.Round, PenLineJoin.Round), g);
            }
        }
    }

    // A stone wall along the outline, in tile units so it scales with the city: a shadow, the stone body, the merlons
    // (dashes one wall-width long) and a light top — the stones and greys of the 1.8.4 buildings.
    private static readonly IBrush WallShadow = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(120, 8, 10, 14));
    private static readonly IBrush WallStone = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#7d776b"));
    private static readonly IBrush WallJoint = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#4e4a43"));
    private static readonly IBrush WallMerlon = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#b8b09e"));
    private static readonly IBrush WallTop = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#e0d8c4"));

    private static void DrawWall(DrawingContext context, Geometry g)
    {
        const double w = .62;
        context.DrawGeometry(null, new Pen(WallShadow, w + .4, lineCap: PenLineCap.Square, lineJoin: PenLineJoin.Miter), g);
        context.DrawGeometry(null, new Pen(WallJoint, w + .12, lineCap: PenLineCap.Square, lineJoin: PenLineJoin.Miter), g);
        context.DrawGeometry(null, new Pen(WallStone, w, lineCap: PenLineCap.Square, lineJoin: PenLineJoin.Miter), g);
        // merlons: blocks one wall-width long with a gap of the same, lighter, and a bright top on each
        context.DrawGeometry(null, new Pen(WallMerlon, w * .7, new DashStyle([1.2, 1.2], 0), PenLineCap.Flat, PenLineJoin.Miter), g);
        context.DrawGeometry(null, new Pen(WallTop, .12, new DashStyle([7, 7], 0), PenLineCap.Flat, PenLineJoin.Miter), g);
    }

    // ---------------- decorations (DEC-0045, UC-41): from .urbe/mundo.json, pixel art at 12 cells per tile

    private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _decorArt = new(StringComparer.Ordinal);

    /// <summary>The image of a decoration kind (cached per view: bitmaps belong to the view's thread).</summary>
    public Avalonia.Media.Imaging.Bitmap? DecorationArt(string kind)
    {
        if (_decorArt.TryGetValue(kind, out var art)) return art;
        if (WorldDecorations.Find(kind) is not { } k) return null;
        var (px, w, h) = LegacyLifeSprites.Render(k.Sprite, 0, false);
        return _decorArt[kind] = Pixels.ToBitmap(px, w, h);
    }

    /// <summary>The screen rectangle of a decoration of <paramref name="kind"/> with its corner at tile (x, y).</summary>
    public Rect DecorationRect(WorldDecorations.Kind kind, int x, int y)
    {
        var p = Camera.WorldToScreen(x * T, y * T);
        double size = T * Camera.Zoom;
        return new Rect(Math.Round(p.X), Math.Round(p.Y), Math.Ceiling(kind.W * size), Math.Ceiling(kind.H * size));
    }

    private void DrawDecorations(DrawingContext context)
    {
        if (Prefs.Decorations.Count == 0) return;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        foreach (var d in Prefs.Decorations.OrderBy(d => d.Y))
        {
            if (WorldDecorations.Find(d.Kind) is not { } k || d.X > x1 || d.X + k.W < x0 || d.Y > y1 || d.Y + k.H < y0) continue;
            if (DecorationArt(d.Kind) is not { } art) continue;
            context.DrawImage(art, new Rect(0, 0, art.Size.Width, art.Size.Height), DecorationRect(k, d.X, d.Y));
        }
    }

    /// <summary>A tap on a decoration (DEC-0045, UC-41): the host offers what can be done with it.</summary>
    public event Action<WorldDecoration>? DecorationTapped;

    /// <summary>The root neighbourhood shown at the top from this zoom on (DEC-0045, UC-41).</summary>
    public const double RootNameZoom = 1.6;

    /// <summary>The root neighbourhood whose name is shown at the top now, or null.</summary>
    public string? RootDistrictShown { get; private set; }

    // Close up, the city around is out of sight: the name of the folder at the root of where the camera is helps to know
    // where one is (owner, DEC-0045). A pill at the top centre, under the top bar.
    private void DrawRootDistrictName(DrawingContext context)
    {
        RootDistrictShown = null;
        if (Camera.Zoom < RootNameZoom || City is not { } city) return;
        int cx = (int)Math.Floor(Camera.X / T), cy = (int)Math.Floor(Camera.Y / T);
        var r = city.RegionAt(cx, cy);
        for (int guard = 0; r?.ParentId is { } parent && guard < 64; guard++) r = city.Regions.Find(x => x.Id == parent) ?? r;
        if (r is null) return;
        RootDistrictShown = r.Name;
        var text = Text(r.Name, 14, FontWeight.Bold, UrbeTheme.Brush(UrbeTheme.Text));
        double padX = 14, h = 30, dot = 8, wide = text.Width + padX * 2 + dot + 8;
        var box = new Rect(Bounds.Width / 2 - wide / 2, 72, wide, h);
        context.DrawRectangle(UrbeTheme.Brush(UrbeTheme.Surface, .9), new Pen(UrbeTheme.Brush(r.Color, .8), 1), box, h / 2, h / 2);
        context.DrawEllipse(UrbeTheme.Brush(r.Color), null, new Point(box.X + padX + dot / 2, box.Y + h / 2), dot / 2, dot / 2);
        context.DrawText(text, new Point(box.X + padX + dot + 8, box.Y + (h - text.Height) / 2));
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
    public Bitmap? ArtOf(LegacyCityBuilding b) => World is null ? null : Art(b, LookOf(b));

    /// <summary>The house art with another look (the Visual picker's previews).</summary>
    public Bitmap? ArtOf(LegacyCityBuilding b, BuildingLook look) => World is null ? null : Art(b, look);

    /// <summary>The look chosen for this building in .urbe/mundo.json (DEC-0045, UC-41), or the automatic one.</summary>
    public BuildingLook LookOf(LegacyCityBuilding b) => Prefs.Looks.GetValueOrDefault(b.Path) ?? new BuildingLook();

    /// <summary>The trade the 1.8.4 gives this building: a note's by its extension, a file building is a store.</summary>
    public static string AutomaticKind(LegacyCityBuilding b) => b.IsNote ? TradeOf(b.Ext) : "store";

    /// <summary>The trade of a note with this extension (urbeArteDaConstrucao: .html a hall, .js a workshop…).</summary>
    public static string TradeOf(string ext)
    {
        ext = ext.TrimStart('.').ToLowerInvariant();
        if (!Noted.Contains(ext)) ext = "md";
        return Trade.GetValueOrDefault(ext, "house");
    }

    /// <summary>The variant the 1.8.4 gives this building (its sprite, or its id).</summary>
    public static int AutomaticVariant(LegacyCityBuilding b) =>
        b.Sprite == "house2" ? 1 : b.Sprite == "house3" ? 2 : (int)(LegacyJsMath.Seed(b.Id.Length > 0 ? b.Id : b.Name) % 3);

    /// <summary>Whether the 1.8.4 puts flowers by this building (by its name).</summary>
    public static bool AutomaticFlowers(LegacyCityBuilding b) => LegacyJsMath.Seed(b.Name) % 3 == 0;

    /// <summary>urbeArteDaConstrucao: a note's trade by extension, a file building is a store; style by biome, variant and
    /// flowers by name; what the person chose in Visual (.urbe/mundo.json) instead, when there is a choice.</summary>
    private Bitmap Art(LegacyCityBuilding b, BuildingLook look)
    {
        var kind = look.Kind ?? AutomaticKind(b);
        if (_ownArt.TryGetValue(kind, out var own)) return own;   // urbeSpritesProprios[kind]
        var biome = World!.At((int)Math.Floor(b.X + b.W / 2.0), (int)Math.Floor(b.Y + b.H / 2.0)).Biome;
        int variant = look.Variant ?? AutomaticVariant(b);
        bool flowers = look.Flowers ?? AutomaticFlowers(b);
        return _sprites.Building(kind, LegacyWorldBuildings.StyleForBiome(LegacyBiomeRules.Id(biome)), variant, flowers);
    }

    /// <summary>urbeNomeCasa: name with its extension, Markdown hidden, at most 30 characters; a file building shows
    /// nomeExibidoArquivo (an attachment its name, otherwise its fileName or name).</summary>
    public static string HouseLabel(LegacyCityBuilding b)
    {
        if (!b.IsNote)
        {
            var file = b.FileDisplayName;
            return file.Length > 30 ? file[..28] + "…" : file;
        }
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
                if (b == Moving) continue;
                var p = Camera.WorldToScreen(b.X * T, b.Y * T);
                double w = 48 * s * (b.W / 3.0), h = 56 * s * (b.H / 3.0);
                context.DrawImage(Art(b, LookOf(b)), new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                    new Rect(Math.Round(p.X), Math.Round(p.Y + b.H * T * z - h), Math.Ceiling(w), Math.Ceiling(h)));
            }
        if (Moving is { } carried)
        {
            // the carried building (moveBuilding): its old lot faint, the new one framed green where it fits, red where not
            var (mx, my) = MovePos;
            bool ok = MoveValid?.Invoke(carried, mx, my) ?? true;
            var old = Camera.WorldToScreen(carried.X * T, carried.Y * T);
            context.DrawRectangle(null, new Pen(UrbeTheme.Brush("#ffffff", .35), 1, new DashStyle([4, 4], 0)), new Rect(old.X, old.Y, carried.W * T * z, carried.H * T * z));
            var p = Camera.WorldToScreen(mx * T, my * T);
            double w = 48 * s * (carried.W / 3.0), h = 56 * s * (carried.H / 3.0);
            using (context.PushOpacity(.8))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(Art(carried, LookOf(carried)), new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                    new Rect(Math.Round(p.X), Math.Round(p.Y + carried.H * T * z - h), Math.Ceiling(w), Math.Ceiling(h)));
            context.DrawRectangle(null, new Pen(UrbeTheme.Brush(ok ? "#7ef29a" : "#ef766f"), 3), new Rect(p.X, p.Y, carried.W * T * z, carried.H * T * z));
        }
    }

    /// <summary>Names are intentionally AFTER lighting, identical to app.js 'urbeAntesDosRotulos'.</summary>
    private void DrawWorldLabels(DrawingContext context)
    {
        var city = City!;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        double z = Camera.Zoom;
        var labels = city.Buildings.Where(b =>
            !(b.X > x1 || b.X + b.W < x0 || b.Y > y1 || b.Y + b.H < y0) &&
            ((z >= .42 && ShowHouseNames) || b == Selected)).ToList();
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
        if (!ShowDistrictNames || Camera.Zoom < .28) return used;
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
            var ground = _ground;
            Task.Run(() => GenerateChunk(world, key.X, key.Y, ground)).ContinueWith(task =>
                Dispatcher.UIThread.Post(() => AcceptChunk(key, generation, task)), TaskScheduler.Default);
        }
    }

    private static (byte[] Near, byte[] Far, (int, int, LegacyVegetation.Tree)[] Trees) GenerateChunk(LegacyWorld world, int cx, int cy, LegacyGroundStyle? ground)
    {
        var near = LegacyChunkPixels.Render(world.At, world.DecorAt, cx, cy, ground);
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
        if (_pointers.Count == 1 && Tool is { } tool && City is not null)
        {
            var (tx, ty) = Camera.TileAt(p);
            _toolGesture = true;
            _pressAt = null;
            tool.Down(tx, ty);
            InvalidateVisual();
        }
        else if (_pointers.Count == 1)
        {
            _pressAt = p;
            _dragged = false;
            var (tx, ty) = Camera.TileAt(p);
            _pressBuilding = City?.BuildingAt(tx, ty);
            _moveHold.Stop();
            if (_pressBuilding is not null && MoveValid is not null) _moveHold.Start();
        }
        else { _moveHold.Stop(); if (Moving is not null) { Moving = null; } _pressAt = null; _pinchDistance = PinchDistance(); if (_toolGesture) { _toolGesture = false; Tool?.Cancel(); } }
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
        else if (_toolGesture && Tool is { } active)
        {
            var (tx, ty) = Camera.TileAt(p);
            active.Move(tx, ty);
        }
        else if (Moving is not null)
        {
            var (tx, ty) = Camera.TileAt(p);
            Carry(tx, ty);
        }
        else
        {
            if (_pressAt is { } start && Distance(start, p) > TapSlop) { _dragged = true; _moveHold.Stop(); }
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
        _moveHold.Stop();
        if (Moving is not null && _pointers.Count == 0)
        {
            Drop();
            _pressAt = null;
            return;
        }
        if (_toolGesture && _pointers.Count == 0)
        {
            _toolGesture = false;
            var (tx, ty) = Camera.TileAt(e.GetPosition(this));
            Tool?.Up(tx, ty);
            InvalidateVisual();
        }
        else if (_pointers.Count == 0 && _pressAt is { } start && !_dragged && City is not null)
        {
            var (tx, ty) = Camera.TileAt(start);
            if (City.BuildingAt(tx, ty) is null && Prefs.DecorationAt(tx, ty) is { } decoration) { Select(null); DecorationTapped?.Invoke(decoration); }
            else Select(City.BuildingAt(tx, ty));   // a note opens its summary, a file building its preview
        }
        if (_pointers.Count < 2) _pinchDistance = 0;
        if (_pointers.Count == 0) _pressAt = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // lost without a release (another control took the pointer): whatever was carried goes back
        if (!_pointers.Remove(e.Pointer)) return;
        _moveHold.Stop();
        if (_pointers.Count == 0 && Moving is not null) { Moving = null; InvalidateVisual(); }
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
