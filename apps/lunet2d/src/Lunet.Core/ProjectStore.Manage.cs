using System.Globalization;

namespace Lunet.Core;

/// <summary>A game in the trash: where it is and when it went there.</summary>
/// <param name="Id">The trash folder name, stable until the game is restored or purged.</param>
/// <param name="Name">The game's name when it was deleted.</param>
/// <param name="DeletedUtc">When it was deleted.</param>
public sealed record TrashedProject(string Id, string Name, DateTime DeletedUtc);

/// <summary>
/// Renaming, duplicating and deleting games without losing anything: delete moves the game to a trash folder inside
/// the projects folder (<see cref="TrashFolder"/>), from where it can be restored, and only "Apagar de vez" removes it.
/// Material's rule: a reversible action needs no confirmation, an undo is enough; an irreversible one asks.
/// </summary>
public sealed partial class ProjectStore
{
    /// <summary>The trash, inside the projects folder: same disk, so deleting and restoring are a rename.</summary>
    public const string TrashFolder = ".lixeira";

    private const string TrashStamp = "yyyyMMdd'T'HHmmssfff";

    /// <summary>Renames a game's folder and its manifest name. The Android identity (GameId, PackageId) stays: the installed APK keeps updating.</summary>
    public LunetProject Rename(string name, string newName)
    {
        var from = Path.Combine(RootDirectory, ValidateName(name));
        var target = ValidateName(newName);
        if (!File.Exists(Path.Combine(from, ManifestFileName))) throw new ProjectException($"Projeto \"{name}\" não encontrado.");
        if (string.Equals(name, target, StringComparison.Ordinal)) return Open(name);
        var to = Path.Combine(RootDirectory, target);
        // A case-only rename is the same folder on case-insensitive disks: go through a temporary name.
        var caseOnly = string.Equals(name, target, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && Directory.Exists(to)) throw new ProjectException($"Já existe um jogo chamado \"{target}\".");
        if (caseOnly)
        {
            var temporary = Path.Combine(RootDirectory, ".rename-" + Guid.NewGuid().ToString("N"));
            Directory.Move(from, temporary);
            Directory.Move(temporary, to);
        }
        else Directory.Move(from, to);
        var project = Open(target);
        project.Manifest.Name = target;
        project.WriteText(ManifestFileName, project.Manifest.ToJson());
        return project;
    }

    /// <summary>
    /// Copies a game into a new one with its own identity (new GameId and PackageId: its APK installs beside the
    /// original, with its own signing key). Caches, saves, history and Git stay with the original.
    /// </summary>
    public LunetProject Duplicate(string name, string? newName = null)
    {
        var source = Open(name);
        var target = newName is null ? FreeName(source.Name + "-copia") : ValidateName(newName);
        var to = Path.Combine(RootDirectory, target);
        if (Directory.Exists(to)) throw new ProjectException($"Já existe um jogo chamado \"{target}\".");
        var staging = Path.Combine(RootDirectory, ".lunet-imports", Guid.NewGuid().ToString("N"));
        try
        {
            CopyProjectFiles(source.Directory, staging, top: true);
            var manifest = ProjectManifest.Parse(File.ReadAllText(Path.Combine(staging, ManifestFileName)));
            manifest.Name = target;
            manifest.GameId = Guid.NewGuid().ToString("D");
            manifest.PackageId = PackageIdFor(target, manifest.GameId);
            AtomicFile.WriteAllText(Path.Combine(staging, ManifestFileName), manifest.ToJson());
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            Directory.Move(staging, to); // the copy appears whole or not at all
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
        return Open(target);
    }

    /// <summary>Moves a game to the trash. Nothing is erased; <see cref="Restore"/> brings it back.</summary>
    public TrashedProject MoveToTrash(string name)
    {
        var from = Path.Combine(RootDirectory, ValidateName(name));
        if (!File.Exists(Path.Combine(from, ManifestFileName))) throw new ProjectException($"Projeto \"{name}\" não encontrado.");
        var now = DateTime.UtcNow;
        var id = name + "~" + now.ToString(TrashStamp, CultureInfo.InvariantCulture);
        var trash = Path.Combine(RootDirectory, TrashFolder);
        Directory.CreateDirectory(trash);
        Directory.Move(from, Path.Combine(trash, id));
        return new TrashedProject(id, name, now);
    }

    /// <summary>The games in the trash, most recently deleted first.</summary>
    public IReadOnlyList<TrashedProject> Trash()
    {
        var trash = Path.Combine(RootDirectory, TrashFolder);
        if (!Directory.Exists(trash)) return [];
        var result = new List<TrashedProject>();
        foreach (var directory in Directory.EnumerateDirectories(trash))
        {
            var id = Path.GetFileName(directory);
            var cut = id.LastIndexOf('~');
            if (cut <= 0 || !File.Exists(Path.Combine(directory, ManifestFileName))) continue;
            var deleted = DateTime.TryParseExact(id[(cut + 1)..], TrashStamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when) ? when : Directory.GetLastWriteTimeUtc(directory);
            result.Add(new TrashedProject(id, id[..cut], deleted));
        }
        return result.OrderByDescending(t => t.DeletedUtc).ToList();
    }

    /// <summary>Brings a game back from the trash, under its name or, if that name is taken now, a free variant of it.</summary>
    public LunetProject Restore(string trashId)
    {
        var from = TrashPath(trashId);
        var name = trashId[..trashId.LastIndexOf('~')];
        var target = FreeName(name);
        Directory.Move(from, Path.Combine(RootDirectory, target));
        var project = Open(target);
        if (project.Manifest.Name != target)
        {
            project.Manifest.Name = target;
            project.WriteText(ManifestFileName, project.Manifest.ToJson());
        }
        return project;
    }

    /// <summary>Erases a game from the trash for good. The only call here that loses data: the UI confirms first.</summary>
    public void Purge(string trashId) => Directory.Delete(TrashPath(trashId), recursive: true);

    /// <summary>The name itself when free, else name-2, name-3…</summary>
    public string FreeName(string name)
    {
        var baseName = ValidateName(name);
        if (!Directory.Exists(Path.Combine(RootDirectory, baseName))) return baseName;
        for (var i = 2; ; i++)
        {
            var suffix = "-" + i.ToString(CultureInfo.InvariantCulture);
            var candidate = (baseName.Length + suffix.Length > 60 ? baseName[..(60 - suffix.Length)] : baseName) + suffix;
            if (!Directory.Exists(Path.Combine(RootDirectory, candidate))) return candidate;
        }
    }

    private string TrashPath(string trashId)
    {
        if (string.IsNullOrEmpty(trashId) || trashId.IndexOfAny(['/', '\\']) >= 0 || trashId.StartsWith('.') || !trashId.Contains('~'))
            throw new ProjectException("Item da lixeira inválido.");
        var path = Path.Combine(RootDirectory, TrashFolder, trashId);
        if (!File.Exists(Path.Combine(path, ManifestFileName))) throw new ProjectException("Este jogo não está mais na lixeira.");
        return path;
    }

    private static void CopyProjectFiles(string from, string to, bool top)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            if (file.EndsWith(".lunet-tmp", StringComparison.Ordinal)) continue;
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue; // never follow links out of the game
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
        foreach (var directory in Directory.EnumerateDirectories(from))
        {
            var name = Path.GetFileName(directory);
            if (top && name is ".lunet" or "bin" or "obj" or ".git") continue;
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
            CopyProjectFiles(directory, Path.Combine(to, name), top: false);
        }
    }
}
