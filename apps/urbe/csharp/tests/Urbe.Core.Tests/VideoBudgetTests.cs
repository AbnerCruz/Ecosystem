using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class VideoBudgetTests
{
    [Fact]
    public void DiskCoversTheMp4AndEveryWav()
    {
        // pixels × fps / 8, between 1 and 20 Mbit/s; a minute of 1080p30 plus 160 kbit/s audio
        Assert.Equal(7_776_000, VideoBudget.VideoBitsPerSecond(1920, 1080, 30));
        Assert.Equal(20_000_000, VideoBudget.VideoBitsPerSecond(3840, 2160, 60));
        Assert.Equal(1_000_000, VideoBudget.VideoBitsPerSecond(320, 180, 10));
        Assert.Equal((7_936_000L * 60) / 8, VideoBudget.DiskNeeded(1920, 1080, 30, 60, 0, 0));
        // two tracks: their sources, two WAVs and the mix
        Assert.Equal(5_000_000 + 176_400L * 60 * 3 + (7_936_000L * 60) / 8, VideoBudget.DiskNeeded(1920, 1080, 30, 60, 2, 5_000_000));
    }

    [Fact]
    public void TimeLeftComesFromThePaceOnceMeasurable()
    {
        Assert.Null(VideoBudget.Remaining(0, 100, TimeSpan.FromSeconds(5)));
        Assert.Null(VideoBudget.Remaining(10, 100, TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(90), VideoBudget.Remaining(10, 100, TimeSpan.FromSeconds(10)));
        Assert.Equal("menos de 1 min", VideoBudget.Words(TimeSpan.FromSeconds(40)));
        Assert.Equal("cerca de 2 min", VideoBudget.Words(TimeSpan.FromSeconds(90)));
    }
}
