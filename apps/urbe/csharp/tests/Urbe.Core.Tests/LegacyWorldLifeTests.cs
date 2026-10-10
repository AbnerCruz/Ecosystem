using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyWorldLifeTests
{
    [Fact]
    public void LightAt_UsesOriginalDayAndNightStops()
    {
        var day = LegacyWorldLife.LightAt(8.2);
        Assert.Equal(255, day.Red);
        Assert.Equal(255, day.Green);
        Assert.Equal(255, day.Blue);
        Assert.Equal(0, day.Darkness);
        var night = LegacyWorldLife.LightAt(20.6);
        Assert.Equal(80, night.Red);
        Assert.Equal(94, night.Green);
        Assert.Equal(160, night.Blue);
        Assert.Equal(1, night.Darkness);
    }

    [Fact]
    public void LightAt_WrapsClockAndRejectsNonFiniteHour()
    {
        Assert.Equal(LegacyWorldLife.LightAt(1.5), LegacyWorldLife.LightAt(25.5));
        Assert.Equal(LegacyWorldLife.LightAt(23), LegacyWorldLife.LightAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyWorldLife.LightAt(double.NaN));
    }

    [Theory]
    [InlineData("dia", 12.5)]
    [InlineData("entardecer", 18.2)]
    [InlineData("noite", 23)]
    public void HourAt_SupportsLegacyFixedModes(string mode, double hour)
    {
        Assert.Equal(hour, LegacyWorldLife.HourAt(DateTimeOffset.UtcNow, mode));
    }

    [Fact]
    public void HourAt_RealClockReadsActualWallTime()
    {
        var time = new DateTimeOffset(2026, 10, 10, 12, 30, 0, TimeSpan.FromHours(-3));
        Assert.Equal(12.5, LegacyWorldLife.HourAt(time));
    }

    [Fact]
    public void RainAndFog_UpdateIndependentOfRendering()
    {
        var sim = new LegacyWorldLife { TimeMode = "dia" };
        var t = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.FromHours(-3));
        sim.StartRain(10);
        sim.StartFog(10);
        sim.Advance(.1, t);
        Assert.True(sim.Rain > 0);
        Assert.True(sim.Fog > 0);
        Assert.True(sim.CurrentLight.Darkness > 0);
        Assert.True(sim.CurrentLight.Red < 255);
        Assert.Equal(.1, sim.ElapsedSeconds, 8);
    }

    [Fact]
    public void InvalidDurationsAndTimeSteps_AreRejected()
    {
        var sim = new LegacyWorldLife();
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.StartRain(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.StartFog(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Advance(-1, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Advance(.2, DateTimeOffset.UtcNow));
    }
}
