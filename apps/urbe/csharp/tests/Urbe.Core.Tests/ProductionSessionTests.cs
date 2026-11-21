using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>AbnerCruz/Urbe#51: the last note and conversation come back only within the same production session.</summary>
public sealed class ProductionSessionTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AShortPauseContinuesAndALongOneStartsANewSession()
    {
        var pause = ProductionSession.DefaultPause;
        Assert.True(ProductionSession.Continues(T, T.AddMinutes(3), pause));            // switched apps, came back
        Assert.True(ProductionSession.Continues(T, T + pause, pause));
        Assert.False(ProductionSession.Continues(T, T + pause + TimeSpan.FromSeconds(1), pause));
        Assert.False(ProductionSession.Continues(T, T.AddDays(2), pause));
        Assert.False(ProductionSession.Continues(null, T, pause));                       // never used: home
        Assert.False(ProductionSession.Continues(T, T.AddMinutes(-10), pause));          // clock went back
        // "Sempre reabrir onde parei"
        Assert.True(ProductionSession.Continues(T, T.AddDays(30), null));
        Assert.True(ProductionSession.Continues(null, T, null));
        Assert.Contains(ProductionSession.Choices, c => c.Pause == ProductionSession.DefaultPause);
    }
}
