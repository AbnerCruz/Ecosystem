namespace Lunet.Client.Shell;

/// <summary>Which workspace tab a project file belongs to, by its name (the formats the Studios already write).</summary>
public static class ProjectFiles
{
    public static bool IsCode(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path == Lunet.Core.ProjectStore.ManifestFileName;

    public static bool IsArt(string path) =>
        path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase);

    public static bool IsMap(string path) => path.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase);

    public static bool IsSound(string path) =>
        path.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);

    public static bool IsNote(string path) =>
        path.StartsWith(Lunet.Core.ProjectNotes.Folder + "/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    /// <summary>The files a tab lists, in the project's order.</summary>
    public static IReadOnlyList<string> For(WorkspaceTab tab, IEnumerable<string> files) => files.Where(tab switch
    {
        WorkspaceTab.Code => IsCode,
        WorkspaceTab.Art => IsArt,
        WorkspaceTab.Map => IsMap,
        WorkspaceTab.Sound => IsSound,
        WorkspaceTab.Notes => IsNote,
        _ => (Func<string, bool>)(_ => false),
    }).ToArray();
}
