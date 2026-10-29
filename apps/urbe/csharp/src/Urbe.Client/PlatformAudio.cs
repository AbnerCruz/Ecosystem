namespace Urbe.Client;

/// <summary>
/// CST-058: what the device does with a long recording. Set by a head that can (Android 13+: MediaCodec and the
/// speech recognizer, on the device); null elsewhere, and the Urbe does not offer the transcription then.
/// </summary>
public static class PlatformAudio
{
    /// <summary>
    /// Decodes an audio file (source path) into a 16 kHz mono 16-bit WAV (target path), streamed to disk; progress goes
    /// from 0 to 1. False when the file cannot be read as audio.
    /// </summary>
    public static Func<string, string, IProgress<double>?, CancellationToken, Task<bool>>? DecodeToWav { get; set; }

    /// <summary>The text of a 16 kHz WAV part, or null when nothing was understood.</summary>
    public static Func<string, CancellationToken, Task<string?>>? TranscribeWav { get; set; }

    public static bool Available => DecodeToWav is not null && TranscribeWav is not null;
}
