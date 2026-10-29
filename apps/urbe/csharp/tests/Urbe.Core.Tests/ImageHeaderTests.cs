using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class ImageHeaderTests
{
    [Fact]
    public void TheWidthComesFromTheHeaderOfEachFormat()
    {
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0x1F, 0x40, 0, 0, 0x0F, 0xA0];
        Assert.Equal(8000, ImageHeader.Width(png));
        byte[] gif = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0x20, 0x03, 0x58, 0x02];
        Assert.Equal(800, ImageHeader.Width(gif));
        var bmp = new byte[26]; bmp[0] = (byte)'B'; bmp[1] = (byte)'M'; bmp[18] = 0x00; bmp[19] = 0x14;
        Assert.Equal(5120, ImageHeader.Width(bmp));
        // JPEG: SOI, an APP0 segment, then SOF0 with height 0x0BB8 (3000) and width 0x1194 (4500)
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 1, 2, 0xFF, 0xC0, 0, 17, 8, 0x0B, 0xB8, 0x11, 0x94, 3, 0, 0, 0, 0];
        Assert.Equal(4500, ImageHeader.Width(jpeg));
        Assert.Null(ImageHeader.Width([1, 2, 3]));
    }

    [Fact]
    public void AHeaderOfASmallPictureHasItsWidth()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 48, 0, 0, 0, 56];
        Assert.Equal(48, ImageHeader.Width(png));
    }
}
