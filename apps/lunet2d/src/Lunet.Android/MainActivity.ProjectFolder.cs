using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Uri = Android.Net.Uri;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private const int ProjectFolderRequestCode = 4138;
    private bool _folderPickerFromSetup;
    private bool _folderOperationInProgress;

    private string PrivateProjects => Path.Combine(FilesDir!.AbsolutePath, "Projects");
    private string WorkingCopy(string id) => Path.Combine(FilesDir!.AbsolutePath, "ProjectWorkingCopies", id);

    private void LoadProjectFolder()
    {
        if (_settings.DirectProjectTree is not null)
        {
            _store = ShellActivity.ProjectStoreFor(this, _settings);
            return;
        }
        var copy = _settings.ProjectWorkingCopy;
        _store = new ProjectStore(_settings.UseSharedProjectFolder && copy is not null && Directory.Exists(WorkingCopy(copy))
            ? WorkingCopy(copy) : PrivateProjects);
    }

    private void ShowProjectFolder()
    {
        if (_project is not null || _folderOperationInProgress) return;
        var active = _settings.UseSharedProjectFolder;
        var options = new List<(string Label, Action Action)>
        {
            ("Escolher ou reconectar pasta", () => PickProjectFolder(fromSetup: false))
        };
        if (active)
        {
            options.Add(("Gravar revisão na pasta", SaveProjectFolderRevision));
            options.Add(("Recuperar revisão da pasta", ChooseProjectFolderRevision));
            if (_settings.PreviousProjectWorkingCopy is { } previous && Directory.Exists(WorkingCopy(previous)))
                options.Add(("Alternar com a cópia local anterior", SwapProjectWorkingCopy));
            options.Add(("Abrir projetos internos", () => SetSharedProjectFolderActive(false)));
            options.Add(("Liberar acesso à pasta", DisconnectProjectFolder));
        }
        else if (_settings.ProjectTree is not null)
            options.Add(("Retomar cópia de trabalho da pasta escolhida", () => SetSharedProjectFolderActive(true)));
        var title = active ? "Pasta escolhida e cópia de trabalho" : "Armazenamento interno";
        ShowActionMenu(title, options.ToArray());
    }

    private void SetSharedProjectFolderActive(bool active)
    {
        if (active && (_settings.ProjectWorkingCopy is not { } id || !Directory.Exists(WorkingCopy(id))))
        {
            PickProjectFolder(fromSetup: false);
            return;
        }
        var draft = _settings.Clone();
        draft.UseSharedProjectFolder = active;
        if (!TrySaveWorkspaceSettings(draft)) return;
        LoadProjectFolder();
        _projectFilter = "";
        ShowHome();
    }

    private void PickProjectFolder(bool fromSetup)
    {
        if (_folderOperationInProgress || _project is not null) return;
        _folderPickerFromSetup = fromSetup;
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission |
            ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantPrefixUriPermission);
        try { StartActivityForResult(intent, ProjectFolderRequestCode); }
        catch (ActivityNotFoundException)
        {
            _folderPickerFromSetup = false;
            Toast.MakeText(this, "O seletor de pastas está indisponível.", ToastLength.Long)?.Show();
            if (fromSetup) ShowFirstUse();
        }
    }

    private bool HandleProjectFolderResult(int request, Result result, Intent? data)
    {
        if (request != ProjectFolderRequestCode) return false;
        var fromSetup = _folderPickerFromSetup;
        _folderPickerFromSetup = false;
        if (result != Result.Ok || data?.Data is not Uri tree)
        {
            if (fromSetup) ShowFirstUse();
            return true;
        }

        var message = fromSetup
            ? "Se houver uma revisão do Lunet, ela será aberta em uma cópia de trabalho. Em uma pasta nova, o Lunet grava uma revisão dos projetos internos e preserva o original."
            : "Uma revisão existente será aberta em uma cópia de trabalho. Em uma pasta nova, o Lunet copia e verifica os projetos atuais antes de trocar a origem. As cópias anteriores são preservadas.";
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Usar esta pasta?")!.SetMessage(message)!
            .SetPositiveButton("Usar pasta", (_, _) => ConnectProjectFolder(tree, data.Flags, fromSetup))!
            .SetNegativeButton("Cancelar", (_, _) => { if (fromSetup) ShowFirstUse(); })!.Create()!;
        dialog.CancelEvent += (_, _) => { if (fromSetup) ShowFirstUse(); };
        dialog.Show();
        return true;
    }

    private async void ConnectProjectFolder(Uri tree, ActivityFlags resultFlags, bool fromSetup)
    {
        var resolver = ContentResolver!;
        var draft = _settings.Clone();
        var source = _store.RootDirectory;
        var key = tree.ToString();
        var reconnect = draft.ProjectTree == key && draft.ProjectWorkingCopy is { } oldId && Directory.Exists(WorkingCopy(oldId));
        bool alreadyGranted = resolver.PersistedUriPermissions?.Any(p => p.Uri?.Equals(tree) == true && p.IsReadPermission && p.IsWritePermission) == true;
        var previousTree = draft.ProjectTree;
        bool committed = false;
        var prepared = await RunFolderOperation("Verificando pasta e projetos…", () =>
        {
            if (!alreadyGranted)
                resolver.TakePersistableUriPermission(tree,
                    resultFlags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission));
            var folder = new AndroidProjectFolder(resolver, tree);
            var latest = folder.Latest();
            if (reconnect) return draft;

            // A different folder must not make unpublished work from the previous connection unreachable.
            if (draft.ProjectWorkingCopy is { } previousId && Directory.Exists(WorkingCopy(previousId)))
            {
                var previousArchive = TempProjectArchive();
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
            var archive = TempProjectArchive();
            try
            {
                if (latest is null)
                {
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
            return draft;
        });
        if (prepared is not null && !IsDestroyed && !IsFinishing)
        {
            prepared.UseSharedProjectFolder = true;
            if (fromSetup) prepared.FirstUseStep = 1;
            if (TrySaveWorkspaceSettings(prepared))
            {
                committed = true;
                LoadProjectFolder();
                ShowHome();
            }
        }
        if (!committed && !alreadyGranted) ReleaseProjectGrant(resolver, tree);
        else if (committed && previousTree is not null && previousTree != key)
        {
            using var oldTree = Uri.Parse(previousTree)!;
            ReleaseProjectGrant(resolver, oldTree);
        }
        if (fromSetup && !IsDestroyed && !IsFinishing) ShowFirstUse();
    }

    private async void SaveProjectFolderRevision()
    {
        if (_settings.ProjectTree is not { } key || !_settings.UseSharedProjectFolder ||
            _settings.ProjectWorkingCopy is not { } id || _store.RootDirectory != WorkingCopy(id)) return;
        var draft = _settings.Clone();
        var source = _store.RootDirectory;
        var resolver = ContentResolver!;
        var result = await RunFolderOperation("Gravando e conferindo a revisão…", () =>
        {
            using var tree = Uri.Parse(key)!;
            var folder = new AndroidProjectFolder(resolver, tree);
            var latest = folder.Latest();
            var archive = TempProjectArchive();
            try
            {
                var digest = ProjectFolderSnapshot.Capture(source, archive);
                if (latest?.EndsWith("-" + digest + ".zip", StringComparison.Ordinal) == true)
                {
                    folder.VerifyRevision(latest, digest);
                    draft.ProjectRevision = latest;
                }
                else if (latest != draft.ProjectRevision)
                    throw new IOException("A pasta tem outra revisão. Exporte as alterações locais ou recupere a revisão externa pelo menu de armazenamento.");
                else draft.ProjectRevision = folder.Publish(archive, digest, draft.ProjectRevision);
            }
            finally { File.Delete(archive); }
            return draft;
        });
        if (result is not null && !IsDestroyed && !IsFinishing && TrySaveWorkspaceSettings(result))
            Toast.MakeText(this, "Revisão verificada na pasta escolhida.", ToastLength.Long)?.Show();
    }

    private void DisconnectProjectFolder()
    {
        if (_settings.ProjectTree is not { } key) return;
        using var tree = Uri.Parse(key)!;
        ReleaseProjectGrant(ContentResolver!, tree);
        Toast.MakeText(this, "Acesso liberado. A cópia de trabalho ainda pode ser usada neste aparelho.", ToastLength.Long)?.Show();
        ShowHome();
    }

    private async void ChooseProjectFolderRevision()
    {
        if (_settings.ProjectTree is not { } key || !_settings.UseSharedProjectFolder) return;
        var resolver = ContentResolver!;
        var revisions = await RunFolderOperation("Consultando revisões…", () =>
        {
            using var tree = Uri.Parse(key)!;
            return new AndroidProjectFolder(resolver, tree).Revisions();
        });
        if (revisions is null || IsDestroyed || IsFinishing) return;
        if (revisions.Count == 0)
        {
            Toast.MakeText(this, "A pasta ainda não tem uma revisão.", ToastLength.Long)?.Show();
            return;
        }
        var actions = revisions.Select((revision, index) =>
        {
            var label = $"Revisão {revisions.Count - index} · {revision[^68..^60]}";
            return (label, (Action)(() => RestoreProjectFolder(revision)));
        }).ToArray();
        ShowActionMenu("Recuperar em outra cópia de trabalho", actions);
    }

    private async void RestoreProjectFolder(string revision)
    {
        if (_settings.ProjectTree is not { } key || !_settings.UseSharedProjectFolder) return;
        var draft = _settings.Clone();
        var resolver = ContentResolver!;
        var recovered = await RunFolderOperation("Carregando revisão…", () =>
        {
            using var tree = Uri.Parse(key)!;
            var folder = new AndroidProjectFolder(resolver, tree);
            var id = Guid.NewGuid().ToString("N");
            var archive = TempProjectArchive();
            try { folder.Restore(revision, archive, WorkingCopy(id)); }
            finally { File.Delete(archive); }
            (draft.PreviousProjectWorkingCopy, draft.PreviousProjectRevision) =
                (draft.ProjectWorkingCopy, draft.ProjectRevision);
            draft.ProjectWorkingCopy = id;
            draft.ProjectRevision = revision;
            return draft;
        });
        if (recovered is not null && !IsDestroyed && !IsFinishing && TrySaveWorkspaceSettings(recovered))
        {
            LoadProjectFolder();
            ShowHome();
        }
    }

    private void SwapProjectWorkingCopy()
    {
        if (_settings.PreviousProjectWorkingCopy is not { } id || !Directory.Exists(WorkingCopy(id))) return;
        var draft = _settings.Clone();
        (draft.ProjectWorkingCopy, draft.PreviousProjectWorkingCopy) = (draft.PreviousProjectWorkingCopy, draft.ProjectWorkingCopy);
        (draft.ProjectRevision, draft.PreviousProjectRevision) = (draft.PreviousProjectRevision, draft.ProjectRevision);
        if (!TrySaveWorkspaceSettings(draft)) return;
        LoadProjectFolder();
        ShowHome();
    }

    private string TempProjectArchive() => Path.Combine(CacheDir!.AbsolutePath, "lunet-" + Guid.NewGuid().ToString("N") + ".zip");

    private async Task<T?> RunFolderOperation<T>(string title, Func<T> operation) where T : class
    {
        if (_folderOperationInProgress) return null;
        _folderOperationInProgress = true;
        var progress = new ProgressBar(this);
        progress.SetPadding(Dp(24), Dp(20), Dp(24), Dp(20));
        using var dialog = new AlertDialog.Builder(this)!.SetTitle(title)!.SetView(progress)!.SetCancelable(false)!.Show()!;
        try { return await Task.Run(operation); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or Java.Lang.Exception)
        {
            if (!IsDestroyed && !IsFinishing)
                Toast.MakeText(this, "Pasta indisponível. Projetos locais preservados. " + ex.Message, ToastLength.Long)?.Show();
            return null;
        }
        finally
        {
            _folderOperationInProgress = false;
            if (!IsDestroyed) dialog.Dismiss();
        }
    }

    private static void ReleaseProjectGrant(ContentResolver resolver, Uri tree)
    {
        try { resolver.ReleasePersistableUriPermission(tree, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission); }
        catch (Java.Lang.SecurityException) { }
    }
}
