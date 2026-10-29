using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The folders of the vault in the Explorer: Nova pasta, Renomear, Mover para (UC-54; the 1.8.4 could not), Agrupar em
/// nova pasta and Mover para a lixeira. Every change is made on the vault first (a real folder created, renamed, files
/// moved through the verified moves of a note or an attachment); only then the documents and the folders recorded in
/// .urbe/notas.json follow. A folder never goes into itself or a folder below it, nor onto a folder that already has its
/// name. When a move fails midway nothing more is moved: the files already moved stay valid where they went and the
/// others stay where they were.
/// </summary>
public sealed partial class MainView
{
    private static string DirOf(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    private static string LastName(string folder) => folder[(folder.LastIndexOf('/') + 1)..];

    /// <summary>A path that is <paramref name="target"/> or under it, ignoring case: two cannot share a name on disk.</summary>
    private static bool Taken(string path, string target) => string.Equals(path, target, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>v21UniqueRegionName for a folder: the typed name, with " (2)", " (3)"… when its parent already has one.</summary>
    private string UniqueFolder(string parent, string name)
    {
        string PathOf(string n) => (parent.Length > 0 ? parent + "/" : "") + n;
        bool Used(string p) => NoteFolders().Concat(_attachments).Concat(_contents.Keys).Any(x => Taken(x, p));
        if (!Used(PathOf(name))) return PathOf(name);
        int n = 2;
        while (Used(PathOf(name + " (" + n + ")"))) n++;
        return PathOf(name + " (" + n + ")");
    }

    /// <summary>criarPastaNoDestino: the name dialog, then the folder made in the vault inside <paramref name="parent"/>
    /// ("" = the root). Returns its path, or null.</summary>
    public async Task<string?> CreateFolderAsync(string? parent = null, string? typedName = null)
    {
        if (_library is null) return null;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Nova pasta", NoVaultMessage);
            return null;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Nova pasta", Label: "Nome da pasta", Placeholder: "Ex.: Projetos",
            Hint: NotesWhere(parent ?? ""), Confirm: "Criar pasta"));
        var name = CleanNoteName(typed);
        if (name.Length == 0) return null;
        var folder = UniqueFolder(parent ?? "", name);
        if (!await WriteFolderAsync(folder)) return null;
        ShowLifeNotice("Pasta criada.", Ui.ToastKind.Success);
        return folder;
    }

    /// <summary>Makes the folder in the vault and records it (model.addFolder: it is in Notas even before it has a note).</summary>
    private async Task<bool> WriteFolderAsync(string folder)
    {
        try { await _vault!.CreateDirectoryAsync(folder); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível criar a pasta", ex.Message);
            return false;
        }
        _library?.AddFolder(folder);
        NotesChanged();
        RefreshNotes();
        return true;
    }

