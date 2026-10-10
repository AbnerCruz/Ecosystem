using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Reference values were sampled from Urbe 1.8.4-beta terrain.js, not from
/// the C# port. The original deterministic generator is the visual contract.
/// </summary>
public sealed class LegacyTerrainParityTests
{
    [Theory]
    [InlineData("urbe", 4001768043u)]
    [InlineData("Cidade Alpha", 2189909641u)]
    [InlineData("Urbe 1.8.4-beta", 291136209u)]
    public void Hash32MatchesOriginalUtf16Seed(string seed, uint expected) =>
        Assert.Equal(expected, LegacyTerrainMath.Hash32(seed));

    [Theory]
    [InlineData("urbe", 0.6959578993264586, 0.9021825399249792)]
    [InlineData("Cidade Alpha", 0.6483555135782808, 0.5452466604765505)]
    public void Mulberry32ProducesSameFirstTwoSamples(
        string seed, double first, double second)
    {
        var rng = new LegacyTerrainMath.Mulberry32(LegacyTerrainMath.Hash32(seed));
        AssertClose(first, rng.Next(), 0);
        AssertClose(second, rng.Next(), 0);
    }

    [Theory]
    [InlineData("urbe", 3.25, -2.75, 0.34833869508705817, 0.21602891079215095)]
    [InlineData("urbe", 100.125, 5.875, 0.15413395008927194, 0.00046146583015197845)]
    [InlineData("urbe", -9.4, 102.3, -0.09488998097852147, -0.17061783673658978)]
    [InlineData("urbe", 0.125, 0.625, 0.10288519028122878, 0.0307552549017189)]
    [InlineData("Cidade Alpha", 3.25, -2.75, 0.12380179946446646, 0.10062348145815653)]
    [InlineData("Cidade Alpha", -20.5, -14.75, -0.3856776799184445, -0.21684644595370628)]
    public void GradientAndFractalNoiseMatchLegacySample(
        string seed, double x, double y, double expectedNoise, double expectedFbm)
    {
        var noise = new LegacyTerrainMath.GradientNoise(
            LegacyTerrainMath.Hash32(seed));
        AssertClose(expectedNoise, noise.Sample(x, y), 1e-6);
        AssertClose(expectedFbm,
            LegacyTerrainMath.Fbm(noise, x, y, 4, 2, .5), 1e-6);
    }

    [Theory]
    [InlineData(0, 0, 1, 0.4915040940977633)]
    [InlineData(-10, 8, 3, 0.689028472173959)]
    [InlineData(100, 200, 12, 0.6636086625512689)]
    public void TileHashMatchesOriginalImul(
        int x, int y, int salt, double expected) =>
        AssertClose(expected, LegacyTerrainMath.TileHash(x, y, salt), 0);

    [Theory]
    [InlineData(.1, .5, .5, false, false, LegacyBiome.Deep)]
    [InlineData(.39, .5, .5, false, false, LegacyBiome.Sea)]
    [InlineData(.5, .5, .5, true, true, LegacyBiome.Lake)]
    [InlineData(.5, .5, .5, false, true, LegacyBiome.River)]
    [InlineData(.422, .6, .6, false, false, LegacyBiome.Beach)]
    [InlineData(.65, .2, .6, false, false, LegacyBiome.Taiga)]
    [InlineData(.63, .85, .12, false, false, LegacyBiome.Desert)]
    [InlineData(.6, .85, .4, false, false, LegacyBiome.Savanna)]
    [InlineData(.6, .84, .94, false, false, LegacyBiome.Dense)]
    [InlineData(.75, .7, .5, false, false, LegacyBiome.Hills)]
    [InlineData(.85, .4, .5, false, false, LegacyBiome.Mountain)]
    [InlineData(.94, .25, .5, false, false, LegacyBiome.Snow)]
    public void BiomeClassifierUsesUnchangedOriginalThresholds(
        double elevation, double temperature, double moisture,
        bool lake, bool river, LegacyBiome expected)
    {
        Assert.Equal(expected,
            LegacyBiomeRules.Classify(elevation, temperature, moisture, lake, river));
    }

