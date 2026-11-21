namespace Urbe.Client;

/// <summary>Platform audio decoding, transcription and playback. Speech synthesis runs through the agent's provider.</summary>
public static class PlatformAudio
{
    public static Func<string, string, IProgress<double>?, CancellationToken, Task<bool>>? DecodeToWav { get; set; }
    public static Func<string, CancellationToken, Task<string?>>? TranscribeWav { get; set; }
    public static Func<string, double, CancellationToken, Task<IPreviewAudio>>? StartPreview { get; set; }

    public static bool Available => DecodeToWav is not null && TranscribeWav is not null;
}

public interface IPreviewAudio : IDisposable
{
    double Position { get; }
    bool Ended { get; }
    bool Failed { get; }
}

public sealed record NarrationVoiceChoice(string? Id);
