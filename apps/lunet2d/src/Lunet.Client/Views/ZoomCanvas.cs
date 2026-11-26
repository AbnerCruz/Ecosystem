using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The common canvas of the Studios (A1 motor de canvas comum): a grid of cells shown to fit, pinch to zoom and
/// two-finger pan (Procreate, Pixaki), the mouse wheel on desktop. One finger always paints a cell; a second finger
/// mid-stroke turns it into a gesture and cancels the stroke. Subclasses say how big the content is and draw it.
/// </summary>
public abstract class ZoomCanvas : Control
{
    private readonly Dictionary<IPointer, Point> _pointers = new();
    private bool _stroke, _gesture;
    private double _startDistance, _startZoom;
    private Point _startMid;
    private Vector _startPan;
    private (int X, int Y)? _last;

    /// <summary>A cell was painted: x, y and whether it starts the stroke.</summary>
    public event Action<int, int, bool>? Paint;
    public event Action? StrokeEnded;
    /// <summary>A second finger came down mid-stroke: the stroke becomes a pinch, so it is undone.</summary>
    public event Action? StrokeCancelled;

    /// <summary>Size of the content in its own pixels (it is scaled to fit, then zoomed).</summary>
    protected abstract (double Width, double Height) ContentSize { get; }
    /// <summary>Number of paintable cells across and down.</summary>
    protected abstract (int X, int Y) Cells { get; }
    /// <summary>Draws the content into <paramref name="area"/>.</summary>
    protected abstract void RenderContent(DrawingContext context, Rect area);

    /// <summary>1 = fit; up to 16× closer.</summary>
    public double Zoom { get; private set; } = 1;
    public Vector Pan { get; private set; }

    protected ZoomCanvas()
    {
        ClipToBounds = true;
        Focusable = false;
        PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _pointers[e.Pointer] = e.GetPosition(this);
            e.Pointer.Capture(this);
            if (_pointers.Count == 1) { _stroke = true; _gesture = false; _last = null; Hit(e.GetPosition(this), first: true); return; }
            if (_stroke) { _stroke = false; StrokeCancelled?.Invoke(); }
            BeginGesture();
        };
        PointerMoved += (_, e) =>
        {
            if (!_pointers.ContainsKey(e.Pointer)) return;
            e.Handled = true;
            _pointers[e.Pointer] = e.GetPosition(this);
            if (_gesture && _pointers.Count >= 2) UpdateGesture();
            else if (_stroke) Hit(e.GetPosition(this), first: false);
        };
        PointerReleased += (_, e) => End(e.Pointer);
        PointerCaptureLost += (_, e) => End(e.Pointer);
        PointerWheelChanged += (_, e) => { ZoomAround(e.GetPosition(this), Zoom * (e.Delta.Y > 0 ? 1.25 : 0.8)); e.Handled = true; };
    }

    public void SetView(double zoom, Vector pan)
    {
        Zoom = Math.Clamp(zoom, 1, 16);
        Pan = Zoom <= 1.001 ? default : pan;
        InvalidateVisual();
    }

    public void ResetView() => SetView(1, default);

    /// <summary>Fills the height (cells big enough for a finger) with the content's left edge at the left: for wide content like a piano roll.</summary>
    public void FitHeight()
    {
        SetView(1, default);
        var a = Area;
        if (a.Height <= 0 || a.Height >= Bounds.Height - 1) return;
        var zoom = Math.Min(16, Bounds.Height / a.Height);
        var width = a.Width * zoom;
        SetView(zoom, new Vector(width > Bounds.Width ? -(Bounds.Width - width) / 2 : 0, 0));
    }

    /// <summary>The rectangle the content occupies: fit (integer scale when it can), then the zoom and pan.</summary>
    public Rect Area
    {
        get
        {
            var (w0, h0) = ContentSize;
            if (w0 <= 0 || h0 <= 0 || Bounds.Width <= 0) return default;
            var scale = Math.Min(Bounds.Width / w0, Bounds.Height / h0);
            if (scale >= 2) scale = Math.Floor(scale);
            scale *= Zoom;
            var w = w0 * scale; var h = h0 * scale;
            return new Rect((Bounds.Width - w) / 2 + Pan.X, (Bounds.Height - h) / 2 + Pan.Y, w, h);
        }
    }

    /// <summary>The cell under a point of the canvas, or null outside.</summary>
    public (int X, int Y)? PixelAt(Point p)
    {
        var a = Area;
        if (a.Width <= 0) return null;
        var (cx, cy) = Cells;
        var x = (int)Math.Floor((p.X - a.X) / a.Width * cx);
        var y = (int)Math.Floor((p.Y - a.Y) / a.Height * cy);
        return x < 0 || y < 0 || x >= cx || y >= cy ? null : (x, y);
    }

    public sealed override void Render(DrawingContext context) => RenderContent(context, Area);

    /// <summary>Thin lines between cells when they are big enough to aim at.</summary>
    protected void DrawGrid(DrawingContext context, Rect a, double minCell = 8)
    {
        var (cx, cy) = Cells;
        var w = a.Width / cx; var h = a.Height / cy;
        if (Math.Min(w, h) < minCell) return;
        var pen = new Pen(LunetTokens.Brush("#22ffffff"), 1);
        for (var i = 1; i < cx; i++) context.DrawLine(pen, new Point(Math.Round(a.X + i * w) + .5, a.Y), new Point(Math.Round(a.X + i * w) + .5, a.Bottom));
        for (var i = 1; i < cy; i++) context.DrawLine(pen, new Point(a.X, Math.Round(a.Y + i * h) + .5), new Point(a.Right, Math.Round(a.Y + i * h) + .5));
    }

    private void Hit(Point p, bool first)
    {
        if (PixelAt(p) is not { } cell || _last == cell) return;
        _last = cell;
        Paint?.Invoke(cell.X, cell.Y, first);
    }

    private void End(IPointer pointer)
    {
        if (!_pointers.Remove(pointer)) return;
        if (_stroke && _pointers.Count == 0) { _stroke = false; _last = null; StrokeEnded?.Invoke(); }
        if (_pointers.Count == 0) _gesture = false;
    }

    private void BeginGesture()
    {
        var (a, b) = TwoPoints();
        _gesture = true;
        _startDistance = Math.Max(1, Distance(a, b));
        _startZoom = Zoom;
        _startMid = (a + b) / 2;
        _startPan = Pan;
    }

    private void UpdateGesture()
    {
        var (a, b) = TwoPoints();
        var mid = (a + b) / 2;
        var zoom = Math.Clamp(_startZoom * Distance(a, b) / _startDistance, 1, 16);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var anchor = (_startMid - center - _startPan) / _startZoom; // the content under the fingers stays there
        SetView(zoom, mid - center - anchor * zoom);
    }

    private void ZoomAround(Point p, double zoom)
    {
        zoom = Math.Clamp(zoom, 1, 16);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var anchor = (p - center - Pan) / Zoom;
        SetView(zoom, p - center - anchor * zoom);
    }

    private (Point, Point) TwoPoints()
    {
        var points = _pointers.Values.Take(2).ToArray();
        return (points[0], points.Length > 1 ? points[1] : points[0]);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
