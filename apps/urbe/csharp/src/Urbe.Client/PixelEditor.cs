using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>A colour in a small flyout: hex field and three sliders, with live change callbacks (the native counterpart of input[type=color]).</summary>
public static class ColorEditor
{
    public static string Hex(Color c) => "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2");

    public static Control Build(string hex, Action<string> changed)
    {
        var color = Color.Parse(hex);
        byte[] ch = [color.R, color.G, color.B];
        var box = new TextBox { Text = Hex(color), FontFamily = UrbeTheme.MonoFont, FontSize = 14, MinHeight = 36, Width = 120, MaxLength = 7 };
        var sliders = new Slider[3];
        bool busy = false;
        void Push()
        {
            var h = Hex(Color.FromRgb(ch[0], ch[1], ch[2]));
            busy = true; box.Text = h; busy = false;
            changed(h);
        }
        var stack = new StackPanel { Spacing = 8, Width = 240 };
        var swatch = new Border { Height = 28, CornerRadius = new CornerRadius(8), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1) };
        stack.Children.Add(swatch);
        string[] names = ["R", "G", "B"];
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            var s = new Slider { Minimum = 0, Maximum = 255, Value = ch[k], SmallChange = 1, LargeChange = 16 };
            sliders[k] = s;
            s.ValueChanged += (_, e) =>
            {
                if (busy) return;
                ch[k] = (byte)Math.Round(e.NewValue);
                swatch.Background = new SolidColorBrush(Color.FromRgb(ch[0], ch[1], ch[2]));
                Push();
            };
            var row = new DockPanel();
            var label = new TextBlock { Text = names[k], Width = 18, FontFamily = UrbeTheme.UiFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(s);
            stack.Children.Add(row);
        }
        swatch.Background = new SolidColorBrush(color);
        box.TextChanged += (_, _) =>
        {
            if (busy || !Color.TryParse(box.Text, out var parsed) || (box.Text?.Length ?? 0) != 7) return;
            busy = true;
            ch[0] = parsed.R; ch[1] = parsed.G; ch[2] = parsed.B;
            for (int i = 0; i < 3; i++) sliders[i].Value = ch[i];
            swatch.Background = new SolidColorBrush(parsed);
            busy = false;
            changed(Hex(parsed));
        };
        stack.Children.Add(box);
        return stack;
    }

    /// <summary>A swatch button that opens the editor; <paramref name="commit"/> runs once, when the flyout closes with a new colour.</summary>
    public static Button Button(string hex, string name, Action<string> preview, Action<string> commit, double width = 44, double height = 36, double radius = 8)
    {
        var swatch = new Border { Background = new SolidColorBrush(Color.Parse(hex)), CornerRadius = new CornerRadius(Math.Max(0, radius - 3)), Margin = new Thickness(3) };
        string current = hex;
        var b = new Button
        {
            Content = swatch, Width = width, Height = height, Padding = new Thickness(0), CornerRadius = new CornerRadius(radius), Background = Brushes.Transparent,
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Avalonia.Automation.AutomationProperties.SetName(b, name);
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Opening += (_, _) => flyout.Content = new Border { Padding = new Thickness(6), Child = Build(current, h => { current = h; swatch.Background = new SolidColorBrush(Color.Parse(h)); preview(h); }) };
        flyout.Closed += (_, _) => { if (!string.Equals(current, hex, StringComparison.OrdinalIgnoreCase)) commit(current); };
        b.Flyout = flyout;
        return b;
    }
}

