using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyWorldGroundDecorTests
{
    // Independent oracle: the original 1.8.4-beta paintDecor function executed
    // in JS with the untouched grass texture, in this order of coordinates:
    // (0,0,slot=0), (37,-12,slot=1), (-23,88,slot=2).
    // Each hash is FNV-1a32 over the complete 16x16 RGBA tile.
    private static readonly (string Kind, string A, string B, string C)[] Golden =
    [
        ("flowers", "7706f2e7", "f52df4fe", "d1d93f42"),
        ("rock", "4e98b93f", "17985737", "52fb2a31"),
        ("boulder", "5184cd0b", "5184cd0b", "5184cd0b"),
        ("pebbles", "96cea610", "82dc4bf8", "d0f708dc"),
        ("bush", "36b1ad4a", "b8b60dd9", "3911138f"),
        ("tallgrass", "2ef8ba86", "8608a7aa", "4171cf23"),
        ("drygrass", "363c16aa", "6b99cd15", "ddbc7890"),
        ("fern", "c63257a4", "1bfed539", "4e3d3304"),
        ("mushroom", "9578c0d1", "48552e42", "da043282"),
        ("log", "f8570f89", "86049e75", "2dac71eb"),
        ("stump", "d93a8445", "6a52ed29", "7ff8b343"),
        ("reeds", "40835745", "8fce0c74", "0277a368"),
        ("puddle", "1e8b2c2a", "1e8b2c2a", "1e8b2c2a"),
        ("lily", "022333e2", "5e640c2e", "2f6d71dc"),
        ("shorerock", "0a95521b", "0a95521b", "0a95521b"),
        ("drybush", "4c12e48d", "7a8e73d1", "2717e054"),
        ("driftwood", "d99cf655", "d99cf655", "c2f3b3a1"),
        ("snowpatch", "177c409b", "cea51af7", "2b914738"),
        ("tuft", "821f9031", "68d084af", "30eaf594"),
        ("shell", "7e623cef", "7e623cef", "7e623cef")
    ];

    [Fact]
    public void EveryLegacyGroundDecorationMatchesOriginalPixelsForThreeSeeds()
    {
        Assert.Equal(20, LegacyWorldGroundDecor.Kinds.Count);
        Assert.Equal(
            Golden.Select(x => x.Kind).ToArray(),
            LegacyWorldGroundDecor.Kinds.ToArray());

        foreach (var sample in Golden)
        {
            AssertDecor(sample.Kind, 0, 0, 0, sample.A);
            AssertDecor(sample.Kind, 37, -12, 1, sample.B);
            AssertDecor(sample.Kind, -23, 88, 2, sample.C);
        }
    }

    [Fact]
    public void DecorationsKeepCanvasOffsetsAndTileBoundaries()
    {
        var background = LegacyWorldPixelTextures.CreateTile("grass", 0);
        var pixels = new byte[32 * 32 * 4];
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 32; x++)
        {
            var origin = ((y % 16) * 16 + x % 16) * 4;
            var offset = (y * 32 + x) * 4;
            Array.Copy(background, origin, pixels, offset, 4);
        }

        LegacyWorldGroundDecor.Paint(pixels, 32, 0, 0, "flowers", 0, 0);
        LegacyWorldGroundDecor.Paint(pixels, 32, 16, 16, "mushroom", 37, -12, 1);
        Assert.Equal("12e1961c", Fnv(pixels));
    }

    [Fact]
    public void RejectsUnsupportedOrMalformedInputWithoutMutation()
    {
        var pixels = LegacyWorldPixelTextures.CreateTile("grass", 0);
        var unchanged = pixels.ToArray();
        Assert.Throws<ArgumentNullException>(() =>
            LegacyWorldGroundDecor.Paint(null!, 16, 0, 0, "rock", 0, 0));
        Assert.Throws<ArgumentNullException>(() =>
            LegacyWorldGroundDecor.Paint(pixels, 16, 0, 0, null!, 0, 0));
        Assert.Throws<ArgumentException>(() =>
            LegacyWorldGroundDecor.Paint(pixels, 16, 0, 0, "not-a-decor", 0, 0));
        Assert.Throws<ArgumentException>(() =>
            LegacyWorldGroundDecor.Paint(pixels, 17, 0, 0, "rock", 0, 0));
        Assert.Equal(unchanged, pixels);
    }

    private static void AssertDecor(
        string kind, int worldX, int worldY, int slot, string expected)
    {
        var pixels = LegacyWorldPixelTextures.CreateTile("grass", 0);
        LegacyWorldGroundDecor.Paint(
            pixels, 16, 0, 0, kind, worldX, worldY, slot);
        Assert.True(
            expected == Fnv(pixels),
            $"{kind} ({worldX},{worldY}) slot {slot}: expected {expected}, got {Fnv(pixels)}");
    }

    private static string Fnv(byte[] buffer)
    {
        var hash = 2166136261u;
        foreach (var value in buffer)
            hash = unchecked((hash ^ value) * 16777619u);
        return hash.ToString("x8");
    }
}
