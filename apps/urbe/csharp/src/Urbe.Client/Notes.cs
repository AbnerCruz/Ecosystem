namespace Urbe.Client;

/// <summary>A file of the open vault (path relative to the vault, text content).</summary>
public sealed record CityNote(string Path, string Content)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>The document's title (document.title) where the vault's store knows it; the file name otherwise.</summary>
    public string? Title { get; init; }
}
