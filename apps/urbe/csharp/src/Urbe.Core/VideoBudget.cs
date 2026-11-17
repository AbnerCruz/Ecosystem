namespace Urbe.Core;

/// <summary>
/// CST-186: what a video export will ask of the device, said before it starts (disk) and while it runs (time left),
/// so a phone never runs out of space halfway or leaves the person guessing.
/// </summary>
public static class VideoBudget
{
    private const long WavBytesPerSecond = 44_100 * 2 * 2;
    private const long AudioBitsPerSecond = 160_000;

    /// <summary>The encoder's video bitrate (the Android adapter uses the same rule).</summary>
    public static long VideoBitsPerSecond(int width, int height, int fps) => Math.Clamp((long)width * height * fps / 8, 1_000_000, 20_000_000);

    /// <summary>
    /// Upper bound of the temporary disk: the copied audio sources, one WAV per track and the mix (each at most the
    /// video's length) and the MP4 itself.
    /// </summary>
    public static long DiskNeeded(int width, int height, int fps, double seconds, int audioTracks, long audioSourceBytes)
    {
        var mp4 = (long)((VideoBitsPerSecond(width, height, fps) + AudioBitsPerSecond) * seconds / 8);
        var wav = audioTracks == 0 ? 0 : (long)(WavBytesPerSecond * seconds) * (audioTracks + 1);
        return audioSourceBytes + wav + mp4;
    }

    /// <summary>Time left from the pace so far; null until there is enough to measure (two seconds).</summary>
    public static TimeSpan? Remaining(int done, int total, TimeSpan elapsed) =>
        done < 1 || elapsed < TimeSpan.FromSeconds(2) ? null : TimeSpan.FromTicks(elapsed.Ticks / done * (total - done));

    public static string Words(TimeSpan left) => left.TotalSeconds < 60 ? "menos de 1 min" : "cerca de " + (int)Math.Ceiling(left.TotalMinutes) + " min";
}