/// <summary>
/// editorPixels() of src/customize/panel.js: a modal pixel editor (pencil, bucket, dropper, undo, grid,
/// a small palette and "other colour") that returns the pixels as hex strings ("" = transparent) or null.
/// </summary>
public sealed class PixelEditor : Grid
{
    private sealed class Canvas(PixelEditor owner) : Control
    {
        public int Cell = 8;
        public override void Render(DrawingContext context)
        {
            var o = owner;
            for (int j = 0; j < o._h; j++)
                for (int i = 0; i < o._w; i++)
                {
                    var c = o._px[j * o._w + i];
                    var rect = new Rect(i * Cell, j * Cell, Cell, Cell);
                    context.FillRectangle(c.Length > 0 ? new SolidColorBrush(Color.Parse(c)) : ((i + j) & 1) == 1 ? Brushes.Gray : Brushes.LightGray, rect);
                }
            if (o._grid && Cell >= 6)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), 1);
                for (int a = 0; a <= o._w; a++) context.DrawLine(pen, new Point(a * Cell + .5, 0), new Point(a * Cell + .5, o._h * Cell));
                for (int b = 0; b <= o._h; b++) context.DrawLine(pen, new Point(0, b * Cell + .5), new Point(o._w * Cell, b * Cell + .5));
            }
        }
        protected override Size MeasureOverride(Size available) => new(owner._w * Cell, owner._h * Cell);
        protected override void OnPointerPressed(PointerPressedEventArgs e) { e.Pointer.Capture(this); owner.Down(e.GetPosition(this)); e.Handled = true; }
        protected override void OnPointerMoved(PointerEventArgs e) { if (e.Pointer.Captured == this) owner.Move(e.GetPosition(this)); }
        protected override void OnPointerReleased(PointerReleasedEventArgs e) { e.Pointer.Capture(null); owner._dragging = false; }
    }

    private readonly int _w, _h;
    private string[] _px;
    private readonly List<string[]> _history = [];
    private readonly Canvas _canvas;
    private readonly WrapPanel _palette = new() { Margin = new Thickness(12, 4, 12, 4) };
    private readonly TaskCompletionSource<string[]?> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _hasBlank;
    private List<string> _colors;
    private string _color;
    private string _tool = "lapis";
    private bool _grid = true, _dragging;
    private readonly List<Button> _toolButtons = [];

    public PixelEditor(string title, int w, int h, string[] initial, IReadOnlyList<string> colors)
    {
        _w = w; _h = h; _px = (string[])initial.Clone();
        _hasBlank = initial.Any(v => v.Length == 0);
        _colors = colors.Take(10).ToList();
        _color = colors.Count > 0 ? colors[0] : "#000000";
        _canvas = new Canvas(this);
        Background = new SolidColorBrush(Color.FromArgb(153, 5, 6, 8));
        var card = new Border
        {
            Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(22),
            MaxWidth = 560, MaxHeight = 720, Margin = new Thickness(12), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
        var close = IconButton(UrbeTheme.Icons.Close, "Fechar", () => Finish(null));
        var head = new DockPanel { Margin = new Thickness(18, 8, 8, 8) };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(new TextBlock { Text = title, FontFamily = UrbeTheme.UiFont, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center });
        layout.Children.Add(new Border { Child = head, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 0, 0, 1) });
        var stage = new Border { Background = UrbeTheme.Brush(UrbeTheme.Bg), Padding = new Thickness(12), Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = _canvas } };
        stage.SizeChanged += (_, e) =>
        {
            _canvas.Cell = Math.Max(4, (int)Math.Floor(Math.Min((e.NewSize.Width - 24) / _w, (e.NewSize.Height - 24) / _h)));
            _canvas.InvalidateMeasure(); _canvas.InvalidateVisual();
        };
        Grid.SetRow(stage, 1);
        layout.Children.Add(stage);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(12, 10) };
        foreach (var (id, icon, tip) in new[] { ("lapis", CustomizePanel.Icons.Pencil, "Lápis"), ("balde", CustomizePanel.Icons.Bucket, "Balde"), ("conta", CustomizePanel.Icons.Dropper, "Conta-gotas") })
        {
            var b = IconButton(icon, tip, () => { _tool = id; MarkTools(); });
            _toolButtons.Add(b); b.Tag = id;
            tools.Children.Add(b);
        }
        tools.Children.Add(IconButton(CustomizePanel.Icons.Restore, "Desfazer", () => { if (_history.Count > 0) { _px = _history[^1]; _history.RemoveAt(_history.Count - 1); _canvas.InvalidateVisual(); } }));
        var grid = new CheckBox { IsChecked = true, Content = "Grade", FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
        grid.IsCheckedChanged += (_, _) => { _grid = grid.IsChecked == true; _canvas.InvalidateVisual(); };
        tools.Children.Add(grid);
        var toolsBox = new Border { Child = tools, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0) };
        Grid.SetRow(toolsBox, 2);
        layout.Children.Add(toolsBox);
        Grid.SetRow(_palette, 3);
        layout.Children.Add(_palette);
        var cancel = new Button { Content = "Cancelar", MinHeight = 40, Padding = new Thickness(14, 0), CornerRadius = new CornerRadius(12), FontFamily = UrbeTheme.UiFont, FontSize = 14, FontWeight = FontWeight.SemiBold, Background = UrbeTheme.Brush(UrbeTheme.Surface3), Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalContentAlignment = VerticalAlignment.Center };
        cancel.Click += (_, _) => Finish(null);
        var save = new Button { Content = "Salvar", MinHeight = 40, Padding = new Thickness(14, 0), CornerRadius = new CornerRadius(12), FontFamily = UrbeTheme.UiFont, FontSize = 14, FontWeight = FontWeight.SemiBold, Background = UrbeTheme.Brush(UrbeTheme.Accent), Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk), VerticalContentAlignment = VerticalAlignment.Center };
        save.Click += (_, _) => Finish(_px);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        footer.Children.Add(cancel); footer.Children.Add(save);
        Grid.SetRow(footer, 4);
        layout.Children.Add(footer);
        card.Child = layout;
        Children.Add(card);
        MarkTools();
        RebuildPalette();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Finish(null); } };
        Focusable = true;
        AttachedToVisualTree += (_, _) => Focus();
    }

    public int Width_ => _w;
    /// <summary>The editor's current pixels (hex per pixel, "" transparent).</summary>
    public string[] Pixels => _px;
    public string Tool { get => _tool; set { _tool = value; MarkTools(); } }
    public string Color_ { get => _color; set => _color = value; }
    public Task<string[]?> Result => _done.Task;
    /// <summary>Test hook: pointer actions on a cell.</summary>
    public void ApplyAt(int i, int j, bool begin) { if (begin) Begin(); Apply(i, j); }

    private Button IconButton(string icon, string tip, Action click)
    {
        var b = new Button { Content = UrbeTheme.Icon(icon, 20, UrbeTheme.Text2), Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0) };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    private void MarkTools()
    {
        foreach (var b in _toolButtons) b.Background = (string?)b.Tag == _tool ? UrbeTheme.AccentSoft : Brushes.Transparent;
    }

    private void RebuildPalette()
    {
        _palette.Children.Clear();
        foreach (var c in _colors) _palette.Children.Add(Swatch(c, c == _color, () => { _color = c; RebuildPalette(); }));
        if (_hasBlank) _palette.Children.Add(Swatch("", _color.Length == 0, () => { _color = ""; RebuildPalette(); }));
        var other = ColorEditor.Button(_color.Length > 0 ? _color : "#888888", "Outra cor", h => _color = h.ToLowerInvariant(), h =>
        {
            _color = h.ToLowerInvariant();
            if (!_colors.Contains(_color)) { _colors.Insert(0, _color); _colors = _colors.Take(12).ToList(); }
            RebuildPalette();
        }, 38, 38, 19);
        _palette.Children.Add(other);
    }

    private static Button Swatch(string hex, bool on, Action click)
    {
        var b = new Button
        {
            Width = 38, Height = 38, Margin = new Thickness(0, 0, 10, 8), Padding = new Thickness(0), CornerRadius = new CornerRadius(19),
            Background = hex.Length > 0 ? new SolidColorBrush(Color.Parse(hex)) : Brushes.LightGray, BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Line2), BorderThickness = new Thickness(on ? 2 : 1)
        };
        Avalonia.Automation.AutomationProperties.SetName(b, hex.Length > 0 ? hex : "Transparente");
        b.Click += (_, _) => click();
        return b;
    }

    private void Finish(string[]? result) { IsVisible = false; (Parent as Panel)?.Children.Remove(this); _done.TrySetResult(result); }

    private void Begin() { _history.Add((string[])_px.Clone()); if (_history.Count > 60) _history.RemoveAt(0); }

    private void Down(Point p)
    {
        Begin();
        _dragging = _tool == "lapis";
        var (i, j) = Cell(p);
        Apply(i, j);
    }

    private void Move(Point p) { if (_dragging) { var (i, j) = Cell(p); Apply(i, j); } }
    private (int, int) Cell(Point p) => ((int)Math.Floor(p.X / _canvas.Cell), (int)Math.Floor(p.Y / _canvas.Cell));

    private void Apply(int i, int j)
    {
        if (i < 0 || j < 0 || i >= _w || j >= _h) return;
        if (_tool == "conta")
        {
            _color = _px[j * _w + i];
            if (_color.Length > 0 && !_colors.Contains(_color)) _colors.Insert(0, _color);
            RebuildPalette();
            return;
        }
        if (_tool == "balde") Fill(i, j); else _px[j * _w + i] = _color;
        _canvas.InvalidateVisual();
    }

    /// <summary>balde(i, j): four-way flood fill of the colour under the pointer.</summary>
    private void Fill(int i, int j)
    {
        var target = _px[j * _w + i];
        if (target == _color) return;
        var stack = new Stack<(int, int)>();
        stack.Push((i, j));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            if (a < 0 || b < 0 || a >= _w || b >= _h || _px[b * _w + a] != target) continue;
            _px[b * _w + a] = _color;
            stack.Push((a + 1, b)); stack.Push((a - 1, b)); stack.Push((a, b + 1)); stack.Push((a, b - 1));
        }
    }
}
