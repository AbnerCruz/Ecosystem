using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-101: renaming or moving a note, a file or a folder broke every link to it (Obsidian keeps them). Each note that
/// links to what moved is rewritten through the same verified save as the editor's (LinkRename): an open note is
/// saved first and shown with its links updated.
/// </summary>
public sealed partial class MainView
{
    private async Task RelinkAsync(IReadOnlyList<(string From, string To)> moves)
    {
        if (_store is null || _vault is null || !VaultWritable || moves.Count == 0) return;
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
        if (files.Count == 0) return;
        var retarget = LinkRename.Retarget(files, all);
        int changed = 0;
        var failed = new List<string>();
        foreach (var doc in _store.List().Where(d => d.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var text = _contents.GetValueOrDefault(doc.Path, doc.Content);
            if (!text.Contains("[[", StringComparison.Ordinal)) continue;
            bool editing = Editor.IsVisible && Editor.Current?.Path == doc.Path;
            if (editing && Editor.HasChanges)
            {
                if (!await Editor.SaveAsync()) { failed.Add(NoteLibrary.FullNoteName(doc.Path)); continue; }
                text = Editor.Current!.Content;
            }
            var next = DocumentModel.RewriteLinks(text, retarget);
            if (next == text) continue;
            try
            {
                await SaveNoteAsync(new CityNote(doc.Path, next), expectedUndoContent: null);
                if (editing) Editor.Open(new CityNote(doc.Path, next));
                changed++;
            }
            catch (Exception) { failed.Add(NoteLibrary.FullNoteName(doc.Path)); }
        }
        if (failed.Count > 0)
            ShowLifeNotice("Os links não foram atualizados em " + string.Join(", ", failed.Select(n => "“" + n + "”")) + ": a nota não pôde ser salva.", Ui.ToastKind.Warning);
        else if (changed > 0)
            ShowLifeNotice(changed == 1 ? "Links atualizados em 1 nota." : "Links atualizados em " + changed + " notas.");
        if (changed > 0) NotesChanged();
    }
}
