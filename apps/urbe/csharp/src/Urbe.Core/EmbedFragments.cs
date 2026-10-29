using System.Text.RegularExpressions;
using Urbe.Core.Assistant;

namespace Urbe.Core;

/// <summary>
/// CST-064: the part of a note an embed names after <c>#</c>. <c>![[Nota#Título]]</c> is the heading's section, up to
/// the next heading of the same or a higher level (and <c>#Título#Subtítulo</c> narrows inside it);
/// <c>![[Nota#^id]]</c> is the paragraph or list item that ends with <c> ^id</c>, without the marker (both as in
/// Obsidian, so a vault reads the same there); <c>![[Nota#L3-L5]]</c> are those lines (GitHub's form). Headings
/// match without case or accents and never inside code.
/// </summary>
public static partial class EmbedFragments
{
    public sealed record Part(string? Text, string? Error);

    /// <summary>The name and the fragment of an embed target (the <c>|label</c> is dropped).</summary>
    public static (string Name, string? Fragment) Split(string target)
    {
        var bar = target.IndexOf('|');
        var main = bar >= 0 ? target[..bar] : target;
        var hash = main.IndexOf('#');
        if (hash < 0) return (main.Trim(), null);
        var fragment = main[(hash + 1)..].Trim();
        return (main[..hash].Trim(), fragment.Length == 0 ? null : fragment);
    }

    public static Part Extract(string markdown, string fragment)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        fragment = fragment.Trim();
        if (fragment.StartsWith('^')) return Block(lines, fragment[1..]);
        if (LinesRegex().Match(fragment) is { Success: true } range)
        {
            int from = int.Parse(range.Groups[1].Value), to = range.Groups[2].Success ? int.Parse(range.Groups[2].Value) : from;
            if (to < from) (from, to) = (to, from);
            if (from < 1 || from > lines.Length) return new(null, "A nota tem " + lines.Length + " linhas; as linhas " + fragment + " não existem.");
            return new(string.Join("\n", lines[(from - 1)..Math.Min(to, lines.Length)]), null);
        }
        int start = 0, end = lines.Length;
        foreach (var name in fragment.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var found = Heading(lines, start, end, name);
            if (found is null) return new(null, "O título \"" + name + "\" não existe nesta nota.");
            (start, end) = found.Value;
        }
        return new(string.Join("\n", lines[start..end]).TrimEnd(), null);
    }

    /// <summary>
    /// CST-120: the line (from 1) a link's fragment leads to — the heading (a path of headings narrows, as in an
    /// embed) or the line that ends with <c> ^id</c>; null when the note has no such part.
    /// </summary>
    public static int? LineOf(string markdown, string fragment)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        fragment = fragment.Trim().TrimStart('#');
        if (fragment.StartsWith('^'))
        {
            var code = CodeLines(lines);
            var marker = " ^" + fragment[1..];
            for (int i = 0; i < lines.Length; i++)
                if (!code[i] && lines[i].TrimEnd().EndsWith(marker, StringComparison.Ordinal)) return i + 1;
            return null;
        }
        int start = 0, end = lines.Length;
        foreach (var name in fragment.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Heading(lines, start, end, name) is not { } found) return null;
            (start, end) = found;
        }
        return start + 1;
    }

    /// <summary>The section [start, end) of the first heading named <paramref name="name"/> between the two lines.</summary>
    private static (int Start, int End)? Heading(string[] lines, int from, int to, string name)
    {
        var wanted = AssistantTools.Fold(name);
        var code = CodeLines(lines);
        for (int i = from; i < to; i++)
        {
            if (code[i] || HeadingRegex().Match(lines[i]) is not { Success: true } h) continue;
            if (AssistantTools.Fold(h.Groups[2].Value.Trim()) != wanted) continue;
            int level = h.Groups[1].Length, end = i + 1;
            while (end < to && (code[end] || HeadingRegex().Match(lines[end]) is not { Success: true } next || next.Groups[1].Length > level)) end++;
            return (i, end);
        }
        return null;
    }

    /// <summary>The text without the <c> ^id</c> markers that end lines (outside code), as Obsidian shows it.</summary>
    public static string HideBlockIds(string markdown)
    {
        if (!markdown.Contains('^')) return markdown;
        var lines = markdown.Split('\n');
        var code = CodeLines(lines);
        for (int i = 0; i < lines.Length; i++)
            if (!code[i]) lines[i] = BlockIdRegex().Replace(lines[i], "");
        return string.Join("\n", lines);
    }

    private static Part Block(string[] lines, string id)
    {
        var code = CodeLines(lines);
        // the marker ends the line, before any authorship seals the Urbe keeps there
        var marker = new Regex(@"(?:^|\s)\^" + Regex.Escape(id) + @"(?=(?:\s*\{ia:""[^""\n]*""\})*\s*$)");
        for (int i = 0; i < lines.Length; i++)
        {
            if (code[i] || !marker.IsMatch(lines[i])) continue;
            var own = marker.Replace(lines[i], "").TrimEnd();
            // a list item is its own block; a paragraph runs back to the blank line before it
            if (ListRegex().IsMatch(own)) return new(own, null);
            if (own.Trim().Length == 0)
            {
                // "^id" alone on the line after a block (tables, quotes): that block
                int last = i - 1;
                while (last >= 0 && lines[last].Trim().Length == 0) last--;
                if (last < 0) break;
                int first = last;
                while (first > 0 && lines[first - 1].Trim().Length > 0) first--;
                return new(string.Join("\n", lines[first..(last + 1)]), null);
            }
            int start = i;
            while (start > 0 && lines[start - 1].Trim().Length > 0 && !ListRegex().IsMatch(lines[start - 1]) && !HeadingRegex().IsMatch(lines[start - 1])) start--;
            return new(string.Join("\n", lines[start..i].Append(own)), null);
        }
        return new(null, "O bloco ^" + id + " não existe nesta nota.");
    }

    private static bool[] CodeLines(string[] lines)
    {
        var code = new bool[lines.Length];
        bool fence = false;
        for (int i = 0; i < lines.Length; i++)
        {
            bool marker = lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal) || lines[i].TrimStart().StartsWith("~~~", StringComparison.Ordinal);
            code[i] = fence || marker;
            if (marker) fence = !fence;
        }
        return code;
    }

    [GeneratedRegex(@"[ \t]+\^[A-Za-z0-9-]+(?=(?:[ \t]*\{ia:""[^""\n]*""\})*[ \t]*$)")]
    private static partial Regex BlockIdRegex();

    [GeneratedRegex(@"^(#{1,6})[ \t]+(.*?)[ \t]*#*[ \t]*$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[ \t]*(?:[-*+]|\d+[.)])[ \t]")]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"^[Ll](\d+)(?:-[Ll]?(\d+))?$")]
    private static partial Regex LinesRegex();
}
