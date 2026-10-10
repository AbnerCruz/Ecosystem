using Lunet.Runtime.Profiling;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class TextureAllocationTrackerTests
{
    [Fact]
    public void Rgba8TracksTexturesAndTargetsExactlyOnce()
    {
        var t = new TextureAllocationTracker();
        t.Track(10, 64, 32);
        t.Track(11, 128, 64);
        Assert.Equal(40960, t.LiveBytes);
        Assert.Equal(2, t.LiveCount);
        Assert.Equal(40960, t.PeakBytes);
        Assert.True(t.Untrack(10));
        Assert.False(t.Untrack(10));
        Assert.False(t.Untrack(999));
        t.Track(10, 16, 16);
        Assert.Equal(33792, t.LiveBytes);
        Assert.True(t.Untrack(11));
        Assert.True(t.Untrack(10));
        Assert.Equal(0, t.LiveBytes);
        Assert.Equal(40960, t.PeakBytes);
        t.Reset();
        Assert.Equal(0, t.PeakBytes);
        Assert.Equal(0, t.LiveCount);
    }

    [Fact]
    public void InvalidHandlesOrRepeatedTrackCannotCorruptCounts()
    {
        var t = new TextureAllocationTracker();
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Track(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Track(1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Track(1, 1, 0));
        t.Track(1, 1, 1);
        Assert.Throws<InvalidOperationException>(() => t.Track(1, 2, 2));
        Assert.Equal(4, t.LiveBytes);
        Assert.Equal(1, t.LiveCount);
    }

    [Fact]
    public void LargeStorageAndPerFrameReadAreSafe()
    {
        var t = new TextureAllocationTracker();
        t.Track(1, 32768, 32768);
        Assert.Equal(4294967296L, t.LiveBytes);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++)
        {
            _ = t.LiveBytes;
            _ = t.PeakBytes;
            _ = t.LiveCount;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        t.Untrack(1);
        Assert.Equal(0, t.LiveBytes);
    }
}
