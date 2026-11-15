using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The look of Markdown while it is written: which characters are marks (dimmed), and which text is bold, italic,
/// struck, code, a link, a tag, a comment or a formula. Used to draw a block's source formatted in its edit box, so
/// writing a block looks like the block drawn (bold stays bold, a link stays a link) and only the marks appear.
/// Spans never overlap; a character outside every span is plain text.
/// </summary>
public static partial class MarkdownSyntax
{
    [Flags]
    public enum Style
    {
        None = 0, Mark = 1, Bold = 2, Italic = 4, Strike = 8, Code = 16, Link = 32, Tag = 64, Comment = 128, Math = 256, Highlight = 512, Bullet = 1024
    }

    public readonly record struct Span(int Start, int Length, Style Style);

    [GeneratedRegex(@"^([ \t]*)(#{1,6}[ \t]+|>[ \t]?|(?:[-*+]|\d+[.)])[ \t]+(?:\[[ xX]\][ \t]+)?)", RegexOptions.Multiline)]
    private static partial Regex LineMarkRx();

    [GeneratedRegex(@"`+")]
    private static partial Regex TicksRx();

    [GeneratedRegex(@"%%.*?%%", RegexOptions.Singleline)]
    private static partial Regex CommentRx();

    [GeneratedRegex(@"(?<![\\$])\$(?!\s)(?:[^$\n\\]|\\.)+?(?<!\s)\$")]
    private static partial Regex MathRx();

    [GeneratedRegex(@"!?\[\[([^\[\]\n]+?)\]\]")]
    private static partial Regex WikiRx();

