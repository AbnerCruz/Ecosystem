using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-056 (owner: the quick recording was too short and its rush kept him from organizing the thought): a pause to
/// think does not end the note; only a long silence, the person's tap or the generous limit does.
/// </summary>
public sealed class VoiceCapturePolicyTests
{
    [Fact]
    public void APauseToThinkDoesNotEndTheNote()
    {
        Assert.False(VoiceCapturePolicy.ShouldStop(elapsedMillis: 20_000, heardSpeech: true, silenceMillis: 8_000));
        Assert.False(VoiceCapturePolicy.ShouldStop(elapsedMillis: 600_000, heardSpeech: true, silenceMillis: 20_000));
    }

    [Fact]
    public void ALongSilenceTheLimitOrNothingSaidEndsIt()
    {
        Assert.True(VoiceCapturePolicy.ShouldStop(60_000, true, VoiceCapturePolicy.SilenceAfterSpeechMillis));
        Assert.True(VoiceCapturePolicy.ShouldStop(VoiceCapturePolicy.MaxMillis, true, 0));
        Assert.True(VoiceCapturePolicy.ShouldStop(VoiceCapturePolicy.NoSpeechMillis, false, VoiceCapturePolicy.NoSpeechMillis));
        Assert.False(VoiceCapturePolicy.ShouldStop(5_000, false, 5_000));
        Assert.True(VoiceCapturePolicy.MaxMillis >= 10 * 60_000);
    }

    [Fact]
    public void TheNotificationShowsTheTimeAndWhatIsStillBeingSaved()
    {
        Assert.Equal("Gravando 0:42 · toque para parar", VoiceCapturePolicy.RecordingTitle(42_300, saving: 0));
        Assert.Equal("Gravando 12:05 · toque para parar · 1 salvando", VoiceCapturePolicy.RecordingTitle(725_000, saving: 1));
        Assert.Equal("Salvando 2 notas…", VoiceCapturePolicy.SavingTitle(2));
        Assert.Equal("Salvando…", VoiceCapturePolicy.SavingTitle(1));
        // the limit is announced before it arrives
        Assert.Contains("termina em 0:30", VoiceCapturePolicy.RecordingTitle(VoiceCapturePolicy.MaxMillis - 30_000, 0));
    }
}
