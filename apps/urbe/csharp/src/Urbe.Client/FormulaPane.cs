using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The formula pane of the 1.8.4 (src/math/editor.js #mathPane, styles/math.css): "Nova fórmula" / "Editar fórmula", No texto
/// or Destaque, the live preview (or "A prévia aparece aqui", or the error), the LaTeX field with the \command suggestions,
/// the symbol tabs (Básico, Grego, Cálculo, Conjuntos, Setas) and Modelos, and Remover / Cancelar / Inserir (Salvar). A
/// bottom sheet on a phone (max-width 640px), a centred card otherwise. Enter inserts, Tab jumps to the next empty {}, a
/// tap outside or × cancels. The answer is the LaTeX and whether it is a display formula.
/// </summary>
public sealed class FormulaPane : Grid
{
    public sealed record Result(string Tex, bool Block, bool Remove = false);

    private readonly Border _card = new();
    private readonly TextBlock _title = new() { FontSize = 15, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _inline, _display, _remove, _ok;
    private readonly Border _preview = new();
    private readonly Border _error = new() { IsVisible = false };
    private readonly TextBlock _errorText = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _sugs = new() { Spacing = 2 };
    private readonly Border _sugsBox = new() { IsVisible = false };
    private readonly MathSymbolsPanel _symbols = new(170) { Margin = new Thickness(12, 10) };
    private TaskCompletionSource<Result?>? _answer;
    private bool _block;
    private List<MathCommand> _list = [];
    private int _active, _from;

    public FormulaPane()
    {
        IsVisible = false;
        Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)); // rgba(0,0,0,.5)
        Input = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 52,
            MaxHeight = 220,
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            Background = UrbeTheme.Brush(UrbeTheme.Bg),
            Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            FontFamily = UrbeTheme.MonoFont,
            FontSize = 15,
            PlaceholderText = "Digite LaTeX: \\frac{a}{b}, x^2, \\sqrt{2}…"
        };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" })
            Input.Resources[key] = UrbeTheme.Brush(UrbeTheme.Bg);
        Input.Resources["TextControlBorderBrushFocused"] = UrbeTheme.Brush(UrbeTheme.Accent);
        Input.TextChanged += (_, _) => { Render(); UpdateSuggestions(); };
        Input.PropertyChanged += (_, e) => { if (e.Property == TextBox.CaretIndexProperty) UpdateSuggestions(); };
        Input.AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // header: the title, No texto | Destaque, ×
        _inline = ModeButton("No texto", false);
        _display = ModeButton("Destaque", true);
        var mode = new Border
        {
            Padding = new Thickness(3),
            CornerRadius = new CornerRadius(10),
            Background = UrbeTheme.Brush(UrbeTheme.Bg),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _inline, _display } },
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Button
        {
            Content = new TextBlock { Text = "×", FontSize = 22, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 36, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(9), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Fechar");
        close.Click += (_, _) => Finish(null);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        var header = new DockPanel { Margin = new Thickness(16, 10, 10, 10) };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(mode, Dock.Right);
        mode.Margin = new Thickness(10, 0);
        header.Children.Add(close);
        header.Children.Add(mode);
        header.Children.Add(_title);

        // .mth-preview: 76px, centred, a dotted grid of 16px
        _preview.MinHeight = 76;
        _preview.Padding = new Thickness(16);
        _preview.Background = new VisualBrush
        {
            Visual = new Avalonia.Controls.Shapes.Ellipse { Width = 2, Height = 2, Fill = UrbeTheme.Brush(UrbeTheme.Line) },
            SourceRect = new RelativeRect(0, 0, 2, 2, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
        _errorText.Foreground = UrbeTheme.Brush(UrbeTheme.Danger);
        _error.Background = UrbeTheme.Brush(UrbeTheme.Danger, .12);
        _error.Padding = new Thickness(16, 8);
        _error.Child = _errorText;

        // .mth-input + .mth-sugs
        _sugsBox.Margin = new Thickness(0, 6, 0, 0);
        _sugsBox.Padding = new Thickness(4);
        _sugsBox.CornerRadius = new CornerRadius(12);
        _sugsBox.BorderThickness = new Thickness(1);
        _sugsBox.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _sugsBox.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _sugsBox.Child = new ScrollViewer { Content = _sugs, MaxHeight = 210 };
        var input = new StackPanel { Margin = new Thickness(12, 10, 12, 0), Children = { Input, _sugsBox } };

        // .mth-syms: tabs then the grid
        _symbols.Picked += tex => InsertSnippet(tex);
        var syms = _symbols;

        // footer: Remover … Cancelar Inserir
        _remove = FooterButton("Remover", UrbeTheme.Danger, Brushes.Transparent, Brushes.Transparent);
        _remove.Click += (_, _) => Finish(new Result(Input.Text ?? "", _block, Remove: true));
        var cancel = FooterButton("Cancelar", UrbeTheme.Text, UrbeTheme.Brush(UrbeTheme.Surface2), UrbeTheme.Brush(UrbeTheme.Line2));
        cancel.Click += (_, _) => Finish(null);
        _ok = FooterButton("Inserir", UrbeTheme.AccentInk, UrbeTheme.Brush(UrbeTheme.Accent), UrbeTheme.Brush(UrbeTheme.Accent));
        _ok.Click += (_, _) => Commit();
        var footer = new DockPanel { Margin = new Thickness(12, 10) };
        DockPanel.SetDock(_remove, Dock.Left);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, _ok } };
        footer.Children.Add(_remove);
        footer.Children.Add(right);

        Border Line(Control c, bool top) => new() { BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = top ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 0, 1), Child = c };
        var body = new StackPanel { Children = { Line(header, false), _preview, _error, input, syms } };
        var layout = new DockPanel();
        var foot = Line(footer, true);
        DockPanel.SetDock(foot, Dock.Bottom);
        layout.Children.Add(foot);
        layout.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        _card.Child = layout;
        _card.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _card.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _card.BorderThickness = new Thickness(1);
        _card.BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        _card.ClipToBounds = true;
        Children.Add(_card);
        PointerPressed += (_, e) => { if (ReferenceEquals(e.Source, this)) Finish(null); };
        SizeChanged += (_, e) => Place(e.NewSize);
    }

    /// <summary>The LaTeX field (the pane's textarea).</summary>
    public TextBox Input { get; }

    public bool IsOpen => IsVisible;

    /// <summary>openPane: a new formula (<paramref name="editing"/> false: "Nova fórmula", Inserir) or an existing one ("Editar
    /// fórmula", Salvar, Remover). Null when cancelled.</summary>
    public Task<Result?> ShowAsync(string tex, bool block, bool editing = false)
    {
        _answer?.TrySetResult(null);
        _answer = new TaskCompletionSource<Result?>();
        _block = block;
        _title.Text = editing ? "Editar fórmula" : "Nova fórmula";
        ((TextBlock)_ok.Content!).Text = editing ? "Salvar" : "Inserir";
        _remove.IsVisible = editing;
        Input.Text = tex;
        IsVisible = true;
        _symbols.Build();
        Render();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { Input.Focus(); Input.CaretIndex = Input.Text?.Length ?? 0; }, Avalonia.Threading.DispatcherPriority.Loaded);
        return _answer.Task;
    }

    /// <summary>commitPane: an empty formula removes the one being edited (or inserts nothing).</summary>
    public void Commit()
    {
        var tex = (Input.Text ?? "").Trim();
        Finish(tex.Length == 0 ? (_remove.IsVisible ? new Result("", _block, Remove: true) : null) : new Result(tex, _block));
    }

    public void Cancel() => Finish(null);

    /// <summary>A symbol or template button: insertSnippet into the field (● is where the caret goes).</summary>
    public void InsertSnippet(string template, int? replaceFrom = null)
    {
        var sn = MathEditing.Snippet(template);
        var text = Input.Text ?? "";
        int a = replaceFrom ?? Math.Min(Input.SelectionStart, Input.SelectionEnd), b = Math.Max(Input.SelectionStart, Input.SelectionEnd);
        if (replaceFrom is not null) b = Input.CaretIndex;
        a = Math.Clamp(a, 0, text.Length);
        b = Math.Clamp(b, a, text.Length);
        Input.Text = text[..a] + sn.Text + text[b..];
        Input.SelectionStart = Input.SelectionEnd = Input.CaretIndex = a + sn.Caret;
        Input.Focus();
    }

    /// <summary>The suggestions shown for the \command before the caret.</summary>
    public IReadOnlyList<string> Suggestions => _list.Select(c => c.Cmd).ToList();

    private void Finish(Result? result)
    {
        if (!IsVisible) return;
        IsVisible = false;
        _list = [];
        _sugsBox.IsVisible = false;
        var answer = _answer;
        _answer = null;
        answer?.TrySetResult(result);
    }

    private void Place(Size size)
    {
        // (pointer:coarse),(max-width:640px): a bottom sheet with the top corners rounded, at most 92 % tall
        bool phone = size.Width <= 640 || OperatingSystem.IsAndroid();
        _card.Width = phone ? double.NaN : Math.Min(640, size.Width - 32);
        _card.HorizontalAlignment = phone ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _card.VerticalAlignment = phone ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        _card.CornerRadius = phone ? new CornerRadius(22, 22, 0, 0) : new CornerRadius(22);
        _card.MaxHeight = phone ? size.Height * .92 : size.Height - 52;
        _card.Margin = phone ? new Thickness(0) : new Thickness(16);
        _symbols.CellMin = phone ? 44 : 46;
        _symbols.LayoutGrid();
    }

    // ------------------------------------------------------------------ preview

    /// <summary>paneRender: the preview (or the placeholder), the error line, which mode is on.</summary>
    private void Render()
    {
        var tex = Input.Text ?? "";
        string? err = null;
        if (tex.Trim().Length == 0)
            _preview.Child = new TextBlock { Text = "A prévia aparece aqui", FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        else
        {
            var view = Formula(tex, _block, 17.5 * 1.21, out err);
            _preview.Child = view ?? new TextBlock { Text = tex, FontFamily = UrbeTheme.MonoFont, Foreground = UrbeTheme.Brush(UrbeTheme.Warn), HorizontalAlignment = HorizontalAlignment.Center };
        }
        _error.IsVisible = err is not null;
        _errorText.Text = err is null ? "" : "⚠ " + err;
        Mode(_inline, !_block);
        Mode(_display, _block);
    }

    /// <summary>M.render: the formula drawn by the Core renderer, or null with the reason.</summary>
    internal static Control? Formula(string tex, bool display, double size, out string? error)
    {
        error = null;
        try
        {
            var r = MathRenderer.RenderSvg(tex, display, (float)Math.Clamp(size, 6, 256));
            if (r.Success && MathSvg.TryBuild(r.Svg, UrbeTheme.Text) is { } view)
            {
                view.HorizontalAlignment = HorizontalAlignment.Center;
                view.VerticalAlignment = VerticalAlignment.Center;
                return view;
            }
            error = r.Diagnostic ?? "Fórmula inválida.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            error = ex.Message;
        }
        return null;
    }

    // ------------------------------------------------------------------ suggestions (completer)

    private void UpdateSuggestions()
    {
        var text = Input.Text ?? "";
        int pos = Input.CaretIndex;
        var m = System.Text.RegularExpressions.Regex.Match(text[..Math.Clamp(pos, 0, text.Length)], @"\\[A-Za-z]{1,30}$");
        if (!m.Success || Input.SelectionStart != Input.SelectionEnd)
        {
            _list = [];
            _sugsBox.IsVisible = false;
            return;
        }
        _from = pos - m.Length;
        _list = MathEditing.Complete(m.Value, 8).ToList();
        _active = 0;
        PaintSuggestions();
    }

    private void PaintSuggestions()
    {
        _sugs.Children.Clear();
        _sugsBox.IsVisible = _list.Count > 0;
        for (int i = 0; i < _list.Count; i++)
        {
            var c = _list[i];
            int index = i;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
            var code = new TextBlock { Text = c.Cmd, FontFamily = UrbeTheme.MonoFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), VerticalAlignment = VerticalAlignment.Center };
            var prev = Formula(MathEditing.Snippet(c.Snip).Text.Replace("{}", "{\\square}", StringComparison.Ordinal), false, 15, out _) ?? new TextBlock();
            prev.Margin = new Thickness(10, 0);
            var desc = new TextBlock { Text = c.Desc, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(prev, 1);
            Grid.SetColumn(desc, 2);
            row.Children.Add(code);
            row.Children.Add(prev);
            row.Children.Add(desc);
            var b = new Button
            {
                Content = row, MinHeight = 38, Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(8), Focusable = false,
                Background = i == _active ? UrbeTheme.AccentSoft : Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            b.Click += (_, _) => Accept(index);
            _sugs.Children.Add(b);
        }
    }

    private void Accept(int i)
    {
        if (i < 0 || i >= _list.Count) return;
        var c = _list[i];
        _list = [];
        _sugsBox.IsVisible = false;
        InsertSnippet(c.Snip, _from);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_list.Count > 0 && _sugsBox.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Down: e.Handled = true; _active = (_active + 1) % _list.Count; PaintSuggestions(); return;
                case Key.Up: e.Handled = true; _active = (_active - 1 + _list.Count) % _list.Count; PaintSuggestions(); return;
                case Key.Enter or Key.Tab: e.Handled = true; Accept(_active); return;
                case Key.Escape: e.Handled = true; _list = []; _sugsBox.IsVisible = false; return;
            }
        }
        if (e.Key == Key.Tab && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // jumpNext: the next empty {} after the caret, else the end
            e.Handled = true;
            var text = Input.Text ?? "";
            var k = text.IndexOf("{}", Input.CaretIndex, StringComparison.Ordinal);
            Input.CaretIndex = k >= 0 ? k + 1 : text.Length;
            return;
        }
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { e.Handled = true; Commit(); return; }
        if (e.Key == Key.Escape) { e.Handled = true; Finish(null); }
    }

    private Button ModeButton(string label, bool block)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = label, FontSize = 12.5, FontWeight = FontWeight.SemiBold },
            Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(0), Focusable = false
        };
        b.Click += (_, _) => { _block = block; Render(); };
        return b;
    }

    private static void Mode(Button b, bool on)
    {
        b.Background = on ? UrbeTheme.Brush(UrbeTheme.Surface3) : Brushes.Transparent;
        ((TextBlock)b.Content!).Foreground = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Text2);
    }

    private static Button FooterButton(string label, string color, IBrush background, IBrush border) => new()
    {
        Content = new TextBlock { Text = label, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(color) },
        MinHeight = 40, Padding = new Thickness(16, 0), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
        Background = background, BorderBrush = border, VerticalContentAlignment = VerticalAlignment.Center, Focusable = false
    };
}
