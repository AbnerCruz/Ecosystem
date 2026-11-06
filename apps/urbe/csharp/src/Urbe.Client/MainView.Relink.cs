using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-101: renaming or moving a note, a file or a folder broke every link to it (Obsidian keeps them), wiki links and
/// Markdown links alike; a note that moved has its relative Markdown links fixed too. Each note that
/// links to what moved is rewritten through the same verified save as the editor's (LinkRename): an open note is
/// saved first and shown with its links updated.
/// </summary>
public sealed partial class MainView
{
    /// <param name="done">What the move itself tells ("Pasta renomeada…"): shown together with the count of notes whose
    /// links were updated, so neither notice hides the other (CST-196).</param>
    private async Task RelinkAsync(IReadOnlyList<(string From, string To)> moves, string? done = null)
    {
        if (_store is null || _vault is null || !VaultWritable || moves.Count == 0) { Tell(done); return; }
        var all = _store.List().Select(d => d.Path).Concat(_attachments).Distinct(StringComparer.Ordinal).ToList();
        var known = all.ToHashSet(StringComparer.Ordinal);
        // a folder that moved: each file inside it
        var files = new List<(string From, string To)>();
        foreach (var (from, to) in moves)
        {
            if (known.Contains(to)) { files.Add((from, to)); continue; }
            var inside = to + "/";
            files.AddRange(all.Where(p => p.StartsWith(inside, StringComparison.Ordinal)).Select(p => (from + "/" + p[inside.Length..], p)));
        }
        if (files.Count == 0) { Tell(done); return; }
        if (_vault.AtlasVaultSessionKey is { } vaultIdentity)
        {
            var vaultKey = ReaderSessionCatalog.VaultKey(vaultIdentity);
            foreach (var (from, to) in files) ReadingState.Move(vaultKey, from, to);
        }
        foreach (var (from, to) in files)
        {
            Editor.Pinned.RetargetSource(from, to); Editor.Split.RetargetSource(from, to);
            foreach (var reader in new[] { FilePreview, Editor.Pinned.Preview, Editor.Split.Preview })
                await reader.RetargetSourceAsync(from, to);
        }
        var retarget = LinkRename.Retarget(files, all);
        var cameFrom = files.ToDictionary(f => f.To, f => f.From, StringComparer.Ordinal);
        int changed = 0;
        var failed = new List<string>();
        foreach (var doc in _store.List().Where(d => d.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var text = _contents.GetValueOrDefault(doc.Path, doc.Content);
            if (!text.Contains("[[", StringComparison.Ordinal) && !text.Contains("](", StringComparison.Ordinal)) continue;
            bool editing = Editor.IsVisible && Editor.Current?.Path == doc.Path;
            if (editing && Editor.HasChanges)
            {
                if (!await Editor.SaveAsync()) { failed.Add(NoteLibrary.FullNoteName(doc.Path)); continue; }
                text = Editor.Current!.Content;
            }
            // wiki links, then Markdown links ([texto](Pasta/Nota.md)), each in the form it was written
            var next = LinkRename.RewriteMarkdownLinks(DocumentModel.RewriteLinks(text, retarget), cameFrom.GetValueOrDefault(doc.Path, doc.Path), doc.Path, files, all);
            if (next == text) continue;
            try
            {
                await SaveNoteAsync(new CityNote(doc.Path, next), expectedUndoContent: null);
                if (editing) Editor.Open(new CityNote(doc.Path, next));
                changed++;
            }
            catch (Exception) { failed.Add(NoteLibrary.FullNoteName(doc.Path)); }
        }
        var lead = done is null ? "" : done + " ";
        if (failed.Count > 0)
            ShowLifeNotice(lead + "Os links não foram atualizados em " + string.Join(", ", failed.Select(n => "“" + n + "”")) + ": a nota não pôde ser salva.", Ui.ToastKind.Warning);
        else if (changed > 0)
            ShowLifeNotice(lead + (changed == 1 ? "Links atualizados em 1 nota." : "Links atualizados em " + changed + " notas."));
        else Tell(done);
        if (changed > 0) NotesChanged();
    }

    private void Tell(string? done) { if (done is not null) ShowLifeNotice(done); }
}
