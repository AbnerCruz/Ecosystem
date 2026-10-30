using SkiaSharp;

namespace Urbe.Client;

/// <summary>
/// CST-081 / DEC-0059: a photo the person attaches is never refused for being big. One that is over
/// <see cref="MaxBytes"/> or wider than <see cref="MaxEdge"/> is turned upright (EXIF) and reduced to JPEG, the size every
/// provider takes: Anthropic asks for 2000 px or less when a message has more than 20 images, and its per-image limit
/// is 5 MB once in base64.
/// </summary>
public static class AttachmentImages
{
    public const int MaxEdge = 2000;
    public const int MaxBytes = 3_700_000;   // ≈ 5 MB in base64

    /// <summary>The image as it will be sent: the same bytes when they already fit, otherwise a reduced JPEG; null when it cannot be read.</summary>
    public static (byte[] Bytes, string Mime, bool Reduced)? Fit(byte[] bytes, string mime)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;
        var origin = codec.EncodedOrigin;
        var (w, h) = (codec.Info.Width, codec.Info.Height);
        if (bytes.Length <= MaxBytes && Math.Max(w, h) <= MaxEdge && origin == SKEncodedOrigin.TopLeft) return (bytes, mime, false);

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null) return null;
        using var upright = Upright(decoded, origin);
        var scale = Math.Min(1.0, (double)MaxEdge / Math.Max(upright.Width, upright.Height));
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(upright.Width * scale)), Math.Max(1, (int)Math.Round(upright.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Premul);
        using var resized = scale < 1 ? upright.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell)) : upright.Copy();
        if (resized is null) return null;
        // a transparent picture keeps its transparency on white, as JPEG has none
        using var surface = SKSurface.Create(new SKImageInfo(resized.Width, resized.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(resized, 0, 0);
        using var image = surface.Snapshot();
        foreach (var quality in new[] { 88, 80, 70, 60, 50 })
        {
            using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            if (jpeg is not null && jpeg.Size <= MaxBytes) return (jpeg.ToArray(), "image/jpeg", true);
        }
        return null;
    }

    private static SKBitmap Upright(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return source.Copy();
        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using var canvas = new SKCanvas(result);
        var (w, h) = (source.Width, source.Height);
        // each case maps a source pixel (x, y) to where it belongs upright; the transforms apply from the last one up
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Scale(-1, 1, w / 2f, 0); break;
            case SKEncodedOrigin.BottomRight: canvas.RotateDegrees(180, w / 2f, h / 2f); break;
            case SKEncodedOrigin.BottomLeft: canvas.Scale(1, -1, 0, h / 2f); break;
            case SKEncodedOrigin.LeftTop: canvas.Translate(h, 0); canvas.RotateDegrees(90); canvas.Scale(1, -1, 0, h / 2f); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(h, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(h, 0); canvas.RotateDegrees(90); canvas.Scale(-1, 1, w / 2f, 0); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, w); canvas.RotateDegrees(270); break;
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
