using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Moving a whole folder (UC-54; the 1.8.4 could not): "Mover para" on a folder's ⋯ and a held folder dragged onto
/// another in the Explorer. Every file under it moves in the vault first, one by one, through the same verified moves
/// as a note (MoveTextFileAsync) or an attachment (MoveFileAsync); only then the documents, the houses and the map
/// follow, the neighbourhood keeping its lots and sub-neighbourhoods under its new parent. A folder never goes into
/// itself or a folder below it, nor onto a folder that already has its name. When a move fails midway nothing more is
/// moved: the files already moved stay valid where they went (their houses change neighbourhood as a moved note's do)
/// and the others stay where they were.
/// </summary>
public sealed partial class MainView
{
    private static string DirOf(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    /// <summary>Moves <paramref name="folder"/> and everything under it into <paramref name="parent"/> ("" = the root;
    /// null asks with "Mover para"). True when every file moved.</summary>
    public async Task<bool> MoveFolderAsync(string folder, string? parent = null)
    {
        if (World.City is not { } city || _store is null || folder.Length == 0) return false;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover pasta", NoVaultMessage);
            return false;
        }
        parent ??= await ChooseFolderAsync(except: folder);
        if (parent is null || parent == DirOf(folder)) return false;
        const string Title = "Não foi possível mover a pasta";
        if (parent == folder || parent.StartsWith(folder + "/", StringComparison.Ordinal))
        {
            await Dialogs.AlertAsync(Title, "Uma pasta não pode ir para dentro dela mesma.");
            return false;
        }
        var name = folder[(folder.LastIndexOf('/') + 1)..];
        var target = (parent.Length > 0 ? parent + "/" : "") + name;
        bool Taken(string p) => string.Equals(p, target, StringComparison.OrdinalIgnoreCase)
            || p.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase);
        if (NoteFolders().Any(Taken) || _attachments.Any(Taken) || _contents.Keys.Any(Taken))
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

        // the notes first, then every other file; the folders on the way (empty neighbourhoods too) exist first
        var notes = _store.List().Select(d => d.Path).Where(Inside).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var others = _attachments.Where(p => Inside(p) && _store.Get(p) is null).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var dirs = new HashSet<string>(StringComparer.Ordinal) { folder };
        foreach (var r in city.RegionPaths().Values.Where(Inside)) dirs.Add(r);
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
        var looks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (from, to, note) in moved)
        {
            if (_attachments.Remove(from)) _attachments.Add(to);
            if (!note || _store.Get(from) is not { } doc) continue;
            if (_contents.Remove(from, out var content)) _contents[to] = content;
            LegacyCity.MoveDocument(_store, doc, to);
            looks[from] = to;
            if (Editor.IsVisible && Editor.Current?.Path == from) Editor.Renamed(to);
        }

        // the city: the whole neighbourhood changes parent; otherwise each house as explorer.move / moverAsset does
        if (parent.Length > 0 && city.RegionByPath(parent) is null) city.EnsureFolder(parent);
        if (failure is not null || !city.ApplyFolderReparent(folder, target, now))
            foreach (var (from, to, note) in moved)
            {
                if (DirOf(to) is { Length: > 0 } where && city.RegionByPath(where) is null) city.EnsureFolder(where);
                if (!note) city.ApplyAttachmentMove(from, to, now);
                else if (city.Buildings.Find(b => b.IsNote && b.Path == from) is { } house && _store.Get(to) is { } doc)
                    city.ApplyFolderMove(house, doc, now);
            }
        await CarryLooksAsync(looks);
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        if (failure is null)
        {
            ExplorerState.Forget(folder);
            await RemoveEmptyDirectoriesAsync(dirs);
        }
        else await RemoveEmptyDirectoriesAsync(made);
        RefreshNotes();
        World.InvalidateVisual();
        if (FilePreview.IsVisible && FilePreview.Current is { } shown && moved.Find(m => m.From == shown) is { To: { } shownTo })
            await FilePreview.OpenAsync(shownTo, city.AttachmentBuilding(shownTo));
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
