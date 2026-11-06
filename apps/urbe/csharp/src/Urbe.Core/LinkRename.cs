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
    /// CST-196: reference definitions (<c>[id]: Pasta/Nota.md</c>) follow too; <c>./</c> stays, a link written with
    /// encoded accents (%C3%AD) keeps them encoded, and fences of any length (``` and ~~~) and <c>``a ` b``</c> are code.
    /// A link that did not find its file before the move is never touched.
    /// </summary>
    /// <param name="noteFrom">Where the note was before the moves (its own path when it did not move).</param>
    /// <param name="noteTo">Where the note is now.</param>
    /// <param name="moves">Each file's old and new path.</param>
    /// <param name="files">Every file of the vault as it is now.</param>
    public static string RewriteMarkdownLinks(string content, string noteFrom, string noteTo, IReadOnlyList<(string From, string To)> moves, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.Contains("](", StringComparison.Ordinal) && !content.Contains("]:", StringComparison.Ordinal)) return content;
        var byFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (from, to) in moves.Where(m => m.From != m.To)) byFrom[from] = to;
        var movedTo = byFrom.Values.ToHashSet(StringComparer.Ordinal);
        // the vault as it was: what did not move, plus each moved file at its old path
        var before = files.Where(f => !movedTo.Contains(f)).Concat(byFrom.Keys).ToHashSet(StringComparer.Ordinal);
        var oldFolder = Folder(noteFrom);
        var newFolder = Folder(noteTo);
        var code = CodeSpans(content);
        // inline links ([texto](alvo), ![alt](alvo)) and reference definitions ([id]: alvo), in the order they appear
        var targets = MarkdownLinkRegex().Matches(content).Concat(ReferenceRegex().Matches(content))
            .Select(m => (Whole: m, Href: m.Groups["href"])).OrderBy(t => t.Href.Index).ToList();
        var output = new StringBuilder(content.Length);
        int at = 0;
        foreach (var (match, group) in targets)
        {
            if (group.Index < at || InCode(code, match.Index, match.Length)) continue;
            if (Retargeted(group.Value) is not { } replaced) continue;
            output.Append(content, at, group.Index - at).Append(replaced);
            at = group.Index + group.Length;
        }
        return at == 0 ? content : output.Append(content, at, content.Length - at).ToString();

        // the destination written again, or null when it stays as it is
        string? Retargeted(string href)
        {
            var original = href;
            bool angled = href.StartsWith('<') && href.EndsWith('>');
            if (angled) href = href[1..^1];
            if (href.Length == 0 || href.StartsWith('#') || href.StartsWith("//", StringComparison.Ordinal) || SchemeRegex().IsMatch(href)) return null;
            int cut = href.IndexOfAny(['#', '?']);
            var target = cut >= 0 ? href[..cut] : href;
            var rest = cut >= 0 ? href[cut..] : "";
            bool encoded = target.Contains('%', StringComparison.Ordinal);
            string decoded;
            try { decoded = encoded ? Uri.UnescapeDataString(target) : target; } catch (UriFormatException) { return null; }
            decoded = decoded.Replace('\\', '/');
            bool rooted = decoded.StartsWith('/');
            bool dotted = decoded.StartsWith("./", StringComparison.Ordinal);
            // which file it meant, in the vault as it was: from the note's folder, then from the root
            string? meant = null;
            bool relative = false;
            if (!rooted && Join(oldFolder, decoded) is { } fromNote && before.Contains(fromNote)) { meant = fromNote; relative = true; }
            else if (Join("", decoded) is { } fromRoot && before.Contains(fromRoot)) meant = fromRoot;
            if (meant is null) return null;
            var now = byFrom.GetValueOrDefault(meant, meant);
            if (now == meant && (!relative || oldFolder == newFolder)) return null;
            var written = relative ? Relative(newFolder, now) : (rooted ? "/" : "") + now;
            if (dotted && relative && !written.StartsWith("../", StringComparison.Ordinal)) written = "./" + written;
            // written with %20, or a new name with a space where the link has no <…>: what breaks a link is encoded
            if (encoded || (!angled && written.AsSpan().IndexOfAny(" ()<>") >= 0)) written = Encode(written, encoded && EncodedNonAscii().IsMatch(target));
            var replaced = (angled ? "<" : "") + written + rest + (angled ? ">" : "");
            return replaced == original ? null : replaced;
        }
    }

    private static bool InCode(List<(int Start, int End)> code, int start, int length)
    {
        foreach (var c in code) if (start < c.End && start + length > c.Start) return true;
        return false;
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

    /// <summary>What breaks a link encoded (%, space, parentheses, &lt;&gt;); letters outside ASCII too when the link had them so.</summary>
    private static string Encode(string path, bool nonAscii)
    {
        var sb = new StringBuilder(path.Length + 8);
        Span<byte> bytes = stackalloc byte[4];
        foreach (var r in path.EnumerateRunes())
        {
            switch (r.Value)
            {
                case '%': sb.Append("%25"); break;
                case ' ': sb.Append("%20"); break;
                case '(': sb.Append("%28"); break;
                case ')': sb.Append("%29"); break;
                case '<': sb.Append("%3C"); break;
                case '>': sb.Append("%3E"); break;
                default:
                    if (nonAscii && r.Value > 0x7F)
                    {
                        int n = r.EncodeToUtf8(bytes);
                        for (int i = 0; i < n; i++) sb.Append('%').Append(bytes[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else sb.Append(r.ToString());
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Fenced blocks (``` or ~~~, of any length, up to three spaces in, open to the end when never closed)
    /// and inline code (`a`, ``a ` b``): links there are text, never followed. An indented paragraph is not taken as
    /// code: the Urbe shows its links (and a list item's continuation is indented as well).</summary>
    private static List<(int Start, int End)> CodeSpans(string content)
    {
        var code = new List<(int Start, int End)>();
        int line = 0;
        string? fence = null;
        int fenceStart = 0;
        while (line <= content.Length)
        {
            int end = content.IndexOf('\n', line);
            if (end < 0) end = content.Length;
            var text = content.AsSpan(line, end - line).TrimEnd('\r');
            var trimmed = text.TrimStart(' ');
            int indent = text.Length - trimmed.Length;
            if (fence is not null)
            {
                if (indent <= 3 && trimmed.StartsWith(fence) && trimmed.TrimStart(fence[0]).Trim().Length == 0)
                {
                    code.Add((fenceStart, end));
                    fence = null;
                }
            }
            // a ``` fence's info string has no backtick
            else if (indent <= 3 && (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                     && (trimmed[0] == '~' || trimmed.TrimStart('`').IndexOf('`') < 0))
            {
                int n = 0;
                while (n < trimmed.Length && trimmed[n] == trimmed[0]) n++;
                fence = new string(trimmed[0], n);
                fenceStart = line;
            }
            if (end == content.Length) break;
            line = end + 1;
        }
        if (fence is not null) code.Add((fenceStart, content.Length));
        foreach (Match inline in InlineCodeRegex().Matches(content))
            if (!code.Any(c => inline.Index >= c.Start && inline.Index < c.End)) code.Add((inline.Index, inline.Index + inline.Length));
        return code;
    }

    // [texto](alvo "título") and ![alt](alvo), the target with or without <…>, with balanced parentheses; not after a \
    [GeneratedRegex(@"(?<!\\)!?\[(?:[^\[\]]|\\[\[\]]|\[[^\]]*\])*\]\([ \t]*(?<href><[^>\n]*>|(?:[^()\s]|\([^()\s]*\))+)(?:\s+(?:""[^""]*""|'[^']*'|\([^()]*\)))?[ \t]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    // [id]: alvo "título", a reference definition (a footnote, [^1]:, is not one)
    [GeneratedRegex(@"^[ ]{0,3}\[(?!\^)[^\]\n]+\]:[ \t]*(?<href><[^>\n]*>|\S+)(?:[ \t]+(?:""[^""\n]*""|'[^'\n]*'|\([^()\n]*\)))?[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeRegex();

    [GeneratedRegex(@"%[89A-Fa-f][0-9A-Fa-f]", RegexOptions.CultureInvariant)]
    private static partial Regex EncodedNonAscii();

    // `a`, ``a ` b``: the same run of backticks opens and closes, on one line (as the Urbe shows it)
    [GeneratedRegex(@"(?<!`)(?<t>`+)(?!`)[^\n]*?(?<!`)\k<t>(?!`)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodeRegex();

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string Key(string value) => value.Trim().ToLowerInvariant();
}
