using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-209: draws the frames of a <see cref="VideoTimeline"/> with SkiaSharp — text, shapes, vault images, gráfico
/// curves and TeX formulas — at the project's size. A scene layer is drawn once per distinct look
/// (<see cref="VideoLayer.StateKey"/>) and reused from a byte-bounded cache; subtitles go over the composed frame.
/// No WebView, no JavaScript.
/// </summary>
public sealed class VideoFrameRenderer : IDisposable
{
    private readonly VideoTimeline _timeline;
    private readonly IReadOnlyDictionary<string, byte[]> _files;
    private readonly Dictionary<string, SKImage?> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (SKPath Path, float W, float H)?> _math = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SKPath?> _paths = new(StringComparer.Ordinal);
    private readonly BoundedCache<SKImage> _cache;
    private readonly object _gate = new();

    /// <param name="files">The vault files the elements use (images), read before rendering by their paths.</param>
    public VideoFrameRenderer(VideoTimeline timeline, IReadOnlyDictionary<string, byte[]>? files = null, long cacheBytes = 160L << 20)
    {
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _files = files ?? new Dictionary<string, byte[]>();
        _cache = new BoundedCache<SKImage>(cacheBytes, image => (long)image.Width * image.Height * 4);
    }

    public VideoTimeline Timeline => _timeline;
    /// <summary>Scene layers actually drawn (the others came from the cache).</summary>
    public int LayersDrawn { get; private set; }
    public BoundedCache<SKImage> Cache => _cache;

    /// <summary>The vault paths of the images a timeline uses (to read before rendering).</summary>
    public static IReadOnlyList<string> FilesUsed(VideoTimeline timeline) => timeline.Scenes
        .SelectMany(s => s.Scene.Elements).Where(e => e.Kind == "image" && e.Source is not null).Select(e => e.Source!)
        .Distinct(StringComparer.Ordinal).ToList();

    public SKBitmap RenderFrame(int index) => Render(_timeline.FrameTime(index));

