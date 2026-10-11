using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Urbe.Core;
using Urbe.Client.World;

namespace Urbe.Client;

/// <summary>Image bytes ↔ RGBA for texture packs (embedded data:image PNG/JPEG/GIF/WebP), the native counterpart of Image + canvas.</summary>
public static class TextureImages
{
    /// <summary>Decodes an embedded image to straight RGBA; null when it cannot be read (SVG included).</summary>
    public static LegacyTexturePack.DecodedImage? Decode(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            using var bitmap = WriteableBitmap.Decode(ms);
            int w = bitmap.PixelSize.Width, h = bitmap.PixelSize.Height;
            if (w < 1 || h < 1) return null;
            using var frame = bitmap.Lock();
            var rgba = new byte[w * h * 4];
            var row = new byte[w * 4];
            bool bgra = frame.Format == PixelFormat.Bgra8888;
            bool premul = bitmap.AlphaFormat == AlphaFormat.Premul;
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(frame.Address + y * frame.RowBytes, row, 0, row.Length);
                for (int x = 0; x < w; x++)
                {
                    int i = x * 4, o = (y * w + x) * 4;
                    byte r = bgra ? row[i + 2] : row[i], g = row[i + 1], b = bgra ? row[i] : row[i + 2], a = row[i + 3];
                    if (premul && a is > 0 and < 255) { r = (byte)Math.Min(255, (r * 255 + a / 2) / a); g = (byte)Math.Min(255, (g * 255 + a / 2) / a); b = (byte)Math.Min(255, (b * 255 + a / 2) / a); }
                    rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = a;
                }
            }
            return new LegacyTexturePack.DecodedImage(rgba, w, h);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>reduzir(file, maxW, maxH): fits the image in the box (never enlarges) and returns it as a PNG data URL.</summary>
    public static string? ReduceToDataUrl(byte[] file, int maxW, int maxH)
    {
        var src = Decode(file);
        if (src is null) return null;
        double s = Math.Min(1, Math.Min((double)maxW / src.Width, (double)maxH / src.Height));
        int w = Math.Max(1, (int)Math.Round(src.Width * s)), h = Math.Max(1, (int)Math.Round(src.Height * s));
        var rgba = w == src.Width && h == src.Height ? src.Rgba : Resize(src, w, h, s < 1);
        using var ms = new MemoryStream();
        Pixels.ToBitmap(rgba, w, h).Save(ms, new PngBitmapEncoderOptions());
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>drawImage into a w×h canvas: nearest, or bilinear when smoothing.</summary>
    public static byte[] Resize(LegacyTexturePack.DecodedImage src, int w, int h, bool smoothing)
    {
        var d = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 4;
                double sx = (x + .5) * src.Width / w, sy = (y + .5) * src.Height / h;
                if (!smoothing)
                {
                    Array.Copy(src.Rgba, (Math.Min(src.Height - 1, (int)sy) * src.Width + Math.Min(src.Width - 1, (int)sx)) * 4, d, o, 4);
                    continue;
                }
                double fx = Math.Clamp(sx - .5, 0, src.Width - 1), fy = Math.Clamp(sy - .5, 0, src.Height - 1);
                int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(src.Width - 1, x0 + 1), y1 = Math.Min(src.Height - 1, y0 + 1);
                double tx = fx - x0, ty = fy - y0;
                for (int c = 0; c < 4; c++)
                {
                    double a = src.Rgba[(y0 * src.Width + x0) * 4 + c], b = src.Rgba[(y0 * src.Width + x1) * 4 + c];
                    double e = src.Rgba[(y1 * src.Width + x0) * 4 + c], f = src.Rgba[(y1 * src.Width + x1) * 4 + c];
                    double top = a + (b - a) * tx, bottom = e + (f - e) * tx;
                    d[o + c] = (byte)Math.Clamp((int)Math.Floor(top + (bottom - top) * ty + .5), 0, 255);
                }
            }
        return d;
    }
}
