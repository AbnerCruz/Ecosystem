using System.Globalization;
using Lunet.Runtime.Profiling;

namespace Lunet.Tests;

public sealed class PreviewProfilerReportTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 11, 6, 30, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void EmptyWindowDoesNotPresentInventedZeroMetrics()
    {
        var text = PreviewProfilerReport.Format(default, CapturedAt, false, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal("PROFILER  |  coletando...", text);
        Assert.DoesNotContain("FPS:", text);
    }

    [Fact]
    public void RealWindowReportsPhasesTexturesAndCaptureTimeWithExplicitMissingMetrics()
    {
        var statistics = new PreviewFrameStatistics();
        for (int i = 0; i < 30; i++) statistics.Record(1.0 / 60, 4, 5, 10, 64, 1.25, 2.5, 1);
        var text = PreviewProfilerReport.Format(statistics.Snapshot(), CapturedAt, true,
            8 * 1048576, 2 * 1048576, 3 * 1048576, 4, 5, 1, 0);
        Assert.Contains("30 quadros  [PAUSADO]", text);
        Assert.Contains("2026-10-11 06:30:00 -03:00", text);
        Assert.Contains("FPS: 60.0  Frame: 16.67 ms", text);
        Assert.Contains("CPU Update: 1.25 ms  (1.0 passos)", text);
        Assert.Contains("CPU Draw: 2.50 ms", text);
        Assert.Contains("Heap .NET: 8.0 MiB", text);
        Assert.Contains("2.00 MiB, 4 vivas", text);
        Assert.Contains("Pico texturas: 3.00 MiB", text);
        Assert.Contains("GC (0/1/2): 5/1/0", text);
        Assert.Contains("GPU total/tempo e audio underruns: não medidos", text);
        Assert.DoesNotContain("\\n", text);
        Assert.True(text.Split('\n').Length >= 15);
    }

    [Fact]
    public void MissingPhaseInstrumentationIsNotReportedAsZeroCost()
    {
        var statistics = new PreviewFrameStatistics();
        statistics.Record(0.02, 3, 1, 2, 0);
        var text = PreviewProfilerReport.Format(statistics.Snapshot(), CapturedAt, false, 0, 0, 0, 0, 0, 0, 0);
        Assert.Contains("CPU Update/Draw: não medidos", text);
        Assert.DoesNotContain("CPU Update: 0", text);
        Assert.DoesNotContain("[PAUSADO]", text);
    }

    [Fact]
    public void ExportIsStableAcrossDeviceCulturesAndPreservesOutlierStatistics()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var statistics = new PreviewFrameStatistics();
            for (int i = 0; i < 19; i++) statistics.Record(0.01, 1, 1, 2, 0);
            statistics.Record(0.08, 3, 5, 10, 256);
            string Format() => PreviewProfilerReport.Format(statistics.Snapshot(), CapturedAt, false, 0, 0, 0, 0, 0, 0, 0);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = Format();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal(english, Format());
            Assert.Contains("Pior: 80.0 ms | engasgos: 1", english);
            Assert.Contains("Frames > 33 ms: 1/20", english);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
