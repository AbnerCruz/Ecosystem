using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// The editor's own scroll moves (UX.md P3, UC-57): keeping a block in place when it enters or leaves edit mode and
/// keeping the caret line in sight while typing. They are told apart from the reader's scrolling, so the header only
/// hides when the person scrolls.
/// </summary>
public static class EditorScrolling
{
    private static readonly AttachedProperty<long> LastMove =
        AvaloniaProperty.RegisterAttached<ScrollViewer, long>("EditorScrollMove", typeof(EditorScrolling));

    /// <summary>While typing, the caret line is kept above this share of the visible height (the upper two thirds).</summary>
    public const double BandBottom = 2.0 / 3.0;

    /// <summary>Scrolls to <paramref name="y"/> (clamped) and marks the move as the editor's.</summary>
    public static void SetOffsetY(ScrollViewer scroll, double y)
    {
        var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        y = Math.Clamp(y, 0, max);
        if (Math.Abs(y - scroll.Offset.Y) < .5) return;
        MarkMove(scroll);
        scroll.Offset = new Vector(scroll.Offset.X, y);
    }

    /// <summary>Marks a scroll change about to happen as the editor's (a layout change it causes).</summary>
    public static void MarkMove(ScrollViewer scroll) => scroll.SetValue(LastMove, Environment.TickCount64);

    /// <summary>True for a scroll change the editor made in the last <paramref name="ms"/> ms.</summary>
    public static bool MovedByEditor(ScrollViewer scroll, int ms = 300) => Environment.TickCount64 - scroll.GetValue(LastMove) < ms;

    /// <summary>The caret's line in the scroll viewer's coordinates (0 = top of the visible area), or null before layout.</summary>
    public static Rect? CaretRect(TextBox box, ScrollViewer scroll)
    {
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null) return null;
        var len = (box.Text ?? "").Length;
        var r = presenter.TextLayout.HitTestTextPosition(Math.Clamp(box.CaretIndex, 0, len));
        var line = Math.Max(r.Height, box.LineHeight > 0 ? box.LineHeight : box.FontSize * 1.4);
        return presenter.TranslatePoint(r.TopLeft, scroll) is { } p ? new Rect(p.X, p.Y, Math.Max(1, r.Width), line) : null;
    }

    /// <summary>
    /// Keeps the caret visible. While typing (<paramref name="typing"/>) its line stays in the upper two thirds of the
    /// visible area, moved only as much as needed (a line at a time, never a jump); a caret merely placed is only
    /// brought back when it is out of sight.
    /// </summary>
    public static void KeepCaret(TextBox box, ScrollViewer scroll, bool typing)
    {
        if (CaretRect(box, scroll) is not { } r) return;
        var h = scroll.Viewport.Height;
        if (h <= 0) return;
        var bottom = typing ? h * BandBottom : h - 8;
        if (r.Bottom > bottom) SetOffsetY(scroll, scroll.Offset.Y + (r.Bottom - bottom));
        else if (r.Top < 0) SetOffsetY(scroll, scroll.Offset.Y + r.Top - 8);
    }

    /// <summary>
    /// True when the caret's line was inside a visible area <paramref name="height"/> tall (the area before it changed).
    /// When the visible area changes, only a caret the person was looking at is kept in sight: the keyboard opening
    /// over it. A caret the person scrolled away from stays where it is, whatever else changes the height (the header
    /// folding away while scrolling fast once pulled the text back to the caret, observation of 22/10/2026).
    /// </summary>
    public static bool InSight(TextBox box, ScrollViewer scroll, double height) =>
        CaretRect(box, scroll) is { } r && r.Bottom > 0 && r.Top < height;

    /// <summary>The y of <paramref name="c"/>'s top in the scroll viewer's coordinates, or null when not laid out there.</summary>
    public static double? TopIn(Visual c, ScrollViewer scroll) => c.TranslatePoint(default, scroll)?.Y;
}
