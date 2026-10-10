using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Species, variants and ground decor from the actual v1.8.4-beta terrain.js.</summary>
public sealed class LegacyVegetationTests
{
    [Theory]
    [InlineData("urbe", -7, -20, "oak", 1, false)]
    [InlineData("urbe", 35, -20, "acacia", 1, false)]
    [InlineData("urbe", 49, -19, "acacia", 0, false)]
    [InlineData("urbe", -6, -18, "birch", 1, false)]
    [InlineData("urbe", 3, -18, "acacia", 2, false)]
    [InlineData("Cidade Alpha", 49, -17, "cactus", 2, false)]
    [InlineData("Cidade Alpha", 53, -14, "cactus", 1, false)]
    [InlineData("Cidade Alpha", 14, -12, "acacia", 0, false)]
    [InlineData("Cidade Alpha", 7, -1, "birch", 1, false)]
    public void TreesKeepOriginalSpeciesVariantAndSnow(
        string seed, int x, int y, string kind, int variant, bool snow)
    {
        var tree = Create(seed).TreeAt(x, y);
        Assert.NotNull(tree);
        Assert.Equal(kind, tree.Kind);
        Assert.Equal(variant, tree.Variant);
        Assert.Equal(snow, tree.Snow);
    }

    [Theory]
    [InlineData("urbe", -5, -20, "fern,bush")]
    [InlineData("urbe", -2, -20, "drygrass,rock")]
    [InlineData("urbe", 4, -20, "drygrass")]
    [InlineData("urbe", 17, -20, "drybush")]
    [InlineData("Cidade Alpha", 17, -20, "driftwood")]
    [InlineData("Cidade Alpha", 24, -20, "drybush")]
    [InlineData("Cidade Alpha", 26, -20, "rock")]
    [InlineData("Cidade Alpha", 42, -20, "pebbles")]
    [InlineData("Cidade Alpha", 20, -20, "drygrass")]
    public void GroundDetailsKeepOriginalNamesAndOrdering(
        string seed, int x, int y, string expected)
    {
        var decor = Create(seed).DecorAt(x, y);
        Assert.Equal(expected, string.Join(",", decor));
        Assert.InRange(decor.Count, 0, 2);
    }

    [Theory]
    [InlineData("urbe")]
    [InlineData("Cidade Alpha")]
    public void VegetationIsStableAndNeverMutatesVault(string seed)
    {
        var first = Create(seed);
        var second = Create(seed);
        for (var x = -4; x < 20; x += 5)
        for (var y = -4; y < 20; y += 5)
        {
            Assert.Equal(first.TreeAt(x, y), second.TreeAt(x, y));
            Assert.Equal(first.DecorAt(x, y), second.DecorAt(x, y));
        }
    }

    private static LegacyVegetation Create(string seed)
    {
        var climate = LegacyElevationClimateField.Generate(seed, 64);
        var hydrology = LegacyHydrologyField.Generate(climate);
        var spawn = LegacyWorldSpawn.Choose(hydrology);
        return new LegacyVegetation(new LegacyTileSampler(hydrology, spawn));
    }
}
