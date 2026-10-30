namespace Urbe.Core;

/// <summary>
/// CST-101: the links to notes and files that were renamed or moved follow them (as Obsidian keeps them): a link
/// written by name (<c>[[Rascunho]]</c>) gets the new name, one written by path (<c>[[Pasta/Rascunho]]</c>) the new
/// path, each keeping its form (with or without the extension). A name that more than one file of the vault answers to
/// is left alone: which one it meant cannot be known.
/// </summary>
public static class LinkRename
{
    /// <param name="moves">Each file's old and new path.</param>
    /// <param name="files">Every file of the vault as it is now (notes and attachments).</param>
    public static Func<string, string?> Retarget(IReadOnlyList<(string From, string To)> moves, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(moves);
        ArgumentNullException.ThrowIfNull(files);
        var moved = moves.Where(m => m.From != m.To).ToList();
        var movedTo = moved.Select(m => m.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // how many files answer to each name (the ones not moved, plus each moved file under its old name)
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        void Count(string path)
        {
            foreach (var key in new[] { Key(ArtifactModel.TitleFromPath(path)), Key(FileName(path)) }.Distinct())
                names[key] = names.GetValueOrDefault(key) + 1;
        }
        foreach (var path in files.Where(f => !movedTo.Contains(f))) Count(path);
        foreach (var (from, _) in moved) Count(from);

        var byPath = new Dictionary<string, (string From, string To)>(StringComparer.Ordinal);
        var byName = new Dictionary<string, (string From, string To)>(StringComparer.Ordinal);
        foreach (var move in moved)
        {
            byPath[Key(move.From)] = move;
            byPath[Key(ArtifactModel.WithoutNoteExtension(move.From))] = move;
            foreach (var key in new[] { Key(ArtifactModel.TitleFromPath(move.From)), Key(FileName(move.From)) }.Distinct())
                if (names.GetValueOrDefault(key) == 1) byName[key] = move;
        }
        return target =>
        {
            var key = Key(target);
            if (target.Contains('/'))
            {
                if (!byPath.TryGetValue(key, out var move)) return null;
                return key == Key(move.From) ? move.To : ArtifactModel.WithoutNoteExtension(move.To);
            }
            if (!byName.TryGetValue(key, out var named)) return null;
            // written with its extension ([[Nota.md]], [[foto.png]]) or by its title
            return key == Key(FileName(named.From)) && key != Key(ArtifactModel.TitleFromPath(named.From))
                ? FileName(named.To) : ArtifactModel.TitleFromPath(named.To);
        };
    }

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string Key(string value) => value.Trim().ToLowerInvariant();
}
