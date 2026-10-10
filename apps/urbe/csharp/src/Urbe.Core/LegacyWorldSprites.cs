using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Urbe.Core;

/// <summary>
/// Original 1.8.4-beta tree/bridge geometry in a host-neutral C# raster.
/// The output is straight-alpha RGBA, without runtime JavaScript or vault IO.
/// Canvas2D antialiased polygon edges still require device/image comparison.
/// </summary>
public static class LegacyWorldSprites
{
    public const int TreeWidth = 24;
    public const int TreeHeight = 32;
    public const int BridgeWidth = 16;
    public const int BridgeHeight = 16;
    private static readonly string[] Names = [
        "oak", "birch", "willow", "pine", "deadpine", "acacia", "palm", "cactus"
    ];
    public static IReadOnlyList<string> TreeKinds { get; } = Array.AsReadOnly(Names);
    private static double Hash(int x, int y, int seed) =>
        LegacyWorldPixelTextures.Hash(x, y, seed);
    private static int Round(double n) => (int)Math.Floor(n + .5);

    public static byte[] CreateTree(string kind, int variant = 0, bool snow = false)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (Array.IndexOf(Names, kind) < 0)
            throw new ArgumentException("Espécie desconhecida.", nameof(kind));
        if ((uint)variant >= 3)
            throw new ArgumentOutOfRangeException(nameof(variant));

        var sp = new PixelCanvas(24, 32);
        var seed = variant * 31 + kind.Length * 7;
        if (kind is not ("cactus" or "palm"))
            sp.Shadow(12, 29, 8, 2.2);

