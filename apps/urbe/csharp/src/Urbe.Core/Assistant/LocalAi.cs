namespace Urbe.Core.Assistant;

/// <summary>
/// A model the app can keep on the device (24/10/2026, offline AI): what it is, where it comes from, how big, and how to
/// check that what was downloaded is the file that was meant. The catalog is the only list of such models.
/// </summary>
public sealed record LocalModel(string Id, string Name, string Detail, string File, string Url, long Bytes, string Sha256, int Context, int MinRamMb, bool Tools)
{
    /// <summary>The size for a person to read ("3,1 GB").</summary>
    public string SizeText => Bytes >= 1_000_000_000 ? (Bytes / 1e9).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " GB" : (Bytes / 1e6).ToString("0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " MB";
}

/// <summary>
/// The text model that answers without the internet: given the conversation (and the tools) it streams an answer. The
/// heads provide it (llama.cpp through LLamaSharp); Urbe.Core knows only this.
/// </summary>
public interface ILocalTextRuntime
{
    /// <summary>The models that can be chosen, best first.</summary>
    IReadOnlyList<LocalModel> Models { get; }

    /// <summary>The model chosen for this device (by its memory), whether or not it is downloaded.</summary>
    LocalModel Recommended { get; }

    /// <summary>True when the model's file is on the device and checked.</summary>
    bool IsInstalled(LocalModel model);

    /// <summary>Downloads the model (resuming a partial file), checks it and keeps it. Progress is bytes done and total.</summary>
    Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel);

    /// <summary>Removes the model's file.</summary>
    void Remove(LocalModel model);

    /// <summary>Answers a request with the installed model.</summary>
    Task<AiResult> ChatAsync(LocalModel model, AiRequest request, Action<AiStreamEvent> onEvent, CancellationToken cancel);
}

/// <summary>A runtime that keeps half-downloaded files (so that "continuar" can say how far it was).</summary>
public interface IPartialDownloads
{
    /// <summary>Bytes of the model already downloaded.</summary>
    long PartialBytes(LocalModel model);
}

/// <summary>Where the head leaves the offline AI (null: this device has none).</summary>
public static class LocalAi
{
    /// <summary>The text model on this device.</summary>
    public static ILocalTextRuntime? Text { get; set; }
}
