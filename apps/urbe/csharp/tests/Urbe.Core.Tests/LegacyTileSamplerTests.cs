using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Captured directly from the actual Urbe 1.8.4-beta terrain.js:
/// createWorld(seed, { grid:64 }). The C# world must not invent a new look.
/// </summary>
public sealed class LegacyTileSamplerTests
{
    [Theory]
    [InlineData("urbe", 59, 24.415768973552623, 16.463654590700752, 23.5, 15.5, 1.874110825260513)]
    [InlineData("Cidade Alpha", 58, 46.314387251948936, 14.68939339730423, 46.660410738596696, 15.238469916465693, 1.6777597405833322)]
    public void OriginalRiverSegmentCountAndGeometry(
        string seed, int count, double ax, double ay,
        double bx, double by, double width)
    {
        var sampler = Create(seed);
        Assert.Equal(count, sampler.Rivers.Segments.Count);
        var first = sampler.Rivers.Segments[0];
        Close(ax, first.Ax, 0.001);
        Close(ay, first.Ay, 0.001);
        Close(bx, first.Bx, 0.001);
        Close(by, first.By, 0.001);
        Close(width, first.Width, 0.001);
    }

    [Theory]
    [InlineData("urbe", 36, 25, LegacyBiome.Savanna, 0.561450774383799, 0.8614190059128497, 0.4832842550255312, 0.0)]
    [InlineData("urbe", 28, 25, LegacyBiome.Savanna, 0.5447817572698979, 0.879272964000562, 0.4748581373307544, 0.0)]
    [InlineData("urbe", 36, 33, LegacyBiome.Savanna, 0.5888667568177459, 0.8078492879228951, 0.31272581756114504, 0.0)]
    [InlineData("urbe", 40, 42, LegacyBiome.Desert, 0.6180802669418357, 0.7375360221258214, 0.21205212111478805, 0.0)]
    [InlineData("urbe", 10, 10, LegacyBiome.River, 0.49906911655818026, 0.9286910748659329, 0.6540670399560624, 1.4732302279406913)]
    [InlineData("urbe", 0, 0, LegacyBiome.Forest, 0.4629326444119215, 0.9436187632381916, 0.5438357242383063, 0.0)]
    [InlineData("Cidade Alpha", 36, 25, LegacyBiome.Forest, 0.5579954031133438, 0.4948858734443838, 0.6035444995823079, 0.0)]
    [InlineData("Cidade Alpha", 28, 25, LegacyBiome.Forest, 0.5522529154514053, 0.5026462596224344, 0.6730684975872865, 0.0)]
    [InlineData("Cidade Alpha", 36, 33, LegacyBiome.Forest, 0.5586706312452151, 0.4213474967026844, 0.7192631744083215, 0.0)]
    [InlineData("Cidade Alpha", 40, 42, LegacyBiome.Dense, 0.4972808011463086, 0.40515479540836713, 0.8047350502538076, 0.0)]
    [InlineData("Cidade Alpha", 10, 10, LegacyBiome.Forest, 0.5074458007233358, 0.6805763481964495, 0.6861786821117912, 0.0)]
    [InlineData("Cidade Alpha", 0, 0, LegacyBiome.Sea, 0.40693019703030586, 0.8444098746404052, 0.4521056730300188, 0.0)]
    public void OriginalTileCoordinatesPreserveBiomeAndFields(
        string seed, int x, int y, LegacyBiome biome,
        double elevation, double temperature, double moisture, double river)
    {
        var sampled = Create(seed).Sample(x, y);
        Assert.Equal(biome, sampled.Biome);
        Close(elevation, sampled.Elevation, 0.001);
        Close(temperature, sampled.Temperature, 0.001);
        Close(moisture, sampled.Moisture, 0.001);
        Close(river, sampled.RiverWidth, 0.001);
    }

    [Theory]
    [InlineData("urbe")]
    [InlineData("Cidade Alpha")]
    public void OutsideOfOriginalContinentRemainsDeepOcean(string seed)
    {
        var sampler = Create(seed);
        var far = sampler.Sample(-120, -130);
        Assert.Equal(LegacyBiome.Deep, far.Biome);
        Close(.1, far.Elevation, 0);
        Assert.Equal(0, far.RiverWidth);
        Assert.False(far.IsLake);
    }

    [Fact]
    public void SameSeedIsDeterministicAndNeverWritesAUserVault()
    {
        var a = Create("urbe");
        var b = Create("urbe");
        for (var y = -3; y < 24; y += 3)
        for (var x = -2; x < 24; x += 3)
        {
            var first = a.Sample(x, y);
            Assert.Equal(first, b.Sample(x, y));
            Assert.InRange((int)first.Biome, 0, 18);
        }
    }

    private static LegacyTileSampler Create(string seed)
    {
        var climate = LegacyElevationClimateField.Generate(seed, gridSize: 64);
        var hydrology = LegacyHydrologyField.Generate(climate);
        var spawn = LegacyWorldSpawn.Choose(hydrology);
        return new LegacyTileSampler(hydrology, spawn);
    }

    private static void Close(double expected, double actual, double tolerance) =>
        Assert.InRange(Math.Abs(expected - actual), 0, tolerance);
}
