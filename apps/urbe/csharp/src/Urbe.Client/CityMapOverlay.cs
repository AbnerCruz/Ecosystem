using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>The #mapao from Urbe 1.8.4: overlay on top of the native procedural world.
/// Same city and camera objects; never copies or persists world state.</summary>
public sealed class CityMapOverlay : Grid
{
    private readonly MapSurface _map = new();
    public event Action<double, double>? Navigated;

    public CityMapOverlay()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("64,*");
        var heading = new TextBlock
        {
            Text = "MAPA DA CIDADE", FontSize = 16, FontWeight = FontWeight.SemiBold,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text), Margin = new Thickness(18, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 20),
            Width = 48, Height = 48, Padding = new Thickness(6),
            Background = UrbeTheme.Brush(UrbeTheme.Surface2),
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8)
        };
        close.Click += (_, _) => IsVisible = false;
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        SetColumn(close, 1);
        bar.Children.Add(heading);
        bar.Children.Add(close);
        SetRow(bar, 0);
        Children.Add(bar);

        var frame = new Border
        {
            Background = UrbeTheme.Brush("#263b26"),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(12, 0, 12, 14),
            Child = _map
        };
        SetRow(frame, 1);
        Children.Add(frame);
        _map.Navigated += (x, y) => Navigated?.Invoke(x, y);
    }

    public bool HasWorld => _map.HasWorld;
    public Size MapSize => _map.Bounds.Size;
    public bool TryNavigate(Point mapPosition) => _map.TryNavigate(mapPosition);
    public void Open(LegacyWorld world, LegacyCity city, Camera camera)
    {
        _map.Load(world, city, camera);
        IsVisible = true;
    }
}

/// <summary>Native renderer following app.js limitesMundo/pintarMapa: terrain,
/// regions and their cells, roads, houses, and the current camera viewport.
/// Terrain sampling is off the UI thread to keep Android input responsive.</summary>
public sealed class MapSurface : Control
{
    private LegacyWorld? _world;
    private LegacyCity? _city;
    private Camera? _camera;
    private WriteableBitmap? _terrain;
    private bool _pending;
    private int _generation;

    public bool HasWorld => _world is not null && _city is not null && _camera is not null;
    public event Action<double, double>? Navigated;

    public void Load(LegacyWorld world, LegacyCity city, Camera camera)
    {
        _generation++;
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _city = city ?? throw new ArgumentNullException(nameof(city));
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _terrain?.Dispose();
        _terrain = null;
        _pending = false;
        InvalidateVisual();
    }

    public readonly record struct Limits(double X0, double Y0, double X1, double Y1)
    {
        public double Width => X1 - X0;
        public double Height => Y1 - Y0;
    }

    public static Limits MapLimits(LegacyCity city, Camera camera, double aspect)
    {
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity;
        double x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        void Include(double x, double y, double w, double h)
        {
            x0 = Math.Min(x0, x); y0 = Math.Min(y0, y);
            x1 = Math.Max(x1, x + w); y1 = Math.Max(y1, y + h);
        }
        foreach (var b in city.Buildings) Include(b.X, b.Y, b.W, b.H);
        foreach (var r in city.Regions) Include(r.X, r.Y, r.W, r.H);
        var cx = camera.X / LegacyWorld.Tile;
        var cy = camera.Y / LegacyWorld.Tile;
        Include(cx - 120, cy - 90, 240, 180);
        if (x0 > x1) { x0 = -25; y0 = -25; x1 = 25; y1 = 25; }
        var margin = Math.Max(4, (x1 - x0) * .06);
        x0 -= margin; y0 -= margin; x1 += margin; y1 += margin;
        if (aspect > 0 && double.IsFinite(aspect))
        {
            var w = x1 - x0; var h = y1 - y0;
            if (w / h < aspect) { var d = (h * aspect - w) / 2; x0 -= d; x1 += d; }
            else { var d = (w / aspect - h) / 2; y0 -= d; y1 += d; }
        }
        return new Limits(x0, y0, x1, y1);
    }

