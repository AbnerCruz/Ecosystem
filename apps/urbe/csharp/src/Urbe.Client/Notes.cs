namespace Urbe.Client;

/// <summary>A file of the open vault (path relative to the vault, text content).</summary>
public sealed record CityNote(string Path, string Content)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
}
