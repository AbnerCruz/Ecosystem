namespace Urbe.Core;

public enum VisualBlockKind
{
    Paragraph,
    Heading,
    Quote,
    UnorderedList,
    OrderedList,
    Code,
    HorizontalRule,
    Raw
}

public sealed record VisualBlock(
    VisualBlockKind Kind,
    string Text,
    int Level = 1,
    string Language = "");

public sealed record VisualDocumentModel(
    string Frontmatter,
    IReadOnlyList<VisualBlock> Blocks);

/// <summary>
/// Host-neutral block editor model. Markdown remains the canonical persisted
/// representation; advanced constructs are carried as Raw so visual editing
/// never silently discards syntax it does not model.
/// </summary>
public static class VisualDocumentEditor
{
    public static VisualDocumentModel Parse(string? markdown)
    {
        var frontmatter = MarkdownEngine.SplitFrontmatter(markdown);
        var lines = frontmatter.Body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var blocks = new List<VisualBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            var line = lines[index];

            if (TryFence(line, out var fence, out var language))
            {
                var code = new List<string>();
                index++;
                while (index < lines.Length &&
                       !lines[index].TrimStart().StartsWith(fence, StringComparison.Ordinal))
                {
                    code.Add(lines[index]);
                    index++;
                }

                if (index < lines.Length)
                    index++;

                blocks.Add(new VisualBlock(
                    VisualBlockKind.Code,
                    string.Join('\n', code),
                    Language: language));
                continue;
            }

            if (TryHeading(line, out var level, out var heading))
            {
                blocks.Add(new VisualBlock(
                    VisualBlockKind.Heading,
                    heading,
                    level));
                index++;
                continue;
            }

            if (IsHorizontalRule(line))
            {
                blocks.Add(new VisualBlock(
                    VisualBlockKind.HorizontalRule,
                    string.Empty));
                index++;
                continue;
            }

            if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
            {
                var source = new List<string>();
                while (index < lines.Length &&
                       lines[index].TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    source.Add(lines[index]);
                    index++;
                }

                if (source.Any(item =>
                        item.TrimStart().StartsWith("> [!", StringComparison.Ordinal)))
                {
                    blocks.Add(new VisualBlock(
                        VisualBlockKind.Raw,
                        string.Join('\n', source)));
                }
                else
                {
                    blocks.Add(new VisualBlock(
                        VisualBlockKind.Quote,
                        string.Join(
                            '\n',
                            source.Select(
                                item => item.TrimStart()[1..].TrimStart()))));
                }

                continue;
            }

            if (TryListMarker(line, out var ordered, out _))
            {
                var source = new List<string>();
                var advanced = false;

                while (index < lines.Length &&
                       TryListMarker(lines[index], out var currentOrdered, out var item) &&
                       currentOrdered == ordered)
                {
                    if (lines[index].Length != lines[index].TrimStart().Length ||
                        item.StartsWith("[ ] ", StringComparison.Ordinal) ||
                        item.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ||
                        item.StartsWith("[X] ", StringComparison.Ordinal) ||
                        item.Contains("[[", StringComparison.Ordinal))
                    {
                        advanced = true;
                    }

                    source.Add(lines[index]);
                    index++;
                }

                if (advanced)
                {
                    blocks.Add(new VisualBlock(
                        VisualBlockKind.Raw,
                        string.Join('\n', source)));
                }
                else
                {
                    blocks.Add(new VisualBlock(
                        ordered ? VisualBlockKind.OrderedList : VisualBlockKind.UnorderedList,
                        string.Join(
                            '\n',
                            source.Select(
                                item =>
                                {
                                    TryListMarker(item, out _, out var value);
                                    return value;
                                }))));
                }

                continue;
            }

            var paragraph = new List<string>();
            while (index < lines.Length &&
                   !string.IsNullOrWhiteSpace(lines[index]) &&
                   !IsStructuralStart(lines[index]))
            {
                paragraph.Add(lines[index]);
                index++;
            }

            var text = string.Join('\n', paragraph);
            blocks.Add(new VisualBlock(
                IsAdvancedParagraph(text)
                    ? VisualBlockKind.Raw
                    : VisualBlockKind.Paragraph,
                text));
        }

