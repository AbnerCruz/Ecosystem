using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyWorldSpritesTests
{
    [Fact]
    public void AllEightTreeSpeciesHaveDeterministicVariantsAndSnowSprites()
    {
        Assert.Equal(
            new[] {"oak","birch","willow","pine","deadpine","acacia","palm","cactus"},
            LegacyWorldSprites.TreeKinds.ToArray());
        foreach (var kind in LegacyWorldSprites.TreeKinds)
        for (var variant = 0; variant < 3; variant++)
        for (var snow = 0; snow < 2; snow++)
        {
            var rgba = LegacyWorldSprites.CreateTree(kind, variant, snow != 0);
            Assert.Equal(24 * 32 * 4, rgba.Length);
            Assert.Contains(rgba.Where((_, i) => i % 4 == 3), x => x > 0);
            Assert.Equal(SHA256.HashData(rgba), SHA256.HashData(
                LegacyWorldSprites.CreateTree(kind, variant, snow != 0)));
        }
    }

    [Fact]
    public void BridgeMatchesLegacyPaletteAndPlankPositions()
    {
        var b = LegacyWorldSprites.CreateBridge();
        Assert.Equal(new byte[]{0xb0,0x8a,0x5c,255},At(b,16,0,0));
        Assert.Equal(new byte[]{0x5a,0x3d,0x25,255},At(b,16,10,1));
        Assert.Equal(new byte[]{0x6b,0x4a,0x2e,255},At(b,16,3,4));
        Assert.Equal(new byte[]{0x8a,0x64,0x40,255},At(b,16,4,4));
        Assert.Equal(new byte[]{0,0,0,0},At(b,16,0,15));
    }

    [Fact]
    public void TreeSpriteMaintainsReferenceTrunksAndCactusFlower()
    {
        var oak = LegacyWorldSprites.CreateTree("oak");
        Assert.Equal(new byte[]{0x6b,0x4a,0x2e,255},At(oak,24,11,25));
        Assert.Equal(new byte[]{0x4b,0x32,0x1f,255},At(oak,24,13,25));
        var cactus = LegacyWorldSprites.CreateTree("cactus");
        Assert.Equal(new byte[]{0xe7,0xcf,0x73,255},At(cactus,24,11,13));
        Assert.Equal(new byte[]{0x4a,0x73,0x3a,255},At(cactus,24,13,15));
    }

    [Fact]
    public void PngRoundTripsExactSpritePixelsWithoutAnyExternalLibrary()
    {
        var source = LegacyWorldSprites.CreateBridge();
        var png = LegacyWorldSprites.EncodePng(source,16,16);
        Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},png[..8]);
        Assert.Equal(16,BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4)));
        Assert.Equal(16,BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4)));
        Assert.Equal((byte)6,png[25]);
        var at = 8;
        byte[]? decoded = null;
        while (at < png.Length)
        {
            var size = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at,4));
            var type = System.Text.Encoding.ASCII.GetString(png,at+4,4);
            if (type == "IDAT")
            {
                using var zip = new ZLibStream(
                    new MemoryStream(png,at+8,size),CompressionMode.Decompress);
                using var output = new MemoryStream();
                zip.CopyTo(output);
                decoded = output.ToArray();
            }
            at += size + 12;
        }
        Assert.NotNull(decoded);
        var pixels = new byte[source.Length];
        for (var y = 0; y < 16; y++)
        {
            Assert.Equal((byte)0,decoded[y*65]);
            Array.Copy(decoded,y*65+1,pixels,y*64,64);
        }
        Assert.Equal(source,pixels);
    }

    [Fact]
    public void InvalidInputsDoNotProduceSilentPlaceholdersOrExcessMemory()
    {
        Assert.Throws<ArgumentException>(() => LegacyWorldSprites.CreateTree("fictional"));
        Assert.Throws<ArgumentNullException>(() => LegacyWorldSprites.CreateTree(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyWorldSprites.CreateTree("oak",-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyWorldSprites.CreateTree("oak",3));
        Assert.Throws<ArgumentException>(() => LegacyWorldSprites.EncodePng(new byte[7],16,16));
        Assert.Throws<ArgumentException>(() => LegacyWorldSprites.EncodePng(
            new byte[4],int.MaxValue,int.MaxValue));
    }

    private static byte[] At(byte[] pixels,int width,int x,int y) =>
        pixels.AsSpan((y*width+x)*4,4).ToArray();
}
