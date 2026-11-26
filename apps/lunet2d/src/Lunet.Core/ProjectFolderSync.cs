namespace Lunet.Core;

/// <summary>Revision storage in a folder the person chose (on Android, a SAF tree). Revisions are verified ZIPs.</summary>
public interface IProjectFolderTree
{
    string? Latest();
    IReadOnlyList<string> Revisions();
    string Publish(string archive, string digest, string? expectedRevision);
    void Restore(string name, string archive, string destination);
    void VerifyRevision(string name, string digest);
}

/// <summary>
/// The "Pasta de projetos" rules, out of the Android screen so the shell and the tests share them (LUNET-536 P1).
/// A line-by-line port of the previous <c>MainActivity.ProjectFolder</c>: games are edited in a local working copy
/// (<c>ProjectWorkingCopies/&lt;id&gt;</c>) and published to the folder as verified revisions; the private
/// <c>Projects</c> folder is never deleted; switching folder refuses while the previous working copy has
/// unpublished work; every step that can fail does so before the settings change. Each method returns the new
/// settings to save (null: nothing to do) and throws <see cref="IOException"/> with the reason otherwise.
/// </summary>
public sealed class ProjectFolderSync(string filesDirectory, string cacheDirectory)
{
    public string PrivateProjects => Path.Combine(filesDirectory, "Projects");
    public string WorkingCopy(string id) => Path.Combine(filesDirectory, "ProjectWorkingCopies", id);

    /// <summary>Where the games are for these settings: the working copy when it is active and exists, else Projects.</summary>
    public string ProjectsRoot(EditorSettings settings) =>
        settings.UseSharedProjectFolder && settings.ProjectWorkingCopy is { } copy && Directory.Exists(WorkingCopy(copy))
            ? WorkingCopy(copy) : PrivateProjects;

    public bool CanSwap(EditorSettings settings) =>
        settings.PreviousProjectWorkingCopy is { } previous && Directory.Exists(WorkingCopy(previous));

    private string TempArchive() => Path.Combine(cacheDirectory, "lunet-" + Guid.NewGuid().ToString("N") + ".zip");

    /// <summary>Connects (or reconnects) the chosen folder: opens its latest revision, or publishes the current games.</summary>
    public EditorSettings Connect(EditorSettings settings, string key, IProjectFolderTree folder)
    {
        var draft = settings.Clone();
        var source = ProjectsRoot(settings);
        var reconnect = draft.ProjectTree == key && draft.ProjectWorkingCopy is { } oldId && Directory.Exists(WorkingCopy(oldId));
        var latest = folder.Latest();
        if (!reconnect)
        {
            // A different folder must not make unpublished work from the previous connection unreachable.
            if (draft.ProjectWorkingCopy is { } previousId && Directory.Exists(WorkingCopy(previousId)))
            {
                var previousArchive = TempArchive();
                try
                {
                    var pendingDigest = ProjectFolderSnapshot.Capture(WorkingCopy(previousId), previousArchive);
                    if (draft.ProjectRevision?.EndsWith("-" + pendingDigest + ".zip", StringComparison.Ordinal) != true)
                        throw new IOException("Grave os projetos da pasta anterior antes de conectar outra.");
                }
                finally { File.Delete(previousArchive); }
            }
            var id = Guid.NewGuid().ToString("N");
            var destination = WorkingCopy(id);
            var archive = TempArchive();
            try
            {
                if (latest is null)
                {
                    Directory.CreateDirectory(source);
                    var digest = ProjectFolderSnapshot.Capture(source, archive);
                    ProjectFolderSnapshot.Restore(archive, digest, destination);
                    latest = folder.Publish(archive, digest, null);
                }
                else folder.Restore(latest, archive, destination);
            }
            finally { File.Delete(archive); }
            (draft.PreviousProjectWorkingCopy, draft.PreviousProjectRevision) = (null, null);
            draft.ProjectTree = key;
            draft.ProjectWorkingCopy = id;
            draft.ProjectRevision = latest;
        }
        draft.UseSharedProjectFolder = true;
        return draft;
    }

    /// <summary>Publishes the working copy as a new verified revision (refused when the folder has another one).</summary>
    public EditorSettings SaveRevision(EditorSettings settings, IProjectFolderTree folder)
    {
        if (settings.ProjectTree is null || !settings.UseSharedProjectFolder || settings.ProjectWorkingCopy is not { } id
            || !Directory.Exists(WorkingCopy(id)))
            throw new IOException("Nenhuma pasta escolhida em uso.");
        var draft = settings.Clone();
        var latest = folder.Latest();
        var archive = TempArchive();
        try
        {
            var digest = ProjectFolderSnapshot.Capture(WorkingCopy(id), archive);
            if (latest?.EndsWith("-" + digest + ".zip", StringComparison.Ordinal) == true)
            {
                folder.VerifyRevision(latest, digest);
                draft.ProjectRevision = latest;
            }
            else if (latest != draft.ProjectRevision)
                throw new IOException("A pasta tem outra revisão. Exporte as alterações locais ou recupere a revisão externa.");
            else draft.ProjectRevision = folder.Publish(archive, digest, draft.ProjectRevision);
        }
        finally { File.Delete(archive); }
        return draft;
    }

    /// <summary>Opens a revision of the folder in a new working copy (the current one stays, one tap away).</summary>
    public EditorSettings Restore(EditorSettings settings, IProjectFolderTree folder, string revision)
    {
        if (settings.ProjectTree is null || !settings.UseSharedProjectFolder) throw new IOException("Nenhuma pasta escolhida em uso.");
        var draft = settings.Clone();
        var id = Guid.NewGuid().ToString("N");
        var archive = TempArchive();
        try { folder.Restore(revision, archive, WorkingCopy(id)); }
        finally { File.Delete(archive); }
        (draft.PreviousProjectWorkingCopy, draft.PreviousProjectRevision) = (draft.ProjectWorkingCopy, draft.ProjectRevision);
        draft.ProjectWorkingCopy = id;
        draft.ProjectRevision = revision;
        return draft;
    }

    /// <summary>Back to the previous working copy (and the current one becomes the previous).</summary>
    public EditorSettings? Swap(EditorSettings settings)
    {
        if (!CanSwap(settings)) return null;
        var draft = settings.Clone();
        (draft.ProjectWorkingCopy, draft.PreviousProjectWorkingCopy) = (draft.PreviousProjectWorkingCopy, draft.ProjectWorkingCopy);
        (draft.ProjectRevision, draft.PreviousProjectRevision) = (draft.PreviousProjectRevision, draft.ProjectRevision);
        return draft;
    }

    /// <summary>Internal games (false) or the chosen folder's working copy (true; null when there is none to resume).</summary>
    public EditorSettings? SetActive(EditorSettings settings, bool active)
    {
        if (active && (settings.ProjectWorkingCopy is not { } id || !Directory.Exists(WorkingCopy(id)))) return null;
        var draft = settings.Clone();
        draft.UseSharedProjectFolder = active;
        return draft;
    }
}
