using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// Tells a touch meant for an item (a tap that opens it, a hold that selects it) from a touch that scrolls the list it
/// is in, as a phone does (observation of 22/10/2026: scrolling selected things and opened the keyboard).
/// <list type="bullet">
/// <item>A press that lands while the content is still moving only stops it: no tap, no hold.</item>
/// <item>Once the content has moved more than <see cref="Slop"/> since the press, the touch is a scroll: no tap, no
/// hold. The item's own position cannot tell this — it moves with the finger — so the scroll offset is used.</item>
/// </list>
/// Attached to a <see cref="ScrollViewer"/>; the items inside ask <see cref="IsIntended"/>.
/// </summary>
public sealed class TouchIntent
{
    /// <summary>How far (px) the content may move under a finger before the touch is a scroll.</summary>
    public const double Slop = 8;

    /// <summary>A touch this soon (ms) after the content moved by itself (a fling) only stops it.</summary>
    public const int SettleMs = 120;

    private static readonly AttachedProperty<TouchIntent?> IntentProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, TouchIntent?>("TouchIntent", typeof(TouchIntent));

    private long _lastMoveMs = long.MinValue / 2;
    private double _pressOffset;
    private bool _stopper;

    /// <summary>The content moved (not by the app itself), at <paramref name="nowMs"/>.</summary>
    public void Moved(long nowMs) => _lastMoveMs = nowMs;

    /// <summary>A press at <paramref name="offset"/>: a touch while the content still moves is one that stops it.</summary>
    public void Press(bool touch, double offset, long nowMs)
    {
        _pressOffset = offset;
        _stopper = touch && nowMs - _lastMoveMs < SettleMs;
    }

    /// <summary>The content moved by itself less than <see cref="SettleMs"/> before <paramref name="nowMs"/>.</summary>
    public bool IsMoving(long nowMs) => nowMs - _lastMoveMs < SettleMs;

    /// <summary>The press stopped a moving content (its tap and hold are not for the item).</summary>
    public bool StoppedScroll => _stopper;

    /// <summary>True while the current touch is still for the item under it.</summary>
    public bool Allows(double offset) => !_stopper && Math.Abs(offset - _pressOffset) < Slop;

    /// <summary>Watches <paramref name="scroll"/>; <paramref name="movedByApp"/> tells the app's own scrolling apart.</summary>
    public static TouchIntent Attach(ScrollViewer scroll, Func<bool>? movedByApp = null)
    {
        var intent = new TouchIntent();
        scroll.SetValue(IntentProperty, intent);
        scroll.ScrollChanged += (_, e) =>
        {
            if (Math.Abs(e.OffsetDelta.Y) > .01 && movedByApp?.Invoke() != true) intent.Moved(Environment.TickCount64);
        };
        scroll.AddHandler(InputElement.PointerPressedEvent,
            (_, e) => intent.Press(e.Pointer.Type == PointerType.Touch, scroll.Offset.Y, Environment.TickCount64),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        return intent;
    }

    /// <summary>The intent of the scroll <paramref name="visual"/> is in, if one is attached.</summary>
    public static (ScrollViewer Scroll, TouchIntent Intent)? Of(Visual visual)
    {
        foreach (var s in visual.GetSelfAndVisualAncestors().OfType<ScrollViewer>())
            if (s.GetValue(IntentProperty) is { } i) return (s, i);
        return null;
    }

    /// <summary>
    /// True when the touch on <paramref name="visual"/> is for it: its scroll did not move since the press and the
    /// press did not stop a moving scroll. Anything outside a watched scroll is intended; something no longer on the
    /// screen (a list rebuilt while scrolling) never is.
    /// </summary>
    public static bool IsIntended(Visual visual) =>
        visual.IsAttachedToVisualTree() && (Of(visual) is not { } o || o.Intent.Allows(o.Scroll.Offset.Y));
}
