using System.Buffers.Binary;
using System.IO.Compression;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class WorldArtProceduralRenderTests
{
    [Fact]
    public void FourGroundTilesAreGeneratedFromTheOriginalCSharpAlgorithm()
    {
        for (var i = 0; i < 4; i++)
        {
            var uri = WorldArt.GroundImageUrl(i);
            Assert.StartsWith("data:image/png;base64,", uri);
            Assert.DoesNotContain("world/v184/", uri);
            Assert.Equal(uri, WorldArt.GroundImageUrl(i));
            Assert.Equal(
                LegacyWorldPixelTextures.CreateTile("grass", i),
                Rgba(uri, 16, 16));
        }
    }

    [Fact]
    public void TreesFlowersAndBuildingsAreGeneratedAndCanBeDecodedOffline()
    {
        var tree = ExtractUrl(WorldArt.DecorationBackgroundStyle(0, 0));
        Assert.Equal(LegacyWorldSprites.CreateTree("oak",
            (int)(WorldArt.LegacySeed("0/0") % 3)), Rgba(tree, 24, 32));

        var flowers = ExtractUrl(WorldArt.DecorationBackgroundStyle(0, 3));
        var petals = new byte[16 * 16 * 4];
        LegacyWorldGroundDecor.Paint(petals, 16, 0, 0, "flowers", 0, 3);
        Assert.Equal(petals, Rgba(flowers, 16, 16));

        foreach (var kind in new[] { "house", "hall" })
        for (var i = 0; i < (kind == "house" ? 3 : 1); i++)
        {
            var uri = WorldArt.BuildingImageUrl("Sala", kind == "hall",
                "house" + (i + 1));
            Assert.Equal(
                LegacyWorldBuildings.CreateBuilding(kind, "temperate", i),
                Rgba(uri, 48, 56));
        }
    }

    [Fact]
    public void CacheIsStableAndTheCityHasNoStaticAssetDependency()
    {
        Assert.Equal("background-image:none;",
            WorldArt.DecorationBackgroundStyle(1, 2));
        Assert.StartsWith("background-image:url('data:image/png;base64,",
            WorldArt.BoardBackgroundStyle);
        var styles = new[]
        {
            WorldArt.BuildingBackgroundStyle("Minhas notas.md", false, "house2"),
            WorldArt.DecorationBackgroundStyle(0, 3),
            WorldArt.TerrainBackgroundStyle(4, 2)
        };
        foreach (var s in styles)
        {
            Assert.DoesNotContain("world/v184/", s);
            Assert.StartsWith("background-image:url('data:image/png;base64,", s);
        }
        Assert.Equal(
            WorldArt.TerrainBackgroundStyle(4, 2),
            WorldArt.TerrainBackgroundStyle(4, 2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldArt.GroundImageUrl(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldArt.GroundImageUrl(4));
    }

    private static string ExtractUrl(string css)
    {
        const string prefix = "background-image:url('";
        Assert.StartsWith(prefix, css);
        return css[prefix.Length..css.LastIndexOf("'", StringComparison.Ordinal)];
    }

    private static byte[] Rgba(string url, int width, int height)
    {
        const string prefix = "data:image/png;base64,";
        Assert.StartsWith(prefix, url);
        var png = Convert.FromBase64String(url[prefix.Length..]);
        Assert.Equal(new byte[] {137,80,78,71,13,10,26,10},png[..8]);
        Assert.Equal(width,BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4)));
        Assert.Equal(height,BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4)));
        Assert.Equal((byte)6,png[25]);
        using var raw = new MemoryStream();
        var offset = 8;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset,4));
            var name = System.Text.Encoding.ASCII.GetString(png,offset+4,4);
            if (name == "IDAT")
            {
                using var data = new MemoryStream(png,offset+8,length);
                using var zip = new ZLibStream(data,CompressionMode.Decompress);
                zip.CopyTo(raw);
            }
            offset += length+12;
        }
        var bytes = raw.ToArray();
        var output = new byte[width*height*4];
        for (var row = 0; row < height; row++)
        {
            Assert.Equal((byte)0,bytes[row*(1+width*4)]);
            Array.Copy(bytes,row*(1+width*4)+1,
                output,row*width*4,width*4);
        }
        return output;
    }
}
