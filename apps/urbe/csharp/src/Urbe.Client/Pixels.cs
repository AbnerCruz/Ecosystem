using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Urbe.Client;

/// <summary>Turns RGBA produced by Urbe.Core into native bitmaps (no PNG round-trip).</summary>
public static class Pixels
{
    public static WriteableBitmap ToBitmap(byte[] rgba, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("RGBA size does not match the image.", nameof(rgba));
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
            PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var frame = bitmap.Lock();
        for (int y = 0; y < height; y++)
            Marshal.Copy(rgba, y * width * 4, frame.Address + y * frame.RowBytes, width * 4);
        return bitmap;
    }
}
