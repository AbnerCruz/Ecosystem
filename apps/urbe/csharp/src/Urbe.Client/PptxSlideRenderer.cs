using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-237: draws one slide of a <see cref="PptxDeck"/> with SkiaSharp at a given width, keeping the deck's aspect ratio:
/// background, shapes (rotation and flips), pictures (crop, decoded no bigger than drawn), tables, text laid out and
/// wrapped in its box with the device's fonts. Objects the deck parser could not read are marked in place with their
/// name. What fails while drawing (a picture that does not decode) is added to <c>problems</c>, never a crash.
/// </summary>
public static class PptxSlideRenderer
{
    /// <summary>The widest a slide is drawn (a 4K screen); the reader zooms from there.</summary>
    public const int MaxWidth = 3840;
    private const long MaxPicturePixels = 40L * 1000 * 1000;

    private static readonly ConcurrentDictionary<(string, bool, bool), SKTypeface> Faces = new();

    public static SKBitmap Render(PptxDeck deck, int index, int width, ICollection<string>? problems = null)
    {
        width = Math.Clamp(width, 16, MaxWidth);
        int height = Math.Max(1, (int)Math.Round(width * deck.Height / deck.Width));
        var slide = deck.Slide(index);
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        float scale = (float)(width / deck.Width);
        canvas.Clear(ToSk(slide.Background).WithAlpha(255));
        canvas.Scale(scale);
        if (slide.BackgroundPicture is { } backgroundPicture)
            DrawPicture(canvas, deck, new PptxPicture(0, 0, deck.Width, deck.Height, 0, false, false, backgroundPicture), scale, problems);
        foreach (var element in slide.Elements)
        {
            canvas.Save();
            var center = new SKPoint((float)(element.X + element.Width / 2), (float)(element.Y + element.Height / 2));
            if (element.Rotation != 0) canvas.RotateDegrees((float)element.Rotation, center.X, center.Y);
            switch (element)
            {
                case PptxShape shape: DrawShape(canvas, shape, center); break;
                case PptxPicture picture: DrawPicture(canvas, deck, picture, scale, problems, center); break;
                case PptxTable table: DrawTable(canvas, table); break;
                case PptxMissing missing: DrawMissing(canvas, missing); break;
            }
            canvas.Restore();
        }
        return bitmap;
    }

