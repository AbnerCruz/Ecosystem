using Lunet.Runtime.Profiling;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class PreviewFrameStatisticsTests
{
    [Fact]
    public void RollingWindowReplacesOldSamplesAndKeepsRealDrawCounts()
    {
        var stats = new PreviewFrameStatistics(3);
        stats.Record(0.02, 5, 2, 20, 100);
        stats.Record(0.01, 2, 4, 40, 300);
        var initial = stats.Snapshot();
        Assert.Equal(2, initial.Samples);
        Assert.Equal(2 / .03, initial.FramesPerSecond, 5);
        Assert.Equal(3, initial.DrawCallsPerFrame);
        Assert.Equal(30, initial.TrianglesPerFrame);
        Assert.Equal(200, initial.AllocatedBytesPerFrame);
        Assert.Equal(0, initial.SlowFrames);

        stats.Record(0.05, 6, 3, 30, 200);
        stats.Record(0.01, 3, 1, 10, 400);
        var recent = stats.Snapshot();
        Assert.Equal(3, recent.Samples);
        Assert.Equal(3 / .07, recent.FramesPerSecond, 5);
        Assert.Equal(1, recent.SlowFrames);
        Assert.Equal(8.0 / 3, recent.DrawCallsPerFrame, 5);
        Assert.Equal(900.0 / 3, recent.AllocatedBytesPerFrame, 5);
        stats.Reset();
        Assert.Equal(default, stats.Snapshot());
    }

    [Fact]
    public void InvalidNumbersRejectWithoutCorruptingHistory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewFrameStatistics(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewFrameStatistics(601));
        var stats = new PreviewFrameStatistics();
        stats.Record(1.0 / 60, 1, 2, 4, 50);
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0, 1, 2, 4, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(double.NaN, 1, 2, 4, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, double.PositiveInfinity, 2, 4, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, -1, 2, 4, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, 1, -1, 4, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, 1, 2, -1, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, 1, 2, 4, -1));
        Assert.Equal(1, stats.Snapshot().Samples);
    }

    [Fact]
    public void RecordAndSnapshotReuseFixedStorageWithoutManagedAllocation()
    {
        var stats = new PreviewFrameStatistics();
        for (int i = 0; i < 150; i++) stats.Record(1.0 / 60, 2, 4, 12, 50);
        _ = stats.Snapshot();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++)
        {
            stats.Record(1.0 / 60, 2, 4, 12, 50);
            _ = stats.Snapshot();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
