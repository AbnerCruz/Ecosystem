namespace Lunet.Client.Shell;

/// <summary>What the palette lists, chosen by the query's first character (VS Code / Obsidian prefixes).</summary>
public enum PaletteMode
{
    /// <summary>Commands (default; "&gt;" also selects it).</summary>
    Commands,
    /// <summary>"@": symbols of the open file (outline).</summary>
    Outline,
    /// <summary>":": go to a line number.</summary>
    Line,
    /// <summary>"#": symbols of the whole game.</summary>
    Symbols,
    /// <summary>"/": find text in the open file.</summary>
    Find,
}

/// <summary>One row of the palette: what it says, a hint, and what a tap does.</summary>
public sealed record PaletteItem(string Title, string Detail, Action Run, string? Key = null);

/// <summary>
/// The command palette of the shell (LUNET-536 M3; study: VS Code prefixes, fuzzy match with recents first —
/// Positron/Obsidian). Pure logic: parsing, matching and ranking; the view only shows the result.
/// </summary>
public static class CommandPalette
{
    /// <summary>The mode a query selects and the text left to match.</summary>
    public static (PaletteMode Mode, string Term) Parse(string query)
    {
        query ??= "";
        if (query.Length == 0) return (PaletteMode.Commands, "");
        return query[0] switch
        {
            '>' => (PaletteMode.Commands, query[1..].Trim()),
            '@' => (PaletteMode.Outline, query[1..].Trim()),
            ':' => (PaletteMode.Line, query[1..].Trim()),
            '#' => (PaletteMode.Symbols, query[1..].Trim()),
            '/' => (PaletteMode.Find, query[1..]),
            _ => (PaletteMode.Commands, query.Trim()),
        };
    }

    /// <summary>
    /// Fuzzy score of <paramref name="term"/> in <paramref name="candidate"/> (letters in order, case-insensitive),
    /// or null when it does not match. Higher is better: consecutive letters, word starts and an early first hit.
    /// </summary>
    public static int? Score(string term, string candidate)
    {
        if (term.Length == 0) return 0;
        int score = 0, last = -2, at = 0;
        foreach (var ch in term)
        {
            if (char.IsWhiteSpace(ch)) continue;
            int found = -1;
            for (int i = at; i < candidate.Length; i++)
                if (char.ToLowerInvariant(candidate[i]) == char.ToLowerInvariant(ch)) { found = i; break; }
            if (found < 0) return null;
            score += 1;
            if (found == last + 1) score += 5;                                                  // consecutive
            if (found == 0 || !char.IsLetterOrDigit(candidate[found - 1]) || char.IsUpper(candidate[found]) && found > 0 && char.IsLower(candidate[found - 1])) score += 8; // word start
            last = found;
            at = found + 1;
        }
        return score - Math.Min(candidate.Length / 8, 5);
    }

    /// <summary>
    /// The items matching the term: with no term, recent ones first then the rest in their order; with a term, the
    /// best fuzzy score first, recents winning ties, then shorter titles (Obsidian's rule).
    /// </summary>
    public static IReadOnlyList<PaletteItem> Rank(IEnumerable<PaletteItem> items, string term, IReadOnlyList<string> recents)
    {
        int Recency(PaletteItem i) { var k = recents.ToList().IndexOf(i.Key ?? i.Title); return k < 0 ? int.MaxValue : k; }
        var list = items.ToArray();
        if (term.Length == 0)
            return list.Select((item, index) => (item, index)).OrderBy(x => Recency(x.item)).ThenBy(x => x.index).Select(x => x.item).ToArray();
        return list.Select(item => (item, score: Score(term, item.Title)))
            .Where(x => x.score is not null)
            .OrderByDescending(x => x.score)
            .ThenBy(x => Recency(x.item))
            .ThenBy(x => x.item.Title.Length)
            .Select(x => x.item).ToArray();
    }

    /// <summary>The 1-based line a ":" query asks for, or null.</summary>
    public static int? Line(string term) => int.TryParse(term.Split(',', ':')[0], out var line) && line > 0 ? line : null;
}