    [Fact]
    public void BiomeIdentifiersRemainExactOriginalTextureNames()
    {
        string[] originalIds =
        [
            "deep", "sea", "river", "lake", "beach", "grass",
            "meadow", "forest", "dense", "swamp", "taiga",
            "tundra", "snow", "hills", "mountain", "peak",
            "desert", "savanna", "steppe"
        ];
        for (int i = 0; i < originalIds.Length; i++)
            Assert.Equal(originalIds[i], LegacyBiomeRules.Id((LegacyBiome)i));

        Assert.False(LegacyBiomeRules.Buildable(LegacyBiome.River));
        Assert.True(LegacyBiomeRules.Roadable(LegacyBiome.River));
        Assert.False(LegacyBiomeRules.Roadable(LegacyBiome.Peak));
        Assert.True(LegacyBiomeRules.Buildable(LegacyBiome.Beach));
    }

    [Theory]
    [InlineData("urbe", 17, 4075713025u, 824, 24)]
    [InlineData("Cidade Alpha", 19, 1976179121u, 773, 23)]
    public void VoronoiPlateMapAndBoundaryBfsMatchOriginal(
        string seed, int plateCount, uint mapHash, int boundaryCount, int maxDistance)
    {
        var field = LegacyTectonicField.Generate(seed, gridSize: 64);
        Assert.Equal(plateCount, field.Plates.Count);
        uint hash = 2166136261;
        int boundaries = 0, max = 0;
        unchecked
        {
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                hash = (hash ^ field.PlateIndexAt(x, y)) * 16777619;
                if (field.DistanceToBoundaryAt(x, y) == 0)
                    boundaries++;
                max = Math.Max(max, field.DistanceToBoundaryAt(x, y));
            }
        }
        Assert.Equal(mapHash, hash);
        Assert.Equal(boundaryCount, boundaries);
        Assert.Equal(maxDistance, max);
        Assert.Equal(field.PlateIndexAt(20, 20), field.PlateIndexAt(21, 21));
    }

    [Fact]
    public void OriginalUpliftAndNearestBoundaryDistancesArePreserved()
    {
        var field = LegacyTectonicField.Generate("Cidade Alpha", gridSize: 64);
        Assert.Equal((byte)4, field.PlateIndexAt(20, 30));
        Assert.Equal((ushort)0, field.DistanceToBoundaryAt(20, 30));
        AssertClose(0.07955308258533478, field.UpliftAt(20, 30), 1e-6);
        AssertClose(0.07955308258533478, field.NearestBoundaryUpliftAt(20, 30), 1e-6);
        Assert.Equal((byte)11, field.PlateIndexAt(63, 63));
        Assert.Equal((ushort)13, field.DistanceToBoundaryAt(63, 63));
        AssertClose(-0.1502608209848404,
            field.NearestBoundaryUpliftAt(63, 63), 1e-6);
        Assert.Equal((byte)4, field.PlateIndexAt(0, 0));
        Assert.Equal((ushort)3, field.DistanceToBoundaryAt(0, 0));
    }

    [Fact]
    public void SeedOrGridNeverChangesTheStoredVault()
    {
        var before = LegacyTectonicField.Generate("urbe", gridSize: 64);
        var again = LegacyTectonicField.Generate("urbe", gridSize: 64);
        Assert.Equal(before.PlateIndexAt(10, 10), again.PlateIndexAt(10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyTectonicField.Generate("urbe", gridSize: 7));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyTectonicField.Generate("urbe", plateCount: 300));
    }

    private static void AssertClose(double expected, double actual, double tolerance) =>
        Assert.InRange(Math.Abs(expected - actual), 0, tolerance);
}
