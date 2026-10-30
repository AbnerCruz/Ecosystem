using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-101: the links to notes and files that were renamed or moved follow them (as Obsidian keeps them): a link
/// written by name (<c>[[Rascunho]]</c>) gets the new name, one written by path (<c>[[Pasta/Rascunho]]</c>) the new
/// path, each keeping its form (with or without the extension). A name that more than one file of the vault answers to
/// is left alone: which one it meant cannot be known.
/// </summary>
public static partial class LinkRename
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

    /// <summary>
    /// The Markdown links of a note (<c>[texto](Pasta/Nota.md)</c>, <c>![](foto.png)</c>) to files that moved, rewritten
    /// in the form they were written: a path relative to the note stays relative (from where the note is now), one from
    /// the root of the vault stays so. A note that moved itself has its relative links to files that stayed put fixed too.
    /// The <c>#trecho</c>, the title, <c>&lt;…&gt;</c> and the %20 encoding stay; addresses (https:, mailto:) and links
    /// inside code are left alone. A target is found as the Urbe opens it: from the note's folder, then from the root.
    /// </summary>
    /// <param name="noteFrom">Where the note was before the moves (its own path when it did not move).</param>
    /// <param name="noteTo">Where the note is now.</param>
    /// <param name="moves">Each file's old and new path.</param>
    /// <param name="files">Every file of the vault as it is now.</param>
    public static string RewriteMarkdownLinks(string content, string noteFrom, string noteTo, IReadOnlyList<(string From, string To)> moves, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.Contains("](", StringComparison.Ordinal)) return content;
        var byFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (from, to) in moves.Where(m => m.From != m.To)) byFrom[from] = to;
        var movedTo = byFrom.Values.ToHashSet(StringComparer.Ordinal);
        // the vault as it was: what did not move, plus each moved file at its old path
        var before = files.Where(f => !movedTo.Contains(f)).Concat(byFrom.Keys).ToHashSet(StringComparer.Ordinal);
        var oldFolder = Folder(noteFrom);
        var newFolder = Folder(noteTo);
        var code = CodeSpans(content);
        var output = new StringBuilder(content.Length);
        int at = 0;
        foreach (Match match in MarkdownLinkRegex().Matches(content))
        {
            var group = match.Groups["href"];
            if (code.Any(c => match.Index < c.End && match.Index + match.Length > c.Start)) continue;
            var href = group.Value;
            bool angled = href.StartsWith('<') && href.EndsWith('>');
            if (angled) href = href[1..^1];
            if (href.Length == 0 || href.StartsWith('#') || href.StartsWith("//", StringComparison.Ordinal) || SchemeRegex().IsMatch(href)) continue;
            int cut = href.IndexOfAny(['#', '?']);
            var target = cut >= 0 ? href[..cut] : href;
            var rest = cut >= 0 ? href[cut..] : "";
            bool encoded = target.Contains('%', StringComparison.Ordinal);
            string decoded;
            try { decoded = encoded ? Uri.UnescapeDataString(target) : target; } catch (UriFormatException) { continue; }
            decoded = decoded.Replace('\\', '/');
            bool rooted = decoded.StartsWith('/');
            // which file it meant, in the vault as it was
            string? meant = null;
            bool relative = false;
            if (!rooted && Join(oldFolder, decoded) is { } fromNote && before.Contains(fromNote)) { meant = fromNote; relative = oldFolder.Length > 0; }
            else if (Join("", decoded) is { } fromRoot && before.Contains(fromRoot)) meant = fromRoot;
            if (meant is null) continue;
            var now = byFrom.GetValueOrDefault(meant, meant);
            if (now == meant && (!relative || oldFolder == newFolder)) continue;
            var written = relative ? Relative(newFolder, now) : (rooted ? "/" : "") + now;
            // written with %20, or a new name with a space where the link has no <…>: what breaks a link is encoded
            if (encoded || (!angled && written.AsSpan().IndexOfAny(" ()") >= 0)) written = Encode(written);
            var replaced = (angled ? "<" : "") + written + rest + (angled ? ">" : "");
            if (replaced == group.Value) continue;
            output.Append(content, at, group.Index - at).Append(replaced);
            at = group.Index + group.Length;
        }
        return at == 0 ? content : output.Append(content, at, content.Length - at).ToString();
    }

    private static string Folder(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash > 0 ? path[..slash] : "";
    }

    /// <summary>The vault path of <paramref name="relative"/> seen from <paramref name="folder"/>, or null when it leaves the vault.</summary>
    private static string? Join(string folder, string relative)
    {
        if (relative.StartsWith('/')) folder = "";
        var parts = new List<string>();
        foreach (var p in (folder + "/" + relative).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (p == ".") continue;
            if (p == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(p);
        }
        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    /// <summary><paramref name="path"/> written from <paramref name="folder"/> (with ../ when it is outside).</summary>
    private static string Relative(string folder, string path)
    {
        var from = folder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var to = path.Split('/');
        int same = 0;
        while (same < from.Length && same < to.Length - 1 && from[same] == to[same]) same++;
        return string.Concat(Enumerable.Repeat("../", from.Length - same)) + string.Join('/', to.Skip(same));
    }

    private static string Encode(string path) => path.Replace("%", "%25").Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29");

    private static List<(int Start, int End)> CodeSpans(string content)
    {
        var code = new List<(int Start, int End)>();
        foreach (Match fence in FenceRegex().Matches(content)) code.Add((fence.Index, fence.Index + fence.Length));
        foreach (Match inline in InlineCodeRegex().Matches(content))
            if (!code.Any(c => inline.Index >= c.Start && inline.Index < c.End)) code.Add((inline.Index, inline.Index + inline.Length));
        return code;
    }

    // [texto](alvo "título") and ![alt](alvo), the target with or without <…>
    [GeneratedRegex(@"!?\[(?:[^\[\]]|\[[^\]]*\])*\]\((?<href><[^>\n]*>|[^)\s]+)(?:\s+(?:""[^""]*""|'[^']*'))?\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeRegex();

    [GeneratedRegex(@"^(?<fence>```|~~~)[^\n]*\n[\s\S]*?^\k<fence>[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"`[^`\n]*`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodeRegex();

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string Key(string value) => value.Trim().ToLowerInvariant();
}
