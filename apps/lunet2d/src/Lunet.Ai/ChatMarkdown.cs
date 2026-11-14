using System.Text;
using System.Text.RegularExpressions;

namespace Lunet.Ai;

public enum ChatBlockKind { Paragraph, Heading, Bullet, Numbered, Quote, Code, Table, Rule }

/// <summary>One rendered block of an AI answer. <see cref="Level"/> is the heading level or the list number.</summary>
public sealed record ChatBlock(ChatBlockKind Kind, string Text, int Level = 0, string? Language = null,
    IReadOnlyList<IReadOnlyList<string>>? Rows = null);

public enum ChatInline { Plain, Bold, Italic, Code, Link }
public sealed record ChatSpan(string Text, ChatInline Style, string? Target = null);

/// <summary>
/// Markdown for chat answers (LUNET-490): the subset models actually produce — headings, paragraphs, bullets,
/// numbered lists, quotes, fenced code, tables and rules — plus inline bold/italic/code/links. Tolerant of
/// unfinished output (an open fence becomes a code block).
/// </summary>
public static class ChatMarkdown
{
    private static readonly Regex Numbered = new(@"^\s*(\d{1,3})[.)]\s+(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Bullet = new(@"^\s*[-*+•]\s+(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.*?)\s*#*\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex TableSeparator = new(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<ChatBlock> Parse(string? markdown)
    {
        var blocks = new List<ChatBlock>();
        var lines = (markdown ?? "").Replace("\r\n", "\n").Split('\n');
        var paragraph = new StringBuilder();
        void Flush()
        {
            if (paragraph.Length == 0) return;
            blocks.Add(new ChatBlock(ChatBlockKind.Paragraph, paragraph.ToString().Trim()));
            paragraph.Clear();
        }
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                var language = trimmed[3..].Trim();
                var code = new List<string>();
                int j = i + 1;
                for (; j < lines.Length && !lines[j].Trim().StartsWith("```", StringComparison.Ordinal); j++) code.Add(lines[j]);
                blocks.Add(new ChatBlock(ChatBlockKind.Code, string.Join("\n", code).TrimEnd('\n'), 0, language.Length == 0 ? null : language));
                i = j;
                continue;
            }
            if (trimmed.Length == 0) { Flush(); continue; }
            if (trimmed is "---" or "***" or "___") { Flush(); blocks.Add(new ChatBlock(ChatBlockKind.Rule, "")); continue; }
            if (Heading.Match(trimmed) is { Success: true } h)
            {
                Flush();
                blocks.Add(new ChatBlock(ChatBlockKind.Heading, h.Groups[2].Value, h.Groups[1].Value.Length));
                continue;
            }
            if (trimmed.StartsWith('|') && i + 1 < lines.Length && TableSeparator.IsMatch(lines[i + 1]))
            {
                Flush();
                var rows = new List<IReadOnlyList<string>> { Cells(trimmed) };
                int j = i + 2;
                for (; j < lines.Length && lines[j].Trim().StartsWith('|'); j++) rows.Add(Cells(lines[j].Trim()));
                blocks.Add(new ChatBlock(ChatBlockKind.Table, "", 0, null, rows));
                i = j - 1;
                continue;
            }
            if (Numbered.Match(line) is { Success: true } n)
            {
                Flush();
                blocks.Add(new ChatBlock(ChatBlockKind.Numbered, n.Groups[2].Value.Trim(), int.Parse(n.Groups[1].Value)));
                continue;
            }
            if (Bullet.Match(line) is { Success: true } b)
            {
                Flush();
                blocks.Add(new ChatBlock(ChatBlockKind.Bullet, b.Groups[1].Value.Trim()));
                continue;
            }
            if (trimmed.StartsWith('>'))
            {
                Flush();
                blocks.Add(new ChatBlock(ChatBlockKind.Quote, trimmed.TrimStart('>').Trim()));
                continue;
            }
            if (paragraph.Length > 0) paragraph.Append('\n');
            paragraph.Append(trimmed);
        }
        Flush();
        return blocks;
    }

    private static IReadOnlyList<string> Cells(string row) =>
        row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    /// <summary>Inline formatting: **bold**, *italic* / _italic_, `code`, [text](target).</summary>
    public static IReadOnlyList<ChatSpan> Inline(string text)
    {
        var spans = new List<ChatSpan>();
        var plain = new StringBuilder();
        void Push(ChatSpan span)
        {
            if (plain.Length > 0) { spans.Add(new ChatSpan(plain.ToString(), ChatInline.Plain)); plain.Clear(); }
            spans.Add(span);
        }
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i) { Push(new ChatSpan(text[(i + 1)..end], ChatInline.Code)); i = end + 1; continue; }
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2) { Push(new ChatSpan(text[(i + 2)..end], ChatInline.Bold)); i = end + 2; continue; }
            }
            else if ((c == '*' || c == '_') && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1])
                     && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
            {
                int end = text.IndexOf(c, i + 1);
                if (end > i + 1 && !char.IsWhiteSpace(text[end - 1])) { Push(new ChatSpan(text[(i + 1)..end], ChatInline.Italic)); i = end + 1; continue; }
            }
            else if (c == '[')
            {
                int close = text.IndexOf("](", i + 1, StringComparison.Ordinal);
                int end = close < 0 ? -1 : text.IndexOf(')', close + 2);
                if (close > i && end > close)
                {
                    Push(new ChatSpan(text[(i + 1)..close], ChatInline.Link, text[(close + 2)..end]));
                    i = end + 1;
                    continue;
                }
            }
            plain.Append(c);
            i++;
        }
        if (plain.Length > 0) spans.Add(new ChatSpan(plain.ToString(), ChatInline.Plain));
        return spans;
    }
}
