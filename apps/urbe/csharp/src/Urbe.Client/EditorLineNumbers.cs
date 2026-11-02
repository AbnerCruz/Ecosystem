using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>CST-147: logical source lines at their real wrapped TextPresenter positions; no second editor.</summary>
public sealed class EditorLineNumbers : Control
{
    private readonly TextBox _editor;
    private readonly Func<ScrollViewer?> _scroll;
    private string _text = "";
    private int[] _starts = [0];
    private object? _layout;

    public EditorLineNumbers(TextBox editor, Func<ScrollViewer?> scroll)
    {
        _editor = editor;
        _scroll = scroll;
        IsHitTestVisible = false;
        Focusable = false;
        editor.TextChanged += (_, _) => { UpdateStarts(); InvalidateMeasure(); InvalidateVisual(); };
        editor.LayoutUpdated += (_, _) =>
        {
            var layout = Presenter()?.TextLayout;
            if (ReferenceEquals(layout, _layout)) return;
            _layout = layout;
            InvalidateVisual();
        };
    }

    public IReadOnlyList<int> LineStarts => _starts;
    public void ViewportChanged() => InvalidateVisual();
    private TextPresenter? Presenter() => _editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();

    private void UpdateStarts()
    {
        var text = _editor.Text ?? "";
        if (_text == text) return;
        _text = text;
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++) if (text[i] == '\n') starts.Add(i + 1);
        _starts = starts.ToArray();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateStarts();
        return new Size(Math.Max(28, _starts.Length.ToString(CultureInfo.InvariantCulture).Length * _editor.FontSize * .6 + 12), 0);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Presenter() is not { } presenter) return;
        var layout = presenter.TextLayout;
        var origin = presenter.TranslatePoint(default, this) ?? default;
        double top = 0, bottom = Bounds.Height;
        if (_scroll() is { } scroll && this.TranslatePoint(default, scroll) is { } inScroll)
        { top = Math.Max(0, -inScroll.Y); bottom = top + scroll.Viewport.Height; }
        var hit = layout.HitTestPoint(new Point(0, Math.Max(0, top - origin.Y))).TextPosition;
        var first = Array.BinarySearch(_starts, hit);
        first = first < 0 ? Math.Max(0, ~first - 1) : first;
        for (int i = first; i < _starts.Length; i++)
        {
            var rect = layout.HitTestTextPosition(_starts[i]);
            var y = rect.Y + origin.Y;
            if (y > bottom) break;
            if (y + rect.Height < top) continue;
            var label = new FormattedText((i + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface(UrbeTheme.MonoFont), _editor.FontSize * .8, UrbeTheme.Brush(UrbeTheme.Text3));
            context.DrawText(label, new Point(Math.Max(0, Bounds.Width - label.Width - 8), y + (rect.Height - label.Height) / 2));
        }
    }
}
