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
        Assert.Equal(0, initial.PhasedSamples);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, 1, 2, 4, 1, -1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, 1, 2, 4, 1, 1, double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Record(0.01, 1, 2, 4, 1, 1, 1, -1));
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

    [Fact]
    public void PhasesTrackOnlyInstrumentedFramesWithinRollingWindow()
    {
        var stats = new PreviewFrameStatistics(3);
        stats.Record(0.01, 3, 4, 8, 20);
        stats.Record(0.02, 6, 5, 10, 30, 2, 1, 2);
        stats.Record(0.02, 8, 8, 16, 40, 3, 2, 0);
        var first = stats.Snapshot();
        Assert.Equal(2, first.PhasedSamples);
        Assert.Equal(2.5, first.UpdateCpuMilliseconds);
        Assert.Equal(1.5, first.DrawCpuMilliseconds);
        Assert.Equal(1, first.UpdateStepsPerFrame);
        stats.Record(0.02, 10, 2, 4, 50, 4, 3, 1);
        var after = stats.Snapshot();
        Assert.Equal(3, after.PhasedSamples);
        Assert.Equal(3, after.UpdateCpuMilliseconds);
        Assert.Equal(2, after.DrawCpuMilliseconds);
        Assert.Equal(1.0, after.UpdateStepsPerFrame);
        stats.Reset();
        Assert.Equal(0, stats.Snapshot().PhasedSamples);
    }

    [Fact]
    public void PhaseMetricsRecordAndSnapshotDoNotAllocatePerFrame()
    {
        var stats = new PreviewFrameStatistics();
        for (int i = 0; i < 100; i++) stats.Record(1.0 / 60, 3, 1, 2, 10, 1, 1, 1);
        _ = stats.Snapshot();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++)
        {
            stats.Record(1.0 / 60, 3, 1, 2, 10, 1, 1, 1);
            _ = stats.Snapshot();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    public void SteadyRefreshRatesDoNotReportFalseHitches(int refreshRate)
    {
        var stats = new PreviewFrameStatistics(60);
        for (int i = 0; i < 60; i++)
            stats.Record(1.0 / refreshRate, 1, 2, 4, 0);
        var snapshot = stats.Snapshot();
        Assert.Equal(1000.0 / refreshRate, snapshot.P50FrameMilliseconds, 5);
        Assert.Equal(1000.0 / refreshRate, snapshot.P95FrameMilliseconds, 5);
        Assert.Equal(1000.0 / refreshRate, snapshot.WorstFrameMilliseconds, 5);
        Assert.Equal(0, snapshot.HitchFrames);
    }

    [Fact]
    public void PercentilesRevealRareFortyMillisecondSpikeAt120Hz()
    {
        var stats = new PreviewFrameStatistics(20);
        for (int i = 0; i < 19; i++)
            stats.Record(1.0 / 120, 1, 1, 2, 0);
        stats.Record(0.04, 5, 2, 4, 100);
        var snapshot = stats.Snapshot();
        Assert.Equal(20, snapshot.Samples);
        Assert.Equal(1000.0 / 120, snapshot.P50FrameMilliseconds, 5);
        Assert.Equal(1000.0 / 120, snapshot.P95FrameMilliseconds, 5);
        Assert.Equal(40, snapshot.WorstFrameMilliseconds, 5);
        Assert.Equal(1, snapshot.HitchFrames);
        Assert.Equal(1, snapshot.SlowFrames);
        Assert.True(snapshot.FrameMilliseconds < 12); // A média esconde o pico.
    }

    [Fact]
    public void HitchesDisappearWhenRollingWindowEvictsOldFrames()
    {
        var stats = new PreviewFrameStatistics(4);
        stats.Record(.01, 1, 1, 2, 0);
        stats.Record(.01, 1, 1, 2, 0);
        stats.Record(.01, 1, 1, 2, 0);
        stats.Record(.04, 1, 1, 2, 0);
        var first = stats.Snapshot();
        Assert.Equal(1, first.HitchFrames);
        Assert.Equal(40, first.P95FrameMilliseconds, 5);
        for (int i = 0; i < 4; i++)
            stats.Record(.01, 1, 1, 2, 0);
        var last = stats.Snapshot();
        Assert.Equal(0, last.HitchFrames);
        Assert.Equal(10, last.P95FrameMilliseconds, 5);
        Assert.Equal(10, last.WorstFrameMilliseconds, 5);
        stats.Reset();
        Assert.Equal(default, stats.Snapshot());
    }

    [Fact]
    public void EvenSizedWindowUsesInterpolatedMedianAndAdaptiveHitchThreshold()
    {
        var stats = new PreviewFrameStatistics(4);
        stats.Record(.010, 1, 1, 2, 0);
        stats.Record(.011, 1, 1, 2, 0);
        stats.Record(.012, 1, 1, 2, 0);
        stats.Record(.019, 1, 1, 2, 0);
        var snapshot = stats.Snapshot();
        Assert.Equal(11.5, snapshot.P50FrameMilliseconds, 5);
        Assert.Equal(19, snapshot.P95FrameMilliseconds, 5);
        Assert.Equal(0, snapshot.HitchFrames); // 19ms < 1.75 * 11.5ms
    }
}
