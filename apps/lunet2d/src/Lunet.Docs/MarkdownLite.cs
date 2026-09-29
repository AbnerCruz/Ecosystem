using System.Text;

namespace Lunet.Docs;

public enum InlineStyle { Normal, Bold, Italic, Code }

public readonly record struct InlineRun(string Text, InlineStyle Style);

public enum MarkdownBlockKind { Heading, Paragraph, Code, Bullet }

public sealed record MarkdownBlock(MarkdownBlockKind Kind, string Text, int Level = 0);

/// <summary>Markdown mínimo dos guias e comentários: títulos, parágrafos, listas, blocos de código, **negrito**, *itálico* e `código`.</summary>
public static class MarkdownLite
{
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        void FlushParagraph()
        {
            if (paragraph.Length == 0) return;
            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, paragraph.ToString().Trim()));
            paragraph.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code.Append(lines[i]).Append('\n');
                    i++;
                }
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, code.ToString().TrimEnd('\n')));
                continue;
            }
            if (line.StartsWith('#'))
            {
                FlushParagraph();
                var level = 0;
                while (level < line.Length && line[level] == '#') level++;
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, line[level..].Trim(), Math.Min(level, 3)));
                continue;
            }
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Bullet, trimmed[2..].Trim()));
                continue;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }
            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(line.Trim());
        }
        FlushParagraph();
        return blocks;
    }

    /// <summary>Divide um texto em trechos com estilo (negrito, itálico, código).</summary>
    public static IReadOnlyList<InlineRun> ParseInline(string text)
    {
        var runs = new List<InlineRun>();
        var current = new StringBuilder();
        var i = 0;

        void Flush(InlineStyle style = InlineStyle.Normal)
        {
            if (current.Length > 0) runs.Add(new InlineRun(current.ToString(), style));
            current.Clear();
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    Flush();
                    runs.Add(new InlineRun(text[(i + 1)..end], InlineStyle.Code));
                    i = end + 1;
                    continue;
                }
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 1)
                {
                    Flush();
                    runs.Add(new InlineRun(text[(i + 2)..end], InlineStyle.Bold));
                    i = end + 2;
                    continue;
                }
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] != ' ')
            {
                var end = text.IndexOf('*', i + 1);
                if (end > i + 1 && text[end - 1] != ' ')
                {
                    Flush();
                    runs.Add(new InlineRun(text[(i + 1)..end], InlineStyle.Italic));
                    i = end + 1;
                    continue;
                }
            }
            current.Append(c);
            i++;
        }
        Flush();
        return runs;
    }
}
