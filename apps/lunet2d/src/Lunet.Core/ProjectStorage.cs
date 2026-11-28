namespace Lunet.Core;

/// <summary>An item directly inside a folder of the project.</summary>
public sealed record ProjectFileEntry(string Name, bool IsDirectory);

/// <summary>
/// Where a game's files really are (owner, 27/11: "arquivo diretamente, não é cópia, não é espelho"). Every read and
/// write of a game goes through this: the disk on the desktop and in the tests, the folder the person chose (SAF) on
/// Android. Paths are relative to the game's folder, with '/', already checked by <see cref="LunetProject"/>.
/// </summary>
public interface IProjectFiles
{
    /// <summary>A readable name of where the game is (a folder path or the chosen folder's name), for messages.</summary>
    string Location { get; }

    /// <summary>The folder on this device's disk, when there is one (the desktop); null for a SAF folder.</summary>
    string? LocalDirectory { get; }

    /// <summary>The items directly inside a folder ("" is the game's folder); empty when it does not exist.</summary>
    IReadOnlyList<ProjectFileEntry> List(string folder);

    bool FileExists(string path);
    bool DirectoryExists(string path);
    byte[] Read(string path);

    /// <summary>Writes the whole file, creating its folders; never leaves a half-written file (atomic or verified).</summary>
    void Write(string path, byte[] bytes);

    void CreateDirectory(string path);

    /// <summary>Moves or renames a file or folder (the destination's folders are created).</summary>
    void Move(string from, string to);

    /// <summary>Deletes a file, or a folder with everything in it.</summary>
    void Delete(string path);

    /// <summary>The same storage seen from one of its folders (the folder of games → one game).</summary>
    IProjectFiles Sub(string folder);
}

/// <summary>The game's folder on this device's disk (desktop, tests, Android's private folder before SAF).</summary>
public sealed class DiskProjectFiles(string directory) : IProjectFiles
{
    private const string TemporarySuffix = ".lunet-tmp";

    public string Location => directory;
    public string? LocalDirectory => directory;

    private string Full(string path) => path.Length == 0 ? directory : Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar));

    public IReadOnlyList<ProjectFileEntry> List(string folder)
    {
        var full = Full(folder);
        if (!Directory.Exists(full)) return [];
        var result = new List<ProjectFileEntry>();
        foreach (var d in Directory.EnumerateDirectories(full)) result.Add(new ProjectFileEntry(Path.GetFileName(d), true));
        foreach (var f in Directory.EnumerateFiles(full))
        {
            var name = Path.GetFileName(f);
            if (!name.EndsWith(TemporarySuffix, StringComparison.Ordinal)) result.Add(new ProjectFileEntry(name, false));
        }
        return result;
    }

    public bool FileExists(string path) => File.Exists(Full(path));
    public bool DirectoryExists(string path) => Directory.Exists(Full(path));
    public byte[] Read(string path) => File.ReadAllBytes(Full(path));

    public void Write(string path, byte[] bytes)
    {
        var full = Full(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + TemporarySuffix;
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, full, overwrite: true);
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(Full(path));

    public void Move(string from, string to)
    {
        var source = Full(from);
        var target = Full(to);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target);
    }

    public void Delete(string path)
    {
        var full = Full(path);
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        else File.Delete(full);
    }

    public IProjectFiles Sub(string folder) => new DiskProjectFiles(Full(folder));
}
