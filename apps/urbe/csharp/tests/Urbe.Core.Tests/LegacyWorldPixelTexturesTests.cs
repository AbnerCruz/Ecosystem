using System.Security.Cryptography;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyWorldPixelTexturesTests
{
    // Oracle: Node 22 executing the makeTexture() algorithm from
    // apps/urbe/src/world/pixel-art.js in the original biome/variant order.
    // The full corpus is 19 biomes × 4 variants × 16 × 16 × 4 RGBA bytes.
    private const string CompleteLegacySha256 =
        "84d2e9695bba78e69f1a60b17bba8d5fee8c56c5efc8a4d71877f5771a820d05";

    [Fact]
    public void AllSeventySixTilesArePixelIdenticalToLegacyVersion()
    {
        Assert.Equal(19, LegacyWorldPixelTextures.Biomes.Count);
        using var pixels = new MemoryStream();

        foreach (var biome in LegacyWorldPixelTextures.Biomes)
        for (var variant = 0; variant < LegacyWorldPixelTextures.VariantCount; variant++)
        {
            var tile = LegacyWorldPixelTextures.CreateTile(biome, variant);
            Assert.Equal(16 * 16 * 4, tile.Length);
            pixels.Write(tile);
            for (var alpha = 3; alpha < tile.Length; alpha += 4)
                Assert.Equal((byte)255, tile[alpha]);
        }

        Assert.Equal(
            CompleteLegacySha256,
            Convert.ToHexString(SHA256.HashData(pixels.ToArray())).ToLowerInvariant());
    }

    [Theory]
    [InlineData("deep", 0, "ff816499d9f2c3ad0dc102ed3ad695e199ee7fbfd8f9a05764620d880c424d2d")]
    [InlineData("river", 1, "09b235c2f5cf8510a640a2023d701e028990acc20ed9eca2d10994b510eb278a")]
    [InlineData("grass", 3, "39f6589f1da89a2b6b6af75833f8988b128a91e4ae62df1ba3a9cb823376c383")]
    [InlineData("snow", 2, "f5846fde597e54e1f2c6462b9a4d353ca210c6d74fb0298ad479d060fa805d3a")]
    [InlineData("peak", 3, "432b0fc9336672d78d059c42fcf5c38b2d9cb3825810b4b26a08ffe60c5ac395")]
    public void IndividualTileMatchesOriginalJavascript(string biome, int variant, string expected)
    {
        var actual = LegacyWorldPixelTextures.CreateTile(biome, variant);
        Assert.Equal(
            expected,
            Convert.ToHexString(SHA256.HashData(actual)).ToLowerInvariant());
    }

    [Fact]
    public void DoesNotLeakMutablePixelBuffersOrChangeAcrossCalls()
    {
        var first = LegacyWorldPixelTextures.CreateTile("forest", 2);
        var expected = SHA256.HashData(first);
        first[0] ^= 0xff;
        var second = LegacyWorldPixelTextures.CreateTile("forest", 2);

        Assert.True(expected.SequenceEqual(SHA256.HashData(second)));
        Assert.False(first.SequenceEqual(second));
    }

    [Fact]
    public void UnknownBiomeAndVariantCannotSilentlyChangePalette()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LegacyWorldPixelTextures.CreateTile(null!, 0));
        Assert.Throws<ArgumentException>(() =>
            LegacyWorldPixelTextures.CreateTile("unknown", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyWorldPixelTextures.CreateTile("grass", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyWorldPixelTextures.CreateTile("grass", 4));
    }
}
