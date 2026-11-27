using Lunet.Content;
using Lunet.Core;

namespace Lunet.Host;

/// <summary>
/// The running game's content read straight from the game's files (<see cref="IProjectFiles"/>: the disk, or the folder
/// chosen on Android), with no copy. Same rules as the disk source: <c>Content/</c> first, then <c>Assets/…</c> paths.
/// </summary>
public sealed class ProjectFilesContentSource(IProjectFiles files) : IContentSource
{
    private const string AssetsPrefix = "Assets/";

    public bool Exists(string path) => Resolve(path) is not null;

    public Stream Open(string path) =>
        Resolve(path) is { } found ? new MemoryStream(files.Read(found), writable: false)
            : throw new FileNotFoundException($"Arquivo de conteúdo não encontrado: {path}");

    private string? Resolve(string path)
    {
        string relative;
        try { relative = LunetProject.Relative(path); }
        catch (ProjectException) { return null; } // a path that leaves the game is never content
        if (files.FileExists("Content/" + relative)) return "Content/" + relative;
        if (relative.StartsWith(AssetsPrefix, StringComparison.Ordinal) && relative.Length > AssetsPrefix.Length && files.FileExists(relative)) return relative;
        return null;
    }
}
