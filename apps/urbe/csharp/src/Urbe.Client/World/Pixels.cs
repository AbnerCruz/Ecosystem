using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Urbe.Client.World;

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

/// <summary>Sprites of the 1.8.4 world (trees, buildings), generated once by Urbe.Core.</summary>
public sealed class SpriteCache
{
    private readonly Dictionary<string, Bitmap> _cache = new(StringComparer.Ordinal);

    public Bitmap Tree(string kind, int variant, bool snow) =>
        Get($"tree:{kind}:{variant}:{snow}", () => Pixels.ToBitmap(
            Core.LegacyWorldSprites.CreateTree(kind, variant, snow),
            Core.LegacyWorldSprites.TreeWidth, Core.LegacyWorldSprites.TreeHeight));

    public Bitmap Building(string kind, string style, int variant, bool flowers) =>
        Get($"building:{kind}:{style}:{variant}:{flowers}", () => Pixels.ToBitmap(
            Core.LegacyWorldBuildings.CreateBuilding(kind, style, variant, flowers),
            Core.LegacyWorldBuildings.Width, Core.LegacyWorldBuildings.Height));

    private Bitmap Get(string key, Func<Bitmap> create)
    {
        if (!_cache.TryGetValue(key, out var bitmap))
            _cache[key] = bitmap = create();
        return bitmap;
    }
}
