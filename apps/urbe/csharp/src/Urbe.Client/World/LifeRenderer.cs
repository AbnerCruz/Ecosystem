using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// Draws the life of the world (Urbe.Core LegacyWorldLife + LegacyFauna) with the drawing
/// code of the 1.8.4-beta ported step by step: life.js chao() (before the trees), app.js
/// urbeDesenharFauna (after the trees), life.js ceu() (over the houses, before the light),
/// app.js urbeJanelasAcesas and life.js brilhoPasso() (after the light). The canvas
/// 'lighter' operation is BitmapBlendingMode.Plus, which Skia applies only to bitmaps, so
/// the additive pass draws everything from small bitmaps.
/// </summary>
public sealed class LifeRenderer
{
    private const int TILE = LegacyLifeView.Tile;
    private readonly Dictionary<string, Bitmap> _bitmaps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBrush> _brushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FormattedText> _texts = new(StringComparer.Ordinal);
    private static readonly RenderOptions Pixelated = new() { BitmapInterpolationMode = BitmapInterpolationMode.None };
    private static readonly RenderOptions Additive = new() { BitmapBlendingMode = BitmapBlendingMode.Plus, BitmapInterpolationMode = BitmapInterpolationMode.None };
    private static readonly RenderOptions AdditiveSmooth = new() { BitmapBlendingMode = BitmapBlendingMode.Plus, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };

    // ---------------------------------------------------------------- caches
    private Bitmap Cached(string key, Func<Bitmap> make)
    {
        if (!_bitmaps.TryGetValue(key, out var b)) _bitmaps[key] = b = make();
        return b;
    }

    private Bitmap Sprite(string key, LegacyLifeSprites.Grid grid, int frame, bool flip) =>
        Cached($"{key}{frame}{(flip ? "f" : "")}", () =>
        {
            var (px, w, h) = LegacyLifeSprites.Render(grid, frame, flip);
            return Pixels.ToBitmap(px, w, h);
        });

    private Bitmap Animal(string kind, int frame, bool flip) => Sprite("a:" + kind, LegacyLifeSprites.Animals[kind], frame, flip);

    private Bitmap GlowImage(string rgb) => Cached("glow:" + rgb, () =>
    {
        var p = rgb.Split(',').Select(byte.Parse).ToArray();
        return Pixels.ToBitmap(LegacyLifeSprites.Glow(p[0], p[1], p[2]), 64, 64);
    });

    private Bitmap CloudImage(int v, bool white) => Cached($"cloud:{v}:{white}", () => Pixels.ToBitmap(LegacyLifeSprites.Cloud(v, white), 200, 120));

    private Bitmap Solid(string hex) => Cached("solid:" + hex, () =>
    {
        var (r, g, b) = LegacyLifeSprites.Hex(hex);
        return Pixels.ToBitmap([r, g, b, 255], 1, 1);
    });

    /// <summary>A 64×1 linear gradient: (from, alpha 1) → (to, alpha 0), unpremultiplied (canvas).</summary>
    private Bitmap Gradient(string from, string to) => Cached($"grad:{from}:{to}", () =>
    {
        var (r0, g0, b0) = LegacyLifeSprites.Hex(from);
        var (r1, g1, b1) = LegacyLifeSprites.Hex(to);
        var px = new byte[64 * 4];
        for (int i = 0; i < 64; i++)
        {
            double t = (i + .5) / 64;
            px[i * 4] = (byte)Math.Round(r0 + (r1 - r0) * t);
            px[i * 4 + 1] = (byte)Math.Round(g0 + (g1 - g0) * t);
            px[i * 4 + 2] = (byte)Math.Round(b0 + (b1 - b0) * t);
            px[i * 4 + 3] = (byte)Math.Round(255 * (1 - t));
        }
        return Pixels.ToBitmap(px, 64, 1);
    });

