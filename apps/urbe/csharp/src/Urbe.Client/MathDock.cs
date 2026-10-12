using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The formula bar of the Fonte mode (math/editor.js #mathDock, styles/math.css): while the caret is inside a formula ($…$,
/// $$…$$) it shows its preview (or "Fórmula vazia", or the error), the \command suggestions in a row and, on a touch device
/// or after ∑, the symbols; a symbol goes into the formula (outside one it becomes $symbol$). Tab inside a formula jumps to
/// its next empty {} and then out of it. On a phone it sits at the bottom across the width; elsewhere it floats 12px from the
/// bottom left of the text, at most 760px wide.
/// </summary>
public sealed class MathDock : Border
{
    private readonly TextBox _source;
    private readonly Func<bool> _isSource;
    private readonly Border _preview = new() { VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
    private readonly StackPanel _sugs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Border _sugsBox = new() { IsVisible = false };
    private readonly MathSymbolsPanel _symbols = new(132);
    private readonly Border _symsBox = new() { IsVisible = false };
    private bool _symsOpen;
    private List<MathCommand> _list = [];
    private int _active, _from;
    private readonly bool _coarse = OperatingSystem.IsAndroid();

    public MathDock(TextBox source, Func<bool> isSource)
    {
        _source = source;
        _isSource = isSource;
        IsVisible = false;
        VerticalAlignment = VerticalAlignment.Bottom;
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        ClipToBounds = true;
        if (_coarse)
        {
            // (pointer:coarse): across the bottom, top corners rounded, no side or bottom border
            HorizontalAlignment = HorizontalAlignment.Stretch;
            CornerRadius = new CornerRadius(16, 16, 0, 0);
            BorderThickness = new Thickness(0, 1, 0, 0);
            _symbols.CellMin = 44;
        }
        else
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            CornerRadius = new CornerRadius(16);
            BorderThickness = new Thickness(1);
            Margin = new Thickness(12, 0, 12, 12);
        }

        // .mth-dock-top: the preview and the ∑ toggle
        var toggle = new Button
        {
            Content = new TextBlock { Text = "∑", FontFamily = new FontFamily("Georgia, serif"), FontWeight = FontWeight.SemiBold, FontSize = 18, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 40, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Focusable = false,
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), Background = UrbeTheme.Brush(UrbeTheme.Surface2),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(toggle, "Símbolos");
        toggle.Click += (_, _) => { _symsOpen = !_symsOpen; Update(); };
        var top = new DockPanel { MinHeight = 48, Margin = new Thickness(14, 6, 6, 6) };
        DockPanel.SetDock(toggle, Dock.Right);
        top.Children.Add(toggle);
        top.Children.Add(new ScrollViewer { Content = _preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });

        // #mathDock .mth-sugs: a row; .mth-syms with a line above
        _sugsBox.Margin = new Thickness(8, 0, 8, 8);
        _sugsBox.Padding = new Thickness(4);
        _sugsBox.CornerRadius = new CornerRadius(12);
        _sugsBox.BorderThickness = new Thickness(1);
        _sugsBox.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _sugsBox.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _sugsBox.Child = new ScrollViewer { Content = _sugs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _symbols.Margin = new Thickness(10, 8);
        _symbols.Picked += Pick;
        _symsBox.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        _symsBox.BorderThickness = new Thickness(0, 1, 0, 0);
        _symsBox.Child = _symbols;
        Child = new StackPanel { Children = { top, _sugsBox, _symsBox } };

        _source.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.SelectionEndProperty) Update();
        };
        _source.GotFocus += (_, _) => Update();
        _source.LostFocus += (_, _) => Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            if (!_source.IsFocused) { IsVisible = false; Close(); }
        }, TimeSpan.FromMilliseconds(150));
        _source.AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>The formula the caret is in (currentMath), or null: Fonte, focused, no selection.</summary>
    public MathFormula? Current
    {
        get
        {
            if (!_isSource() || !_source.IsFocused) return null;
            if (_source.SelectionStart != _source.SelectionEnd) return null;
            return MathEditing.FindAt(_source.Text ?? "", _source.CaretIndex);
        }
    }

    /// <summary>The suggestions shown.</summary>
    public IReadOnlyList<string> Suggestions => _list.Select(c => c.Cmd).ToList();

    /// <summary>The symbols are shown (∑ or a touch device inside a formula).</summary>
    public bool SymbolsShown => _symsBox.IsVisible;

    /// <summary>The ∑ toggle (symsOpen).</summary>
    public void ToggleSymbols() { _symsOpen = !_symsOpen; Update(); }

    /// <summary>updateDock.</summary>
    public void Update()
    {
        var it = Current;
        var show = it is not null || (_symsOpen && _source.IsFocused && _isSource());
        if (!show) { IsVisible = false; Close(); return; }
        IsVisible = true;
        if (it is not null)
        {
            if (it.Tex.Trim().Length == 0) _preview.Child = Placeholder("Fórmula vazia");
            else
            {
                var view = FormulaPane.Formula(it.Tex, false, 15.4 * 1.21, out var err);
                _preview.Child = view is not null
                    ? view
                    : new TextBlock { Text = "⚠ " + err, FontSize = 12.5, Foreground = UrbeTheme.Brush(UrbeTheme.Danger), TextWrapping = TextWrapping.Wrap };
                if (view is not null) view.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
        else
        {
            var hint = new TextBlock { FontSize = 12.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center };
            hint.Inlines!.Add(new Avalonia.Controls.Documents.Run("Toque num símbolo para inserir · "));
            hint.Inlines.Add(new Avalonia.Controls.Documents.Run("$…$") { FontFamily = UrbeTheme.MonoFont, FontSize = 12 });
            hint.Inlines.Add(new Avalonia.Controls.Documents.Run(" cria uma fórmula"));
            _preview.Child = hint;
        }
        _symsBox.IsVisible = _symsOpen || (_coarse && it is not null);
        UpdateSuggestions(it is not null);
    }

    private static TextBlock Placeholder(string text) => new() { Text = text, FontSize = 12.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The width beside the text: min(width − 24, 760) on a large screen.</summary>
    public void Place(double width)
    {
        if (!_coarse) Width = Math.Max(0, Math.Min(width - 24, 760));
    }

    private void Close()
    {
        _list = [];
        _sugsBox.IsVisible = false;
    }

    /// <summary>A symbol of the dock: inside a formula as it is, outside one as $symbol$ (insertSnippet).</summary>
    private void Pick(string tex)
    {
        var t = Current is null ? "$" + tex + "$" : tex;
        Insert(t, null);
        Update();
    }

    private void Insert(string template, int? replaceFrom)
    {
        var sn = MathEditing.Snippet(template);
        var text = _source.Text ?? "";
        int a = replaceFrom ?? Math.Min(_source.SelectionStart, _source.SelectionEnd), b = replaceFrom is null ? Math.Max(_source.SelectionStart, _source.SelectionEnd) : _source.CaretIndex;
        a = Math.Clamp(a, 0, text.Length);
        b = Math.Clamp(b, a, text.Length);
        _source.Text = text[..a] + sn.Text + text[b..];
        _source.SelectionStart = _source.SelectionEnd = _source.CaretIndex = a + sn.Caret;
        _source.Focus();
    }

    // ------------------------------------------------------------------ suggestions (completer bound to the textarea)

    private void UpdateSuggestions(bool inMath)
    {
        var text = _source.Text ?? "";
        int pos = Math.Clamp(_source.CaretIndex, 0, text.Length);
        var m = System.Text.RegularExpressions.Regex.Match(text[..pos], @"\\[A-Za-z]{1,30}$");
        if (!m.Success || _source.SelectionStart != _source.SelectionEnd || !inMath) { Close(); return; }
        _from = pos - m.Length;
        _list = MathEditing.Complete(m.Value, 8).ToList();
        _active = 0;
        Paint();
    }

    private void Paint()
    {
        _sugs.Children.Clear();
        _sugsBox.IsVisible = _list.Count > 0;
        for (int i = 0; i < _list.Count; i++)
        {
            var c = _list[i];
            int index = i;
            var prev = FormulaPane.Formula(MathEditing.Snippet(c.Snip).Text.Replace("{}", "{\\square}", StringComparison.Ordinal), false, 15, out _) ?? new TextBlock();
            prev.Margin = new Thickness(10, 0, 0, 0);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = c.Cmd, FontFamily = UrbeTheme.MonoFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), VerticalAlignment = VerticalAlignment.Center }, prev } };
            var b = new Button
            {
                Content = row, MinHeight = 38, Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(8), Focusable = false, BorderThickness = new Thickness(0),
                Background = i == _active ? UrbeTheme.AccentSoft : Brushes.Transparent
            };
            b.Click += (_, _) => Accept(index);
            _sugs.Children.Add(b);
        }
    }

    private void Accept(int i)
    {
        if (i < 0 || i >= _list.Count) return;
        var c = _list[i];
        Close();
        Insert(c.Snip, _from);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (IsVisible && _list.Count > 0 && _sugsBox.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Down or Key.Up: e.Handled = true; _active = (_active + (e.Key == Key.Down ? 1 : -1) + _list.Count) % _list.Count; Paint(); return;
                case Key.Enter or Key.Tab: e.Handled = true; Accept(_active); return;
                case Key.Escape: e.Handled = true; Close(); return;
            }
        }
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && Current is { } it)
        {
            // jumpNext inside the formula; with no empty {} left the caret leaves it
            e.Handled = true;
            var text = _source.Text ?? "";
            int limit = it.End - it.Close.Length;
            int k = text.IndexOf("{}", _source.CaretIndex, StringComparison.Ordinal);
            int caret = k >= 0 && k < limit ? k + 1 : limit;
            if (caret == limit) caret = it.End;
            _source.SelectionStart = _source.SelectionEnd = _source.CaretIndex = caret;
            Update();
        }
    }
}
