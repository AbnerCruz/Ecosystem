using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>The link written where the caret is in a piece of Markdown: what "Abrir" follows while writing.</summary>
public static partial class EditorLinks
{
    /// <summary>A link at the caret: a note (<c>[[Nota#título|apelido]]</c> → "Nota#título") or a web address.</summary>
    public sealed record Link(bool Note, string Target, int Start, int End);

    [GeneratedRegex(@"!?\[\[([^\[\]\n]+?)\]\]")]
    private static partial Regex WikiRx();

    [GeneratedRegex(@"\[([^\]\n]*)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex MarkdownRx();

    [GeneratedRegex(@"(?:https?://|mailto:)[^\s<>()\[\]]+")]
    private static partial Regex BareRx();

    /// <summary>The link that holds <paramref name="caret"/> (the caret touching either end counts), or null.</summary>
    public static Link? At(string text, int caret)
    {
        if (string.IsNullOrEmpty(text)) return null;
        caret = Math.Clamp(caret, 0, text.Length);
        // only the caret's line is looked at: links never cross lines
        var from = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var to = text.IndexOf('\n', caret);
        if (to < 0) to = text.Length;
        var line = text[from..to];
        int at = caret - from;
        foreach (Match m in WikiRx().Matches(line))
            if (at > m.Index && at < m.Index + m.Length)
            {
                var inner = m.Groups[1].Value;
                var bar = inner.IndexOf('|');
                var target = (bar >= 0 ? inner[..bar] : inner).Trim();
                return target.Length == 0 ? null : new Link(true, target, from + m.Index, from + m.Index + m.Length);
            }
        foreach (Match m in MarkdownRx().Matches(line))
            if (at > m.Index && at < m.Index + m.Length)
            {
                var url = m.Groups[2].Value;
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
                    return new Link(false, uri.AbsoluteUri, from + m.Index, from + m.Index + m.Length);
                // a relative Markdown link to a note of the vault: [texto](Pasta/Nota.md)
                var path = Uri.UnescapeDataString(url.Split('#')[0]);
                if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    return new Link(true, path[..^3], from + m.Index, from + m.Index + m.Length);
                return null;
            }
        foreach (Match m in BareRx().Matches(line))
            if (at >= m.Index && at <= m.Index + m.Length)
            {
                var url = m.Value.TrimEnd('.', ',', ';', ':', '!', '?');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) return new Link(false, uri.AbsoluteUri, from + m.Index, from + m.Index + url.Length);
            }
        return null;
    }
}
