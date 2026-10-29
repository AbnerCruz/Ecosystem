using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-067: a graph inside a note shares the screen with the note's scroll, as a map embedded in a web page does
/// (cooperative gestures): one finger scrolls the note and a tap opens a card; two fingers move and pinch the graph.
/// A finger trying to move it shows how, for a moment.
/// </summary>
public sealed partial class GraphCanvasView
{
    private readonly Dictionary<long, (IPointer Pointer, Point At)> _touches = [];
    private Point _tapAt;
    private bool _tapMoved;
    private DateTime _hintUntil;

    /// <summary>Inside a note: the note's scroll comes first (see the type's summary).</summary>
    public bool Cooperative { get; set; }

    /// <summary>A card was tapped in a graph inside a note (it points to a note, a file or a folder).</summary>
    public event Action<GraphNode>? NodeOpened;

    public double Zoom => _zoom;
    public Vector Origin => _origin;

    /// <summary>"Use dois dedos para mover o grafo" is on screen.</summary>
    public bool HintShown => DateTime.UtcNow < _hintUntil;

    /// <summary>The centre of a card on the canvas (tests).</summary>
    public Point ScreenOf(GraphNode node)
    {
        if (!_fitted) Fit();
        return NodeRect(node).Center;
    }

    private GraphNode? NodeAtScreen(Point at) => _document?.Nodes.Where(n => n.Kind == GraphNodeKind.Group)
        .Concat(_document.Nodes.Where(n => n.Kind != GraphNodeKind.Group)).Reverse().FirstOrDefault(n => NodeRect(n).Contains(at));

    private static (Point Middle, double Distance) Pair(IEnumerable<Point> points)
    {
        var two = points.Take(2).ToArray();
        var middle = new Point((two[0].X + two[1].X) / 2, (two[0].Y + two[1].Y) / 2);
        var d = two[0] - two[1];
        return (middle, Math.Max(1, Math.Sqrt(d.X * d.X + d.Y * d.Y)));
    }

    /// <summary>The note's scroll took a finger (it captures it once the finger moves): the graph lets it go.</summary>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _touches.Remove(e.Pointer.Id);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _touches.Clear();
    }

    private void TouchPressed(PointerPressedEventArgs e)
    {
        if (!_fitted) Fit();
        var at = e.GetPosition(this);
        _touches[e.Pointer.Id] = (e.Pointer, at);
        if (_touches.Count == 1)
        {
            // one finger is the note's: held only to see a tap, never handled, so the scroll takes it when it moves
            e.Pointer.Capture(this);
            _tapAt = at;
            _tapMoved = false;
            return;
        }
        // the second finger: the graph takes both, and the note's scroll does not take this one
        e.PreventGestureRecognition();
        foreach (var (pointer, _) in _touches.Values) pointer.Capture(this);
        _tapMoved = true;
        e.Handled = true;
    }

    private void TouchMoved(PointerEventArgs e)
    {
        if (!_touches.TryGetValue(e.Pointer.Id, out var was)) return;
        var at = e.GetPosition(this);
        if (_touches.Count >= 2)
        {
            var (middle, distance) = Pair(_touches.Values.Select(t => t.At));
            _touches[e.Pointer.Id] = (was.Pointer, at);
            var (nowMiddle, nowDistance) = Pair(_touches.Values.Select(t => t.At));
            _origin -= (nowMiddle - middle) / _zoom;
            ZoomAt(nowMiddle, nowDistance / distance);
            e.Handled = true;
            return;
        }
        _touches[e.Pointer.Id] = (was.Pointer, at);
        var moved = at - _tapAt;
        if (!_tapMoved && moved.X * moved.X + moved.Y * moved.Y > 100)
        {
            _tapMoved = true;
            _hintUntil = DateTime.UtcNow.AddSeconds(1.6);
            InvalidateVisual();
            DispatcherTimer.RunOnce(InvalidateVisual, TimeSpan.FromSeconds(1.7));
        }
    }

    private void TouchReleased(PointerReleasedEventArgs e)
    {
        if (!_touches.Remove(e.Pointer.Id, out var was)) return;
        if (_touches.Count > 0 || _tapMoved) { was.Pointer.Capture(null); e.Handled = _touches.Count > 0 || e.Handled; return; }
        was.Pointer.Capture(null);
        // a tap: a card opens what it points to (and the block under it is not opened for editing)
        if (NodeAtScreen(e.GetPosition(this)) is { Target: not null } node)
        {
            e.Handled = true;
            NodeOpened?.Invoke(node);
        }
    }

    /// <summary>Drawn over the graph while <see cref="HintShown"/>.</summary>
    private void RenderHint(DrawingContext ctx)
    {
        if (!HintShown) return;
        var text = new FormattedText("Use dois dedos para mover o grafo", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface(UrbeTheme.UiFont), 13, UrbeTheme.Brush(UrbeTheme.Text));
        var box = new Rect((Bounds.Width - text.Width) / 2 - 12, Bounds.Height / 2 - text.Height / 2 - 8, text.Width + 24, text.Height + 16);
        ctx.DrawRectangle(UrbeTheme.Brush(UrbeTheme.Surface2, .94), null, box, 10, 10);
        ctx.DrawText(text, new Point(box.X + 12, box.Y + 8));
    }
}