    public SKBitmap Render(double time)
    {
        var bitmap = new SKBitmap(new SKImageInfo(_timeline.Width, _timeline.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        RenderInto(time, bitmap);
        return bitmap;
    }

    /// <summary>Draws the frame at <paramref name="time"/> into an RGBA bitmap of the timeline's size.</summary>
    public void RenderInto(double time, SKBitmap target)
    {
        var frame = _timeline.Evaluate(time);
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Black);
        lock (_gate)
        {
            foreach (var layer in frame.Layers)
            {
                // drawn right after it is taken: the next layer may evict it from the cache
                var image = _cache.GetOrAdd(layer.StateKey, () => DrawLayer(layer));
                using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(layer.Opacity * 255)) };
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);
                if (!_cache.Owns(image)) image.Dispose();
            }
        }
        if (frame.Subtitle is { Length: > 0 } subtitle) DrawSubtitle(canvas, subtitle);
        canvas.Flush();
    }

    private SKImage DrawLayer(VideoLayer layer)
    {
        LayersDrawn++;
        int w = _timeline.Width, h = _timeline.Height;
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Sem memória para desenhar o quadro.");
        var canvas = surface.Canvas;
        // a theme paints the scenes that keep the default background with its gradient
        if (_timeline.Theme is { } theme && string.Equals(layer.Scene.Scene.Background, VideoThemes.DefaultBackground, StringComparison.OrdinalIgnoreCase))
        {
            using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w * 0.25f, h),
                [Color(theme.Top, SKColors.Black), Color(theme.Bottom, SKColors.Black)], SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(0, 0, w, h, paint);
        }
        else canvas.Clear(Color(layer.Scene.Scene.Background, SKColors.Black));
        // #1004 slice 4: the camera moves what is drawn after the background (which stays)
        int cameraRestore = canvas.SaveCount;
        if (layer.Camera is { } cam)
        {
            canvas.Save();
            canvas.Translate(w / 2f, h / 2f);
            canvas.Scale((float)cam.Zoom);
            if (cam.Rotation != 0) canvas.RotateDegrees((float)-cam.Rotation);
            canvas.Translate((float)(-cam.X * w), (float)(-cam.Y * h));
        }
        foreach (var e in layer.Elements)
        {
            if (e.Opacity <= 0 || e.Scale <= 0) continue;
            // #1004 slice 3: the element is drawn in a layer of its own, and the layer gets its glow, shadow and blur
            int restoreTo = e.Effects is { Any: true } fx ? Effects(canvas, e, fx, w, h) : -1;
            if (e.Split is not null && e.Instance >= 0) { DrawUnit(canvas, e, w, h); FinishEffects(canvas, e, restoreTo, w, h); continue; }
            float bw = (float)(e.Width * w), bh = (float)(e.Height * h);
            canvas.Save();
            canvas.Translate((float)(e.X * w), (float)(e.Y * h));
            if (e.Rotation != 0) canvas.RotateDegrees((float)e.Rotation);
            if (e.Scale != 1) canvas.Scale((float)e.Scale);
            var box = new SKRect(-bw / 2, -bh / 2, bw / 2, bh / 2);
            var color = Color(e.Color, SKColors.White).WithAlpha((byte)Math.Round(e.Opacity * 255));
            switch (e.Kind)
            {
                case "shape": DrawShape(canvas, e, box, color); break;
                case "image": DrawImage(canvas, e, box); break;
                case "plot": DrawPlot(canvas, e, box, color); break;
                case "math": DrawMath(canvas, e, box, color); break;
                case "path": DrawPath(canvas, e, box, color, h); break;
                case "group": break;   // only carries its children
                default: DrawText(canvas, e.Text, box, (float)(e.FontSize * h), color, e.Kind == "title", e.Kind == "script" ? SKTextAlign.Left : SKTextAlign.Center, e.Progress); break;
            }
            canvas.Restore();
            FinishEffects(canvas, e, restoreTo, w, h);
        }
        canvas.RestoreToCount(cameraRestore);
        return surface.Snapshot();
    }

    private static void DrawShape(SKCanvas canvas, VideoElementState e, SKRect box, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        var r = box;
        if (e.Progress < 1) r = new SKRect(box.Left, box.Top, box.Left + box.Width * (float)e.Progress, box.Bottom);
        var kind = e.Text?.Trim().ToLowerInvariant();
        if (kind is "line" or "linha" or "arrow" or "seta")
        {
            // a stroke along the box's middle, as thick as the box is tall (at most a fifth of its length), drawn by progress
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeCap = SKStrokeCap.Round;
            float thick = Math.Clamp(box.Height, 2, Math.Max(2, box.Width / 5)), x2 = box.Left + box.Width * (float)Math.Clamp(e.Progress, 0, 1);
            paint.StrokeWidth = thick;
            bool arrow = kind is "arrow" or "seta";
            float head = arrow ? Math.Min(box.Width * 0.3f, thick * 3) : 0;
            canvas.DrawLine(box.Left + thick / 2, 0, Math.Max(box.Left + thick / 2, x2 - head * 0.6f), 0, paint);
            if (arrow && e.Progress > 0)
            {
                paint.Style = SKPaintStyle.Fill;
                using var tip = new SKPath();
                tip.MoveTo(x2, 0); tip.LineTo(x2 - head, -head * 0.6f); tip.LineTo(x2 - head, head * 0.6f); tip.Close();
                canvas.DrawPath(tip, paint);
            }
        }
        else if (kind is "ellipse" or "circle" or "elipse" or "círculo") canvas.DrawOval(r, paint);
        else canvas.DrawRoundRect(r, Math.Min(box.Width, box.Height) * 0.08f, Math.Min(box.Width, box.Height) * 0.08f, paint);
    }

    private void DrawImage(SKCanvas canvas, VideoElementState e, SKRect box)
    {
        if (e.Source is null) return;
        if (!_images.TryGetValue(e.Source, out var image))
            _images[e.Source] = image = _files.TryGetValue(e.Source, out var bytes) ? SKImage.FromEncodedData(bytes) : null;
        if (image is null)
        {
            using var missing = new SKPaint { Color = new SKColor(255, 255, 255, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
            canvas.DrawRect(box, missing);
            return;
        }
        // fitted inside its box, never cropped
        float scale = Math.Min(box.Width / image.Width, box.Height / image.Height);
        var dest = SKRect.Create(-image.Width * scale / 2, -image.Height * scale / 2, image.Width * scale, image.Height * scale);
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(e.Opacity * 255)) };
        canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
    }

    private static void DrawPlot(SKCanvas canvas, VideoElementState e, SKRect box, SKColor color)
    {
        var drawing = FunctionPlot.Parse(e.Text).Sample(600);
        using var axis = new SKPaint { Color = color.WithAlpha((byte)(color.Alpha * 0.45)), StrokeWidth = Math.Max(1, box.Height / 220), IsAntialias = true, Style = SKPaintStyle.Stroke };
        float X(double x) => box.Left + (float)((x - drawing.XMin) / (drawing.XMax - drawing.XMin)) * box.Width;
        float Y(double y) => box.Bottom - (float)((y - drawing.YMin) / (drawing.YMax - drawing.YMin)) * box.Height;
        if (drawing.YMin <= 0 && drawing.YMax >= 0) canvas.DrawLine(box.Left, Y(0), box.Right, Y(0), axis);
        if (drawing.XMin <= 0 && drawing.XMax >= 0) canvas.DrawLine(X(0), box.Top, X(0), box.Bottom, axis);
        canvas.Save();
        canvas.ClipRect(box);
        // progress draws the curve from left to right: a graph that is traced as the narration explains it
        if (e.Progress < 1) canvas.ClipRect(new SKRect(box.Left, box.Top, box.Left + box.Width * (float)e.Progress, box.Bottom));
        foreach (var curve in drawing.Curves)
        {
            var hue = SKColor.Parse(FunctionPlot.Colors[curve.Index % FunctionPlot.Colors.Length]).WithAlpha(color.Alpha);
            using var paint = new SKPaint { Color = hue, StrokeWidth = Math.Max(2, box.Height / 90), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
            foreach (var segment in curve.Segments)
            {
                using var path = new SKPath();
                path.MoveTo(X(segment[0].X), Y(segment[0].Y));
                for (int i = 1; i < segment.Count; i++) path.LineTo(X(segment[i].X), Y(segment[i].Y));
                canvas.DrawPath(path, paint);
            }
        }
        canvas.Restore();
    }

    /// <summary>
    /// #956: SVG path data fitted inside the box (aspect kept, like an image), stroked; progress draws it on along its
    /// length — the "trim path" of motion design tools.
    /// </summary>
    private void DrawPath(SKCanvas canvas, VideoElementState e, SKRect box, SKColor color, int h)
    {
        var data = e.Text ?? "";
        if (!_paths.TryGetValue(data, out var source)) _paths[data] = source = SKPath.ParseSvgPathData(data);
        if (source is null || source.Bounds.Width + source.Bounds.Height <= 0 || e.Progress <= 0) return;
        var b = source.Bounds;
        float scale = Math.Min(box.Width / Math.Max(1e-3f, b.Width), box.Height / Math.Max(1e-3f, b.Height));
        using var path = new SKPath(source);
        path.Transform(SKMatrix.CreateTranslation(-b.MidX, -b.MidY).PostConcat(SKMatrix.CreateScale(scale, scale)));
        using var paint = new SKPaint
        {
            Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1.5f, (float)(e.FontSize * h / 6)),
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
        };
        if (e.Progress >= 1) { canvas.DrawPath(path, paint); return; }
        using var measure = new SKPathMeasure(path, false);
        var lengths = new List<float>();
        do lengths.Add(measure.Length); while (measure.NextContour());
        float left = lengths.Sum() * (float)e.Progress;
        using var drawn = new SKPath();
        using var again = new SKPathMeasure(path, false);
        foreach (var length in lengths)
        {
            if (left <= 0) break;
            again.GetSegment(0, Math.Min(length, left), drawn, true);
            left -= length;
            again.NextContour();
        }
        canvas.DrawPath(drawn, paint);
    }

    private void DrawMath(SKCanvas canvas, VideoElementState e, SKRect box, SKColor color)
    {
        var tex = e.Text ?? "";
        if (!_math.TryGetValue(tex, out var shape)) _math[tex] = shape = MathPath(tex);
        if (shape is not { } m) { DrawText(canvas, tex, box, box.Height / 3, color, false, SKTextAlign.Center, 1); return; }
        float scale = Math.Min(box.Width / m.W, box.Height / m.H);
        canvas.Save();
        canvas.Scale(scale);
        canvas.Translate(-m.W / 2, -m.H / 2);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawPath(m.Path, paint);
        canvas.Restore();
    }

    /// <summary>The formula as one filled Skia path, from the SVG subset (path, line) the Core's math canvas writes.</summary>
    public static (SKPath Path, float W, float H)? MathPath(string tex)
    {
        var result = MathRenderer.RenderSvg(tex, true, 48);
        if (!result.Success) return null;
        try
        {
            var root = XDocument.Parse(result.Svg).Root!;
            float W(XElement el, string a) => float.Parse(el.Attribute(a)?.Value ?? "0", CultureInfo.InvariantCulture);
            var path = new SKPath();
            foreach (var el in root.Elements())
            {
                var fill = el.Attribute("fill")?.Value;
                if (el.Name.LocalName == "path" && el.Attribute("d")?.Value is { } d &&
                    fill is not null && fill != "none" && !(fill.StartsWith("rgba(", StringComparison.Ordinal) && fill.EndsWith(",0)", StringComparison.Ordinal)))
                {
                    using var piece = SKPath.ParseSvgPathData(d);
                    if (piece is not null) path.AddPath(piece);
                }
                else if (el.Name.LocalName == "line")
                {
                    float width = Math.Max(1, el.Attribute("stroke-width") is { } sw ? float.Parse(sw.Value, CultureInfo.InvariantCulture) : 1);
                    float x1 = W(el, "x1"), y1 = W(el, "y1"), x2 = W(el, "x2"), y2 = W(el, "y2");
                    path.AddRect(new SKRect(Math.Min(x1, x2), Math.Min(y1, y2) - width / 2, Math.Max(x1, x2), Math.Max(y1, y2) + width / 2));
                }
            }
            float w = W(root, "width"), h = W(root, "height");
            return w > 0 && h > 0 ? (path, w, h) : null;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or NullReferenceException) { return null; }
    }

    /// <summary>
    /// Text wrapped to its box; when it does not fit, the size goes down until it does (to 35 %), and only then the
    /// last line ends with an ellipsis — the box is the limit the person set.
    /// </summary>
    private static void DrawText(SKCanvas canvas, string text, SKRect box, float size, SKColor color, bool bold, SKTextAlign align, double progress)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (progress < 1) text = text[..(int)Math.Round(text.Length * progress)];
        using var font = Font(bold, size);
        var (lines, _) = Layout(text, box, font);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        float y = -lines.Count * font.Spacing / 2 - font.Metrics.Ascent;
        foreach (var line in lines)
        {
            float x = align == SKTextAlign.Left ? box.Left : 0;
            canvas.DrawText(line, x, y, align, font, paint);
            y += font.Spacing;
        }
    }

    /// <summary>Opens the element's effect layers; returns the save count to restore to.</summary>
    private static int Effects(SKCanvas canvas, VideoElementState e, VideoEffects fx, int w, int h)
    {
        int restoreTo = canvas.SaveCount;
        SKImageFilter? filter = null;
        if (fx.Blur > 0) filter = SKImageFilter.CreateBlur((float)(fx.Blur * h), (float)(fx.Blur * h));
        if (fx.Glow > 0)
        {
            var glow = Color(fx.GlowColor ?? e.Color, SKColors.White).WithAlpha((byte)Math.Round(Math.Clamp(e.Opacity, 0, 1) * 230));
            // two halos, one tight and one wide, read as light rather than as a smudge
            var tight = SKImageFilter.CreateDropShadow(0, 0, (float)(fx.Glow * h * 0.35), (float)(fx.Glow * h * 0.35), glow, filter);
            filter = SKImageFilter.CreateDropShadow(0, 0, (float)(fx.Glow * h), (float)(fx.Glow * h), glow, tight);
        }
        if (fx.Shadow > 0)
            filter = SKImageFilter.CreateDropShadow((float)(fx.Shadow * h * 0.4), (float)(fx.Shadow * h * 0.6), (float)(fx.Shadow * h * 0.6), (float)(fx.Shadow * h * 0.6),
                new SKColor(0, 0, 0, (byte)Math.Round(Math.Clamp(e.Opacity, 0, 1) * 150)), filter);
        using (var paint = new SKPaint { ImageFilter = filter }) canvas.SaveLayer(paint);
        filter?.Dispose();
        // the gradient is laid over what the element draws, in a layer under the effects
        if (fx.Gradient is not null) canvas.SaveLayer();
        return restoreTo;
    }

    /// <summary>Lays the gradient over the element (only where it drew) and closes its effect layers.</summary>
    private static void FinishEffects(SKCanvas canvas, VideoElementState e, int restoreTo, int w, int h)
    {
        if (restoreTo < 0) return;
        if (e.Effects?.Gradient is { } to)
        {
            float cy = (float)(e.Y * h), half = (float)(e.Height * h * Math.Max(0.001, e.Scale) / 2);
            using var shader = SKShader.CreateLinearGradient(new SKPoint(0, cy - half), new SKPoint(0, cy + half),
                [Color(e.Color, SKColors.White), Color(to, SKColors.White)], SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.SrcIn };
            canvas.DrawPaint(paint);
        }
        canvas.RestoreToCount(restoreTo);
    }

    private sealed record UnitPlace(string Text, float X, float Y, float Size);
    private readonly Dictionary<(string Text, string Split, float Size, float W, float H, bool Bold), List<UnitPlace>> _units = new();

    /// <summary>
    /// #1004 slice 2: one letter or word of a split text, where it sits when the whole text is laid out in its box,
    /// turned and scaled about its own centre by its instance's state.
    /// </summary>
    private void DrawUnit(SKCanvas canvas, VideoElementState e, int w, int h)
    {
        bool bold = e.Kind == "title";
        float bw = (float)(e.Width * w), bh = (float)(e.Height * h), size = (float)(e.FontSize * h);
        var key = (e.Text, e.Split!, size, bw, bh, bold);
        if (!_units.TryGetValue(key, out var places))
        {
            if (_units.Count > 256) _units.Clear();
            places = [];
            using var layoutFont = Font(bold, size);
            var (lines, _) = Layout(e.Text, new SKRect(-bw / 2, -bh / 2, bw / 2, bh / 2), layoutFont);
            float y = -lines.Count * layoutFont.Spacing / 2 - layoutFont.Metrics.Ascent;
            float middle = (layoutFont.Metrics.Ascent + layoutFont.Metrics.Descent) / 2;
            foreach (var line in lines)
            {
                float left = -layoutFont.MeasureText(line) / 2;
                foreach (var (start, length) in VideoProcedural.Units(line, e.Split))
                {
                    var unit = line.Substring(start, length);
                    float x = left + layoutFont.MeasureText(line[..start]) + layoutFont.MeasureText(unit) / 2;
                    places.Add(new UnitPlace(unit, x, y + middle, layoutFont.Size));
                }
                y += layoutFont.Spacing;
            }
            _units[key] = places;
        }
        if (e.Instance >= places.Count) return;
        var p = places[e.Instance];
        using var font = Font(bold, p.Size);
        using var paint = new SKPaint { Color = Color(e.Color, SKColors.White).WithAlpha((byte)Math.Round(e.Opacity * 255)), IsAntialias = true };
        canvas.Save();
        canvas.Translate((float)(e.X * w) + p.X, (float)(e.Y * h) + p.Y);
        if (e.Rotation != 0) canvas.RotateDegrees((float)e.Rotation);
        if (e.Scale != 1) canvas.Scale((float)e.Scale);
        canvas.DrawText(p.Text, 0, -(font.Metrics.Ascent + font.Metrics.Descent) / 2, SKTextAlign.Center, font, paint);
        canvas.Restore();
    }

    private static readonly SKTypeface Regular = Face(SKFontStyleWeight.Normal), Bold = Face(SKFontStyleWeight.Bold);
    private static SKTypeface Face(SKFontStyleWeight weight) =>
        SKTypeface.FromFamilyName(null, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
    private static SKFont Font(bool bold, float size) => new(bold ? Bold : Regular, size) { Subpixel = true, Edging = SKFontEdging.Antialias };

    /// <summary>The lines drawn in the box; the font's size goes down to fit it (to 35 %), then the last line is cut with "…".</summary>
    private static (List<string> Lines, bool Cut) Layout(string text, SKRect box, SKFont font)
    {
        List<string> lines;
        float min = font.Size * 0.35f;
        while (true)
        {
            lines = Wrap(text, font, box.Width);
            if (lines.Count * font.Spacing <= box.Height || font.Size <= min) break;
            font.Size = Math.Max(min, font.Size * 0.9f);
        }
        int fit = Math.Max(1, (int)(box.Height / font.Spacing));
        if (lines.Count <= fit) return (lines, false);
        lines = lines.Take(fit).ToList();
        lines[^1] = lines[^1].TrimEnd() + "…";
        return (lines, true);
    }

    /// <summary>#956: how a text element fits its box when drawn — the measure the agent's review uses.</summary>
    public static VideoTextFit Fit(VideoElementState e, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(e.Text)) return new(1, false);
        float size = (float)(e.FontSize * height);
        using var font = Font(e.Kind == "title", size);
        var (_, cut) = Layout(e.Text, new SKRect(0, 0, (float)(e.Width * width), (float)(e.Height * height)), font);
        return new(font.Size / size, cut);
    }

    /// <summary>
    /// #956: the frames at <paramref name="times"/> in a grid of up to four columns, each numbered in its corner — one
    /// image the agent's visual model looks at.
    /// </summary>
    public byte[] RenderSheet(IReadOnlyList<double> times)
    {
        int columns = Math.Min(4, Math.Max(1, times.Count)), rows = (times.Count + columns - 1) / columns, w = _timeline.Width, h = _timeline.Height, gap = 8;
        using var sheet = new SKBitmap(columns * w + (columns + 1) * gap, Math.Max(1, rows) * h + (rows + 1) * gap);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(40, 40, 40));
        using var label = new SKFont(SKTypeface.Default, Math.Max(14, h / 12f)) { Edging = SKFontEdging.Antialias };
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 200) };
        using var ink = new SKPaint { Color = SKColors.Yellow, IsAntialias = true };
        for (int i = 0; i < times.Count; i++)
        {
            float x = gap + i % columns * (w + gap), y = gap + i / columns * (h + gap);
            using (var frame = Render(times[i])) canvas.DrawBitmap(frame, x, y);
            var n = (i + 1).ToString(CultureInfo.InvariantCulture);
            canvas.DrawRect(x, y, label.MeasureText(n) + label.Size * 0.8f, label.Spacing * 1.1f, back);
            canvas.DrawText(n, x + label.Size * 0.4f, y - label.Metrics.Ascent + label.Spacing * 0.05f, SKTextAlign.Left, label, ink);
        }
        using var image = SKImage.FromBitmap(sheet);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static List<string> Wrap(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var current = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && font.MeasureText(candidate) > width) { lines.Add(current); current = word; }
                else current = candidate;
            }
            lines.Add(current);
        }
        return lines;
    }

    private void DrawSubtitle(SKCanvas canvas, string text)
    {
        int w = _timeline.Width, h = _timeline.Height;
        float size = Math.Min(w, h) * 0.05f;
        using var font = new SKFont(SKTypeface.Default, size) { Edging = SKFontEdging.Antialias };
        var lines = Wrap(text, font, w * 0.86f);
        float lineHeight = font.Spacing, top = h * 0.93f - lines.Count * lineHeight;
        using var back = new SKPaint { Color = Color(_timeline.Theme?.SubtitleBox, new SKColor(0, 0, 0, 170)), IsAntialias = true };
        float widest = lines.Max(l => font.MeasureText(l));
        canvas.DrawRoundRect(new SKRect(w / 2f - widest / 2 - size * 0.5f, top - size * 0.25f, w / 2f + widest / 2 + size * 0.5f, top + lines.Count * lineHeight + size * 0.25f), size * 0.3f, size * 0.3f, back);
        using var paint = new SKPaint { Color = Color(_timeline.Theme?.SubtitleText, SKColors.White), IsAntialias = true };
        float y = top - font.Metrics.Ascent;
        foreach (var line in lines) { canvas.DrawText(line, w / 2f, y, SKTextAlign.Center, font, paint); y += lineHeight; }
    }

    private static SKColor Color(string? value, SKColor fallback) => SKColor.TryParse(value, out var c) ? c : fallback;

    public void Dispose()
    {
        _cache.Dispose();
        foreach (var image in _images.Values) image?.Dispose();
        foreach (var m in _math.Values) m?.Path.Dispose();
        foreach (var p in _paths.Values) p?.Dispose();
        _images.Clear();
        _math.Clear();
        _paths.Clear();
    }
}