    public bool TryNavigate(Point p)
    {
        if (!HasWorld || Bounds.Width <= 0 || Bounds.Height <= 0 ||
            p.X < 0 || p.Y < 0 || p.X > Bounds.Width || p.Y > Bounds.Height)
            return false;
        var lim = MapLimits(_city!, _camera!, Bounds.Width / Bounds.Height);
        Navigated?.Invoke(lim.X0 + p.X / Bounds.Width * lim.Width,
            lim.Y0 + p.Y / Bounds.Height * lim.Height);
        return true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (TryNavigate(e.GetPosition(this))) e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(UrbeTheme.Brush("#263b26"), new Rect(Bounds.Size));
        if (!HasWorld || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var lim = MapLimits(_city!, _camera!, Bounds.Width / Bounds.Height);
        double sx = Bounds.Width / lim.Width, sy = Bounds.Height / lim.Height;
        double X(double v) => (v - lim.X0) * sx;
        double Y(double v) => (v - lim.Y0) * sy;
        RequestTerrain(lim);
        if (_terrain is not null)
        {
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(_terrain, new Rect(0, 0, _terrain.PixelSize.Width, _terrain.PixelSize.Height), new Rect(Bounds.Size));
        }
        foreach (var r in _city!.Regions)
        {
            if (r.W <= 0 || r.H <= 0) continue;
            var fill = UrbeTheme.Brush(r.Color, .28);
            if (r.Cells.Count > 0)
            {
                foreach (var (tx, ty) in r.Cells)
                    context.FillRectangle(fill, new Rect(X(tx), Y(ty), Math.Max(1, sx), Math.Max(1, sy)));
            }
            else context.FillRectangle(fill, new Rect(X(r.X), Y(r.Y), r.W * sx, r.H * sy));
            context.DrawRectangle(null, new Pen(UrbeTheme.Brush(r.Color, .9), 1),
                new Rect(X(r.X), Y(r.Y), r.W * sx, r.H * sy));
            if (r.ParentId is null && r.W * sx > 45)
            {
                var label = new FormattedText(r.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    UrbeTheme.UiTypeface(FontWeight.SemiBold), 10, UrbeTheme.Brush("#e0ece5"));
                context.DrawText(label, new Point(X(r.X) + 3, Y(r.Y) + 2));
            }
        }
        foreach (var (rx, ry) in _city.Roads)
            context.FillRectangle(UrbeTheme.Brush("#a98a57"),
                new Rect(X(rx), Y(ry), Math.Max(1, sx), Math.Max(1, sy)));
        foreach (var b in _city.Buildings)
            context.FillRectangle(UrbeTheme.Brush("#ffd36a"),
                new Rect(X(b.X), Y(b.Y), Math.Max(2, Math.Min(5, 3 * sx)), Math.Max(2, Math.Min(5, 3 * sy))));
        var cam = _camera!;
        double vw = cam.Viewport.Width / cam.Zoom / LegacyWorld.Tile;
        double vh = cam.Viewport.Height / cam.Zoom / LegacyWorld.Tile;
        double cx = cam.X / LegacyWorld.Tile, cy = cam.Y / LegacyWorld.Tile;
        context.DrawRectangle(null, new Pen(UrbeTheme.Brush("#ecf5f7"), 1.5),
            new Rect(X(cx - vw / 2), Y(cy - vh / 2), vw * sx, vh * sy));
    }

    private void RequestTerrain(Limits limits)
    {
        if (_pending || _terrain is not null || _world is null) return;
        int w = Math.Clamp((int)Math.Ceiling(Bounds.Width / 2), 1, 480);
        int h = Math.Clamp((int)Math.Ceiling(Bounds.Height / 2), 1, 480);
        int generation = _generation;
        var world = _world;
        _pending = true;
        Task.Run(() =>
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int tx = (int)Math.Floor(limits.X0 + (x + .5) / w * limits.Width);
                int ty = (int)Math.Floor(limits.Y0 + (y + .5) / h * limits.Height);
                var biome = world.At(tx, ty).Biome;
                var water = biome is LegacyBiome.Deep or LegacyBiome.Sea or LegacyBiome.River or LegacyBiome.Lake;
                var sand = biome is LegacyBiome.Beach or LegacyBiome.Desert;
                int i = (y * w + x) * 4;
                rgba[i] = (byte)(water ? 0x23 : sand ? 0x77 : 0x38);
                rgba[i + 1] = (byte)(water ? 0x4d : sand ? 0x6b : 0x5d);
                rgba[i + 2] = (byte)(water ? 0x5a : sand ? 0x43 : 0x34);
                rgba[i + 3] = 255;
            }
            return rgba;
        }).ContinueWith(task => Dispatcher.UIThread.Post(() =>
        {
            if (generation != _generation) return;
            _pending = false;
            if (!task.IsCompletedSuccessfully) return;
            _terrain = Pixels.ToBitmap(task.Result, w, h);
            InvalidateVisual();
        }), TaskScheduler.Default);
    }
}
