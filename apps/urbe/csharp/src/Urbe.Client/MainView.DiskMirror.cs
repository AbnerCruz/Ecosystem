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
            await ShowVaultAsync(snapshot, listedFiles: files, listedFolders: folders);
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
}
