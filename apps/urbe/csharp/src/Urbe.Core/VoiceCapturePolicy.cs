using System.Globalization;

namespace Urbe.Core;

/// <summary>
/// CST-056: when the quick recording (widget, UC-66) ends by itself, and what its notification says. The owner found the
/// old rule (stop 2.5 s after speech went quiet, at most 2 minutes) rushed him: a pause to think ended the note. Like
/// the phone's own recorders, it now ends with a tap; by itself only after a long silence (a forgotten recording), when
/// nothing was said at all (a stray tap) or at a generous limit that is announced before it arrives. The recognizer
/// keeps its own short pauses as segment breaks, so the text still comes in parts.
/// </summary>
public static class VoiceCapturePolicy
{
    public const int MaxMillis = 15 * 60_000;
    public const int SilenceAfterSpeechMillis = 45_000;
    public const int NoSpeechMillis = 10_000;
    public const int WarnBeforeEndMillis = 60_000;

    public static bool ShouldStop(int elapsedMillis, bool heardSpeech, int silenceMillis) =>
        elapsedMillis >= MaxMillis ||
        heardSpeech && silenceMillis >= SilenceAfterSpeechMillis ||
        !heardSpeech && elapsedMillis >= NoSpeechMillis;

    /// <summary>"Gravando 0:42 · toque para parar", with the notes still being saved and the end when it is near.</summary>
    public static string RecordingTitle(int elapsedMillis, int saving)
    {
        var title = "Gravando " + Clock(elapsedMillis) + " · toque para parar";
        int left = MaxMillis - elapsedMillis;
        if (left <= WarnBeforeEndMillis) title += " · termina em " + Clock(Math.Max(0, left));
        if (saving > 0) title += " · " + saving + " salvando";
        return title;
    }

    public static string SavingTitle(int saving) => saving > 1 ? "Salvando " + saving + " notas…" : "Salvando…";

    private static string Clock(int millis)
    {
        int seconds = millis / 1000;
        return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
