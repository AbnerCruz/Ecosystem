using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyChunkPixelsTests
{
    private static LegacyTileSampler.Tile Sample(
        LegacyBiome biome, double elevation = .53) =>
        new(biome, elevation, .55, .55, false, 0);

    private static IReadOnlyList<string> NoDetails(int x, int y) => [];

    [Fact]
    public void UniformDeepWaterPreservesOriginalTextureAndDepthShading()
    {
        var pngPixels = LegacyChunkPixels.Render(
            (_, _) => Sample(LegacyBiome.Deep, .1), NoDetails, 0, 0);
        Assert.Equal(256 * 256 * 4, pngPixels.Length);

        int variant = (int)Math.Floor(LegacyTerrainMath.TileHash(0, 0, 5) * 4);
        byte[] original = LegacyWorldPixelTextures.CreateTile("deep", variant);
        // Sea depth is clamped to one: 1 - .28 = .72.
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        for (int channel = 0; channel < 3; channel++)
        {
            int from = (y * 16 + x) * 4 + channel;
            int to = (y * 256 + x) * 4 + channel;
            byte expected = (byte)Math.Round(
                original[from] * .72, MidpointRounding.ToEven);
            Assert.Equal(expected, pngPixels[to]);
        }

        for (int offset = 3; offset < pngPixels.Length; offset += 4)
            Assert.Equal((byte)255, pngPixels[offset]);
    }

    [Fact]
    public void ChunkCoordinatesAreAbsoluteAndDeterministicIncludingNegativeTiles()
    {
        LegacyTileSampler.Tile World(int x, int y) =>
            Sample((x + 2 * y) % 3 == 0 ? LegacyBiome.Forest :
                (x - y) % 4 == 0 ? LegacyBiome.Hills : LegacyBiome.Grass,
                .55 + x / 3000d - y / 5000d);
        var first = LegacyChunkPixels.Render(World, NoDetails, -2, 1);
        var second = LegacyChunkPixels.Render(World, NoDetails, -2, 1);
        var adjacent = LegacyChunkPixels.Render(World, NoDetails, -1, 1);

        Assert.Equal(first, second);
        Assert.NotEqual(Convert.ToHexString(first), Convert.ToHexString(adjacent));
    }

    [Fact]
    public void MixedCoastAndOriginalDecorAreComposedWithoutAssets()
    {
        LegacyTileSampler.Tile Coast(int x, int y) =>
            Sample(x < 8 ? LegacyBiome.Sea : LegacyBiome.Meadow,
                x < 8 ? .34 : .55);
        var undecorated = LegacyChunkPixels.Render(Coast, NoDetails, 0, 0);
        IReadOnlyList<string> Decor(int x, int y) =>
            x == 9 && y == 7 ? ["flowers", "tallgrass"] : [];
        var decorated = LegacyChunkPixels.Render(Coast, Decor, 0, 0);

        Assert.Equal(undecorated.Length, decorated.Length);
        Assert.NotEqual(Convert.ToHexString(undecorated), Convert.ToHexString(decorated));
        // Decoration may not repaint the sea or tiles outside the selected square.
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16 * 8; x++)
        {
            int index = (y * 256 + x) * 4;
            Assert.Equal(undecorated[index], decorated[index]);
        }
        Assert.Equal((byte)255, decorated[3]);
    }

    [Fact]
    public void ValidatesInputsAndDoesNotRequireFilesystemOrWeb()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LegacyChunkPixels.Render(null!,
                (Func<int, int, IReadOnlyList<string>>)NoDetails, 0, 0));
        Assert.Throws<ArgumentNullException>(() =>
            LegacyChunkPixels.Render((_, _) => Sample(LegacyBiome.Grass),
                null!, 0, 0));
        Assert.Throws<OverflowException>(() =>
            LegacyChunkPixels.Render((_, _) => Sample(LegacyBiome.Grass),
                NoDetails, int.MaxValue, 0));
    }
}
