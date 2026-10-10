using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyTileChunksTests
{
    [Theory]
    [InlineData(-17, -2)]
    [InlineData(-16, -1)]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(15, 0)]
    [InlineData(16, 1)]
    [InlineData(31, 1)]
    public void CacheChunkCoordinatesUseOriginalJavaScriptFloor(int tile, int chunk) =>
        Assert.Equal(chunk, LegacyTileChunks.ChunkCoordinate(tile));

    [Fact]
    public void SamplingChunkUsesExactlyTheSameOriginalBiomeAsDirectSampler()
    {
        var source = Create();
        var chunks = new LegacyTileChunks(source, capacity: 3);
        foreach (var (x, y) in new[]
            { (-33, -18), (-16, -1), (-1, 0), (0, 0), (10, 10), (36, 25) })
            Assert.Equal(source.Sample(x, y), chunks.At(x, y));
    }

    [Fact]
    public void OnlyVisibleChunksAreMaterializedAndCached()
    {
        var source = Create();
        var chunks = new LegacyTileChunks(source, capacity: 2);
        var zero = chunks.Get(0, 0);
        Assert.Equal(256, zero.Count);
        Assert.Same(zero, chunks.Get(0, 0));
        Assert.Equal(1L, chunks.GeneratedChunks);
        chunks.Get(1, 0);
        Assert.Equal(2, chunks.CachedCount);
        chunks.Get(2, 0);
        Assert.Equal(2, chunks.CachedCount);
        Assert.Equal(3L, chunks.GeneratedChunks);
        // The oldest inserted key was evicted; terrain.js uses FIFO.
        chunks.Get(0, 0);
        Assert.Equal(4L, chunks.GeneratedChunks);
        chunks.Clear();
        Assert.Equal(0, chunks.CachedCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LegacyTileChunks(source, capacity: 0));
    }

    private static LegacyTileSampler Create()
    {
        var climate = LegacyElevationClimateField.Generate("urbe", 64);
        var hydrology = LegacyHydrologyField.Generate(climate);
        return new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology));
    }
}
