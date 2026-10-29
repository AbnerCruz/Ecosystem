using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Urbe.Client.Ui;

/// <summary>
/// An item of a long list that is made only when it comes near the screen: until then it is an empty line of about its
/// height. Every item is there (the list scrolls the whole way and keeps its order); only the controls of the ones far
/// from the screen wait. <see cref="Watch"/> makes a list's items as they come near.
/// </summary>
public sealed class Deferred : Decorator
{
    private Func<Control>? _make;

    public Deferred(Func<Control> make, double estimatedHeight = 56)
    {
        _make = make;
        MinHeight = EstimatedHeight = estimatedHeight;
    }

    public bool IsMade => _make is null;
    public double EstimatedHeight { get; }

    /// <summary>The height it takes: the measured one once laid out, the estimate before.</summary>
    public double TakenHeight => Bounds.Height > 0 && IsArrangeValid ? Bounds.Height : EstimatedHeight;

    /// <summary>The control itself (made now if it was still waiting).</summary>
    public Control View
    {
        get { Make(); return Child!; }
    }

    public void Make()
    {
        if (_make is not { } make) return;
        _make = null;
        Child = make();
        MinHeight = 0;
    }

    /// <summary>The view of an item of a watched list: the made control, or the item itself if it is not deferred.</summary>
    public static Control? Of(Control item) => item is Deferred d ? d.Child : item;

    /// <summary>
    /// Makes the deferred items of <paramref name="list"/> (a vertical panel inside a scroll viewer) that are within a
    /// screen of the visible part, after each layout and each scroll. An item made above the visible part that turns out
    /// taller or shorter than its estimate moves the scroll by the difference, so what is on the screen stays still.
    /// </summary>
    public static void Watch(Panel list)
    {
        ScrollViewer? scroll = null;
        var above = new List<(Deferred Item, double Estimate)>();
        void Check(bool laidOut)
        {
            if (scroll is null || !list.IsEffectivelyVisible || list.Children.Count == 0) return;
            if (laidOut && above.Count > 0)
            {
                // an item made during a layout is measured by the next one: only those already laid out count
                double shift = 0;
                above.RemoveAll(x =>
                {
                    if (!x.Item.IsMeasureValid || !x.Item.IsArrangeValid) return false;
                    shift += x.Item.Bounds.Height - x.Estimate;
                    return true;
                });
                if (Math.Abs(shift) > .5) scroll.Offset = scroll.Offset.WithY(Math.Max(0, scroll.Offset.Y + shift));
            }
            if (list.TranslatePoint(default, scroll) is not { } origin) return;
            double viewport = scroll.Viewport.Height > 0 ? scroll.Viewport.Height : scroll.Bounds.Height;
            double from = -viewport, to = viewport * 2;
            foreach (var child in list.Children)
            {
                if (child is not Deferred { IsMade: false } waiting) continue;
                double top = origin.Y + child.Bounds.Y;
                if (top > to) break;   // the items are in order: the rest are further down
                if (top + child.Bounds.Height < from) continue;
                if (top + child.Bounds.Height <= 0) above.Add((waiting, child.Bounds.Height));   // its real height is known after the layout
                waiting.Make();
            }
        }
        void Scrolled(object? sender, ScrollChangedEventArgs e) => Check(false);
        void Laid(object? sender, EventArgs e) => Check(true);
        list.AttachedToVisualTree += (_, _) =>
        {
            // the scroll that moves the list up and down (a code block's own scroll only goes sideways)
            scroll = list.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault(s => s.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
            if (scroll is null) return;
            scroll.ScrollChanged += Scrolled;
            list.LayoutUpdated += Laid;
        };
        list.DetachedFromVisualTree += (_, _) =>
        {
            if (scroll is not null) scroll.ScrollChanged -= Scrolled;
            list.LayoutUpdated -= Laid;
            scroll = null;
        };
    }
}
