using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-011: a stall of the interface is written to the report with what the app was doing, and nothing else.</summary>
public sealed class UiStallWatchTests
{
    [Fact]
    public void ALateBeatIsAStallWrittenWithWhatWasHappeningAndNoText()
    {
        var old = CrashLog.FilePath;
        CrashLog.FilePath = Path.Combine(Path.GetTempPath(), "urbe-stall-" + Guid.NewGuid().ToString("N"), "erros.log");
        try
        {
            UiStallWatch.Doing("abrir uma nota");
            Assert.Null(UiStallWatch.Check(UiStallWatch.Beat + TimeSpan.FromMilliseconds(300)));   // a little late: not a stall
            Assert.Null(UiStallWatch.Check(UiStallWatch.Beat + TimeSpan.FromMinutes(5)));   // the computer slept
            var stall = UiStallWatch.Check(UiStallWatch.Beat + TimeSpan.FromMilliseconds(1800));
            Assert.Equal(1800, stall!.Value.TotalMilliseconds, 1);
            var log = CrashLog.Read();
            Assert.Contains("· travamento", log);
            Assert.Contains("A interface ficou parada 1,8 s durante: abrir uma nota.", log);
            Assert.Contains(UiStallWatch.Seen, s => s.Doing == "abrir uma nota");
        }
        finally { CrashLog.FilePath = old; }
    }
}
