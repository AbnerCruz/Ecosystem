using Avalonia;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// app.js urbeForma: the outline of a neighbourhood as closed loops of its border edges
/// (neighbourhood on the right), collinear points removed and rounded with three Chaikin
/// passes, in tile units. Also the label anchor (top of the centre column).
/// </summary>
public sealed class RegionShape
{
    private RegionShape(Geometry geometry, Point label, int cellCount)
    {
        Geometry = geometry;
        Label = label;
        CellCount = cellCount;
    }

    public Geometry Geometry { get; }
    public Point Label { get; }
    public int CellCount { get; }
    public double X0 { get; private init; }
    public double X1 { get; private init; }
    public double Y0 { get; private init; }
    public double Y1 { get; private init; }

    public static RegionShape Build(LegacyCityRegion r)
    {
        var set = new HashSet<(int X, int Y)>(r.Cells);
        var top = new Dictionary<int, int>();
        int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
        foreach (var (x, y) in r.Cells)
        {
            if (!top.TryGetValue(x, out var t) || t > y) top[x] = y;
            x0 = Math.Min(x0, x);
            x1 = Math.Max(x1, x);
            y0 = Math.Min(y0, y);
            y1 = Math.Max(y1, y);
        }
        // oriented border edges, keyed by start point, in cell order (Map insertion order)
        var outgoing = new Dictionary<(int, int), List<(int X, int Y)>>();
        var order = new List<(int, int)>();
        void Edge(int ax, int ay, int bx, int by)
        {
            if (!outgoing.TryGetValue((ax, ay), out var l)) { outgoing[(ax, ay)] = l = []; order.Add((ax, ay)); }
            l.Add((bx, by));
        }
        foreach (var (x, y) in r.Cells)
        {
            if (!set.Contains((x, y - 1))) Edge(x, y, x + 1, y);
            if (!set.Contains((x + 1, y))) Edge(x + 1, y, x + 1, y + 1);
            if (!set.Contains((x, y + 1))) Edge(x + 1, y + 1, x, y + 1);
            if (!set.Contains((x - 1, y))) Edge(x, y + 1, x, y);
        }
        var loops = new List<List<(double X, double Y)>>();
        foreach (var start in order)
        {
            var l0 = outgoing[start];
            while (l0.Count > 0)
            {
                var (sx, sy) = start;
                int px = sx, py = sy, dx = 0, dy = 0, guard = 0;
                var pts = new List<(double X, double Y)> { (sx, sy) };
                for (; ; )
                {
                    if (!outgoing.TryGetValue((px, py), out var lst) || lst.Count == 0) break;
                    int idx = 0;
                    // where two corners meet, turn right (keeps the loops apart)
                    if (lst.Count > 1)
                        for (int q = 0; q < lst.Count; q++)
                        {
                            int ex = lst[q].X - px, ey = lst[q].Y - py;
                            if (ex == -dy && ey == dx) { idx = q; break; }
                        }
                    var (nx, ny) = lst[idx];
                    lst.RemoveAt(idx);
                    dx = nx - px; dy = ny - py; px = nx; py = ny;
                    if (px == sx && py == sy) break;
                    pts.Add((px, py));
                    if (++guard > 1_000_000) break;
                }
                loops.Add(pts);
            }
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.SetFillRule(FillRule.NonZero);
            foreach (var loop in loops)
            {
                var p = Simplify(loop);
                for (int it = 0; it < 3; it++) p = Chaikin(p);
                g.BeginFigure(new Point(p[0].X, p[0].Y), true);
                for (int i = 1; i < p.Count; i++) g.LineTo(new Point(p[i].X, p[i].Y));
                g.EndFigure(true);
            }
        }
        double cx = 0, cy = 0;
        foreach (var (x, y) in r.Cells) { cx += x; cy += y; }
        cx = r.Cells.Count > 0 ? cx / r.Cells.Count + .5 : r.X + r.W / 2.0;
        cy = r.Cells.Count > 0 ? cy / r.Cells.Count + .5 : r.Y + r.H / 2.0;
        int col = (int)Math.Floor(cx);
        Point? best = null;
        for (int ddx = 0; ddx <= Math.Max(2, r.W / 4.0) && best is null; ddx++)
            foreach (var x in new[] { col - ddx, col + ddx })
                if (best is null && top.TryGetValue(x, out var ty)) best = new Point(x + .5, ty);
        return new RegionShape(geometry, best ?? new Point(cx, r.Y), r.Cells.Count) { X0 = x0, X1 = x1 + 1, Y0 = y0, Y1 = y1 + 1 };
    }

    private static List<(double X, double Y)> Simplify(List<(double X, double Y)> p)
    {
        var o = new List<(double X, double Y)>();
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[(i + p.Count - 1) % p.Count];
            var b = p[i];
            var c = p[(i + 1) % p.Count];
            if ((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X) != 0) o.Add(b);
        }
        return o.Count >= 3 ? o : p;
    }

    private static List<(double X, double Y)> Chaikin(List<(double X, double Y)> p)
    {
        var o = new List<(double X, double Y)>(p.Count * 2);
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            o.Add((a.X * .75 + b.X * .25, a.Y * .75 + b.Y * .25));
            o.Add((a.X * .25 + b.X * .75, a.Y * .25 + b.Y * .75));
        }
        return o;
    }
}
