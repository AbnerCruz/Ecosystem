namespace Urbe.Core;

/// <summary>
/// CST-040/041: the files a <c>[[…]]</c> or <c>![[…]]</c> can point at besides notes — images, PDFs, audio, video, code,
/// pages, graphs, anything in the vault. The link is the file's name with its extension, as Obsidian writes it, or its
/// path when two files share the name. Whether the editor can show the file inside the note is a separate question
/// (the embed says so); a link always opens the file.
/// </summary>
public static class WikiTargets
{
    /// <summary>
    /// The files matching <paramref name="query"/> (case and accents ignored): names that start with it, then names with
    /// a word that starts with it, then anywhere in the path; ties by name. Empty query: the first by name.
    /// </summary>
    public static IReadOnlyList<string> Files(IEnumerable<string> files, string? query, int max)
    {
        var q = Fold(query ?? "").Trim();
        var ranked = new List<(int Rank, string Name, string Path)>();
        foreach (var path in files)
        {
            var name = Fold(NameOf(path));
            int rank = q.Length == 0 ? 0
                : name.StartsWith(q, StringComparison.Ordinal) ? 0
                : name.Contains(" " + q, StringComparison.Ordinal) || name.Contains("-" + q, StringComparison.Ordinal) || name.Contains("_" + q, StringComparison.Ordinal) ? 1
                : Fold(path).Contains(q, StringComparison.Ordinal) ? 2 : -1;
            if (rank >= 0) ranked.Add((rank, name, path));
        }
        return [.. ranked.OrderBy(r => r.Rank).ThenBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Path, StringComparer.Ordinal)
            .Take(Math.Max(0, max)).Select(r => r.Path)];
    }

    /// <summary>The text inside the brackets for a file: its name, or its path when another file has the same name.</summary>
    public static string LinkName(string path, IEnumerable<string> files)
    {
        var name = NameOf(path);
        int same = files.Count(f => string.Equals(NameOf(f), name, StringComparison.OrdinalIgnoreCase));
        return same > 1 ? path : name;
    }

    /// <summary>The file a link names: its exact path, or the one file with that name (case ignored); a #heading is ignored. Null when none or several.</summary>
    public static string? ResolveFile(string name, IEnumerable<string> files)
    {
        var target = name.Split('#')[0].Split('|')[0].Trim().TrimStart('/');
        if (target.Length == 0) return null;
        var list = files as IReadOnlyCollection<string> ?? [.. files];
        if (list.FirstOrDefault(f => string.Equals(f, target, StringComparison.Ordinal)) is { } exact) return exact;
        var byName = list.Where(f => string.Equals(NameOf(f), target, StringComparison.OrdinalIgnoreCase) || string.Equals(f, target, StringComparison.OrdinalIgnoreCase)).ToList();
        return byName.Count == 1 ? byName[0] : null;
    }

    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string Fold(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
