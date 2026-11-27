using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lunet.Ai;

namespace Lunet.Client;

/// <summary>
/// The AI's image perception (PNG/JPG/WebP) through Avalonia's decoder, so every head has it without its own code.
/// Large photos are decoded at most 512 px wide to keep memory bounded; pixels come out straight RGBA.
/// </summary>
public sealed class AvaloniaAiImages : ILunetAiImages
{
    public static readonly AvaloniaAiImages Instance = new();
    public const int MaxSide = 512;

    public LunetAiImage? Load(string fullPath, string relativePath)
    {
        try
        {
            using var file = File.OpenRead(fullPath);
            using var probe = new Bitmap(file);
            var (w0, h0) = (probe.PixelSize.Width, probe.PixelSize.Height);
            if (w0 <= 0 || h0 <= 0) return null;
            Bitmap bitmap = probe;
            Bitmap? scaled = null;
            if (Math.Max(w0, h0) > MaxSide)
            {
                file.Position = 0;
                scaled = w0 >= h0 ? Bitmap.DecodeToWidth(file, MaxSide) : Bitmap.DecodeToHeight(file, MaxSide);
                bitmap = scaled;
            }
            using (scaled)
            {
                int w = bitmap.PixelSize.Width, h = bitmap.PixelSize.Height, stride = w * 4;
                var buffer = new byte[stride * h];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try { bitmap.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, stride); }
                finally { handle.Free(); }
                ToStraightRgba(buffer, bitmap.Format ?? PixelFormat.Bgra8888, bitmap.AlphaFormat ?? AlphaFormat.Premul);
                return new LunetAiImage(w, h, buffer);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null; // an image the decoder cannot read is "not perceivable", never a crash of the conversation
        }
    }

    /// <summary>Converts the decoder's 32-bit layout (BGRA or RGBA, premultiplied or not) to straight RGBA, in place.</summary>
    public static void ToStraightRgba(byte[] pixels, PixelFormat format, AlphaFormat alpha)
    {
        bool bgra = format == PixelFormat.Bgra8888;
        if (!bgra && format != PixelFormat.Rgba8888) throw new NotSupportedException("formato de pixel " + format);
        bool premultiplied = alpha == AlphaFormat.Premul;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte c0 = pixels[i], c2 = pixels[i + 2], a = alpha == AlphaFormat.Opaque ? (byte)255 : pixels[i + 3];
            if (bgra) { pixels[i] = c2; pixels[i + 2] = c0; }
            pixels[i + 3] = a;
            if (premultiplied && a is > 0 and < 255)
                for (int k = 0; k < 3; k++) pixels[i + k] = (byte)Math.Min(255, (pixels[i + k] * 255 + a / 2) / a);
        }
    }
}
