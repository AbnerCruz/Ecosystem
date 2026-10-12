using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// symbolsHtml() of math/editor.js, shared by the formula pane and the formula bar of Fonte: the tabs (Básico, Grego, Cálculo,
/// Conjuntos, Setas, Modelos; the chosen one in the accent) and a grid of symbols (42px buttons in a serif face) or templates
/// (their formula drawn, the name under it), repeat(auto-fill, minmax(46px or 118px, 1fr)) with a 6px gap. The tab chosen is
/// shared, as tabIdx is in the original.
/// </summary>
public sealed class MathSymbolsPanel : StackPanel
{
    private static int s_tab;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly WrapPanel _grid = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _gridScroll;
    private double _gridWidth;

    public MathSymbolsPanel(double maxGridHeight)
    {
        var tabs = new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 8) };
        _gridScroll = new ScrollViewer { Content = _grid, MaxHeight = maxGridHeight, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _gridScroll.SizeChanged += (_, e) => { _gridWidth = e.NewSize.Width; LayoutGrid(); };
        Children.Add(tabs);
        Children.Add(_gridScroll);
        Build();
    }

    /// <summary>A symbol or template was tapped: its snippet (● where the caret goes).</summary>
    public event Action<string>? Picked;

    /// <summary>The narrowest symbol cell: 46px, 44px on a phone.</summary>
    public double CellMin { get; set; } = 46;

    public void Build()
    {
        s_tab = Math.Min(s_tab, MathEditing.Symbols.Count);
        BuildTabs();
        BuildGrid();
    }

    private void BuildTabs()
    {
        _tabs.Children.Clear();
        var names = MathEditing.Symbols.Select(g => g.Name).Append("Modelos").ToList();
        for (int i = 0; i < names.Count; i++)
        {
            int index = i;
            bool on = i == s_tab;
            var b = new Button
            {
                Content = new TextBlock { Text = names[i], FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(on ? UrbeTheme.AccentInk : UrbeTheme.Text2) },
                Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(99), Focusable = false, BorderThickness = new Thickness(1),
                Background = on ? UrbeTheme.Brush(UrbeTheme.Accent) : UrbeTheme.Brush(UrbeTheme.Surface2),
                BorderBrush = on ? UrbeTheme.Brush(UrbeTheme.Accent) : UrbeTheme.Brush(UrbeTheme.Line)
            };
            b.Click += (_, _) => { s_tab = index; Build(); };
            _tabs.Children.Add(b);
        }
    }

    private void BuildGrid()
    {
        _grid.Children.Clear();
        if (s_tab < MathEditing.Symbols.Count)
        {
            foreach (var s in MathEditing.Symbols[s_tab].Items)
            {
                var sym = s;
                var b = new Button
                {
                    Content = new TextBlock { Text = sym.Label, FontFamily = new FontFamily("Times New Roman, Georgia, serif"), FontSize = 17, Foreground = UrbeTheme.Brush(UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    Height = 42, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), Focusable = false, BorderThickness = new Thickness(1),
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), Background = UrbeTheme.Brush(UrbeTheme.Surface2),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
                };
                ToolTip.SetTip(b, MathEditing.Snippet(sym.Tex).Text);
                b.Click += (_, _) => Picked?.Invoke(sym.Tex);
                _grid.Children.Add(b);
            }
        }
        else
        {
            foreach (var t in MathEditing.Templates)
            {
                var tpl = t;
                var prev = FormulaPane.Formula("\\displaystyle " + MathEditing.Snippet(tpl.Tex).Text.Replace("{}", "{\\square}", StringComparison.Ordinal), false, 15, out _) ?? new TextBlock();
                var b = new Button
                {
                    Content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, Children = { prev, new TextBlock { Text = tpl.Label, FontSize = 11.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center } } },
                    MinHeight = 74, Padding = new Thickness(6), CornerRadius = new CornerRadius(12), Focusable = false, BorderThickness = new Thickness(1),
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), Background = UrbeTheme.Brush(UrbeTheme.Surface2),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
                };
                b.Click += (_, _) => Picked?.Invoke(tpl.Tex);
                _grid.Children.Add(b);
            }
        }
        LayoutGrid();
    }

    public void LayoutGrid()
    {
        if (_gridWidth <= 0) return;
        double min = s_tab < MathEditing.Symbols.Count ? CellMin : 118, gap = 6;
        int columns = Math.Max(1, (int)Math.Floor((_gridWidth + gap) / (min + gap)));
        double w = Math.Floor((_gridWidth - gap * (columns - 1)) / columns);
        for (int i = 0; i < _grid.Children.Count; i++)
        {
            var c = (Control)_grid.Children[i];
            c.Width = w;
            c.Margin = new Thickness(0, 0, (i + 1) % columns == 0 ? 0 : gap, gap);
        }
    }
}