        if (kind is "oak" or "birch" or "willow")
        {
            var trunk = kind == "birch" ? "#e9e4d8" : "#6b4a2e";
            var dark = kind == "birch" ? "#3b3530" : "#4b321f";
            sp.Rect(11, 18, 3, 12, trunk);
            sp.Rect(13, 18, 1, 12, dark);
            if (kind == "birch")
            {
                sp.Point(11, 21, dark);
                sp.Point(12, 25, dark);
                sp.Point(11, 27, dark);
            }

            string[] colors = kind switch {
                "birch" => ["#8fb25a", "#a9c86c", "#6f9344", "#3f5a26"],
                "willow" => ["#6f8f4a", "#86a65c", "#56733a", "#33472a"],
                _ => variant switch {
                    0 => ["#4f8a3a", "#67a24a", "#3b6f2d", "#243f1b"],
                    1 => ["#5a8f3c", "#74a84e", "#437331", "#28431c"],
                    _ => ["#4c7f36", "#63964a", "#3a6a2b", "#223b19"]
                }
            };
            Canopy(sp, 12, 11 + variant % 2,
                9 - (variant == 2 ? 1 : 0), colors, seed);
            if (variant != 1)
                Canopy(sp, 8 + variant * 3, 8, 4, colors, seed + 5);
            if (kind == "willow")
                for (var i = 0; i < 7; i++)
                    sp.Rect(5 + 2 * i, 14, 1,
                        6 + (int)Math.Floor(Hash(i, seed, 4) * 5), colors[2]);
            if (snow)
                sp.Disc(10, 5, 5, 2.2, "#f2f5f8");
        }
        else if (kind is "pine" or "deadpine")
        {
            sp.Rect(11, 22, 2, 8, "#5a3d25");
            if (kind == "deadpine")
            {
                sp.Rect(11, 6, 2, 18, "#6a5140");
                for (var i = 0; i < 4; i++)
                {
                    var y = 9 + i * 4;
                    sp.Rect(7 + i, y, 4 - i, 1, "#6a5140");
                    sp.Rect(13, y + 1, 4 - i, 1, "#5b4435");
                }
                if (snow)
                    sp.Rect(11, 6, 2, 1, "#f2f5f8");
            }
            else
            {
                string[] c = variant switch {
                    0 => ["#2f5a3a", "#3f6f47", "#224530"],
                    1 => ["#355f3b", "#467849", "#274a2e"],
                    _ => ["#2c5536", "#3b6a43", "#1f402a"]
                };
                for (var i = 0; i < 4; i++)
                {
                    var top = 3 + 5 * i;
                    var half = 3 + 2 * i;
                    sp.Polygon([12,top,12+half+1,top+8,12-half-1,top+8],c[2]);
                    sp.Polygon([12,top+1,12+half,top+7.5,12-half,top+7.5],c[0]);
                    sp.Polygon([12,top+1,12,top+7.5,12-half,top+7.5],c[1]);
                    if (snow)
                        sp.Polygon([12,top+1,12+half*.6,top+4,12-half*.6,top+4],"#eef3f6");
                }
            }
        }
        else if (kind == "acacia")
        {
            sp.Rect(11, 16, 2, 14, "#6b4a2e");
            sp.Point(10, 17, "#6b4a2e");
            sp.Point(13, 17, "#6b4a2e");
            sp.Disc(12, 13, 10, 3.4, "#3f5a24");
            sp.Disc(12, 12, 9, 2.6, "#6f8a3a");
            sp.Disc(10, 11, 5, 1.6, "#86a24a");
        }
        else if (kind == "palm")
        {
            sp.Shadow(13, 29, 6, 1.8);
            for (var i = 0; i < 14; i++)
                sp.Rect(12 + Round(i * .18), 15 + i, 2, 1,
                    i % 3 == 0 ? "#6b4f33" : "#8a6a45");
            string[] fronds = ["#4e8a3c", "#65a34a", "#3a6d2d"];
            (int X, int Y)[] tips = [(-8,2),(8,2),(-6,-3),(6,-3),(0,-5)];
            for (var i = 0; i < tips.Length; i++)
                for (var t = 0; t < 9; t++)
                    sp.Point(
                        12 + Round(tips[i].X * t / 8d),
                        13 + Round(tips[i].Y * t / 8d + t * t * .04),
                        fronds[i % 3]);
        }
        else if (kind == "cactus")
        {
            sp.Shadow(12, 29, 4, 1.4);
            sp.Rect(10, 14, 4, 15, "#5f8f47");
            sp.Rect(13, 14, 1, 15, "#4a733a");
            sp.Rect(6, 18, 2, 6, "#5f8f47");
            sp.Rect(6, 23, 4, 2, "#5f8f47");
            sp.Rect(16, 16, 2, 6, "#5f8f47");
            sp.Rect(14, 21, 4, 2, "#5f8f47");
            sp.Point(11, 13, "#e7cf73");
        }
        return sp.Pixels;
    }

    public static byte[] CreateBridge()
    {
        var sp = new PixelCanvas(16, 16);
        sp.Rect(0, 2, 16, 12, "#8a6440");
        for (var x = 0; x < 16; x += 3)
            sp.Rect(x, 2, 1, 12, "#6b4a2e");
        sp.Rect(0, 1, 16, 1, "#5a3d25");
        sp.Rect(0, 14, 16, 1, "#5a3d25");
        sp.Rect(0, 0, 16, 1, "#b08a5c");
        return sp.Pixels;
    }

    public static byte[] EncodePng(byte[] pixels, int width, int height) =>
        PixelCanvas.EncodePng(pixels, width, height);

    private static void Canopy(PixelCanvas sp, double x, double y,
        double r, string[] c, int seed)
    {
        sp.Disc(x, y, r + 1, r + 1, c[3]);
        sp.Disc(x, y, r, r, c[0]);
        sp.Disc(x - r * .28, y - r * .3, r * .62, r * .62, c[1]);
        sp.Disc(x + r * .25, y + r * .35, r * .55, r * .55, c[2]);
        for (var i = 0; i < 14; i++)
        {
            var a = Hash(i, seed, 1) * 6.28;
            var d = Hash(i, seed, 2) * r * .8;
            sp.Point(Round(x + Math.Cos(a) * d),
                Round(y + Math.Sin(a) * d),
                Hash(i, seed, 3) < .5 ? c[1] : c[2]);
        }
    }
}