        return new VisualDocumentModel(
            frontmatter.Raw,
            Array.AsReadOnly(blocks.ToArray()));
    }

    /// <summary>
    /// Returns true only when entering the structural editor and saving its
    /// unchanged model would preserve every byte of the current Markdown.
    /// When false, the Source editor remains available without normalization.
    /// </summary>
    public static bool IsLosslessRoundTrip(string? markdown)
    {
        var source = markdown ?? string.Empty;
        var model = Parse(source);
        return string.Equals(
            ToMarkdown(model.Frontmatter, model.Blocks),
            source,
            StringComparison.Ordinal);
    }

    public static string ToMarkdown(
        string? frontmatter,
        IEnumerable<VisualBlock>? blocks)
    {
        var parts = new List<string>();
        var fm = frontmatter?.TrimEnd() ?? string.Empty;
        if (fm.Length > 0)
            parts.Add(fm);

        foreach (var block in blocks ?? Array.Empty<VisualBlock>())
        {
            var text = block.Text ?? string.Empty;
            var rendered = block.Kind switch
            {
                VisualBlockKind.Paragraph => text.TrimEnd(),
                VisualBlockKind.Heading =>
                    new string('#', Math.Clamp(block.Level, 1, 6)) +
                    (text.Length > 0 ? " " + text.TrimEnd() : " "),
                VisualBlockKind.Quote =>
                    string.Join(
                        '\n',
                        text.Replace("\r\n", "\n", StringComparison.Ordinal)
                            .Replace('\r', '\n')
                            .Split('\n')
                            .Select(line => line.Length == 0 ? ">" : "> " + line)),
                VisualBlockKind.UnorderedList => RenderList(text, false),
                VisualBlockKind.OrderedList => RenderList(text, true),
                VisualBlockKind.Code => RenderCode(text, block.Language),
                VisualBlockKind.HorizontalRule => "---",
                VisualBlockKind.Raw => text.TrimEnd(),
                _ => text.TrimEnd()
            };

            if (rendered.Length > 0)
                parts.Add(rendered);
        }

        return parts.Count == 0
            ? string.Empty
            : string.Join("\n\n", parts).TrimEnd() + "\n";
    }

    private static string RenderList(string text, bool ordered)
    {
        var items = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();

        var output = new List<string>(items.Length);
        for (var i = 0; i < items.Length; i++)
            output.Add((ordered ? (i + 1).ToString() + ". " : "- ") + items[i]);

        return string.Join('\n', output);
    }

    private static string RenderCode(string text, string language)
    {
        const char fenceChar = (char)96;
        var fence = new string(fenceChar, 3);
        return fence + (language?.Trim() ?? string.Empty) +
               "\n" + (text ?? string.Empty).TrimEnd() + "\n" + fence;
    }

    private static bool TryFence(
        string line,
        out string fence,
        out string language)
    {
        var trimmed = line.TrimStart();
        fence = trimmed.StartsWith("```", StringComparison.Ordinal)
            ? "```"
            : trimmed.StartsWith("~~~", StringComparison.Ordinal)
                ? "~~~"
                : string.Empty;

        language = fence.Length == 0
            ? string.Empty
            : trimmed[fence.Length..].Trim();

        return fence.Length > 0;
    }

    private static bool TryHeading(
        string line,
        out int level,
        out string text)
    {
        var trimmed = line.TrimStart();
        level = 0;
        text = string.Empty;

        while (level < trimmed.Length &&
               level < 6 &&
               trimmed[level] == '#')
            level++;

        if (level == 0 ||
            level >= trimmed.Length ||
            trimmed[level] is not (' ' or '\t'))
            return false;

        text = trimmed[(level + 1)..].Trim();
        return true;
    }

    private static bool TryListMarker(
        string line,
        out bool ordered,
        out string item)
    {
        var trimmed = line.TrimStart();
        ordered = false;
        item = string.Empty;

        if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
            trimmed.StartsWith("* ", StringComparison.Ordinal) ||
            trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            item = trimmed[2..].TrimEnd();
            return true;
        }

        var digits = 0;
        while (digits < trimmed.Length && char.IsDigit(trimmed[digits]))
            digits++;

        if (digits > 0 &&
            digits + 1 < trimmed.Length &&
            trimmed[digits] == '.' &&
            trimmed[digits + 1] == ' ')
        {
            ordered = true;
            item = trimmed[(digits + 2)..].TrimEnd();
            return true;
        }

        return false;
    }

    private static bool IsHorizontalRule(string line)
    {
        var compact = line.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);

        return compact.Length >= 3 &&
               (compact.All(ch => ch == '-' || ch == '*' || ch == '_'));
    }

    private static bool IsStructuralStart(string line) =>
        TryFence(line, out _, out _) ||
        TryHeading(line, out _, out _) ||
        IsHorizontalRule(line) ||
        line.TrimStart().StartsWith(">", StringComparison.Ordinal) ||
        TryListMarker(line, out _, out _);

    private static bool IsAdvancedParagraph(string text) =>
        text.TrimStart().StartsWith("|", StringComparison.Ordinal) ||
        text.Contains("\n|", StringComparison.Ordinal) ||
        text.Contains("\n    ", StringComparison.Ordinal) ||
        text.Contains("\n\t", StringComparison.Ordinal);
}
