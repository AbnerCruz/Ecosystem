using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-169: the drawings the Urbe makes itself (graphs, formulas: paths, lines, text, groups and a nested clipped
/// viewport) turned into PNG for formats that need pixels beside the vector (Word, PowerPoint). It is not a general
/// SVG renderer: what it does not know it skips, and the vector original always travels with the PNG.
/// </summary>
public static class SvgRaster
{
    /// <summary>The SVG drawn on white at <paramref name="width"/>×<paramref name="height"/> pixels, or null when unreadable.</summary>
    public static OfficeRaster? ToPng(string svg, int width, int height)
    {
        XElement root;
        try { root = XElement.Parse(svg); }
        catch (System.Xml.XmlException) { return null; }
        if (width <= 0 || height <= 0 || width > 8000 || height > 8000) return null;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface is null) return null;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        var (vx, vy, vw, vh) = ViewBox(root) ?? (0, 0, Num(root, "width") ?? width, Num(root, "height") ?? height);
        canvas.Scale((float)(width / vw), (float)(height / vh));
        canvas.Translate((float)-vx, (float)-vy);
        Draw(canvas, root, Style.Default);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new OfficeRaster(data.ToArray(), width, height);
    }

    /// <summary>
    /// Draws <paramref name="svg"/> into <paramref name="box"/> of any canvas (a PDF page keeps it as vectors), keeping
    /// its proportions. False when the SVG cannot be read.
    /// </summary>
    public static bool DrawInto(SKCanvas canvas, string svg, SKRect box)
    {
        XElement root;
        try { root = XElement.Parse(svg); }
        catch (System.Xml.XmlException) { return false; }
        var (vx, vy, vw, vh) = ViewBox(root) ?? (0, 0, Num(root, "width") ?? box.Width, Num(root, "height") ?? box.Height);
        var scale = (float)Math.Min(box.Width / vw, box.Height / vh);
        canvas.Save();
        canvas.Translate(box.Left + (box.Width - (float)(vw * scale)) / 2, box.Top + (box.Height - (float)(vh * scale)) / 2);
        canvas.ClipRect(new SKRect(0, 0, (float)(vw * scale), (float)(vh * scale)));
        canvas.Scale(scale);
        canvas.Translate((float)-vx, (float)-vy);
        Draw(canvas, root, Style.Default);
        canvas.Restore();
        return true;
    }

    /// <summary>The SVG's own size (viewBox or width/height), for laying it out.</summary>
    public static (double Width, double Height)? SizeOf(string svg)
    {
        try
        {
            var root = XElement.Parse(svg);
            var vb = ViewBox(root);
            return vb is { } v ? (v.W, v.H) : Num(root, "width") is { } w && Num(root, "height") is { } h ? (w, h) : null;
        }
        catch (System.Xml.XmlException) { return null; }
    }

    private sealed record Style(string Fill, string Stroke, double StrokeWidth, double FillOpacity, double StrokeOpacity, double FontSize, bool Bold, string Anchor)
    {
        public static readonly Style Default = new("#000000", "none", 1, 1, 1, 16, false, "start");
    }

    private static void Draw(SKCanvas canvas, XElement element, Style inherited)
    {
        foreach (var child in element.Elements())
        {
            var style = Read(child, inherited);
            switch (child.Name.LocalName)
            {
                case "g":
                    Draw(canvas, child, style);
                    break;
                case "svg":
                    canvas.Save();
                    double x = Num(child, "x") ?? 0, y = Num(child, "y") ?? 0, w = Num(child, "width") ?? 0, h = Num(child, "height") ?? 0;
                    if (w > 0 && h > 0)
                    {
                        if ((string?)child.Attribute("overflow") != "visible") canvas.ClipRect(new SKRect((float)x, (float)y, (float)(x + w), (float)(y + h)));
                        canvas.Translate((float)x, (float)y);
                        if (ViewBox(child) is { } vb)
                        {
                            canvas.Scale((float)(w / vb.W), (float)(h / vb.H));
                            canvas.Translate((float)-vb.X, (float)-vb.Y);
                        }
                    }
                    Draw(canvas, child, style);
                    canvas.Restore();
                    break;
                case "path":
                    using (var path = SKPath.ParseSvgPathData((string?)child.Attribute("d") ?? ""))
                        if (path is not null) Paint(canvas, path, style);
                    break;
                case "line":
                    using (var path = new SKPath())
                    {
                        path.MoveTo((float)(Num(child, "x1") ?? 0), (float)(Num(child, "y1") ?? 0));
                        path.LineTo((float)(Num(child, "x2") ?? 0), (float)(Num(child, "y2") ?? 0));
                        Paint(canvas, path, style with { Fill = "none" });
                    }
                    break;
                case "rect":
                    using (var path = new SKPath())
                    {
                        path.AddRect(new SKRect((float)(Num(child, "x") ?? 0), (float)(Num(child, "y") ?? 0),
                            (float)((Num(child, "x") ?? 0) + (Num(child, "width") ?? 0)), (float)((Num(child, "y") ?? 0) + (Num(child, "height") ?? 0))));
                        Paint(canvas, path, style);
                    }
                    break;
                case "circle":
                    using (var path = new SKPath())
                    {
                        path.AddCircle((float)(Num(child, "cx") ?? 0), (float)(Num(child, "cy") ?? 0), (float)(Num(child, "r") ?? 0));
                        Paint(canvas, path, style);
                    }
                    break;
                case "polyline":
                case "polygon":
                    var pts = ((string?)child.Attribute("points") ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (float)d : float.NaN).ToArray();
                    if (pts.Length >= 4 && pts.All(float.IsFinite))
                        using (var path = new SKPath())
                        {
                            path.MoveTo(pts[0], pts[1]);
                            for (int i = 2; i + 1 < pts.Length; i += 2) path.LineTo(pts[i], pts[i + 1]);
                            if (child.Name.LocalName == "polygon") path.Close();
                            Paint(canvas, path, style);
                        }
                    break;
                case "text":
                    canvas.Save();
                    Transform(canvas, (string?)child.Attribute("transform"));
                    Text(canvas, child, style);
                    canvas.Restore();
                    break;
            }
        }
    }

    private static void Paint(SKCanvas canvas, SKPath path, Style style)
    {
        if (Color(style.Fill, style.FillOpacity) is { } fill)
        {
            using var paint = new SKPaint { Color = fill, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawPath(path, paint);
        }
        if (Color(style.Stroke, style.StrokeOpacity) is { } stroke)
        {
            using var paint = new SKPaint { Color = stroke, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)style.StrokeWidth, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
            canvas.DrawPath(path, paint);
        }
    }

    private static void Text(SKCanvas canvas, XElement text, Style style)
    {
        if (Color(style.Fill, style.FillOpacity) is not { } color) return;
        using var typeface = SKTypeface.FromFamilyName("sans-serif", style.Bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, (float)style.FontSize);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        // the text of the element itself, not of its <title> (a tooltip, never drawn)
        var value = string.Concat(text.Nodes().OfType<XText>().Select(n => n.Value));
        var width = font.MeasureText(value);
        double x = Num(text, "x") ?? 0, y = (Num(text, "y") ?? 0) + (Num(text, "dy") ?? 0);
        x -= style.Anchor switch { "middle" => width / 2, "end" => width, _ => 0 };
        canvas.DrawText(value, (float)x, (float)y, font, paint);
    }

    /// <summary>translate(x y) and rotate(a) of a transform attribute, in order.</summary>
    private static void Transform(SKCanvas canvas, string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform)) return;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(transform, @"(translate|rotate|scale)\(([^)]*)\)"))
        {
            var n = m.Groups[2].Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (float)d : 0f).ToArray();
            if (n.Length == 0) continue;
            switch (m.Groups[1].Value)
            {
                case "translate": canvas.Translate(n[0], n.Length > 1 ? n[1] : 0); break;
                case "rotate": canvas.RotateDegrees(n[0]); break;
                case "scale": canvas.Scale(n[0], n.Length > 1 ? n[1] : n[0]); break;
            }
        }
    }

    private static Style Read(XElement e, Style s)
    {
        string? A(string name) => (string?)e.Attribute(name);
        return s with
        {
            Fill = A("fill") ?? s.Fill,
            Stroke = A("stroke") ?? s.Stroke,
            StrokeWidth = Num(e, "stroke-width") ?? s.StrokeWidth,
            FillOpacity = Num(e, "fill-opacity") ?? s.FillOpacity,
            StrokeOpacity = Num(e, "stroke-opacity") ?? s.StrokeOpacity,
            FontSize = Num(e, "font-size") ?? s.FontSize,
            Bold = A("font-weight") is { } wgt ? wgt is "bold" or "600" or "700" or "800" or "900" : s.Bold,
            Anchor = A("text-anchor") ?? s.Anchor,
        };
    }

    private static SKColor? Color(string value, double opacity)
    {
        value = value.Trim();
        if (value is "none" or "transparent" or "") return null;
        SKColor color;
        if (value is "currentColor" or "black") color = new SKColor(0x25, 0x27, 0x2c);
        else if (value == "white") color = SKColors.White;
        else if (value.StartsWith("rgba(", StringComparison.Ordinal) || value.StartsWith("rgb(", StringComparison.Ordinal))
        {
            var parts = value[(value.IndexOf('(') + 1)..value.IndexOf(')')].Split(',');
            if (parts.Length < 3) return null;
            byte P(int i) => (byte)Math.Clamp(double.Parse(parts[i], CultureInfo.InvariantCulture), 0, 255);
            color = new SKColor(P(0), P(1), P(2));
            if (parts.Length > 3) opacity *= double.Parse(parts[3], CultureInfo.InvariantCulture);
        }
        else if (!SKColor.TryParse(value, out color)) return null;
        return color.WithAlpha((byte)Math.Clamp(color.Alpha * opacity, 0, 255));
    }

    private static (double X, double Y, double W, double H)? ViewBox(XElement e)
    {
        var parts = ((string?)e.Attribute("viewBox") ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return null;
        var n = parts.Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).ToArray();
        return n.Any(double.IsNaN) || n[2] <= 0 || n[3] <= 0 ? null : (n[0], n[1], n[2], n[3]);
    }

    private static double? Num(XElement e, string name)
    {
        var raw = ((string?)e.Attribute(name))?.Trim();
        if (string.IsNullOrEmpty(raw) || raw.EndsWith('%')) return null;
        raw = raw.EndsWith("px", StringComparison.Ordinal) ? raw[..^2] : raw;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