/// <summary>
/// Canvas-like finite RGBA raster primitives. Filling polygons uses hard
/// pixel-center edges; the old HTML Canvas antialias requires separate
/// screenshot verification before declaring pixel-exact polygons.
/// </summary>
internal sealed class PixelCanvas
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[checked(width * height * 4)];
    }

    private static byte Component(string hex, int offset)
    {
        static int N(char c) => c <= '9' ? c - '0' : (c | (char)0x20) - 'a' + 10;
        return (byte)((N(hex[offset]) << 4) | N(hex[offset + 1]));
    }
    private static byte Clamp(double value) =>
        (byte)Math.Clamp(Math.Round(value, MidpointRounding.ToEven), 0, 255);

    public void Point(int x, int y, string hex, double opacity = 1)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;
        var k = (y * Width + x) * 4;
        var r = Component(hex, 1);
        var g = Component(hex, 3);
        var b = Component(hex, 5);
        if (opacity >= 1)
        {
            Pixels[k] = r;
            Pixels[k + 1] = g;
            Pixels[k + 2] = b;
            Pixels[k + 3] = 255;
            return;
        }
        var oldA = Pixels[k + 3] / 255d;
        var a = opacity + oldA * (1 - opacity);
        if (a <= 0) return;
        var src = opacity / a;
        var dst = oldA * (1 - opacity) / a;
        Pixels[k] = Clamp(r * src + Pixels[k] * dst);
        Pixels[k + 1] = Clamp(g * src + Pixels[k + 1] * dst);
        Pixels[k + 2] = Clamp(b * src + Pixels[k + 2] * dst);
        Pixels[k + 3] = Clamp(a * 255);
    }

    public void Rect(int x, int y, int w, int h, string color)
    {
        var x0 = Math.Clamp((long)x, 0, Width);
        var x1 = Math.Clamp((long)x + w, 0, Width);
        var y0 = Math.Clamp((long)y, 0, Height);
        var y1 = Math.Clamp((long)y + h, 0, Height);
        for (var yy = y0; yy < y1; yy++)
            for (var xx = x0; xx < x1; xx++)
                Point((int)xx, (int)yy, color);
    }

    public void Disc(double cx, double cy, double rx, double ry,
        string color, double opacity = 1)
    {
        if (rx <= 0 || ry <= 0) return;
        for (var y = -(int)Math.Ceiling(ry); y <= Math.Ceiling(ry); y++)
        for (var x = -(int)Math.Ceiling(rx); x <= Math.Ceiling(rx); x++)
            if (x * x / (rx * rx) + y * y / (ry * ry) <= 1)
                Point((int)Math.Floor(cx + x + .5),
                    (int)Math.Floor(cy + y + .5), color, opacity);
    }
    public void Shadow(int x, int y, double rx, double ry) =>
        Disc(x, y, rx, ry, "#1b2415", .28);

    public void Polygon(double[] xy, string hex)
    {
        if (xy.Length < 6 || (xy.Length & 1) != 0)
            throw new ArgumentException("Polígono inválido.", nameof(xy));
        var count = xy.Length / 2;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var inside = false;
            for (var i = 0, j = count - 1; i < count; j = i++)
            {
                var yi = xy[2 * i + 1];
                var yj = xy[2 * j + 1];
                if ((yi > y + .5) != (yj > y + .5) &&
                    x + .5 < (xy[2 * j] - xy[2 * i]) *
                        (y + .5 - yi) / (yj - yi) + xy[2 * i])
                    inside = !inside;
            }
            if (inside) Point(x, y, hex);
        }
    }

    public static byte[] EncodePng(byte[] rgba, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0 ||
            (long)width * height * 4 != rgba.Length ||
            rgba.Length > 64 * 1024 * 1024)
            throw new ArgumentException("Dimensões RGBA inválidas.");

        using var output = new MemoryStream();
        output.Write([137,80,78,71,13,10,26,10]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(4,4), height);
        header[8] = 8; header[9] = 6;
        Chunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = checked(width * 4);
            for (var y = 0; y < height; y++)
            {
                zip.WriteByte(0);
                zip.Write(rgba, y * row, row);
            }
        }
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream target, string name, ReadOnlySpan<byte> content)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, content.Length);
        target.Write(buffer);
        Span<byte> label = stackalloc byte[4];
        Encoding.ASCII.GetBytes(name, label);
        target.Write(label);
        target.Write(content);
        var crc = 0xffffffffu;
        foreach (var b in label) crc = Update(crc, b);
        foreach (var b in content) crc = Update(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, ~crc);
        target.Write(buffer);
    }
    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (var i = 0; i < 8; i++)
            crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        return crc;
    }
}