    [GeneratedRegex(@"!?\[([^\]\n]*)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex LinkRx();

    [GeneratedRegex(@"(?:https?://|mailto:)[^\s<>()\[\]]+")]
    private static partial Regex UrlRx();

    [GeneratedRegex(@"(?<![\w&#])#[\p{L}\p{N}_][\p{L}\p{N}_/\-]*")]
    private static partial Regex TagRx();

    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1")]
    private static partial Regex BoldRx();

    [GeneratedRegex(@"(?<![*\w])([*_])(?=[^\s*_])(.+?)(?<=[^\s*_])\1(?![*\w])")]
    private static partial Regex ItalicRx();

    [GeneratedRegex(@"~~(?=\S)(.+?)(?<=\S)~~")]
    private static partial Regex StrikeRx();

    [GeneratedRegex(@"==(?=\S)(.+?)(?<=\S)==")]
    private static partial Regex HighlightRx();

    /// <summary>The spans of <paramref name="text"/>, in order, without overlaps. A fenced code block is left plain.</summary>
    public static IReadOnlyList<Span> Spans(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var style = new Style[text.Length];
        // code and comments first: nothing inside them is Markdown
        var locked = new bool[text.Length];
        void Set(int start, int length, Style s, bool lockIt = false)
        {
            for (int i = Math.Max(0, start); i < Math.Min(text.Length, start + length); i++)
            {
                if (locked[i]) continue;
                style[i] |= s;
                if (lockIt) locked[i] = true;
            }
        }
        bool Free(int start, int length)
        {
            for (int i = start; i < start + length && i < text.Length; i++) if (locked[i]) return false;
            return true;
        }
        // fenced code: a block that is code is shown as it is
        if (text.TrimStart().StartsWith("```", StringComparison.Ordinal) || text.TrimStart().StartsWith("~~~", StringComparison.Ordinal)) return [];

        // inline code: a run of backticks closed by the same run
        var ticks = TicksRx().Matches(text).ToList();
        for (int i = 0; i < ticks.Count; i++)
        {
            for (int j = i + 1; j < ticks.Count; j++)
            {
                if (ticks[j].Length != ticks[i].Length) continue;
                if (text.AsSpan(ticks[i].Index, ticks[j].Index - ticks[i].Index).Contains("\n\n", StringComparison.Ordinal)) break;
                Set(ticks[i].Index, ticks[i].Length, Style.Mark | Style.Code, true);
                Set(ticks[i].Index + ticks[i].Length, ticks[j].Index - ticks[i].Index - ticks[i].Length, Style.Code, true);
                Set(ticks[j].Index, ticks[j].Length, Style.Mark | Style.Code, true);
                i = j;
                break;
            }
        }
        foreach (Match m in CommentRx().Matches(text))
            if (Free(m.Index, m.Length)) { Set(m.Index, 2, Style.Mark | Style.Comment, true); Set(m.Index + 2, m.Length - 4, Style.Comment, true); Set(m.Index + m.Length - 2, 2, Style.Mark | Style.Comment, true); }
        foreach (Match m in MathRx().Matches(text))
            if (Free(m.Index, m.Length)) { Set(m.Index, 1, Style.Mark, true); Set(m.Index + 1, m.Length - 2, Style.Math, true); Set(m.Index + m.Length - 1, 1, Style.Mark, true); }

        // the marks that open a line: heading, quote, list item, task
        foreach (Match m in LineMarkRx().Matches(text))
        {
            var mark = m.Groups[2];
            if (!Free(mark.Index, mark.Length)) continue;
            var bullet = mark.Value.TrimStart()[0] is '-' or '*' or '+' || char.IsDigit(mark.Value.TrimStart()[0]);
            Set(mark.Index, mark.Length, bullet ? Style.Bullet : Style.Mark);
        }

        // links: the brackets and the address are marks, the text is the link
        foreach (Match m in WikiRx().Matches(text))
        {
            if (!Free(m.Index, m.Length)) continue;
            var open = m.Value.StartsWith('!') ? 3 : 2;
            var inner = m.Groups[1];
            var bar = inner.Value.IndexOf('|');
            Set(m.Index, open, Style.Mark, true);
            if (bar >= 0)
            {
                Set(inner.Index, bar + 1, Style.Mark, true);
                Set(inner.Index + bar + 1, inner.Length - bar - 1, Style.Link, true);
            }
            else Set(inner.Index, inner.Length, Style.Link, true);
            Set(m.Index + m.Length - 2, 2, Style.Mark, true);
        }
        foreach (Match m in LinkRx().Matches(text))
        {
            if (!Free(m.Index, m.Length)) continue;
            var label = m.Groups[1];
            Set(m.Index, label.Index - m.Index, Style.Mark, true);
            Set(label.Index, label.Length, Style.Link);
            for (int i = label.Index; i < label.Index + label.Length; i++) locked[i] = false;
            Set(label.Index + label.Length, m.Index + m.Length - label.Index - label.Length, Style.Mark, true);
        }
        foreach (Match m in UrlRx().Matches(text))
            if (Free(m.Index, m.Length)) Set(m.Index, m.Length, Style.Link, true);
        foreach (Match m in TagRx().Matches(text))
            if (Free(m.Index, m.Length) && !IsHeadingMark(text, m.Index)) Set(m.Index, m.Length, Style.Tag, true);

        // emphasis
        Emphasis(BoldRx(), Style.Bold, 2);
        Emphasis(StrikeRx(), Style.Strike, 2);
        Emphasis(HighlightRx(), Style.Highlight, 2);
        Emphasis(ItalicRx(), Style.Italic, 1);

        void Emphasis(Regex rx, Style s, int marks)
        {
            foreach (Match m in rx.Matches(text))
            {
                if (!Free(m.Index, marks) || !Free(m.Index + m.Length - marks, marks)) continue;
                // a mark already taken by a stronger emphasis (the ** of bold read as two *) is not italic
                if ((style[m.Index] & (Style.Mark | Style.Bullet)) != 0 || (style[m.Index + m.Length - 1] & (Style.Mark | Style.Bullet)) != 0) continue;
                Set(m.Index, marks, Style.Mark | s);
                Set(m.Index + marks, m.Length - 2 * marks, s);
                Set(m.Index + m.Length - marks, marks, Style.Mark | s);
            }
        }

        var spans = new List<Span>();
        int k = 0;
        while (k < text.Length)
        {
            var st = style[k];
            int e = k + 1;
            while (e < text.Length && style[e] == st) e++;
            if (st != Style.None) spans.Add(new Span(k, e - k, st));
            k = e;
        }
        return spans;
    }

    private static bool IsHeadingMark(string text, int at)
    {
        var lineStart = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
        return text.AsSpan(lineStart, at - lineStart).Trim().Length == 0 && at + 1 < text.Length && (text[at + 1] == '#' || text[at + 1] == ' ');
    }
}
