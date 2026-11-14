using Android.Content;
using Android.Views;

namespace Lunet.Android;

/// <summary>
/// Shared canvas gestures for the Studios (LUNET-486): pinch zoom anchored at the fingers, two-finger pan,
/// two-finger tap = undo, three-finger tap = redo. One finger is left to the tool. When a second finger lands
/// during a tool gesture, <see cref="CancelTool"/> is raised so the partial stroke can be rolled back.
/// </summary>
internal sealed class CanvasGestures
{
    private readonly View _view;
    private readonly ScaleGestureDetector _scale;
    private bool _multi, _scaled;
    private int _maxPointers;
    private long _downTime;
    private float _midX, _midY, _travel;

    public float Zoom { get; private set; } = 1;
    public float PanX { get; private set; }
    public float PanY { get; private set; }
    public float MinZoom { get; init; } = 0.5f;
    public float MaxZoom { get; init; } = 40f;
    public event Action? CancelTool;
    public event Action? Undo;
    public event Action? Redo;

    public CanvasGestures(Context context, View view)
    {
        _view = view;
        _scale = new ScaleGestureDetector(context, new Listener(this));
    }

    public void Reset() { Zoom = 1; PanX = PanY = 0; _view.Invalidate(); }

    /// <summary>Returns true when the event belongs to a multi-finger gesture (the tool must ignore it).</summary>
    public bool Handle(MotionEvent e)
    {
        _scale.OnTouchEvent(e);
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _view.Parent?.RequestDisallowInterceptTouchEvent(true);
                _multi = false; _scaled = false; _maxPointers = 1; _travel = 0; _downTime = e.EventTime;
                return false;
            case MotionEventActions.PointerDown:
                _maxPointers = Math.Max(_maxPointers, e.PointerCount);
                if (!_multi) { _multi = true; CancelTool?.Invoke(); }
                Midpoint(e, out _midX, out _midY);
                return true;
        }
        if (!_multi) return false;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Move when e.PointerCount >= 2:
                Midpoint(e, out var x, out var y);
                PanX += x - _midX; PanY += y - _midY;
                _travel += Math.Abs(x - _midX) + Math.Abs(y - _midY);
                _midX = x; _midY = y;
                _view.Invalidate();
                break;
            case MotionEventActions.PointerUp:
                int remaining = e.ActionIndex == 0 ? 1 : 0;
                _midX = e.GetX(remaining); _midY = e.GetY(remaining);
                break;
            case MotionEventActions.Up or MotionEventActions.Cancel:
                bool tap = e.ActionMasked == MotionEventActions.Up && !_scaled && e.EventTime - _downTime < 320 &&
                           _travel < 24 * _view.Resources!.DisplayMetrics!.Density;
                if (tap && _maxPointers == 2) Undo?.Invoke();
                else if (tap && _maxPointers >= 3) Redo?.Invoke();
                _multi = false;
                break;
        }
        return true;
    }

    private static void Midpoint(MotionEvent e, out float x, out float y)
    {
        x = 0; y = 0;
        for (int i = 0; i < e.PointerCount; i++) { x += e.GetX(i); y += e.GetY(i); }
        x /= e.PointerCount; y /= e.PointerCount;
    }

    private sealed class Listener(CanvasGestures owner) : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        public override bool OnScale(ScaleGestureDetector detector)
        {
            float previous = owner.Zoom;
            owner.Zoom = Math.Clamp(owner.Zoom * detector.ScaleFactor, owner.MinZoom, owner.MaxZoom);
            float ratio = owner.Zoom / previous;
            float cx = owner._view.Width / 2f, cy = owner._view.Height / 2f;
            owner.PanX = (owner.PanX + cx - detector.FocusX) * ratio - cx + detector.FocusX;
            owner.PanY = (owner.PanY + cy - detector.FocusY) * ratio - cy + detector.FocusY;
            if (Math.Abs(detector.ScaleFactor - 1) > 0.01f) owner._scaled = true;
            owner._view.Invalidate();
            return true;
        }
    }
}