    /// <summary>The radial of urbeJanelasAcesas: rgba(255,186,92,1) → 0, linear.</summary>
    private Bitmap WindowGlow() => Cached("window", () =>
    {
        const int n = 64;
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            double d = Math.Sqrt((x + .5 - 32) * (x + .5 - 32) + (y + .5 - 32) * (y + .5 - 32)) / 32;
            int o = (y * n + x) * 4;
            px[o] = 255; px[o + 1] = 186; px[o + 2] = 92; px[o + 3] = (byte)Math.Round(Math.Max(0, 1 - d) * 255);
        }
        return Pixels.ToBitmap(px, n, n);
    });

    private Bitmap Balloon(string[] cs) => Cached("balao" + string.Join(",", cs), () => Pixels.ToBitmap(LegacyLifeSprites.HotAirBalloon(cs[0], cs[1]), 16, 24));

    private IBrush Brush(string hex, double alpha = 1)
    {
        var key = hex + "|" + alpha.ToString("R", CultureInfo.InvariantCulture);
        if (!_brushes.TryGetValue(key, out var b))
        {
            var (r, g, bl) = LegacyLifeSprites.Hex(hex);
            _brushes[key] = b = new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), r, g, bl));
        }
        return b;
    }

    private static IBrush Rgba(int r, int g, int b, double a) =>
        new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), (byte)r, (byte)g, (byte)b));

    private static Point Pt((double X, double Y) p) => new(p.X, p.Y);

    private static void Img(DrawingContext c, Bitmap img, double x, double y, double w, double h) =>
        c.DrawImage(img, new Rect(0, 0, img.PixelSize.Width, img.PixelSize.Height), new Rect(x, y, w, h));

    /// <summary>life.js desenha(img,x,y,s): feet anchored, s ≥ 1.1, rounded position.</summary>
    private static void Feet(DrawingContext c, Bitmap img, LegacyLifeView v, double x, double y, double s, double anchor = 1)
    {
        s = Math.Max(s, 1.1);
        var p = v.P(x, y);
        double w = img.PixelSize.Width * s, h = img.PixelSize.Height * s;
        Img(c, img, Math.Round(p.X - w / 2), Math.Round(p.Y - h * anchor), Math.Ceiling(w), Math.Ceiling(h));
    }

    /// <summary>brilho(cor,px,py,r,a) under 'lighter'.</summary>
    private void Glow(DrawingContext c, string rgb, double px, double py, double r, double a)
    {
        if (a <= .01) return;
        using (c.PushOpacity(Math.Min(1, a))) Img(c, GlowImage(rgb), px - r, py - r, r * 2, r * 2);
    }

    /// <summary>fillRect under 'lighter'.</summary>
    private void AddRect(DrawingContext c, string hex, double x, double y, double w, double h, double a)
    {
        using (c.PushOpacity(Math.Min(1, a))) Img(c, Solid(hex), x, y, w, h);
    }

    /// <summary>A stroked segment under 'lighter' (lineCap round adds the end discs).</summary>
    private void AddLine(DrawingContext c, Bitmap img, Point a, Point b, double width, double alpha, bool round)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
        using (c.PushOpacity(Math.Min(1, alpha)))
        {
            if (len > 0)
                using (c.PushTransform(Matrix.CreateRotation(Math.Atan2(dy, dx)) * Matrix.CreateTranslation(a.X, a.Y)))
                    Img(c, img, 0, -width / 2, len, width);
            if (round)
            {
                var disc = Disc();
                Img(c, disc, a.X - width / 2, a.Y - width / 2, width, width);
                Img(c, disc, b.X - width / 2, b.Y - width / 2, width, width);
            }
        }
    }

    private Bitmap Disc() => Cached("disc", () =>
    {
        const int n = 16;
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            double d = Math.Sqrt((x + .5 - 8) * (x + .5 - 8) + (y + .5 - 8) * (y + .5 - 8));
            int o = (y * n + x) * 4;
            px[o] = px[o + 1] = px[o + 2] = 255;
            px[o + 3] = (byte)Math.Round(Math.Clamp(8.5 - d, 0, 1) * 255);
        }
        return Pixels.ToBitmap(px, n, n);
    });

    // ---------------------------------------------------------------- chão (before the trees)
    public void Ground(DrawingContext c, LegacyWorldLife life, LegacyLifeView v)
    {
        double s = v.S, z = v.Zoom;
        var L = life.CurrentLight;
        using var _ = c.PushRenderOptions(Pixelated);
        // ripples
        if (life.Ripples.Count > 0)
        {
            double lw = Math.Max(1, s * .55);
            foreach (var o in life.Ripples)
            {
                if (o.T < 0 || !v.Inside(o.X, o.Y, 1)) continue;
                double k = o.T / o.D, rx = o.R * v.T * (.3 + k * .9);
                var p = v.P(o.X, o.Y);
                c.DrawEllipse(null, new Pen(Rgba(225, 238, 255, Math.Round((1 - k) * .55, 3)), lw), Pt(p), rx, rx * .42);
            }
        }
        // sun sparkles on the water
        if (L.Darkness < .5 && life.Sparkles.Count > 0)
            foreach (var b in life.Sparkles)
            {
                if (!v.Inside(b.X, b.Y)) continue;
                double a = Math.Sin(Math.PI * b.T / b.D) * (1 - L.Darkness * 1.6);
                var pb = v.P(b.X, b.Y);
                double u = Math.Max(1, Math.Round(s * .9));
                if (a <= .05) continue;
                var white = Rgba(255, 255, 255, a);
                double bx = Math.Round(pb.X), by = Math.Round(pb.Y);
                c.FillRectangle(white, new Rect(bx, by, u, u));
                if (a > .7)
                {
                    c.FillRectangle(white, new Rect(Math.Round(pb.X - u), by, u, u));
                    c.FillRectangle(white, new Rect(Math.Round(pb.X + u), by, u, u));
                    c.FillRectangle(white, new Rect(bx, Math.Round(pb.Y - u), u, u));
                    c.FillRectangle(white, new Rect(bx, Math.Round(pb.Y + u), u, u));
                }
            }
        // jumping fish
        foreach (var pe in life.Fishes)
        {
            double k2 = pe.T / pe.D, px = pe.X + pe.Dir * .9 * k2, alt = Math.Sin(Math.PI * k2) * pe.Alt;
            var pp = v.P(px, pe.Y);
            var im = Sprite("peixe", LegacyLifeSprites.FishGrid, 0, pe.Dir < 0);
            double w = im.PixelSize.Width * s, h = im.PixelSize.Height * s;
            var m = Matrix.CreateRotation((pe.Dir > 0 ? 1 : -1) * (k2 - .5) * 1.6) * Matrix.CreateTranslation(pp.X, pp.Y - alt * v.T);
            using (c.PushTransform(m)) Img(c, im, -w / 2, -h / 2, w, h);
        }
        // boat
        if (life.SailBoat is { } boat && z >= .35)
        {
            var ib = Sprite("barco", LegacyLifeSprites.Boat, 0, boat.DX < 0);
            var pb2 = v.P(boat.X, boat.Y);
            double bob = Math.Sin(life.Time * 2) * s * .5, iw = ib.PixelSize.Width, ih = ib.PixelSize.Height;
            c.DrawEllipse(Rgba(20, 40, 60, .22), null, new Point(pb2.X, pb2.Y + s), iw * s * .5, s * 1.4);
            Img(c, ib, Math.Round(pb2.X - iw * s * .5), Math.Round(pb2.Y - ih * s + s * 2 + bob), Math.Ceiling(iw * s), Math.Ceiling(ih * s));
        }
        // street lamps (post)
        if (z >= .45)
        {
            bool lit = L.Darkness > .32;
            foreach (var l in life.Lamps())
            {
                if (!v.Inside(l.X, l.Y, 1)) continue;
                var pl = v.P(l.X, l.Y);
                c.FillRectangle(Rgba(0, 0, 0, .18), new Rect(Math.Round(pl.X - s), Math.Round(pl.Y), Math.Ceiling(s * 2.5), Math.Ceiling(s * .8)));
                var post = Brush("#2e2a2a");
                c.FillRectangle(post, new Rect(Math.Round(pl.X), Math.Round(pl.Y - s * 9), Math.Ceiling(s), Math.Ceiling(s * 9)));
                c.FillRectangle(post, new Rect(Math.Round(pl.X - s), Math.Round(pl.Y - s * 11), Math.Ceiling(s * 3), Math.Ceiling(s * 2.2)));
                c.FillRectangle(Brush(lit ? "#ffe29a" : "#d9d2b8"), new Rect(Math.Round(pl.X - s * .5), Math.Round(pl.Y - s * 10.5), Math.Ceiling(s * 2), Math.Ceiling(s * 1.4)));
            }
        }
        // pigeons on the ground
        foreach (var po in life.Pigeons)
        {
            if (po.Flying || !v.Inside(po.X, po.Y, 1)) continue;
            Feet(c, Sprite("pombo", LegacyLifeSprites.Pigeon, po.Frame != 0 ? 1 : 0, po.Flip), v, po.X, po.Y, s);
        }
        // dog and fox
        if (life.TheDog is { } dog)
        {
            var ic = Sprite("cao", LegacyLifeSprites.Dog, dog.Sitting ? 2 : dog.Frame, dog.Flip);
            var pc = v.P(dog.X, dog.Y);
            c.DrawEllipse(Rgba(0, 0, 0, .2), null, Pt(pc), ic.PixelSize.Width * s * .35, s * 1.1);
            Feet(c, ic, v, dog.X, dog.Y, s);
        }
        if (life.TheFox is { } fox && v.Inside(fox.X, fox.Y, 2))
        {
            var ir = Sprite("raposa", LegacyLifeSprites.Fox, fox.Frame, fox.Dir < 0);
            var pr = v.P(fox.X, fox.Y);
            c.DrawEllipse(Rgba(0, 0, 0, .2), null, Pt(pr), ir.PixelSize.Width * s * .35, s * 1.1);
            Feet(c, ir, v, fox.X, fox.Y, s);
        }
    }

    // ---------------------------------------------------------------- fauna (app.js urbeDesenharFauna)
    public void Fauna(DrawingContext c, LegacyFauna fauna, LegacyLifeView v)
    {
        if (fauna.Animals.Count == 0 || v.Zoom < .3) return;
        double sp = TILE * v.Zoom / 16;
        using var _ = c.PushRenderOptions(Pixelated);
        foreach (var a in fauna.Animals.OrderBy(a => a.Y))
        {
            if (a.X < v.X0 - 2 || a.X > v.X1 + 2 || a.Y < v.Y0 - 2 || a.Y > v.Y1 + 3) continue;
            if (a.Kind == "bird")
            {
                var qs = v.P(a.X, a.Y);
                var img = Animal("bird", a.Frame, a.Flip);
                c.DrawEllipse(Rgba(0, 0, 0, .16), null, Pt(qs), 3 * sp, 1.2 * sp);
                Img(c, img, Math.Round(qs.X - img.PixelSize.Width * sp / 2), Math.Round(qs.Y - a.Alt * TILE * v.Zoom),
                    Math.Ceiling(img.PixelSize.Width * sp), Math.Ceiling(img.PixelSize.Height * sp));
                continue;
            }
            if (v.Zoom < .45) continue;
            var q = v.P(a.X, a.Y);
            var im = Animal(a.Kind, a.Frame, a.Flip);
            double w = im.PixelSize.Width * sp, h = im.PixelSize.Height * sp;
            if (a.Kind != "duck") c.DrawEllipse(Rgba(0, 0, 0, .2), null, Pt(q), w * .38, sp * 1.4);
            Img(c, im, Math.Round(q.X - w / 2), Math.Round(q.Y - h + (a.Kind == "duck" ? sp * 2 : 0)), Math.Ceiling(w), Math.Ceiling(h));
        }
    }

    // ---------------------------------------------------------------- céu (over the houses, before the light)
    public void Sky(DrawingContext c, LegacyWorldLife life, IReadOnlyList<ILegacyLifePerson> people, LegacyLifeView v)
    {
        double s = v.S, z = v.Zoom, W = v.Width, Hh = v.Height, T = life.Time;
        var L = life.CurrentLight;
        using var _ = c.PushRenderOptions(Pixelated);
        // smoke
        foreach (var fu in life.Smokes)
        {
            if (!v.Inside(fu.X, fu.Y, 2)) continue;
            double k = fu.T / fu.D, r = (fu.R0 + fu.T * .1) * v.T;
            var pf = v.P(fu.X, fu.Y);
            c.DrawEllipse(Brush(L.Darkness > .5 ? "#9aa0b4" : "#ececec", (k < .15 ? k / .15 : 1 - k) * .34), null, Pt(pf), r, r);
        }
        // leaves and gusts
        foreach (var fo in life.Leaves)
        {
            if (!v.Inside(fo.X, fo.Y, 1)) continue;
            var pfo = v.P(fo.X, fo.Y);
            double u = Math.Max(1, Math.Round(s));
            double a = fo.Alt < 0 ? Math.Max(0, 1 + fo.Alt / .8) : 1;
            bool turn = Math.Floor(T * 4 + fo.Ph) % 2 != 0;
            c.FillRectangle(Brush(fo.Color, a), new Rect(Math.Round(pfo.X), Math.Round(pfo.Y - Math.Max(0, fo.Alt) * v.T), turn ? u * 2 : u, turn ? u : u * 2));
        }
        if (life.Gusts.Count > 0)
        {
            double lw = Math.Max(1, s * .5), ang = Math.Atan2(life.Breeze.Y, life.Breeze.X);
            foreach (var ra in life.Gusts)
            {
                double kr = ra.T / ra.D, comp = ra.L * v.T * Math.Sin(Math.PI * kr);
                var p1 = v.P(ra.X, ra.Y);
                c.DrawLine(new Pen(Rgba(255, 255, 255, .35 * Math.Sin(Math.PI * kr) * .8), lw), Pt(p1),
                    new Point(p1.X - Math.Cos(ang) * comp, p1.Y - Math.Sin(ang) * comp));
            }
        }
        // butterflies
        foreach (var bo in life.Butterflies)
        {
            if (!v.Inside(bo.X, bo.Y, 1)) continue;
            var pbo = v.P(bo.X, bo.Y);
            double yy = pbo.Y - bo.Alt * v.T, u2 = Math.Max(1.5, Math.Round(s));
            bool open = Math.Floor(bo.Ph * 13) % 2 != 0;
            var wing = Brush(bo.Color);
            if (open)
            {
                c.FillRectangle(wing, new Rect(Math.Round(pbo.X - u2 * 2), Math.Round(yy - u2), u2 * 2, u2 * 2));
                c.FillRectangle(wing, new Rect(Math.Round(pbo.X + u2), Math.Round(yy - u2), u2 * 2, u2 * 2));
            }
            else
            {
                c.FillRectangle(wing, new Rect(Math.Round(pbo.X - u2), Math.Round(yy - u2), u2, u2 * 2));
                c.FillRectangle(wing, new Rect(Math.Round(pbo.X + u2), Math.Round(yy - u2), u2, u2 * 2));
            }
            c.FillRectangle(Brush("#3a2e28"), new Rect(Math.Round(pbo.X), Math.Round(yy - u2), u2, u2 * 2));
        }
        // flying pigeons
        foreach (var po in life.Pigeons)
        {
            if (!po.Flying) continue;
            var pp = v.P(po.X, po.Y);
            var im = Sprite("pombo", LegacyLifeSprites.Pigeon, po.Frame != 0 ? 2 : 3, po.Flip);
            double iw = im.PixelSize.Width, ih = im.PixelSize.Height;
            c.FillRectangle(Rgba(0, 0, 0, .14), new Rect(Math.Round(pp.X - s * 2), Math.Round(pp.Y), Math.Ceiling(s * 4), Math.Ceiling(s)));
            Img(c, im, Math.Round(pp.X - iw * s / 2), Math.Round(pp.Y - po.Alt * v.T - ih * s), Math.Ceiling(iw * s), Math.Ceiling(ih * s));
        }
        // villagers: umbrellas and speech bubbles
        double sv = TILE * z / 20;
        if (z >= .38 && (life.Rain.K > .25 || life.Emotes.Count > 0))
            for (int i = 0; i < people.Count; i++)
            {
                var a = people[i];
                if (!a.Placed || !v.Inside(a.X, a.Y, 1) || a.Indoors) continue;
                var pa = v.P(a.X, a.Y);
                if (life.Rain.K > .25 && !life.Rain.Snow)
                {
                    string[] umbrella = ["#d9483b", "#3d7dd8", "#f4d35e", "#2a9d8f", "#6c4ab6"];
                    var cu = umbrella[LegacyWorldLife.Hash(a.ShirtLength ?? i, i) % 5];
                    double cy = pa.Y - sv * 20;
                    c.DrawLine(new Pen(Brush("#3a2e28"), Math.Max(1, sv * .8)), new Point(pa.X + sv * 3, cy), new Point(pa.X + sv * 3, cy + sv * 7));
                    c.DrawGeometry(Brush(cu), null, HalfEllipse(pa.X + sv * 3, cy, sv * 8, sv * 4.2));
                    c.FillRectangle(Rgba(0, 0, 0, .18), new Rect(Math.Round(pa.X + sv * 3 - sv * 8), Math.Round(cy - sv * .6), Math.Ceiling(sv * 16), Math.Ceiling(sv * .8)));
                }
                if (life.Emotes.TryGetValue(a, out var e) && z >= .45 && !(a.Talk > 0))
                {
                    double bx = pa.X + sv * 3, by = pa.Y - sv * 23, bw = sv * 9, bh = sv * 8, dur = e.End - T;
                    double al = Math.Clamp(Math.Min(Math.Min(1, dur * 2), (T - (e.End - 3.5)) * 4 + .2), 0, 1);
                    using (c.PushOpacity(al))
                    {
                        var bubble = new Rect(bx, by - bh, bw, bh);
                        c.DrawRectangle(Rgba(255, 252, 240, .96), new Pen(Rgba(40, 30, 25, .8), Math.Max(1, sv * .6)), bubble, sv * 2.5, sv * 2.5);
                        var tail = new StreamGeometry();
                        using (var g = tail.Open())
                        {
                            g.BeginFigure(new Point(bx + sv * 1.5, by), true);
                            g.LineTo(new Point(bx, by + sv * 2));
                            g.LineTo(new Point(bx + sv * 3.5, by));
                            g.EndFigure(true);
                        }
                        c.DrawGeometry(Rgba(255, 252, 240, .96), null, tail);
                        var color = e.Text == "♥" ? "#d9483b" : e.Text == "☀" ? "#e0a02a" : "#3a2e28";
                        var ft = Text(e.Text, Math.Round(sv * 6.4), color);
                        c.DrawText(ft, new Point(bx + bw / 2 - ft.Width / 2, by - bh / 2 + sv * .3 - ft.Height / 2));
                    }
                }
            }
        // party balloons and confetti
        foreach (var bl in life.PartyBalloons)
        {
            var pbl = v.P(bl.X, bl.Y);
            double yb = pbl.Y - bl.Alt * v.T, ub = Math.Max(1, s);
            c.DrawLine(new Pen(Rgba(60, 50, 40, .6), 1), new Point(pbl.X, yb), new Point(pbl.X + Math.Sin(bl.Ph * 2) * ub * 2, yb + ub * 6));
            c.DrawEllipse(Brush(bl.Color), null, new Point(pbl.X, yb - ub * 2), ub * 2.2, ub * 2.8);
            c.FillRectangle(Rgba(255, 255, 255, .55), new Rect(Math.Round(pbl.X - ub), Math.Round(yb - ub * 3.5), Math.Ceiling(ub), Math.Ceiling(ub)));
        }
        foreach (var co in life.Confettis)
        {
            var pco = v.P(co.X, co.Y);
            double uc = Math.Max(1, Math.Round(s * .8));
            c.FillRectangle(Brush(co.Color), new Rect(Math.Round(pco.X), Math.Round(pco.Y - co.Alt * v.T), Math.Floor(T * 9 + co.Ph) % 2 != 0 ? uc * 2 : uc, uc));
        }
        // hot-air balloon with its shadow on the ground
        if (life.Balloon is { } balao)
        {
            var ib2 = Balloon(balao.Colors);
            double sc = s * 1.25, bob = Math.Sin(T * .9) * s * 1.2, iw = 16, ih = 24;
            var pbh = v.P(balao.X, balao.Y);
            c.DrawEllipse(Rgba(0, 0, 0, .16), null, new Point(pbh.X + balao.Alt * v.T * .25, pbh.Y), iw * sc * .4, iw * sc * .16);
            Img(c, ib2, Math.Round(pbh.X - iw * sc / 2), Math.Round(pbh.Y - balao.Alt * v.T - ih * sc + bob), Math.Ceiling(iw * sc), Math.Ceiling(ih * sc));
        }
        // clouds: shadows passing over the city
        if (life.Clouds.Count > 0)
        {
            double an = (L.Darkness > .5 ? .07 : .11) * (1 + life.Rain.K * 1.4);
            using (c.PushOpacity(Math.Min(.3, an)))
                foreach (var nu in life.Clouds)
                {
                    var pn = v.P(nu.X, nu.Y);
                    double rw = nu.R * v.T * 1.6, rh = nu.R * v.T;
                    if (pn.X + rw < 0 || pn.X - rw > W || pn.Y + rh < 0 || pn.Y - rh > Hh) continue;
                    Img(c, CloudImage(nu.V, false), pn.X - rw, pn.Y - rh, rw * 2, rh * 2);
                }
        }
        // fog
        if (life.Fog.K > .02)
        {
            using (c.PushOpacity(Math.Min(.55, life.Fog.K * .5)))
                foreach (var nb in life.FogBanks)
                {
                    var pnb = v.P(nb.X, nb.Y);
                    double rw2 = nb.R * v.T * 1.8, rh2 = nb.R * v.T;
                    Img(c, CloudImage(nb.V, true), pnb.X - rw2, pnb.Y - rh2, rw2 * 2, rh2 * 2);
                }
            c.FillRectangle(Brush("#e8eef4", life.Fog.K * .12), new Rect(0, 0, W, Hh));
        }
        // rain or snow
        if (life.Drops.Count > 0)
        {
            if (life.Rain.Snow)
            {
                var flake = Rgba(255, 255, 255, .85);
                foreach (var g in life.Drops)
                {
                    int ug = g.L > 13 ? 2 : 1;
                    c.FillRectangle(flake, new Rect(Math.Round(g.X), Math.Round(g.Y), ug + 1, ug + 1));
                }
            }
            else
            {
                var rain = new StreamGeometry();
                using (var g = rain.Open())
                    foreach (var gg in life.Drops)
                    {
                        g.BeginFigure(new Point(gg.X, gg.Y), false);
                        g.LineTo(new Point(gg.X - life.Breeze.X * gg.L * .35, gg.Y - gg.L));
                        g.EndFigure(false);
                    }
                c.DrawGeometry(null, new Pen(Rgba(196, 210, 235, .5), 1), rain);
                var splash = Rgba(215, 228, 245, .55);
                foreach (var rp in life.Splashes)
                {
                    double kk = rp.T / .25;
                    c.FillRectangle(splash, new Rect(Math.Round(rp.X - 2 - kk * 2), Math.Round(rp.Y), 1, 1));
                    c.FillRectangle(splash, new Rect(Math.Round(rp.X + 2 + kk * 2), Math.Round(rp.Y), 1, 1));
                    c.FillRectangle(splash, new Rect(Math.Round(rp.X), Math.Round(rp.Y - 2 - kk), 1, 1));
                }
            }
        }
        // rainbow after the rain
        if (life.Arc is { } arco && T > arco.T0)
        {
            double ka = Math.Min(Math.Min(1, (T - arco.T0) / 6), (arco.End - T) / 8), cxA = W * arco.CX, cyA = Hh * 1.08, RA = Math.Max(W, Hh) * .78, lw = RA * .028;
            int[][] colors = [[255, 70, 70], [255, 160, 60], [255, 230, 80], [90, 210, 110], [70, 160, 255], [110, 90, 220], [170, 90, 210]];
            for (int i = 0; i < colors.Length; i++)
            {
                double r = RA - i * lw;
                var arc = new StreamGeometry();
                using (var g = arc.Open())
                {
                    g.BeginFigure(new Point(cxA + r * Math.Cos(Math.PI * 1.06), cyA + r * Math.Sin(Math.PI * 1.06)), false);
                    g.ArcTo(new Point(cxA + r * Math.Cos(Math.PI * 1.94), cyA + r * Math.Sin(Math.PI * 1.94)), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    g.EndFigure(false);
                }
                c.DrawGeometry(null, new Pen(Rgba(colors[i][0], colors[i][1], colors[i][2], Math.Round(.2 * ka, 3)), lw), arc);
            }
        }
    }

    private static Geometry HalfEllipse(double cx, double cy, double rx, double ry)
    {
        // ctx.ellipse(cx,cy,rx,ry,0,PI,0): the upper half, closed by fill
        var g = new StreamGeometry();
        using (var o = g.Open())
        {
            o.BeginFigure(new Point(cx - rx, cy), true);
            o.ArcTo(new Point(cx + rx, cy), new Size(rx, ry), 0, false, SweepDirection.Clockwise);
            o.EndFigure(true);
        }
        return g;
    }

    private FormattedText Text(string text, double size, string color)
    {
        var key = text + "|" + size.ToString(CultureInfo.InvariantCulture) + "|" + color;
        if (!_texts.TryGetValue(key, out var ft))
            _texts[key] = ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(UrbeTheme.UiFont, FontStyle.Normal, FontWeight.Bold), Math.Max(1, size), Brush(color));
        return ft;
    }

    // ---------------------------------------------------------------- after the light
    /// <summary>app.js urbeJanelasAcesas(k): each house lights up at a slightly different hour.</summary>
    public void Windows(DrawingContext c, IReadOnlyList<LegacyCityBuilding> buildings, LegacyLifeView v, double k, double nowMs)
    {
        using var _ = c.PushRenderOptions(AdditiveSmooth);
        var img = WindowGlow();
        foreach (var b in buildings)
        {
            if (b.X > v.X1 || b.X + b.W < v.X0 || b.Y > v.Y1 || b.Y + b.H < v.Y0) continue;
            uint sem = unchecked((uint)((b.X * 73856093) ^ (b.Y * 19349663)));
            double on = (sem % 100) / 100.0 * .35;
            if (k < on) continue;
            double kk = Math.Min(1, (k - on) / .3) * (.9 + .1 * Math.Sin(nowMs / 700 + sem));
            var p = v.P(b.X + b.W / 2.0, b.Y + b.H * .7);
            double r = TILE * v.Zoom * 1.7;
            using (c.PushOpacity(Math.Min(1, .42 * kk))) Img(c, img, p.X - r, p.Y - r, r * 2, r * 2);
        }
    }

    /// <summary>life.js brilhoPasso(): everything that lights up, under 'lighter'.</summary>
    public void Glow(DrawingContext c, LegacyWorldLife life, IReadOnlyList<ILegacyLifePerson> people, LegacyLifeView v)
    {
        double s = v.S, z = v.Zoom, esc = life.CurrentLight.Darkness, T = life.Time;
        using var _ = c.PushRenderOptions(Additive);
        // street lamps
        if (esc > .32 && z >= .45)
        {
            double kL = Math.Min(1, (esc - .32) / .35);
            foreach (var l in life.Lamps())
            {
                if (!v.Inside(l.X, l.Y, 2)) continue;
                var pl = v.P(l.X, l.Y);
                double fl = .92 + .08 * Math.Sin(T * 9 + l.Ph);
                Glow(c, "255,196,110", pl.X + s * .5, pl.Y - s * 10, s * 9, .75 * kL * fl);
                Glow(c, "255,170,90", pl.X + s * .5, pl.Y - s * 1, s * 16, .28 * kL * fl);
            }
        }
        // lanterns of those walking at night
        if (esc > .55 && z >= .38)
        {
            double sv = TILE * z / 20;
            foreach (var a in people)
            {
                if (!a.Placed || !v.Inside(a.X, a.Y, 1) || a.Pause > 0) continue;
                var pa = v.P(a.X, a.Y);
                Glow(c, "255,190,105", pa.X + sv * 5, pa.Y - sv * 8, sv * 11, .55 * (esc - .4));
            }
        }
        // fireflies
        foreach (var va in life.Fireflies)
        {
            if (!v.Inside(va.X, va.Y)) continue;
            var pv = v.P(va.X, va.Y);
            double b = Math.Pow((Math.Sin(T * 2.1 + va.Ph) + 1) / 2, 3) * Math.Min(Math.Min(1, va.T), va.Life - va.T);
            if (b < .04) continue;
            double yv = pv.Y - va.Alt * v.T, u = Math.Max(1, Math.Round(s * .8));
            Glow(c, "205,255,120", pv.X, yv, s * 5, b * .9);
            AddRect(c, "#f4ffc8", Math.Round(pv.X), Math.Round(yv), u, u, Math.Min(1, b * 1.4));
        }
        // moonlight on the water
        if (esc > .6)
            foreach (var bb in life.Sparkles)
            {
                if (!v.Inside(bb.X, bb.Y)) continue;
                double ab = Math.Sin(Math.PI * bb.T / bb.D) * .55;
                if (ab < .05) continue;
                var pb = v.P(bb.X, bb.Y);
                double u = Math.Max(1, Math.Round(s * .9));
                AddRect(c, "#bcd2ff", Math.Round(pb.X), Math.Round(pb.Y), u, u, ab);
            }
        // fireworks
        foreach (var fg in life.Rockets)
            for (int j = 0; j < fg.Trail.Count; j++)
            {
                var r = fg.Trail[j];
                var pr = v.P(r.X, r.Y);
                Glow(c, "255,220,160", pr.X, pr.Y - r.Alt * v.T, Math.Max(3, s * 2.6), (j + 1.0) / fg.Trail.Count * .8);
            }
        foreach (var f in life.Flashes)
        {
            var pc = v.P(f.X, f.Y);
            var rgb = Rgb(f.Color);
            Glow(c, rgb, pc.X, pc.Y - f.Alt * v.T, Math.Max(50, v.T * 5), (1 - f.T / .45) * .32);
            Glow(c, rgb, pc.X, pc.Y, Math.Max(70, v.T * 7), (1 - f.T / .45) * .22);
        }
        double rf = Math.Max(5, s * 5), nuc = Math.Max(2, Math.Round(s * 1.1));
        foreach (var fs in life.Sparks)
        {
            double k = fs.T / fs.D, a2 = (1 - k * k) * (k < .06 ? k / .06 : 1);
            if (a2 < .03) continue;
            var pf = v.P(fs.X, fs.Y);
            double yy = pf.Y - fs.Alt * v.T;
            // trail: from where the spark came to where it is
            double tx = pf.X - fs.VX * v.T * .09, ty = yy - (fs.VY - fs.VAlt) * v.T * .09;
            AddLine(c, Solid(fs.Color), new Point(tx, ty), new Point(pf.X, yy), Math.Max(1.2, s * .7), a2 * .8, round: true);
            Glow(c, Rgb(fs.Color), pf.X, yy, rf * (fs.Heavy ? .8 : 1), a2);
            AddRect(c, k > .08 && k < .4 ? "#ffffff" : fs.Color, Math.Round(pf.X - nuc / 2), Math.Round(yy - nuc / 2), nuc, nuc, a2);
        }
        // shooting stars
        foreach (var e in life.Stars)
        {
            double ke = e.T / e.D, x = e.X + e.VX * e.T, y = e.Y + e.VY * e.T, tail = .16, ae = Math.Sin(Math.PI * ke);
            using (c.PushRenderOptions(AdditiveSmooth))
                AddLine(c, Gradient("#ffffff", "#b4c8ff"), new Point(x, y), new Point(x - e.VX * tail, y - e.VY * tail), 2, .95 * ae, round: false);
            Glow(c, "220,230,255", x, y, 6, ae);
        }
    }

    private static string Rgb(string hex)
    {
        var (r, g, b) = LegacyLifeSprites.Hex(hex);
        return r + "," + g + "," + b;
    }
}
