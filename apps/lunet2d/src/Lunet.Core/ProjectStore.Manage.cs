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
        var from = ValidateName(name);
        var target = ValidateName(newName);
        var original = Open(from);
        if (from == target) return original;
        var caseOnly = from.Equals(target, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && Exists(target)) throw new ProjectException($"Já existe um jogo chamado \"{target}\".");
        var staged = caseOnly ? ".rename-" + Guid.NewGuid().ToString("N") : from;
        if (caseOnly) Root.Move(from, staged);
        try { Root.Move(staged, target); }
        catch
        {
            if (caseOnly) Root.Move(staged, from);
            throw;
        }
        var project = Open(target);
        project.Manifest.Name = target;
        try { project.WriteText(ManifestFileName, project.Manifest.ToJson()); }
        catch
        {
            if (caseOnly) Root.Move(target, staged);
            Root.Move(caseOnly ? staged : target, from);
            throw;
        }
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
        if (Exists(target)) throw new ProjectException($"Já existe um jogo chamado \"{target}\".");
        var staging = ".lunet-duplicate-" + Guid.NewGuid().ToString("N");
        Root.CreateDirectory(staging);
        try
        {
            var files = Root.Sub(staging);
            CopyProjectFiles(source.Files, files, "");
            var manifest = ProjectManifest.Parse(source.ReadText(ManifestFileName));
            manifest.Name = target;
            manifest.GameId = Guid.NewGuid().ToString("D");
            manifest.PackageId = PackageIdFor(target, manifest.GameId);
            files.Write(ManifestFileName, System.Text.Encoding.UTF8.GetBytes(manifest.ToJson()));
            Root.Move(staging, target);
        }
        finally
        {
            if (Root.DirectoryExists(staging)) Root.Delete(staging);
        }
        return Open(target);
    }

    /// <summary>Moves a game to the trash. Nothing is erased; <see cref="Restore"/> brings it back.</summary>
    public TrashedProject MoveToTrash(string name)
    {
        var from = ValidateName(name);
        if (!Root.FileExists(from + "/" + ManifestFileName)) throw new ProjectException($"Projeto \"{name}\" não encontrado.");
        var now = DateTime.UtcNow;
        var id = name + "~" + now.ToString(TrashStamp, CultureInfo.InvariantCulture);
        Root.CreateDirectory(TrashFolder);
        Root.Move(from, TrashFolder + "/" + id);
        return new TrashedProject(id, name, now);
    }

    /// <summary>The games in the trash, most recently deleted first.</summary>
    public IReadOnlyList<TrashedProject> Trash()
    {

        var result = new List<TrashedProject>();
        foreach (var directory in Root.List(TrashFolder).Where(e => e.IsDirectory))
        {
            var id = directory.Name;
            var cut = id.LastIndexOf('~');
            if (cut <= 0 || !Root.FileExists(TrashFolder + "/" + id + "/" + ManifestFileName)) continue;
            var deleted = DateTime.TryParseExact(id[(cut + 1)..], TrashStamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when) ? when : DateTime.MinValue;
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
        Root.Move(from, target);
        var project = Open(target);
        if (project.Manifest.Name != target)
        {
            project.Manifest.Name = target;
            project.WriteText(ManifestFileName, project.Manifest.ToJson());
        }
        return project;
    }

    /// <summary>Erases a game from the trash for good. The only call here that loses data: the UI confirms first.</summary>
    public void Purge(string trashId) => Root.Delete(TrashPath(trashId));

    /// <summary>The name itself when free, else name-2, name-3…</summary>
    public string FreeName(string name)
    {
        var baseName = ValidateName(name);
        if (!Exists(baseName)) return baseName;
        for (var i = 2; ; i++)
        {
            var suffix = "-" + i.ToString(CultureInfo.InvariantCulture);
            var candidate = (baseName.Length + suffix.Length > 60 ? baseName[..(60 - suffix.Length)] : baseName) + suffix;
            if (!Exists(candidate)) return candidate;
        }
    }

    private string TrashPath(string trashId)
    {
        if (string.IsNullOrEmpty(trashId) || trashId.IndexOfAny(['/', '\\']) >= 0 || trashId.StartsWith('.') || !trashId.Contains('~'))
            throw new ProjectException("Item da lixeira inválido.");
        var path = TrashFolder + "/" + trashId;
        if (!Root.FileExists(path + "/" + ManifestFileName)) throw new ProjectException("Este jogo não está mais na lixeira.");
        return path;
    }

    private bool Exists(string name) => Root.DirectoryExists(name) || Root.FileExists(name);

    private static void CopyProjectFiles(IProjectFiles from, IProjectFiles to, string folder)
    {
        foreach (var entry in from.List(folder))
        {
            if (folder.Length == 0 && entry.IsDirectory && entry.Name is ".lunet" or "bin" or "obj" or ".git") continue;
            var path = folder.Length == 0 ? entry.Name : folder + "/" + entry.Name;
            if (from.LocalDirectory is { } disk &&
                (File.GetAttributes(Path.Combine(disk, path)) & FileAttributes.ReparsePoint) != 0) continue;
            if (entry.IsDirectory)
            {
                to.CreateDirectory(path);
                CopyProjectFiles(from, to, path);
            }
            else to.Write(path, from.Read(path));
        }
    }
}
