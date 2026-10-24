using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// A finger on a text box inside the editor's scroll behaves as on a native text field (the 1.8.4's textarea in a
/// browser): dragging scrolls the note, with inertia; a tap puts the caret where the finger touched; holding still
/// selects the word, with the selection handles to widen it. Without this the text box takes the touch for its caret
/// and selection and the scroll sticks. A touch that only stops the note while it still glides places nothing (and so
/// opens no keyboard), as on a phone (22/10/2026). Mouse and pen are left to the text box.
/// </summary>
public static class TouchTextScroll
{
    private const double Slop = 8;            // px a finger moves before it is a drag
    private const int HoldMs = 500;           // a still finger held this long selects the word
    private const double Friction = .95;      // per 16 ms frame of the fling

    public static void Attach(ScrollViewer scroll)
    {
        TextBox? box = null;
        IPointer? finger = null;
        Point start = default, last = default;
        Vector startOffset = default;
        bool dragging = false, stopped = false;
        long lastMs = 0;
        double velocity = 0;   // px per ms, positive = content moves up (offset grows)
        var clock = System.Diagnostics.Stopwatch.StartNew();
        DispatcherTimer? hold = null, fling = null;
        // the last tap of a finger (anywhere in the scroll): a second one close by, soon after, selects the word
        long lastTapMs = -10_000;
        Point lastTapAt = default;
        bool DoubleTap(Point p)
        {
            long now = clock.ElapsedMilliseconds;
            bool twice = now - lastTapMs < DoubleTapMs && Math.Abs(p.X - lastTapAt.X) < DoubleTapSlop && Math.Abs(p.Y - lastTapAt.Y) < DoubleTapSlop;
            lastTapMs = twice ? -10_000 : now;
            lastTapAt = p;
            return twice;
        }

        void StopFling() => fling?.Stop();

        scroll.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.Pointer.Type != PointerType.Touch) return;
            // a finger on a note that still glides only stops it: no caret, no selection, no keyboard
            stopped = fling?.IsEnabled == true || TouchIntent.Of(scroll)?.Intent.IsMoving(Environment.TickCount64) == true;
            StopFling();
            box = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<TextBox>().FirstOrDefault();
            if (box is not null && box.SelectionStart != box.SelectionEnd) { box = null; return; }
            if (box is null || !box.GetVisualAncestors().Contains(scroll)) { box = null; return; }
            finger = e.Pointer;
            start = last = e.GetPosition(scroll);
            startOffset = scroll.Offset;
            dragging = false;
            velocity = 0;
            lastMs = clock.ElapsedMilliseconds;
            e.Pointer.Capture(scroll);
            e.Handled = true;
            var heldBox = box;
            var heldAt = e.GetPosition(heldBox);
            hold?.Stop();
            if (stopped) return;
            hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldMs) };
            hold.Tick += (_, _) =>
            {
                hold!.Stop();
                if (dragging || finger is null) return;
                var heldFinger = finger;
                SelectWordAt(heldBox, heldAt);
                Ui.PlatformHaptics.Hold();
                heldFinger.Capture(null);
                finger = null;   // the hold ends the gesture: lifting the finger keeps the selection
            };
            hold.Start();
        }, RoutingStrategies.Tunnel);

        scroll.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (finger is null || e.Pointer != finger) return;
            var p = e.GetPosition(scroll);
            if (!dragging && Math.Abs(p.Y - start.Y) < Slop && Math.Abs(p.X - start.X) < Slop) return;
            dragging = true;
            hold?.Stop();
            long now = clock.ElapsedMilliseconds;
            double dt = Math.Max(1, now - lastMs);
            velocity = velocity * .3 + ((last.Y - p.Y) / dt) * .7;
            lastMs = now;
            last = p;
            scroll.Offset = new Vector(scroll.Offset.X, Clamp(scroll, startOffset.Y + (start.Y - p.Y)));
            e.Handled = true;
        }, RoutingStrategies.Tunnel);

        scroll.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (e.Pointer.Type != PointerType.Touch) return;
            hold?.Stop();
            var tapped = box;
            if (finger is null || e.Pointer != finger)
            {
                // a tap outside a text box (a drawn block about to become one) still counts for a double tap
                if (finger is null && box is null) DoubleTap(e.GetPosition(scroll));
                finger = null;
                return;
            }
            finger = null;
            e.Pointer.Capture(null);
            e.Handled = true;
            if (!dragging)
            {
                if (tapped is null || stopped) return;
                if (DoubleTap(e.GetPosition(scroll))) { SelectWordAt(tapped, e.GetPosition(tapped)); Ui.PlatformHaptics.Tick(); }
                else PlaceCaret(tapped, e.GetPosition(tapped));
                return;
            }
            if (clock.ElapsedMilliseconds - lastMs > 80 || Math.Abs(velocity) < .05) return;
            fling ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            fling.Tick -= OnFling;
            fling.Tick += OnFling;
            fling.Start();
        }, RoutingStrategies.Tunnel);

        scroll.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => { hold?.Stop(); finger = null; }, RoutingStrategies.Bubble);

        void OnFling(object? sender, EventArgs e)
        {
            var y = Clamp(scroll, scroll.Offset.Y + velocity * 16);
            if (y == scroll.Offset.Y || Math.Abs(velocity) < .02) { fling!.Stop(); return; }
            scroll.Offset = new Vector(scroll.Offset.X, y);
            velocity *= Friction;
        }
    }

    private const int DoubleTapMs = 350;
    private const double DoubleTapSlop = 32;

    private static double Clamp(ScrollViewer scroll, double y) =>
        Math.Clamp(y, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));

    /// <summary>The character under a point of the box (its text presenter's layout).</summary>
    public static int IndexAt(TextBox box, Point inBox)
    {
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null) return box.CaretIndex;
        var p = box.TranslatePoint(inBox, presenter) ?? inBox;
        var hit = presenter.TextLayout.HitTestPoint(p);
        return Math.Clamp(hit.TextPosition + (hit.IsTrailing ? 1 : 0), 0, (box.Text ?? "").Length);
    }

    private static void PlaceCaret(TextBox box, Point inBox)
    {
        var i = IndexAt(box, inBox);
        box.Focus(NavigationMethod.Pointer);
        box.SelectionStart = box.SelectionEnd = i;
        box.CaretIndex = i;
    }

    private static void SelectWordAt(TextBox box, Point inBox)
    {
        var text = box.Text ?? "";
        var i = Math.Min(IndexAt(box, inBox), text.Length);
        var (s, e) = VisualSurface.WordAt(text, i);
        box.Focus(NavigationMethod.Pointer);
        box.SelectionStart = s;
        box.SelectionEnd = e;
        ShowSelectionHandles(box);
    }

    /// <summary>
    /// The text box's own selection handles (and its Copiar/Recortar/Colar bubble) over a selection made here: the box
    /// shows them only for a hold or a double tap it handled itself, and this class takes those touches. Avalonia keeps
    /// the call internal (TextPresenter.EnsureTextSelectionLayer, TextSelectionHandleCanvas.Show); it is kept from
    /// trimming below and <c>TouchTextScrollTests</c> fails if an Avalonia update renames it. False when unavailable.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(TextPresenter))]
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods, HandleCanvasType, "Avalonia.Controls")]
    public static bool ShowSelectionHandles(TextBox box)
    {
        if (box.SelectionStart == box.SelectionEnd) return false;
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null || HandleApi is not var (ensure, canvasOf, show)) return false;
        try
        {
            ensure.Invoke(presenter, null);
            if (canvasOf.GetValue(presenter) is not { } canvas) return false;
            show.Invoke(canvas, [true]);
            return true;
        }
        catch (System.Reflection.TargetInvocationException) { return false; }
    }

    private const string HandleCanvasType = "Avalonia.Controls.Primitives.TextSelectionHandleCanvas";
    private const System.Reflection.BindingFlags Internal = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    /// <summary>The internal members behind <see cref="ShowSelectionHandles"/>, or null when this Avalonia has none.</summary>
    public static (System.Reflection.MethodInfo Ensure, System.Reflection.PropertyInfo Canvas, System.Reflection.MethodInfo Show)? HandleApi { get; } =
        typeof(TextPresenter).GetMethod("EnsureTextSelectionLayer", Internal, Type.EmptyTypes) is { } ensure
        && typeof(TextPresenter).GetProperty("TextSelectionHandleCanvas", Internal) is { } canvas
        && typeof(TextPresenter).Assembly.GetType(HandleCanvasType)?.GetMethod("Show", Internal, [typeof(bool)]) is { } show
            ? (ensure, canvas, show) : null;
}
