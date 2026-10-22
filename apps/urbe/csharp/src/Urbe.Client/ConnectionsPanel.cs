using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-71 (ADD-0029): "Conexões" of the open note — a calm local graph (the note in the middle, the mutual links close,
/// the notes it cites and the ones citing it around) and, under it, the full list grouped by relation. A tap on a
/// point or a row opens that note, and the panel follows it. Placed like the "Contexto" panel: 360 px at the right of
/// the editor, the whole width minus 8 px on each side up to 600 px.
/// </summary>
public sealed class ConnectionsPanel : Border
{
    private readonly StackPanel _list = new();
    private readonly TextBlock _title = new() { FontWeight = FontWeight.Bold, FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    public ConnectionsPanel()
    {
        IsVisible = false;
        Margin = new Thickness(8);
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(16);
        BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        ClipToBounds = true;
        var close = Ui.UrbeMotion.Pressable(new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 20, UrbeTheme.Text),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4)
        });
        ToolTip.SetTip(close, "Fechar");
        close.Click += (_, _) => IsVisible = false;
        var header = new DockPanel { Height = 42, Margin = new Thickness(12, 0) };
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(_title);
        Graph.NoteChosen += path => NoteChosen?.Invoke(path);
        Child = new DockPanel
        {
            Children =
            {
                new Border { Child = header, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(0, 0, 0, 1), [DockPanel.DockProperty] = Dock.Top },
                new ScrollViewer { Content = new StackPanel { Children = { Graph, new Border { Padding = new Thickness(8, 0, 8, 8), Child = _list } } } }
            }
        };
    }

    /// <summary>A note was chosen (a point or a row): its path.</summary>
    public event Action<string>? NoteChosen;

    /// <summary>The drawing.</summary>
    public ConnectionsGraph Graph { get; } = new() { Height = 300, Margin = new Thickness(8) };

    /// <summary>The panel's width for the editor's width (min(360px, 88vw); left:8px under 600 px).</summary>
    public void Place(double width)
    {
        bool narrow = width <= 600;
        HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        Width = narrow ? double.NaN : Math.Min(360, width * .88);
        // the drawing about as tall as it is wide
        Graph.Height = Math.Clamp((narrow ? width - 16 : Width) - 34, 240, 360);
    }

    /// <summary>The connections of <paramref name="graph"/>'s note, as it is now.</summary>
    public void Show(LinkGraphView graph)
    {
        _title.Text = "Conexões · " + graph.Title;
        Graph.View = graph;
        _list.Children.Clear();
        if (graph.Neighbours.Count == 0)
        {
            _list.Children.Add(Text("Esta nota ainda não tem ligações. Escreva [[ e o nome de outra nota para ligar as duas.", UrbeTheme.Text2, 13));
            IsVisible = true;
            return;
        }
        if (graph.Hidden > 0)
            _list.Children.Add(Text(graph.Hidden == 1 ? "1 ligação está só na lista." : graph.Hidden + " ligações estão só na lista.", UrbeTheme.Text3, 12));
        foreach (var (relation, label) in new[] { (LinkRelation.Mutual, "Ligação mútua"), (LinkRelation.Cites, "Cita"), (LinkRelation.CitedBy, "Citada por") })
        {
            var group = graph.Neighbours.Where(n => n.Relation == relation).ToList();
            if (group.Count == 0) continue;
            _list.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant() + " · " + group.Count, FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = .88,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(6, 12, 6, 6)
            });
            foreach (var n in group) _list.Children.Add(Row(n));
        }
        IsVisible = true;
    }

    /// <summary>The titles listed (tests).</summary>
    public IEnumerable<string> Items => _list.Children.OfType<Button>().Select(b => AutomationProperties.GetName(b) ?? "");

    private static TextBlock Text(string text, string color, double size) =>
        new() { Text = text, Margin = new Thickness(8), FontSize = size, Foreground = UrbeTheme.Brush(color), TextWrapping = TextWrapping.Wrap };

    private Button Row(LinkNeighbour n)
    {
        var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(ConnectionsGraph.FolderColor(n.Folder)), VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = n.Title, FontSize = 14, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis };
        var where = new TextBlock { Text = n.Folder.Length == 0 ? "Na raiz" : n.Folder, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis };
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { dot, new StackPanel { Children = { name, where } } } },
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8), MinHeight = 48
        });
        AutomationProperties.SetName(b, n.Title);
        b.Click += (_, _) => NoteChosen?.Invoke(n.Path);
        return b;
    }
}

/// <summary>
/// The drawing of <see cref="ConnectionsPanel"/>: a night sky with soft points of light — the note in the middle in
/// the accent, its neighbours in the colour of their folder, bigger when more connected, joined to it by faint lines
/// (brighter for the mutual ones). Nothing moves: it is drawn again only when the note or the size changes.
/// </summary>
public sealed class ConnectionsGraph : Control
{
    private static readonly Color[] Palette =
        new[] { "#8fb3ff", "#7fdca6", "#f1c46e", "#c3a6ff", "#ff9fb4", "#7fd0e6", "#e8b48c", "#b5d97a" }.Select(Color.Parse).ToArray();