    /// <summary>
    /// Renomear a pasta (openRegionEditDialog → confirmRegion's name): the real folder is renamed in the vault first
    /// (IUrbeVaultStorage.RenameFolderAsync, verified file by file); only then the documents follow.
    /// </summary>
    public async Task<bool> RenameFolderAsync(string folder, string? typedName = null)
    {
        if (_library is null || _store is null || folder.Length == 0) return false;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Renomear pasta", NoVaultMessage);
            return false;
        }
        const string Title = "Não foi possível renomear";
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Renomear pasta", Label: "Nome da pasta", Value: LastName(folder), Confirm: "Renomear"));
        var name = CleanNoteName(typed);
        if (name.Length == 0 || name == LastName(folder)) return false;
        var parent = DirOf(folder);
        var target = (parent.Length > 0 ? parent + "/" : "") + name;
        // another folder of the same parent with that name (a change of case only is the same folder)
        if (!string.Equals(target, folder, StringComparison.OrdinalIgnoreCase) && NoteFolders().Concat(_attachments).Concat(_contents.Keys).Any(p => Taken(p, target)))
        {
            await Dialogs.AlertAsync(Title, "Já existe uma pasta com esse nome aqui.");
            return false;
        }
        bool Inside(string path) => path.StartsWith(folder + "/", StringComparison.Ordinal);
        string Moved(string path) => target + path[folder.Length..];
        if (Editor.IsVisible && Editor.Current is { } open && Inside(open.Path) && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync(Title, "A nota aberta tem alterações que não foram salvas no vault.");
            return false;
        }
        try { await _vault!.RenameFolderAsync(folder, target); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync(Title, ex.Message);
            return false;
        }
        var moves = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var doc in _store.List().Where(d => Inside(d.Path)).ToList())
        {
            var to = Moved(doc.Path);
            NoteLibrary.MoveDocument(_store, doc, to);
            if (_contents.Remove(doc.Path, out var content)) _contents[to] = content;
            moves[doc.Path] = to;
        }
        foreach (var p in _attachments.Where(Inside).ToList()) { _attachments.Remove(p); _attachments.Add(Moved(p)); }
        _library.MoveFolder(folder, target);
        AreaFollows(folder, target);
        ExplorerState.Forget(folder);
        if (Editor.IsVisible && Editor.Current is { } current && moves.TryGetValue(current.Path, out var renamed)) Editor.Renamed(renamed);
        NotesChanged();
        RefreshNotes();
        await RetargetGraphsAsync([(folder, target)]);
        ShowLifeNotice("Pasta renomeada para “" + name + "”.");
        return true;
    }

    /// <summary>Moves <paramref name="folder"/> and everything under it into <paramref name="parent"/> ("" = the root;
    /// null asks with "Mover para"). True when every file moved.</summary>
    public async Task<bool> MoveFolderAsync(string folder, string? parent = null)
    {
        if (_library is null || _store is null || folder.Length == 0) return false;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover pasta", NoVaultMessage);
            return false;
        }
        parent ??= await ChooseFolderAsync(except: folder, current: DirOf(folder));
        if (parent is null || parent == DirOf(folder)) return false;
        const string Title = "Não foi possível mover a pasta";
        if (parent == folder || parent.StartsWith(folder + "/", StringComparison.Ordinal))
        {
            await Dialogs.AlertAsync(Title, "Uma pasta não pode ir para dentro dela mesma.");
            return false;
        }
        var name = LastName(folder);
        var target = (parent.Length > 0 ? parent + "/" : "") + name;
        if (NoteFolders().Concat(_attachments).Concat(_contents.Keys).Any(p => Taken(p, target)))
        {
            await Dialogs.AlertAsync(Title, "Já existe “" + name + "” em " + (parent.Length > 0 ? "“" + parent + "”" : "Raiz") + ".");
            return false;
        }
        bool Inside(string p) => p.StartsWith(folder + "/", StringComparison.Ordinal);
        string Moved(string p) => p == folder ? target : target + p[folder.Length..];
        if (Editor.IsVisible && Editor.Current is { } open && Inside(open.Path) && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync(Title, "A nota aberta tem alterações que não foram salvas no vault.");
            return false;
        }

        // the notes first, then every other file; the folders on the way (empty ones too) exist first
        var notes = _store.List().Select(d => d.Path).Where(Inside).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var others = _attachments.Where(p => Inside(p) && _store.Get(p) is null).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var dirs = new HashSet<string>(StringComparer.Ordinal) { folder };
        foreach (var sub in _library.Folders.Where(Inside)) dirs.Add(sub);
        foreach (var p in notes.Concat(others))
            for (var d = DirOf(p); d.Length > folder.Length; d = DirOf(d)) dirs.Add(d);
        var made = new List<string>();
        try
        {
            foreach (var d in dirs.OrderBy(d => d.Count(c => c == '/')).ThenBy(d => d, StringComparer.Ordinal))
            {
                await _vault!.CreateDirectoryAsync(Moved(d));
                made.Add(Moved(d));
            }
        }
        catch (Exception ex)
        {
            await RemoveEmptyDirectoriesAsync(made);
            await Dialogs.AlertAsync(Title, ex.Message);
            return false;
        }

        var moved = new List<(string From, string To, bool Note)>();
        (string Path, Exception Error)? failure = null;
        foreach (var from in notes.Concat(others))
        {
            var to = Moved(from);
            bool note = _store.Get(from) is not null;
            try
            {
                if (note || IUrbeVaultStorage.TextDocument(from)) await _vault!.MoveTextFileAsync(from, to);
                else await _vault!.MoveFileAsync(from, to);
            }
            catch (Exception ex)
            {
                failure = (from, ex);
                break;
            }
            moved.Add((from, to, note));
        }

        // the documents follow the files that moved
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var (from, to, note) in moved)
        {
            if (_attachments.Remove(from)) _attachments.Add(to);
            if (!note || _store.Get(from) is not { } doc) continue;
            if (_contents.Remove(from, out var content)) _contents[to] = content;
            NoteLibrary.MoveDocument(_store, doc, to);
            _library.Edited(to, doc.Content, now);
            if (Editor.IsVisible && Editor.Current?.Path == from) Editor.Renamed(to);
        }
        if (failure is null)
        {
            _library.MoveFolder(folder, target);
        AreaFollows(folder, target);
            ExplorerState.Forget(folder);
            await RemoveEmptyDirectoriesAsync(dirs);
        }
        else
        {
            // what moved is where it went; the rest of the folder stays where it was
            foreach (var d in made) _library.AddFolder(d);
            await RemoveEmptyDirectoriesAsync(made.Where(d => !moved.Any(m => m.To.StartsWith(d + "/", StringComparison.Ordinal))));
        }
        NotesChanged();
        RefreshNotes();
        // the graphs follow what moved: the whole folder, or the files that went before a failure
        await RetargetGraphsAsync(failure is null ? [(folder, target)] : [.. moved.Select(m => (m.From, m.To))]);
        if (FilePreview.IsVisible && FilePreview.Current is { } shown && moved.Find(m => m.From == shown) is { To: { } shownTo })
            await FilePreview.OpenAsync(shownTo);
        if (failure is { } f)
        {
            int total = notes.Count + others.Count;
            await Dialogs.AlertAsync(Title, "“" + f.Path + "” não foi movido: " + f.Error.Message + " " + moved.Count + " de " + total
                + " arquivos já estão em “" + target + "”; os outros continuam em “" + folder + "”. Nenhum arquivo foi perdido.");
            return false;
        }
        ShowLifeNotice("Pasta movida para " + (parent.Length > 0 ? "“" + parent + "”" : "a raiz") + ".");
        return true;
    }

    /// <summary>What a drop on a folder of the Explorer moves: the held folders first, each with all that is in it, then
    /// the notes that were not inside them.</summary>
    private async Task MoveSelectionToAsync(IReadOnlyList<string> selection, string target)
    {
        var folders = selection.Where(x => x.StartsWith(NotesExplorer.FolderPrefix, StringComparison.Ordinal))
            .Select(x => x[NotesExplorer.FolderPrefix.Length..]).Where(f => f.Length > 0).ToList();
        var notes = selection.Where(x => !x.StartsWith(NotesExplorer.FolderPrefix, StringComparison.Ordinal)
            && !folders.Any(f => x.StartsWith(f + "/", StringComparison.Ordinal))).ToList();
        foreach (var f in folders)
            if (!await MoveFolderAsync(f, target)) return;
        if (notes.Count > 0) await MoveNotesToAsync(notes, target);
    }

    /// <summary>
    /// v20AgruparSelecao: at least two notes; the folder's name; the folder made in the vault inside the folder shown in
    /// Notas; then each note moves into it (explorer.move).
    /// </summary>
    public async Task GroupIntoFolderAsync(IReadOnlyList<string> paths, string? typedName = null)
    {
        if (_library is null || _store is null) return;
        if (paths.Count < 2) { ShowLifeNotice("Selecione pelo menos dois arquivos."); return; }
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Agrupar em nova pasta", NoVaultMessage);
            return;
        }
        var name = CleanNoteName(typedName ?? await Dialogs.PromptAsync(new PromptOptions("Agrupar em nova pasta", Label: "Nome da pasta", Value: "Nova pasta", Confirm: "Criar pasta")));
        if (name.Length == 0) return;
        var folder = UniqueFolder(Explorer.CurrentFolder ?? "", name);
        if (!await WriteFolderAsync(folder)) return;
        var notes = paths.Where(p => _store.Get(p) is not null).ToList();
        await MoveNotesToAsync(notes, folder);
        int moved = notes.Count(p => _store.Get(p) is null);
        Explorer.ClearSelection();
        RefreshNotes();
        ShowLifeNotice("Pasta criada com " + moved + " itens.");
    }

    /// <summary>
    /// explorer.deleteFolder (operations.js removeFolder): every note under the folder goes to the trash as a note
    /// deleted alone (the trash written and verified before each file leaves); then the folder and the ones below it
    /// leave the record and the emptied folders are removed, deepest first, like the sync's FS.apagarPasta. Deliberate
    /// deviation for the user's data: the 1.8.4 sync also deletes the attached files (images, PDFs…) of that folder for
    /// good; here any file that is not a note stays in the vault, and a folder that still holds something stays too.
    /// If a note cannot go to the trash, nothing more is done.
    /// </summary>
    private async Task<bool> DeleteFolderAsync(string folder)
    {
        if (_library is null || _store is null || _vault is null) return false;
        var docs = NoteLibrary.FolderDocuments(_store, folder);
        return await InBatchAsync(docs.Count, () => DeleteFolderNotesAsync(folder, docs));
    }

    private async Task<bool> DeleteFolderNotesAsync(string folder, IReadOnlyList<UrbeDocument> docs)
    {
        if (_library is null || _vault is null) return false;
        var dirs = new HashSet<string>(StringComparer.Ordinal) { folder };
        foreach (var f in _library.Folders.Where(f => f.StartsWith(folder + "/", StringComparison.Ordinal))) dirs.Add(f);
        foreach (var d in docs)
            for (var dir = DirOf(d.Path); dir.Length > folder.Length; dir = DirOf(dir)) dirs.Add(dir);
        if (docs.Count > 0 && !await TrashNotesAsync(docs)) return false;
        // a folder that still holds a file that is not a note stays, as on the disk
        bool Holds(string dir) => _attachments.Any(p => p.StartsWith(dir + "/", StringComparison.Ordinal));
        foreach (var dir in dirs) if (!Holds(dir)) { _library.RemoveFolder(dir); AreaFollows(dir, null); }
        ExplorerState.Forget(folder); // explorer.forgetFolder
        NotesChanged();
        await RemoveEmptyDirectoriesAsync(dirs.Where(d => !Holds(d)));
        RefreshNotes();
        return true;
    }

    /// <summary>FS.apagarPasta for the emptied folders, deepest first; a folder that still holds anything stays.</summary>
    private async Task RemoveEmptyDirectoriesAsync(IEnumerable<string> dirs)
    {
        if (_vault is null) return;
        foreach (var dir in dirs.Distinct(StringComparer.Ordinal).OrderByDescending(d => d.Count(c => c == '/')).ThenBy(d => d, StringComparer.Ordinal).ToList())
        {
            try { await _vault.RemoveEmptyDirectoryAsync(dir); }
            catch (Exception) { /* removeEntry errors are ignored, as the sync does */ }
        }
    }
}
