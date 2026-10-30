using SkiaSharp;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-081 / DEC-0059: a big or sideways photo is sent upright and reduced, never refused.</summary>
public sealed class AttachmentImagesTests
{
    /// <summary>40×20: the left half red with its top-left quarter green, the right half blue; with an EXIF orientation.</summary>
    private static byte[] Photo(int orientation, int width = 40, int height = 20)
    {
        using var bitmap = new SKBitmap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bitmap.SetPixel(x, y, x >= width / 2 ? SKColors.Blue : y < height / 2 ? SKColors.Lime : SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100).ToArray();
        if (orientation == 1) return jpeg;
        // APP1 Exif with one tag, 0x0112 Orientation, little-endian TIFF
        byte[] tiff = [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x01, 0x00, 0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, (byte)orientation, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        var exif = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        int length = exif.Length + 2;
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. exif, .. jpeg.Skip(2)];
    }

    private static char At(byte[] jpeg, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(jpeg);
        var c = bitmap.GetPixel(x, y);
        return c.Green > 160 && c.Red < 120 ? 'g' : c.Red > 160 ? 'r' : c.Blue > 160 ? 'b' : '?';
    }

    [Theory]
    // orientation, width × height upright, then the colour at four points (x, y)
    [InlineData(3, 40, 20, "35,15:g 5,5:b 35,5:r")]
    [InlineData(5, 20, 40, "5,5:g 15,5:r 5,35:b")]
    [InlineData(6, 20, 40, "15,5:g 5,5:r 5,35:b")]
    [InlineData(7, 20, 40, "15,35:g 5,35:r 5,5:b")]
    [InlineData(8, 20, 40, "5,35:g 15,35:r 5,5:b")]
    public void ASidewaysPhotoIsSentUpright(int orientation, int width, int height, string expected)
    {
        var fitted = AttachmentImages.Fit(Photo(orientation), "image/jpeg");
        Assert.NotNull(fitted);
        using var bitmap = SKBitmap.Decode(fitted.Value.Bytes);
        Assert.Equal((width, height), (bitmap.Width, bitmap.Height));
        foreach (var point in expected.Split(' '))
        {
            var (xy, colour) = (point.Split(':')[0].Split(','), point.Split(':')[1][0]);
            Assert.Equal(colour, At(fitted.Value.Bytes, int.Parse(xy[0]), int.Parse(xy[1])));
        }
    }

    [Fact]
    public void ABigPhotoIsReducedAndASmallOneGoesAsItIs()
    {
        var small = Photo(1);
        var same = AttachmentImages.Fit(small, "image/jpeg");
        Assert.False(same!.Value.Reduced);
        Assert.Same(small, same.Value.Bytes);

        var big = AttachmentImages.Fit(Photo(1, 4000, 3000), "image/jpeg");
        Assert.True(big!.Value.Reduced);
        Assert.Equal("image/jpeg", big.Value.Mime);
        Assert.True(big.Value.Bytes.Length <= AttachmentImages.MaxBytes);
        using var bitmap = SKBitmap.Decode(big.Value.Bytes);
        Assert.Equal((2000, 1500), (bitmap.Width, bitmap.Height));

        Assert.Null(AttachmentImages.Fit([1, 2, 3], "image/png"));
    }
}
