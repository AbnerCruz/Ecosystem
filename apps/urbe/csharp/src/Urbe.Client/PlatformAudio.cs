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

    /// <summary>
    /// #956: speaks a text with the device's voice into a WAV (voice 0 or 1, the podcast's two hosts); false when it
    /// could not. Null where the platform has no speech synthesis, and the podcast then stays a script.
    /// </summary>
    public static Func<string, int, string, CancellationToken, Task<bool>>? SynthesizeToWav { get; set; }

    /// <summary>
    /// #1004 (narration): the device's Portuguese voices by name, for the person to choose; null where the platform
    /// cannot list them. The list is read when asked (voices can be installed while the app runs).
    /// </summary>
    public static Func<IReadOnlyList<string>>? Voices { get; set; }

    /// <summary>The voice the person chose for narration (a name from <see cref="Voices"/>); null is the platform's choice.</summary>
    public static string? PreferredVoice { get; set; }

    /// <summary>#1004: plays a WAV file to the end (or until cancelled); null where the platform cannot play one here.</summary>
    public static Func<string, CancellationToken, Task>? PlayWav { get; set; }

    /// <summary>Starts a local WAV at the requested timeline position and reports the media clock.</summary>
    public static Func<string, double, CancellationToken, Task<IPreviewAudio>>? StartPreview { get; set; }

    public static Func<string, string, string, CancellationToken, Task<bool>>? SynthesizeWithVoice { get; set; }

    public static IReadOnlyList<string> InstalledNarrationVoices() =>
        (Voices?.Invoke() ?? []).Where(id => Urbe.Core.Voice.NeuralVoices.Find(id) is not { } voice ||
            NeuralSpeech.Shared?.IsInstalled(Urbe.Core.Voice.NeuralVoices.ModelOf(voice)) == true).ToList();

    public static async Task<bool> NarrateAsync(string text, string? voice, string target, CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(voice))
            return SynthesizeToWav is { } fallback && await fallback(text, 0, target, cancel);
        if (Urbe.Core.Voice.NeuralVoices.Find(voice) is { } chosen)
            return NeuralSpeech.Shared is { } neural && await neural.SpeakAsync(chosen, text, target, cancel);
        return SynthesizeWithVoice is { } named && await named(text, voice, target, cancel);
    }

    public static bool Available => DecodeToWav is not null && TranscribeWav is not null;
}


public interface IPreviewAudio : IDisposable
{
    double Position { get; }
    bool Ended { get; }
    bool Failed { get; }
}

public sealed record NarrationVoiceChoice(string? Id);
