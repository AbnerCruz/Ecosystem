using Android.Content.Res;
using Lunet.Content;

namespace Lunet.Android;

/// <summary>
/// Reads game/Content and game/Assets directly from the packaged APK,
/// preserving Preview's relative Content paths and Assets fallback.
/// No external storage, IDE process, or arbitrary filesystem access.
/// </summary>
internal sealed class PackagedContentSource(AssetManager assets) : IContentSource
{
    private const string Root = "game/";

    public bool Exists(string path) => TryOpen(path, out var stream) && DisposeFound(stream);

    public Stream Open(string path)
    {
        if (TryOpen(path, out var stream)) return stream!;
        throw new FileNotFoundException("Asset do jogo ausente ou caminho inválido: " + path);
    }

    private bool TryOpen(string path, out Stream? stream)
    {
        stream = null;
        if (!Valid(path)) return false;
        if (OpenAsset(Root + "Content/" + path, out stream)) return true;
        return path.StartsWith("Assets/", StringComparison.Ordinal)
            && OpenAsset(Root + path, out stream);
    }

    private bool OpenAsset(string name, out Stream? stream)
    {
        stream = null;
        try
        {
            stream = assets.Open(name, Access.Streaming);
            return true;
        }
        catch (Java.IO.FileNotFoundException)
        {
            return false;
        }
    }

    private static bool DisposeFound(Stream? stream)
    {
        stream?.Dispose();
        return stream is not null;
    }

    private static bool Valid(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':'))
            return false;
        return path.Split('/').All(part => part.Length != 0 && part is not ("." or ".."));
    }
}
