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
    public void Rain_DarkensAndCoolsTheLight()
    {
        var host = new EmptyHost();
        var life = new LegacyWorldLife(host, options: new LegacyLifeOptions { TimeMode = "dia" }, random: LegacyJsMath.Rng(3));
        var view = new LegacyLifeView(0, 0, 1, 800, 600, -14, -11, 14, 11);
        life.Start("chuva", view);
        for (int i = 0; i < 30; i++) life.Tick(.1, view);
        Assert.True(life.Rain.K > 0);
        Assert.True(life.CurrentLight.Darkness > 0);
        Assert.True(life.CurrentLight.Red < 255);
        Assert.True(life.Hurry > 1);
        Assert.Equal(3, life.Time, 8);
    }

    [Fact]
    public void InvalidTimeSteps_AreRejected_AndLongStepsClamped()
    {
        var life = new LegacyWorldLife();
        Assert.Throws<ArgumentOutOfRangeException>(() => life.Tick(-1, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => life.Tick(double.NaN, default));
        life.Tick(5, default);
        Assert.Equal(.1, life.Time, 8);
    }

    private sealed class EmptyHost : ILegacyLifeHost
    {
        public bool Water(int x, int y) => false;
        public int Biome(int x, int y) => (int)LegacyBiome.Grass;
        public bool Road(int x, int y) => false;
        public bool House(int x, int y) => false;
        public bool District(int x, int y) => false;
        public IReadOnlyList<LegacyCityBuilding> Buildings => [];
        public IReadOnlyCollection<(int X, int Y)> Roads => [];
        public IEnumerable<LegacyLifeDistrict> Districts => [];
        public IReadOnlyList<ILegacyLifePerson> People => [];
    }
}
