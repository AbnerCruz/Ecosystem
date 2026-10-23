using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>A star of the Constelação: the note, where it is (in the sky's units), its project, how linked and how recent.</summary>
public sealed record SkyStar(string Id, string Path, string Title, string Project, double X, double Y, int Degree, bool Recent);

/// <summary>
/// UC-72 (DEC-0052, ADD-0029): the Constelação — the notes as a calm night sky, never the base of the app. Each project is a
/// soft mist in its colour, each note a point of light (brighter when touched lately); a finger drags the sky, a pinch or
/// the wheel zooms; from far away the projects are single lights. Tapping a star shows only it and its links, the rest
/// dimmed; tapping it again opens the note. Nothing moves by itself, and only what is on the screen is drawn.
/// </summary>
public sealed class ConstellationView : Control
{
    private IReadOnlyList<SkyStar> _stars = [];
    private IReadOnlyList<Constellation.Region> _regions = [];
    private Func<string, IReadOnlyCollection<string>> _links = _ => [];
    private Dictionary<string, SkyStar> _byId = new(StringComparer.Ordinal);
    private Vector _offset;        // the sky's point at the middle of the screen
    private double _scale = 1;
    private bool _fitted;
    private string? _focus;
    private HashSet<string> _focusLinks = [];
    private Point? _press;
    private Point _last;
    private bool _moved;

    /// <summary>From this scale down, the projects are drawn as single lights.</summary>
    public const double FarScale = .22;

    public ConstellationView()
    {
        ClipToBounds = true;
        Focusable = true;
        AutomationProperties.SetName(this, "Constelação");
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, (_, e) => { ZoomAt(e.ScaleOrigin, e.Scale / _pinch); _pinch = e.Scale; e.Handled = true; });
        AddHandler(InputElement.PinchEndedEvent, (_, _) => _pinch = 1);
    }

    private double _pinch = 1;

    /// <summary>A focused star was tapped again: its note's path.</summary>
    public event Action<string>? NoteChosen;

    /// <summary>The star in focus (tests), or null.</summary>
    public string? FocusedStar => _focus;

    /// <summary>The current zoom (tests).</summary>
    public double Scale => _scale;

    /// <summary>The sky to show; <paramref name="links"/> gives a note's linked notes (by id).</summary>
    public void Show(IReadOnlyList<SkyStar> stars, IReadOnlyList<Constellation.Region> regions, Func<string, IReadOnlyCollection<string>> links)
    {
        _stars = stars;
        _regions = regions;
        _links = links;
        _byId = stars.ToDictionary(s => s.Id, StringComparer.Ordinal);
        _focus = null;
        _focusLinks = [];
        _fitted = false;
        AutomationProperties.SetName(this, "Constelação: " + stars.Count + " notas em " + regions.Count + " regiões");
        InvalidateVisual();
    }

    /// <summary>Where a note's star is on the screen (tests and taps), or null.</summary>
    public Point? PointOf(string id) => _byId.TryGetValue(id, out var s) ? ToScreen(s.X, s.Y) : null;

    /// <summary>Focuses a note's star (as a tap does) or, when it is already in focus, opens it.</summary>
    public void Choose(string id)
    {
        if (!_byId.TryGetValue(id, out var s)) return;
        if (_focus == id) { NoteChosen?.Invoke(s.Path); return; }
        _focus = id;
        _focusLinks = [.. _links(id).Where(_byId.ContainsKey)];
        InvalidateVisual();
    }

    /// <summary>A tap on the sky: the nearest star within reach (or none: the focus goes away).</summary>
    public void TapAt(Point p)
    {
        SkyStar? best = null;
        double bestD = 26;
        if (_scale >= FarScale)
            foreach (var s in _stars)
            {
                var q = ToScreen(s.X, s.Y);
                double d = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
                if (d < bestD) (best, bestD) = (s, d);
            }
        if (best is not null) { Choose(best.Id); return; }
        // far away, a project's light comes closer
        if (_scale < FarScale && _regions.FirstOrDefault(r => Distance(ToScreen(r.X, r.Y), p) < 30) is { } region)
        {
            _offset = new Vector(region.X, region.Y);
            _scale = .7;
            InvalidateVisual();
            return;
        }
        if (_focus is null) return;
        _focus = null;
        _focusLinks = [];
        InvalidateVisual();
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ------------------------------------------------------------------ moving around

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _press = _last = e.GetPosition(this);
        _moved = false;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_press is not { } start) return;
        var at = e.GetPosition(this);
        if (!_moved && Distance(at, start) < 8) return;
        _moved = true;
        _offset -= (at - _last) / _scale;
        _last = at;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_press is null) return;
        _press = null;
        e.Pointer.Capture(null);
        if (!_moved) TapAt(e.GetPosition(this));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomAt(e.GetPosition(this), Math.Pow(1.15, e.Delta.Y));
        e.Handled = true;
    }

    /// <summary>Zooms by <paramref name="factor"/> keeping the sky under <paramref name="at"/> in place.</summary>
    public void ZoomAt(Point at, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        var before = ToSky(at);
        _scale = Math.Clamp(_scale * factor, .05, 4);
        var after = ToSky(at);
        _offset += before - after;
        InvalidateVisual();
    }

    private Point ToScreen(double x, double y) => new((x - _offset.X) * _scale + Bounds.Width / 2, (y - _offset.Y) * _scale + Bounds.Height / 2);

    private Vector ToSky(Point p) => new((p.X - Bounds.Width / 2) / _scale + _offset.X, (p.Y - Bounds.Height / 2) / _scale + _offset.Y);

    /// <summary>The whole sky on the screen, as the view opens.</summary>
    private void Fit()
    {
        _fitted = true;
        if (_stars.Count == 0 || Bounds.Width < 1) return;
        double minX = _stars.Min(s => s.X), maxX = _stars.Max(s => s.X), minY = _stars.Min(s => s.Y), maxY = _stars.Max(s => s.Y);
        _offset = new Vector((minX + maxX) / 2, (minY + maxY) / 2);
        double w = Math.Max(120, maxX - minX + 160), h = Math.Max(120, maxY - minY + 160);
        _scale = Math.Clamp(Math.Min(Bounds.Width / w, Bounds.Height / h), .08, 1.4);
    }

    // ------------------------------------------------------------------ drawing

    public override void Render(DrawingContext context)
    {
        if (!_fitted) Fit();
        var rect = new Rect(Bounds.Size);
        context.DrawRectangle(new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Color.Parse("#141822"), 0), new GradientStop(Color.Parse("#08090c"), 1) }
        }, null, rect);
        var view = rect.Inflate(40);
        bool far = _scale < FarScale;

        // each project, a soft mist in its colour (far away: one light)
        foreach (var r in _regions)
        {
            var c = ConnectionsGraph.FolderColor(r.Project);
            var at = ToScreen(r.X, r.Y);
            double radius = (Constellation.Step * Math.Sqrt(Math.Max(1, r.Seats)) + 50) * _scale;
            if (!view.Inflate(radius).Contains(at)) continue;
            context.DrawEllipse(new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.FromArgb(far ? (byte)90 : (byte)38, c.R, c.G, c.B), 0), new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1) }
            }, null, at, radius, radius);
            if (far) context.DrawEllipse(new SolidColorBrush(c), null, at, 3 + Math.Min(9, Math.Sqrt(r.Seats)), 3 + Math.Min(9, Math.Sqrt(r.Seats)));
            // the project's name under its mist, dim; with a star in focus only the sky speaks
            if (_focus is null)
                Label(context, r.Project.Length > 0 ? r.Project : "Raiz", new Point(at.X, at.Y + (far ? 12 : radius * .72)), Color.FromArgb(170, c.R, c.G, c.B), 12, FontWeight.SemiBold);
        }
        if (far) return;

        // the focus: its links as faint lines
        SkyStar? focus = _focus is not null && _byId.TryGetValue(_focus, out var f) ? f : null;
        if (focus is not null)
        {
            var from = ToScreen(focus.X, focus.Y);
            var pen = new Pen(new SolidColorBrush(Color.Parse(UrbeTheme.Accent), .45), 1.2);
            foreach (var id in _focusLinks)
                if (_byId.TryGetValue(id, out var n)) context.DrawLine(pen, from, ToScreen(n.X, n.Y));
        }
        bool labels = _scale >= 1.3;
        int labelled = 0;
        foreach (var s in _stars)
        {
            var at = ToScreen(s.X, s.Y);
            if (!view.Contains(at)) continue;
            bool lit = focus is null || s.Id == focus.Id || _focusLinks.Contains(s.Id);
            var c = ConnectionsGraph.FolderColor(s.Project);
            double r = (2.6 + Math.Min(3, Math.Sqrt(s.Degree) * .9)) * Math.Clamp(_scale, .9, 1.6);
            double alpha = !lit ? .12 : s.Recent ? 1 : .72;
            if (s.Recent && lit)
                context.DrawEllipse(new RadialGradientBrush
                {
                    GradientStops = { new GradientStop(Color.FromArgb(120, c.R, c.G, c.B), 0), new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1) }
                }, null, at, r * 3.2, r * 3.2);
            context.DrawEllipse(new SolidColorBrush(c, alpha), null, at, r, r);
            if (focus is not null && s.Id == focus.Id) context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse(UrbeTheme.Accent)), 1.5), at, r + 5, r + 5);
            bool named = focus is not null ? lit : labels && labelled < 160;
            if (named)
            {
                labelled++;
                Label(context, s.Title, new Point(at.X, at.Y + r + 3), focus is not null && s.Id == focus.Id ? Color.Parse(UrbeTheme.Text) : Color.Parse(UrbeTheme.Text2), 11,
                    focus is not null && s.Id == focus.Id ? FontWeight.SemiBold : FontWeight.Normal);
            }
        }
    }

    private static void Label(DrawingContext context, string title, Point at, Color color, double size, FontWeight weight)
    {
        var text = title.Length > 24 ? title[..23] + "…" : title;
        var f = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            UrbeTheme.UiTypeface(weight), size, new SolidColorBrush(color));
        context.DrawText(f, new Point(at.X - f.Width / 2, at.Y));
    }
}

/// <summary>The Constelação's screen: a header (Voltar, the title, a hint) over the sky.</summary>
public sealed class ConstellationPanel : Grid
{
    public ConstellationView Sky { get; } = new();

    /// <summary>Voltar.</summary>
    public event Action? Closed;

    public ConstellationPanel()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush("#08090c");
        RowDefinitions = new RowDefinitions("Auto,*");
        var back = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Back, 20, UrbeTheme.Text), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(back, "Voltar");
        back.Click += (_, _) => { IsVisible = false; Closed?.Invoke(); };
        var title = new StackPanel
        {
            Spacing = 1, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Constelação", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                new TextBlock { Text = "Toque numa estrela para ver as ligações; de novo, para abrir.", FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap }
            }
        };
        var head = new DockPanel { Margin = new Thickness(6, 8, 12, 6) };
        DockPanel.SetDock(back, Dock.Left);
        head.Children.Add(back);
        head.Children.Add(title);
        Children.Add(head);
        SetRow(Sky, 1);
        Children.Add(Sky);
    }
}
