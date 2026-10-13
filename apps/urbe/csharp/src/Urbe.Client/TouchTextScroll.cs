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
/// selects the word. Without this the text box takes the touch for its caret and selection and the scroll sticks.
/// Mouse and pen are left to the text box.
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
        bool dragging = false;
        long lastMs = 0;
        double velocity = 0;   // px per ms, positive = content moves up (offset grows)
        var clock = System.Diagnostics.Stopwatch.StartNew();
        DispatcherTimer? hold = null, fling = null;

        void StopFling() => fling?.Stop();

        scroll.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.Pointer.Type != PointerType.Touch) return;
            StopFling();
            box = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<TextBox>().FirstOrDefault();
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
            hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldMs) };
            hold.Tick += (_, _) =>
            {
                hold!.Stop();
                if (dragging || finger is null) return;
                SelectWordAt(heldBox, heldAt);
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
            if (finger is null || e.Pointer != finger) { finger = null; return; }
            finger = null;
            e.Pointer.Capture(null);
            e.Handled = true;
            if (!dragging)
            {
                if (tapped is not null) PlaceCaret(tapped, e.GetPosition(tapped));
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
        int s = i, e = i;
        while (s > 0 && char.IsLetterOrDigit(text[s - 1])) s--;
        while (e < text.Length && char.IsLetterOrDigit(text[e])) e++;
        box.Focus(NavigationMethod.Pointer);
        box.SelectionStart = s;
        box.SelectionEnd = e;
    }
}