    /// <summary>The Skia pixels as an Avalonia bitmap (one copy, no PNG round-trip).</summary>
    public static WriteableBitmap ToAvalonia(SKBitmap source)
    {
        var bitmap = new WriteableBitmap(new PixelSize(source.Width, source.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        var pixels = source.GetPixels();
        int row = source.Width * 4;
        var buffer = new byte[row];
        for (int y = 0; y < source.Height; y++)
        {
            Marshal.Copy(pixels + y * source.RowBytes, buffer, 0, row);
            Marshal.Copy(buffer, 0, frame.Address + y * frame.RowBytes, row);
        }
        return bitmap;
    }

    private static SKColor ToSk(PptxColor c) => new(c.R, c.G, c.B, c.A);

    private static SKRect Rect(PptxElement e) => SKRect.Create((float)e.X, (float)e.Y, (float)e.Width, (float)e.Height);

    private static void Flip(SKCanvas canvas, PptxElement e, SKPoint center)
    {
        if (e.FlipH || e.FlipV) canvas.Scale(e.FlipH ? -1 : 1, e.FlipV ? -1 : 1, center.X, center.Y);
    }

    private static void DrawShape(SKCanvas canvas, PptxShape shape, SKPoint center)
    {
        var r = Rect(shape);
        canvas.Save();
        Flip(canvas, shape, center);
        using var path = new SKPath();
        bool closed = true;
        switch (shape.Geometry)
        {
            case "ellipse": path.AddOval(r); break;
            case "roundRect" or "flowChartAlternateProcess":
                float radius = (float)(shape.Corner * Math.Min(r.Width, r.Height));
                path.AddRoundRect(r, radius, radius); break;
            case "triangle": path.MoveTo(r.MidX, r.Top); path.LineTo(r.Right, r.Bottom); path.LineTo(r.Left, r.Bottom); path.Close(); break;
            case "rtTriangle": path.MoveTo(r.Left, r.Top); path.LineTo(r.Right, r.Bottom); path.LineTo(r.Left, r.Bottom); path.Close(); break;
            case "diamond": path.MoveTo(r.MidX, r.Top); path.LineTo(r.Right, r.MidY); path.LineTo(r.MidX, r.Bottom); path.LineTo(r.Left, r.MidY); path.Close(); break;
            case "rightArrow" or "leftArrow":
            {
                float head = Math.Min(r.Height, r.Width / 2), shaft = r.Height / 4;
                path.MoveTo(r.Left, r.MidY - shaft); path.LineTo(r.Right - head, r.MidY - shaft); path.LineTo(r.Right - head, r.Top); path.LineTo(r.Right, r.MidY);
                path.LineTo(r.Right - head, r.Bottom); path.LineTo(r.Right - head, r.MidY + shaft); path.LineTo(r.Left, r.MidY + shaft); path.Close();
                if (shape.Geometry == "leftArrow") path.Transform(SKMatrix.CreateScale(-1, 1, r.MidX, r.MidY));
                break;
            }
            case "line": path.MoveTo(r.Left, r.Top); path.LineTo(r.Right, r.Bottom); closed = false; break;
            case "custom":
                foreach (var figure in shape.Paths ?? [])
                {
                    float sx = figure.Width > 0 ? r.Width / (float)figure.Width : 1, sy = figure.Height > 0 ? r.Height / (float)figure.Height : 1;
                    SKPoint P((double X, double Y) p) => new(r.Left + (float)p.X * sx, r.Top + (float)p.Y * sy);
                    foreach (var c in figure.Commands)
                        switch (c.Kind)
                        {
                            case 'M': path.MoveTo(P(c.Points[0])); break;
                            case 'L': path.LineTo(P(c.Points[0])); break;
                            case 'C': path.CubicTo(P(c.Points[0]), P(c.Points[1]), P(c.Points[2])); break;
                            case 'Q': path.QuadTo(P(c.Points[0]), P(c.Points[1])); break;
                            case 'Z': path.Close(); break;
                        }
                }
                break;
            default: path.AddRect(r); break;
        }
        if (shape.Fill is { } fill && closed && fill.A > 0)
        {
            using var paint = new SKPaint { Color = ToSk(fill), IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawPath(path, paint);
        }
        if (shape.Line is { } line && line.A > 0 && shape.LineWidth > 0)
        {
            using var paint = new SKPaint { Color = ToSk(line), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)shape.LineWidth };
            canvas.DrawPath(path, paint);
        }
        canvas.Restore();
        // text is never mirrored by a flip; it turns with the shape
        if (shape.Text is { } text) DrawText(canvas, text, r);
    }

    private static void DrawPicture(SKCanvas canvas, PptxDeck deck, PptxPicture picture, float scale, ICollection<string>? problems, SKPoint? center = null)
    {
        var target = Rect(picture);
        if (target.Width <= 0 || target.Height <= 0) return;
        var bytes = deck.Picture(picture.Media);
        using var decoded = bytes is null ? null : Decode(bytes, (int)Math.Ceiling(target.Width * scale), (int)Math.Ceiling(target.Height * scale));
        if (decoded is null)
        {
            problems?.Add("uma imagem não pôde ser mostrada (" + System.IO.Path.GetFileName(picture.Media) + ")");
            DrawMissing(canvas, new PptxMissing(picture.X, picture.Y, picture.Width, picture.Height, 0, "Imagem"));
            return;
        }
        double l = Math.Clamp(picture.CropLeft, 0, .99), t = Math.Clamp(picture.CropTop, 0, .99), rr = Math.Clamp(picture.CropRight, 0, .99), b = Math.Clamp(picture.CropBottom, 0, .99);
        var source = new SKRect((float)(l * decoded.Width), (float)(t * decoded.Height), (float)((1 - rr) * decoded.Width), (float)((1 - b) * decoded.Height));
        if (source.Width <= 0 || source.Height <= 0) source = new SKRect(0, 0, decoded.Width, decoded.Height);
        canvas.Save();
        if (center is { } c) Flip(canvas, picture, c);
        using var image = SKImage.FromBitmap(decoded);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, source, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        canvas.Restore();
    }

    /// <summary>A picture decoded about as big as it is drawn (JPEG/WebP scale while decoding); a huge one never allocated whole.</summary>
    private static SKBitmap? Decode(byte[] bytes, int width, int height)
    {
        try
        {
            using var codec = SKCodec.Create(new MemoryStream(bytes, writable: false));
            if (codec is null) return null;
            var full = codec.Info;
            float wanted = Math.Min(1, Math.Max(width / (float)Math.Max(1, full.Width), height / (float)Math.Max(1, full.Height)) * 1.25f);
            var size = codec.GetScaledDimensions(wanted);
            if ((long)size.Width * size.Height > MaxPicturePixels) return null;
            var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) { bitmap.Dispose(); return null; }
            if (size.Width > width * 2 && size.Height > height * 2 && width > 0 && height > 0)
            {
                // PNG does not scale while decoding: shrink once so the slide is drawn from a picture of its size
                var smaller = bitmap.Resize(new SKImageInfo(Math.Max(1, width), Math.Max(1, (int)((long)width * size.Height / size.Width)), SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                if (smaller is not null) { bitmap.Dispose(); bitmap = smaller; }
            }
            return bitmap;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private static void DrawTable(SKCanvas canvas, PptxTable table)
    {
        var xs = new List<float> { (float)table.X };
        foreach (var w in table.Columns) xs.Add(xs[^1] + (float)w);
        var ys = new List<float> { (float)table.Y };
        foreach (var h in table.Rows) ys.Add(ys[^1] + (float)h);
        using var border = new SKPaint { Color = new SKColor(0x80, 0x80, 0x80), Style = SKPaintStyle.Stroke, StrokeWidth = .75f, IsAntialias = true };
        foreach (var cell in table.Cells)
        {
            int c2 = Math.Min(xs.Count - 1, cell.Column + cell.ColumnSpan), r2 = Math.Min(ys.Count - 1, cell.Row + cell.RowSpan);
            var rect = new SKRect(xs[cell.Column], ys[cell.Row], xs[c2], ys[r2]);
            if (cell.Fill is { } fill)
            {
                using var paint = new SKPaint { Color = ToSk(fill), Style = SKPaintStyle.Fill };
                canvas.DrawRect(rect, paint);
            }
            canvas.DrawRect(rect, border);
            DrawText(canvas, cell.Text, rect);
        }
    }

    private static void DrawMissing(SKCanvas canvas, PptxMissing missing)
    {
        var r = Rect(missing);
        using var fill = new SKPaint { Color = new SKColor(0x90, 0x90, 0x90, 0x30), Style = SKPaintStyle.Fill };
        using var line = new SKPaint { Color = new SKColor(0x70, 0x70, 0x70), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true, PathEffect = SKPathEffect.CreateDash([6, 4], 0) };
        canvas.DrawRect(r, fill);
        canvas.DrawRect(r, line);
        float size = Math.Clamp(Math.Min(r.Width, r.Height) / 8, 6, 18);
        using var font = new SKFont(Face(null, true, false), size);
        using var ink = new SKPaint { Color = new SKColor(0x50, 0x50, 0x50), IsAntialias = true };
        foreach (var (text, dy) in new[] { (missing.Label, -size * .2f), ("não desenhado aqui", size * 1.1f) })
        {
            float w = font.MeasureText(text);
            canvas.DrawText(text, r.MidX - w / 2, r.MidY + dy, SKTextAlign.Left, font, ink);
        }
    }

    // ---------------------------------------------------------------- text

    private static SKTypeface Face(string? family, bool bold, bool italic) => Faces.GetOrAdd((family ?? "", bold, italic), key =>
        (key.Item1.Length > 0 ? SKTypeface.FromFamilyName(key.Item1, bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) : null)
        ?? SKTypeface.FromFamilyName("sans-serif", bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
        ?? SKTypeface.Default);

    private static SKTypeface For(string text, SKTypeface asked)
    {
        if (asked.ContainsGlyphs(text)) return asked;
        foreach (var rune in text.EnumerateRunes())
            if (!asked.ContainsGlyph(rune.Value) && SKFontManager.Default.MatchCharacter(rune.Value) is { } other) return other;
        return asked;
    }

    private sealed record Piece(string Text, SKFont Font, SKColor Color, float Width, bool Space, bool Underline, bool Strike);
    private sealed record Line(List<Piece> Pieces, float Width, float Ascent, float Height, float Start);

    /// <summary>The text laid out in <paramref name="box"/> less its insets: wrapped, aligned, anchored; overflow is drawn, not cut (as in PowerPoint).</summary>
    private static void DrawText(SKCanvas canvas, PptxText text, SKRect box)
    {
        var inner = new SKRect(box.Left + (float)text.InsetLeft, box.Top + (float)text.InsetTop, box.Right - (float)text.InsetRight, box.Bottom - (float)text.InsetBottom);
        float available = Math.Max(1, inner.Width);
        var fonts = new List<SKFont>();
        try
        {
            var laid = new List<(PptxParagraph Paragraph, List<Line> Lines, float Before, float After)>();
            foreach (var paragraph in text.Paragraphs)
            {
                var lines = new List<Line>();
                float left = (float)paragraph.MarginLeft, first = (float)(paragraph.MarginLeft + (paragraph.Bullet is null ? paragraph.Indent : 0));
                var current = new List<Piece>();
                float x = 0;
                float LineStart() => lines.Count == 0 ? first : left;
                void Push()
                {
                    while (current.Count > 0 && current[^1].Space) { x -= current[^1].Width; current.RemoveAt(current.Count - 1); }
                    float size = current.Count == 0 ? (float)paragraph.EmptySize : current.Max(p => p.Font.Size);
                    float ascent = current.Count == 0 ? size * .9f : current.Max(p => -p.Font.Metrics.Ascent);
                    float height = paragraph.LineSpacingPoints is { } pts ? (float)pts : size * 1.2f * (float)paragraph.LineSpacing;
                    lines.Add(new Line(current, x, ascent, height, LineStart()));
                    current = [];
                    x = 0;
                }
                foreach (var run in paragraph.Runs)
                {
                    if (run.LineBreak) { Push(); continue; }
                    if (run.Text.Length == 0) continue;
                    foreach (var token in Tokens(run.Text))
                    {
                        var font = new SKFont(For(token, Face(run.Font, run.Bold, run.Italic)), (float)run.Size) { Subpixel = true };
                        fonts.Add(font);
                        bool space = token.Trim().Length == 0;
                        float w = font.MeasureText(token);
                        float room = available - LineStart();
                        if (text.Wrap && !space && x + w > room && current.Any(p => !p.Space)) Push();
                        if (space && current.Count == 0 && lines.Count > 0) continue;
                        if (text.Wrap && !space && w > available - LineStart())
                        {
                            // a word longer than the box breaks by letters
                            string rest = token;
                            while (rest.Length > 0)
                            {
                                int n = rest.Length;
                                while (n > 1 && x + font.MeasureText(rest[..n]) > available - LineStart()) n--;
                                current.Add(new Piece(rest[..n], font, ToSk(run.Color), font.MeasureText(rest[..n]), false, run.Underline, run.Strike));
                                x += font.MeasureText(rest[..n]);
                                rest = rest[n..];
                                if (rest.Length > 0) Push();
                            }
                            continue;
                        }
                        current.Add(new Piece(token, font, ToSk(run.Color), w, space, run.Underline, run.Strike));
                        x += w;
                    }
                }
                if (current.Count > 0 || lines.Count == 0) Push();
                laid.Add((paragraph, lines, (float)paragraph.SpaceBefore, (float)paragraph.SpaceAfter));
            }
            float total = 0;
            for (int i = 0; i < laid.Count; i++) total += (i > 0 ? laid[i].Before : 0) + laid[i].Lines.Sum(l => l.Height) + (i < laid.Count - 1 ? laid[i].After : 0);
            float y = text.Anchor switch
            {
                PptxAnchor.Middle => inner.Top + (inner.Height - total) / 2,
                PptxAnchor.Bottom => inner.Bottom - total,
                _ => inner.Top
            };
            using var paint = new SKPaint { IsAntialias = true };
            for (int i = 0; i < laid.Count; i++)
            {
                var (paragraph, lines, before, after) = laid[i];
                if (i > 0) y += before;
                for (int k = 0; k < lines.Count; k++)
                {
                    var line = lines[k];
                    float baseline = y + line.Ascent;
                    float width = available - line.Start;
                    float offset = paragraph.Align switch
                    {
                        PptxTextAlign.Center => (width - line.Width) / 2,
                        PptxTextAlign.Right => width - line.Width,
                        _ => 0
                    };
                    float gap = 0;
                    if (paragraph.Align == PptxTextAlign.Justify && k < lines.Count - 1)
                    {
                        int spaces = line.Pieces.Count(p => p.Space);
                        if (spaces > 0) gap = (width - line.Width) / spaces;
                    }
                    float px = inner.Left + line.Start + offset;
                    if (k == 0 && paragraph.Bullet is { } bullet && line.Pieces.Count > 0)
                    {
                        var lead = line.Pieces.First(p => !p.Space);
                        using var bulletFont = new SKFont(For(bullet, Face(null, false, false)), lead.Font.Size);
                        paint.Color = paragraph.BulletColor is { } bc ? ToSk(bc) : lead.Color;
                        float bx = inner.Left + (float)(paragraph.MarginLeft + Math.Min(0, paragraph.Indent));
                        if (paragraph.Indent >= 0) bx = inner.Left + (float)paragraph.MarginLeft - bulletFont.MeasureText(bullet + " ");
                        canvas.DrawText(bullet, Math.Max(inner.Left, bx) + (paragraph.Align == PptxTextAlign.Center ? offset : 0), baseline, SKTextAlign.Left, bulletFont, paint);
                    }
                    foreach (var piece in line.Pieces)
                    {
                        if (!piece.Space && piece.Color.Alpha > 0)
                        {
                            paint.Color = piece.Color;
                            canvas.DrawText(piece.Text, px, baseline, SKTextAlign.Left, piece.Font, paint);
                            if (piece.Underline || piece.Strike)
                            {
                                float thickness = Math.Max(.5f, piece.Font.Size / 16);
                                using var stroke = new SKPaint { Color = piece.Color, StrokeWidth = thickness, IsAntialias = true };
                                if (piece.Underline) canvas.DrawLine(px, baseline + thickness * 2, px + piece.Width, baseline + thickness * 2, stroke);
                                if (piece.Strike) canvas.DrawLine(px, baseline - piece.Font.Size * .3f, px + piece.Width, baseline - piece.Font.Size * .3f, stroke);
                            }
                        }
                        px += piece.Width + (piece.Space ? gap : 0);
                    }
                    y += line.Height;
                }
                if (i < laid.Count - 1) y += after;
            }
        }
        finally { foreach (var f in fonts) f.Dispose(); }
    }

    /// <summary>Words and the spaces between them, so a line breaks between words.</summary>
    private static IEnumerable<string> Tokens(string text)
    {
        int start = 0;
        for (int i = 1; i <= text.Length; i++)
            if (i == text.Length || char.IsWhiteSpace(text[i]) != char.IsWhiteSpace(text[i - 1]))
            {
                yield return text[start..i];
                start = i;
            }
    }
}
