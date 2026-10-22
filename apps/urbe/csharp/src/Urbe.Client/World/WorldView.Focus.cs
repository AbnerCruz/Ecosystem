using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// UC-70: the links focus. A tap on a note darkens the city and draws on top only that building, the ones it links
/// with (green: it cites them, yellow: they cite it, blue: mutual, each also with its own arrow badge) and the streets
/// between them. An overlay over the normal drawing: the city's art is not changed.
/// </summary>
public sealed partial class WorldView
{
    /// <summary>Colours of the groups (warm, soft tones that read on the dark layer).</summary>
    public static class FocusColors
    {
        public const string Focus = "#ffd9a0";
        public const string Outgoing = "#8be0a0";
        public const string Incoming = "#f4cf6a";
        public const string Mutual = "#86bfff";
        /// <summary>The dark layer: a warm night, not grey.</summary>
        public const string Dark = "#0f0b10";
        public const double DarkOpacity = .74;

        public static string Of(LinkGroup g) => g switch
        {
            LinkGroup.Outgoing => Outgoing,
            LinkGroup.Incoming => Incoming,
            LinkGroup.Mutual => Mutual,
            _ => Focus
        };
    }

    /// <summary>The arrow icons of the groups (24-unit paths), so the groups never depend on colour alone (P12).</summary>
    public static class FocusIcons
    {
        public const string Outgoing = "M7 17L17 7 M9.5 7H17v7.5";
        public const string Incoming = "M17 7L7 17 M14.5 17H7V9.5";
        public const string Mutual = "M5 9h13 M15 5.5L18.5 9 15 12.5 M19 15H6 M9 11.5L5.5 15 9 18.5";

        public static string Of(LinkGroup g) => g switch
        {
            LinkGroup.Outgoing => Outgoing,
            LinkGroup.Incoming => Incoming,
            _ => Mutual
        };
    }

    private static readonly IBrush DarkLayer = new ImmutableSolidColorBrush(Color.Parse(FocusColors.Dark));
    private static readonly Dictionary<LinkGroup, IBrush> Glows = [];
    private static readonly Dictionary<LinkGroup, Geometry> BadgeIcons = [];

    // the focus being drawn: kept while it fades out after Linked went back to null
    private LinkFocus? _drawnFocus;
    private double _focusFrom, _focusTo, _focusSince = double.NegativeInfinity;

    /// <summary>The links focus, or null outside it.</summary>
    public LinkFocus? Linked { get; private set; }

    /// <summary>The linked building tapped in the focus (its name and "Abrir" are shown), or null.</summary>
    public LegacyCityBuilding? Peeked { get; private set; }

    /// <summary>Whether a tap on a note enters the links focus (on by default).</summary>
    public bool LinkFocusEnabled { get; set; } = true;

    /// <summary>The focus started, changed building, peeked another or ended.</summary>
    public event EventHandler? LinkFocusChanged;

    /// <summary>The focused building was tapped again: its details (the 1.8.4 house card) are asked for.</summary>
    public event Action<LegacyCityBuilding>? FocusDetailsRequested;

    /// <summary>How dark the city is now, 0 to 1 (with the 220 ms transition).</summary>
    public double FocusAmount => Amount(_clock.Elapsed.TotalMilliseconds);

    private double Amount(double now)
    {
        if (UrbeTokens.Motion.Reduced) return _focusTo;
        double t = Math.Clamp((now - _focusSince) / UrbeTokens.Motion.Normal.TotalMilliseconds, 0, 1);
        return _focusFrom + (_focusTo - _focusFrom) * UrbeTokens.Motion.EaseOut.Ease(t);
    }

    private void Fade(double to)
    {
        double now = _clock.Elapsed.TotalMilliseconds;
        _focusFrom = Amount(now);
        _focusTo = to;
        _focusSince = now;
        InvalidateVisual();
    }