    private LinkGraphView? _view;

    public ConnectionsGraph()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        Tapped += (_, e) => TapAt(e.GetPosition(this));
    }

    /// <summary>A neighbour's point was tapped: its path.</summary>
    public event Action<string>? NoteChosen;

    public LinkGraphView? View
    {
        get => _view;
        set
        {
            _view = value;
            AutomationProperties.SetName(this, value is null ? "Conexões" : "Conexões de " + value.Title + ": " + value.Neighbours.Count + " ligações");
            InvalidateVisual();
        }
    }

    /// <summary>The soft colour of a folder (its first level), the same on every run; the root is a pale starlight.</summary>
    public static Color FolderColor(string folder)
    {
        if (folder.Length == 0) return Color.Parse("#cfd6e4");
        var top = folder.Split('/')[0];
        return Palette[LinkGraph.Hash(top) % (uint)Palette.Length];
    }

    /// <summary>Where a neighbour is drawn, in this control's coordinates (tests and taps).</summary>
    public Point? PointOf(string path) =>
        _view?.Placed.FirstOrDefault(n => n.Path == path) is { } n ? Map(n.X, n.Y) : null;

    /// <summary>A tap: the nearest neighbour within reach opens; the middle and the empty sky do nothing.</summary>
    public bool TapAt(Point p)
    {
        if (_view is null) return false;
        LinkNeighbour? best = null;
        double bestD = double.MaxValue;
        foreach (var n in _view.Placed)
        {
            var q = Map(n.X, n.Y);
            double d = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
            if (d < Math.Max(22, Radius(n) + 12) && d < bestD) (best, bestD) = (n, d);
        }
        if (best is null) return false;
        NoteChosen?.Invoke(best.Path);
        return true;
    }

    private double Half => Math.Max(1, Math.Min(Bounds.Width, Bounds.Height) / 2 - 34);

    private Point Map(double x, double y) => new(Bounds.Width / 2 + x * Half, Bounds.Height / 2 + y * Half);

    private static double Radius(LinkNeighbour n) => 4.5 + Math.Min(5.5, Math.Sqrt(n.Degree) * 1.6);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        // the sky: a little lighter in the middle
        context.DrawRectangle(new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Color.Parse(UrbeTheme.Surface2), 0), new GradientStop(Color.Parse(UrbeTheme.Bg), 1) }
        }, null, new RoundedRect(rect, 14));
        if (_view is null) return;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var ring = new Pen(new SolidColorBrush(Color.Parse(UrbeTheme.Line2), .45), 1, new DashStyle([2, 6], 0));
        if (_view.Placed.Any(n => n.Relation == LinkRelation.Mutual)) context.DrawEllipse(null, ring, centre, LinkGraph.InnerRing * Half, LinkGraph.InnerRing * Half);
        if (_view.Placed.Any(n => n.Relation != LinkRelation.Mutual)) context.DrawEllipse(null, ring, centre, LinkGraph.OuterRing * Half, LinkGraph.OuterRing * Half);
        var accent = Color.Parse(UrbeTheme.Accent);
        foreach (var n in _view.Placed)
            context.DrawLine(new Pen(new SolidColorBrush(accent, n.Relation == LinkRelation.Mutual ? .5 : .22), n.Relation == LinkRelation.Mutual ? 1.6 : 1), centre, Map(n.X, n.Y));
        foreach (var n in _view.Placed)
        {
            var at = Map(n.X, n.Y);
            Star(context, at, Radius(n), FolderColor(n.Folder));
            Label(context, n.Title, at, Radius(n), UrbeTheme.Text2, 11, FontWeight.Normal);
        }
        Star(context, centre, 9, accent);
        Label(context, _view.Title, centre, 9, UrbeTheme.Text, 12, FontWeight.SemiBold);
    }

    /// <summary>A point of light: a wide soft glow and a bright core.</summary>
    private static void Star(DrawingContext context, Point at, double r, Color color)
    {
        context.DrawEllipse(new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Color.FromArgb(110, color.R, color.G, color.B), 0), new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1) }
        }, null, at, r * 2.6, r * 2.6);
        context.DrawEllipse(new SolidColorBrush(color), null, at, r, r);
    }

    private static void Label(DrawingContext context, string title, Point at, double r, string color, double size, FontWeight weight)
    {
        var text = title.Length > 20 ? title[..19] + "…" : title;
        var f = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            UrbeTheme.UiTypeface(weight), size, UrbeTheme.Brush(color));
        context.DrawText(f, new Point(at.X - f.Width / 2, at.Y + r + 4));
    }
}
