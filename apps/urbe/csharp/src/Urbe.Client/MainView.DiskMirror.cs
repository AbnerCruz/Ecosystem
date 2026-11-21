using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The app is the folder, 1:1 (observation of 15/11/2026: "apaguei algumas pastas no dispositivo e elas permaneceram
/// no app ... eu não quero sincronização, eu quero que seja um pra um"). Coming back to the app reads the folder again;
/// when anything differs from what is shown (a note or file deleted, created or edited outside, a folder removed or
/// added), the vault is shown as it is on disk now.
/// </summary>
public sealed partial class MainView
{
    private bool _refreshingFromDisk;

    /// <summary>Reads the open vault again and shows it when the folder changed outside the app. True when it did.</summary>
    public async Task<bool> RefreshFromDiskAsync()
    {
        if (_refreshingFromDisk || _vault is not { IsConnected: true } vault || _snapshot is null || _library is null || _batch > 0) return false;
        // what is being written stays on the screen: the next save checks the folder and the next return refreshes
        if (Editor.IsVisible && Editor.HasChanges) return false;
        _refreshingFromDisk = true;
        try
        {
            if (await vault.ReloadQuietlyAsync() is not { } snapshot) return false;   // writes never wait for this reading
            IReadOnlyList<string> files;
            IReadOnlyList<string>? folders;
            try
            {
                files = await vault.ListFilePathsAsync();
                folders = await vault.ListFolderPathsAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return false; }
            if (!DiffersFromShown(snapshot, files, folders)) { _snapshot = snapshot; return false; }
            // the note open in the editor, as it was known before the folder is shown again
            var openPath = Editor.IsVisible ? Editor.Current?.Path : null;
            var openText = openPath is null ? null : _contents.GetValueOrDefault(openPath);
            var known = new HashSet<string>(_contents.Keys, StringComparer.Ordinal);
            await ShowVaultAsync(snapshot, listedFiles: files, listedFolders: folders);
            if (openPath is not null && _store?.Get(openPath) is null) FollowOpenNote(openPath, openText, known);
            return true;
        }
        finally { _refreshingFromDisk = false; }
    }

    /// <summary>Whether the folder read now differs from the vault shown: notes, their text, other files, folders.</summary>
    private bool DiffersFromShown(VaultSnapshot snapshot, IReadOnlyList<string> files, IReadOnlyList<string>? folders)
    {
        var notes = snapshot.Documents.Where(d => d.Text is not null && !GraphDocument.IsGraphPath(d.Path)).ToList();
        if (notes.Count != _contents.Count) return true;
        foreach (var n in notes)
            if (!_contents.TryGetValue(n.Path, out var shown) || !string.Equals(shown, n.Text, StringComparison.Ordinal)) return true;
        var noteSet = new HashSet<string>(notes.Select(n => n.Path), StringComparer.Ordinal);
        var others = files.Where(f => !noteSet.Contains(f)).ToHashSet(StringComparer.Ordinal);
        if (!others.SetEquals(_attachments)) return true;
        if (folders is not null && _library is { } library)
        {
            var onDisk = new HashSet<string>(folders, StringComparer.Ordinal);
            if (!onDisk.SetEquals(library.Folders)) return true;
        }
        return false;
    }

    /// <summary>
    /// CST-101: the note open in the editor is not in the folder any more. Moved outside the Urbe (the same text, under
    /// a name that was not there), the editor goes with it; deleted, the person is told at once, the text stays in the
    /// editor and saving it creates the note again (nothing written is lost).
    /// </summary>
    private void FollowOpenNote(string path, string? text, HashSet<string> known)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var moved = text is null ? [] : _contents.Where(p => !known.Contains(p.Key) && string.Equals(p.Value, text, StringComparison.Ordinal)).Select(p => p.Key).ToList();
        if (moved.Count == 1)
        {
            Editor.Renamed(moved[0]);
            Editor.Notify("“" + name + "” foi movida fora do Numa para “" + moved[0] + "”. O editor acompanhou.");
            return;
        }
        Editor.Notify("“" + name + "” foi apagada fora do Numa. O texto continua aqui; se você salvar, a nota é criada de novo.");
    }

    /// <summary>The open note is not in the vault any more (deleted outside): saving it creates it again, with the
    /// person's text, in its folder (created again too when needed).</summary>
    private async Task<bool> RecreateIfGoneAsync(CityNote note)
    {
        if (_store is null || _library is null || _vault is null || _store.Get(note.Path) is not null) return false;
        if (await OnDiskAsync(note.Path) is not null) return false;
        await EnsureFolderAsync(DirOf(note.Path));
        await _vault.CreateTextFileAsync(note.Path, note.Content);
        _library.AddNote(note.Path, note.Content, Now, "editor.recreate");
        _contents[note.Path] = note.Content;
        NotesChanged();
        RefreshNotes();
        await SaveHistoryAsync();
        Editor.Notify("“" + System.IO.Path.GetFileNameWithoutExtension(note.Path) + "” tinha sido apagada fora do Numa e foi criada de novo com o seu texto.");
        return true;
    }
}
