using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// CST-147: the numbers of the logical source lines, at their real wrapped TextPresenter positions (a number on the first
/// visual line of each logical line; no second editor). The line with the caret is marked. Typing costs one scan of the
/// text for its line breaks and one redraw of the visible numbers; the gutter only measures again when the count of
/// digits changes.
/// </summary>
public sealed class EditorLineNumbers : Control
{
    /// <summary>Personalização → Editor → "Números das linhas": always, only in code files, or never (Fonte only).</summary>
    public const string Always = "sempre", CodeOnly = "codigo", Never = "nunca";

    private const double Pad = 8;
    private readonly TextBox _editor;
    private readonly Func<ScrollViewer?> _scroll;
    private string _text = "";
    private int[] _starts = [0];
    private int _digits = 1, _current;
    private object? _layout;
    private TextPresenter? _presenter;
    private readonly Dictionary<int, FormattedText> _labels = [];
    private double _labelSize;
    private IBrush? _labelBrush;

    public EditorLineNumbers(TextBox editor, Func<ScrollViewer?> scroll)
    {
        _editor = editor;
        _scroll = scroll;
        IsHitTestVisible = false;
        Focusable = false;
        editor.TextChanged += (_, _) => { if (IsVisible) Changed(); };
        editor.PropertyChanged += (_, e) =>
        {
            if (!IsVisible) return;
            if (e.Property == TextBox.CaretIndexProperty) MoveCurrent();
            else if (e.Property == TextBlock.FontSizeProperty) { _labels.Clear(); InvalidateMeasure(); InvalidateVisual(); }
        };
        editor.LayoutUpdated += (_, _) =>
        {
            if (!IsVisible) return;
            var layout = Presenter()?.TextLayout;
            if (ReferenceEquals(layout, _layout)) return;
            _layout = layout;
            InvalidateVisual();
        };
        editor.DetachedFromVisualTree += (_, _) => _presenter = null;
        editor.TemplateApplied += (_, _) => _presenter = null;
    }

    public IReadOnlyList<int> LineStarts => _starts;

    /// <summary>The logical line (0-based) that holds the caret, drawn marked.</summary>
    public int CurrentLine => _current;

    public void ViewportChanged() { if (IsVisible) InvalidateVisual(); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // hidden, the gutter does no work while typing; shown again, it catches up with the text once
        if (change.Property == IsVisibleProperty && IsVisible) Changed();
    }

    private TextPresenter? Presenter()
    {
        if (_presenter is { } cached) return cached;
        return _presenter = _editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
    }

    private void Changed()
    {
        UpdateStarts();
        var digits = Digits(_starts.Length);
        if (digits != _digits) { _digits = digits; InvalidateMeasure(); }
        MoveCurrent(force: true);
        InvalidateVisual();
    }

    private static int Digits(int count) => count.ToString(CultureInfo.InvariantCulture).Length;

    private void UpdateStarts()
    {
        var text = _editor.Text ?? "";
        if (ReferenceEquals(_text, text) || _text == text) return;
        _text = text;
        var starts = new List<int>(_starts.Length + 8) { 0 };
        var span = text.AsSpan();
        for (int at = 0; ;)
        {
            var next = span[at..].IndexOf('\n');
            if (next < 0) break;
            at += next + 1;
            starts.Add(at);
        }
        _starts = starts.ToArray();
    }

    /// <summary>The logical line of a text position.</summary>
    public int LineOf(int position)
    {
        var i = Array.BinarySearch(_starts, Math.Clamp(position, 0, _text.Length));
        return i < 0 ? Math.Max(0, ~i - 1) : i;
    }

    private void MoveCurrent(bool force = false)
    {
        UpdateStarts();
        var line = LineOf(_editor.CaretIndex);
        if (line == _current && !force) return;
        var changed = line != _current;
        _current = line;
        if (changed) InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateStarts();
        _digits = Digits(_starts.Length);
        return new Size(Math.Max(28, _digits * _editor.FontSize * .6 + 12), 0);
    }

    private FormattedText Label(int number, bool current)
    {
        var size = _editor.FontSize * .8;
        var brush = UrbeTheme.Brush(UrbeTheme.Text3);
        if (size != _labelSize || !ReferenceEquals(brush, _labelBrush) || _labels.Count > 1024)
        {
            _labels.Clear();
            _labelSize = size;
            _labelBrush = brush;
        }
        if (current)
            return new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(UrbeTheme.MonoFont, FontStyle.Normal, FontWeight.SemiBold), size, UrbeTheme.Brush(UrbeTheme.Text));
        if (!_labels.TryGetValue(number, out var label))
            _labels[number] = label = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface(UrbeTheme.MonoFont), size, brush);
        return label;
    }

    /// <summary>Where each visible number is drawn (line index, top, height), in this control's coordinates; for tests.</summary>
    public IReadOnlyList<(int Line, double Y, double Height)> VisibleRows()
    {
        var rows = new List<(int, double, double)>();
        Walk((line, y, height) => rows.Add((line, y, height)));
        return rows;
    }

    private void Walk(Action<int, double, double> row)
    {
        if (Presenter() is not { } presenter) return;
        var layout = presenter.TextLayout;
        var origin = presenter.TranslatePoint(default, this) ?? default;
        double top = 0, bottom = Bounds.Height;
        if (_scroll() is { } scroll && this.TranslatePoint(default, scroll) is { } inScroll)
        { top = Math.Max(0, -inScroll.Y); bottom = top + scroll.Viewport.Height; }
        var first = LineOf(layout.HitTestPoint(new Point(0, Math.Max(0, top - origin.Y))).TextPosition);
        for (int i = first; i < _starts.Length; i++)
        {
            if (_starts[i] > _text.Length) break;
            var rect = layout.HitTestTextPosition(_starts[i]);
            var y = rect.Y + origin.Y;
            if (y > bottom) break;
            if (y + rect.Height < top) continue;
            row(i, y, rect.Height);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Walk((i, y, height) =>
        {
            var current = i == _current;
            if (current)
                context.FillRectangle(UrbeTheme.Brush(UrbeTheme.Accent, .14), new Rect(0, y, Bounds.Width, height), 4);
            var label = Label(i + 1, current);
            context.DrawText(label, new Point(Math.Max(0, Bounds.Width - label.Width - Pad), y + (height - label.Height) / 2));
        });
    }
}