    /// <summary>Enters the links focus on <paramref name="house"/> (or moves it there) and selects it.</summary>
    public void FocusLinks(LegacyCityBuilding house)
    {
        if (City is null) return;
        bool entering = Linked is null;
        Linked = _drawnFocus = LinkFocus.Of(City, house);
        Peeked = null;
        if (entering) Fade(1);
        Selected = house;
        InvalidateVisual();
        SelectionChanged?.Invoke(this, house);
        LinkFocusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a linked building's name in the focus (null: back to the focused one).</summary>
    public void Peek(LegacyCityBuilding? house)
    {
        if (Linked is null || (house is not null && Linked.GroupOf(house) is null or LinkGroup.Focus)) house = null;
        if (Peeked == house) return;
        Peeked = house;
        InvalidateVisual();
        LinkFocusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Leaves the links focus (the city fades back in 220 ms). The selection stays as it is.</summary>
    public void ClearLinkFocus()
    {
        if (Linked is null) return;
        Linked = null;
        Peeked = null;
        Fade(0);
        LinkFocusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetLinkFocus()
    {
        Linked = _drawnFocus = null;
        Peeked = null;
        _focusFrom = _focusTo = 0;
    }

    // the streets changed: the same focus, with the links as they are now
    private void RefreshLinkFocus()
    {
        if (Linked is not { } f || City is null) return;
        if (!City.Buildings.Contains(f.Focus)) { ClearLinkFocus(); return; }
        Linked = _drawnFocus = LinkFocus.Of(City, f.Focus);
        if (Peeked is not null && Linked.GroupOf(Peeked) is null) Peeked = null;
        LinkFocusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A tap on a building, as the focus sees it. Returns false when the normal tap goes on (a file building, the focus
    /// switched off). A note enters the focus; the focused note again asks for its details; a linked one is peeked
    /// first and becomes the focus on a second tap; any other note becomes the focus.
    /// While a linked one is peeked, the focused note only goes back to itself.
    /// </summary>
    private bool TapLinked(LegacyCityBuilding tapped)
    {
        if (!LinkFocusEnabled || !tapped.IsNote) return false;
        if (Linked is { } f)
        {
            if (tapped == f.Focus)
            {
                if (Peeked is not null) Peek(null);
                else FocusDetailsRequested?.Invoke(tapped);
                return true;
            }
            if (f.GroupOf(tapped) is not null && Peeked != tapped) { Peek(tapped); return true; }
        }
        FocusLinks(tapped);
        return true;
    }

    // ---------------------------------------------------------------- drawing

    private static IBrush Glow(LinkGroup g)
    {
        if (Glows.TryGetValue(g, out var b)) return b;
        var c = Color.Parse(FocusColors.Of(g));
        var brush = new RadialGradientBrush();
        byte core = (byte)(g == LinkGroup.Focus ? 200 : 150), mid = (byte)(g == LinkGroup.Focus ? 95 : 70);
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(core, c.R, c.G, c.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(mid, c.R, c.G, c.B), .55));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        return Glows[g] = brush.ToImmutable();
    }

    private static Geometry BadgeIcon(LinkGroup g)
    {
        if (!BadgeIcons.TryGetValue(g, out var geo)) BadgeIcons[g] = geo = Geometry.Parse(FocusIcons.Of(g));
        return geo;
    }

    /// <summary>The dark layer, then the focused streets, the linked buildings with their glow, badges and names.</summary>
    private void DrawLinkFocus(DrawingContext context)
    {
        if (_drawnFocus is not { } focus) return;
        double now = _clock.Elapsed.TotalMilliseconds, amount = Amount(now);
        if (amount <= 0.001)
        {
            if (Linked is null) _drawnFocus = null;
            return;
        }
        // keep the frames coming while the transition runs (the life timer may be paused)
        if (!UrbeTokens.Motion.Reduced && now - _focusSince < UrbeTokens.Motion.Normal.TotalMilliseconds + 32)
            Avalonia.Threading.DispatcherTimer.RunOnce(InvalidateVisual, TimeSpan.FromMilliseconds(16));

        using (context.PushOpacity(amount * FocusColors.DarkOpacity))
            context.FillRectangle(DarkLayer, new Rect(Bounds.Size));
        using var _ = context.PushOpacity(amount);
        DrawFocusRoads(context, focus);
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        double z = Camera.Zoom, s = T * z / LegacyWorld.TilePixels;
        // glows first (under every building), then the art, then badges and names
        var shown = new List<(LegacyCityBuilding B, LinkGroup G)>();
        foreach (var b in focus.Buildings)
        {
            if (b.X > x1 + 1 || b.X + b.W < x0 - 1 || b.Y > y1 + 2 || b.Y + b.H < y0 - 1 || !City!.Buildings.Contains(b)) continue;
            shown.Add((b, focus.GroupOf(b)!.Value));
        }
        shown.Sort((a, b) => a.B.Y.CompareTo(b.B.Y));
        foreach (var (b, g) in shown)
        {
            var c = Camera.WorldToScreen((b.X + b.W / 2.0) * T, (b.Y + b.H * .62) * T);
            double rx = b.W * T * z * (g == LinkGroup.Focus ? 1.25 : .9), ry = b.H * T * z * (g == LinkGroup.Focus ? 1.1 : .8);
            context.DrawEllipse(Glow(g), null, c, rx, ry);
        }
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            foreach (var (b, _) in shown)
            {
                if (b == Moving) continue;
                var p = Camera.WorldToScreen(b.X * T, b.Y * T);
                double w = 48 * s * (b.W / 3.0), h = 56 * s * (b.H / 3.0);
                context.DrawImage(Art(b, LookOf(b)), new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                    new Rect(Math.Round(p.X), Math.Round(p.Y + b.H * T * z - h), Math.Ceiling(w), Math.Ceiling(h)));
            }
        foreach (var (b, g) in shown)
        {
            if (g == LinkGroup.Focus) continue;
            var p = Camera.WorldToScreen((b.X + b.W) * T, b.Y * T);
            double h = 56 * s * (b.H / 3.0), top = p.Y + b.H * T * z - h;
            DrawBadge(context, g, new Point(p.X - 4 * z, top + 6 * z), b == Peeked);
        }
        // names: the focus and the peeked one first, then the others where they fit
        var used = new List<(Rect Box, LegacyCityRegion? Region, int Level, string Name)>();
        foreach (var (b, g) in shown.OrderBy(o => o.G == LinkGroup.Focus || o.B == Peeked ? 0 : 1))
        {
            var q = Camera.WorldToScreen((b.X + b.W / 2.0) * T, (b.Y + b.H) * T);
            var name = HouseLabel(b);
            var box = HouseLabelBox(name, q.X, q.Y + 4 * z);
            bool first = g == LinkGroup.Focus || b == Peeked;
            if (!first && (z < .42 || Overlaps(used, box))) continue;
            used.Add((box, null, 0, ""));
            DrawFocusLabel(context, name, q.X, q.Y + 4 * z, g, first);
        }
    }

    private void DrawBadge(DrawingContext context, LinkGroup g, Point centre, bool strong)
    {
        double r = Math.Clamp(9 * Camera.Zoom, 8, 13) * (strong ? 1.2 : 1);
        var fill = UrbeTheme.Brush(FocusColors.Of(g));
        context.DrawEllipse(UrbeTheme.Brush(FocusColors.Dark, .55), null, centre + new Point(0, 1.5), r + 2, r + 2);
        context.DrawEllipse(fill, new Pen(UrbeTheme.Brush("#1a1209", .55), 1.2), centre, r, r);
        double k = r * 1.5 / 24;
        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(k, k) * Matrix.CreateTranslation(centre.X, centre.Y)))
            context.DrawGeometry(null, new Pen(UrbeTheme.Brush("#1a1209"), 2.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), BadgeIcon(g));
    }

    private void DrawFocusLabel(DrawingContext context, string text, double cx, double top, LinkGroup g, bool strong)
    {
        var box = HouseLabelBox(text, cx, top);
        bool focus = g == LinkGroup.Focus;
        var ink = focus || strong ? "#1a1209" : "#f4ede4";
        var ft = Text(text, LabelSize, FontWeight.SemiBold, UrbeTheme.Brush(ink));
        IBrush back = focus ? UrbeTheme.Brush(FocusColors.Focus) : strong ? UrbeTheme.Brush(FocusColors.Of(g)) : LabelBackground;
        IPen? edge = focus || strong ? null : new Pen(UrbeTheme.Brush(FocusColors.Of(g), .85), 1.5);
        context.DrawRectangle(back, edge, box, box.Height / 2, box.Height / 2);
        context.DrawText(ft, new Point(cx - ft.Width / 2, top + box.Height / 2 + .5 - ft.Height / 2));
    }

    /// <summary>The streets of the focus, drawn as the city draws them, over a soft line of the way's colour.</summary>
    private void DrawFocusRoads(DrawingContext context, LinkFocus focus)
    {
        if (focus.Roads.Count == 0) return;
        var roads = focus.Roads;
        var (x0, y0, x1, y1) = Camera.VisibleTiles();
        double z = Camera.Zoom, s = T * z, lg = s * .56, mg = (s - lg) / 2, edge = Math.Max(1, s * .07);
        bool On(int x, int y) => roads.ContainsKey((x, y));
        var dark = UrbeTheme.Brush("#6f6250");
        var light = UrbeTheme.Brush("#b8a684");
        // the glow under the way: a faint wide band and a brighter narrow one per tile, joined to the neighbours
        foreach (var (width, alpha) in new[] { (.78, .13), (.4, .26) })
        foreach (var ((x, y), g) in roads)
        {
            if (x < x0 - 1 || x > x1 + 1 || y < y0 - 1 || y > y1 + 1) continue;
            var p = Camera.WorldToScreen(x * T, y * T);
            double gw = Math.Min(s, lg + s * width), gm = (s - gw) / 2;
            var glow = UrbeTheme.Brush(FocusColors.Of(g), alpha);
            context.FillRectangle(glow, new Rect(p.X + gm, p.Y + gm, gw, gw));
            if (On(x + 1, y)) context.FillRectangle(glow, new Rect(p.X + gm + gw, p.Y + gm, s - gw, gw));
            if (On(x, y + 1)) context.FillRectangle(glow, new Rect(p.X + gm, p.Y + gm + gw, gw, s - gw));
        }
        for (int pass = 0; pass < 2; pass++)
        {
            var fill = pass == 0 ? dark : light;
            double extra = pass == 0 ? edge : 0;
            foreach (var (x, y) in roads.Keys)
            {
                if (x < x0 - 1 || x > x1 + 1 || y < y0 - 1 || y > y1 + 1) continue;
                var p = Camera.WorldToScreen(x * T, y * T);
                double cx = p.X + mg - extra, cy = p.Y + mg - extra, l = lg + extra * 2;
                context.FillRectangle(fill, new Rect(cx, cy, l, l));
                if (On(x + 1, y)) context.FillRectangle(fill, new Rect(cx + l - extra * 2, cy, s - mg - l + extra * 3 + mg, l));
                if (On(x - 1, y)) context.FillRectangle(fill, new Rect(p.X - .5, cy, mg + extra + .5, l));
                if (On(x, y + 1)) context.FillRectangle(fill, new Rect(cx, cy + l - extra * 2, l, s - mg - l + extra * 3 + mg));
                if (On(x, y - 1)) context.FillRectangle(fill, new Rect(cx, p.Y - .5, l, mg + extra + .5));
            }
        }
    }
}
