using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyChunkViewportTests
{
    [Fact]
    public void VisibleCameraLoadsOnlyIntersectingChunksInStableRowOrder()
    {
        var chunks = LegacyChunkViewport.Visible(32, 24, 24, 20);
        Assert.Equal(4, chunks.Count);
        Assert.Equal(new LegacyChunkViewport.Chunk(2, 1, 0, -8), chunks[0]);
        Assert.Equal(new LegacyChunkViewport.Chunk(3, 1, 16, -8), chunks[1]);
        Assert.Equal(new LegacyChunkViewport.Chunk(2, 2, 0, 8), chunks[2]);
        Assert.Equal(new LegacyChunkViewport.Chunk(3, 2, 16, 8), chunks[3]);
    }

    [Fact]
    public void NegativePanPreservesJsFloorSemantics()
    {
        var chunks = LegacyChunkViewport.Visible(-1, -17, 17, 18);
        Assert.Equal(6, chunks.Count);
        Assert.Equal(new LegacyChunkViewport.Chunk(-1, -2, -15, -15), chunks[0]);
        Assert.Equal(new LegacyChunkViewport.Chunk(0, -2, 1, -15), chunks[1]);
        Assert.Equal(new LegacyChunkViewport.Chunk(-1, 0, -15, 17), chunks[4]);
        Assert.Equal(new LegacyChunkViewport.Chunk(0, 0, 1, 17), chunks[5]);
    }

    [Fact]
    public void ExactChunkBoundaryNeverRendersUnseenNeighbor()
    {
        var one = LegacyChunkViewport.Visible(16, 32, 16, 16);
        Assert.Single(one);
        Assert.Equal(new LegacyChunkViewport.Chunk(1, 2, 0, 0), one[0]);
    }

    [Fact]
    public void CameraSizeAndWorldOverflowAreBounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LegacyChunkViewport.Visible(0, 0, 0, 12));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LegacyChunkViewport.Visible(0, 0, 24, 49));
        Assert.Throws<OverflowException>(
            () => LegacyChunkViewport.Visible(int.MaxValue, 0, 2, 2));
    }
}
