namespace Urbe.Core;

/// <summary>One occurrence: where it starts (UTF-16 index, as JS), its length and its 1-based line.</summary>
public sealed record LegacyFindHit(int Index, int Length, int Line);

/// <summary>
/// src/editor/workspace.js (1.8.4-beta) find/replace over a document's text, what the "Localizar e substituir" bar
/// (find-ui.js) uses: case-insensitive by default (toLowerCase), at most 1000 hits, the search resumes after each hit
/// (at least one character on), and Substituir replaces the first hit while Todos replaces every one.
/// </summary>
public static class LegacyEditorFind
{
    public static IReadOnlyList<LegacyFindHit> Find(string? text, string? query, bool caseSensitive = false)
    {
        var output = new List<LegacyFindHit>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return output;
        string needle = caseSensitive ? query : query.ToLowerInvariant(), hay = caseSensitive ? text : text.ToLowerInvariant();
        int at = 0, line = 1, counted = 0;
        while ((at = hay.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            // text.slice(0,at).split('\n').length
            for (; counted < at; counted++) if (text[counted] == '\n') line++;
            output.Add(new LegacyFindHit(at, query.Length, line));
            at += Math.Max(1, query.Length);
            if (output.Count >= 1000) break;
        }
        return output;
    }

    /// <summary>replace(query, replacement, id, {all}): the text after replacing the first hit, or every hit; the same
    /// text when nothing matches.</summary>
    public static string Replace(string text, string? query, string? replacement, bool all, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hits = Find(text, query, caseSensitive);
        if (hits.Count == 0) return text;
        var repl = replacement ?? "";
        var next = text;
        if (all)
            for (int i = hits.Count - 1; i >= 0; i--) next = next[..hits[i].Index] + repl + next[(hits[i].Index + hits[i].Length)..];
        else next = next[..hits[0].Index] + repl + next[(hits[0].Index + hits[0].Length)..];
        return next;
    }
}
