using System.Text;

namespace Urbe.Core;

/// <summary>One top-level block of a note: the source range it renders from (UTF-16, on the LF-normalised text).</summary>
public sealed record MarkdownBlock(int Start, int End, string Kind);

public static partial class MarkdownEngine
{
    /// <summary>
    /// Splits a note into the top-level blocks renderBlocks (src/editor/markdown.js) turns into elements, with the
    /// source range of each (blank lines separate; a list/quote/table/code block is one block, a paragraph is one line,
    /// a math formula never splits). Rendering the pieces one by one gives the same HTML as rendering the whole note
    /// (MarkdownBlocksTests); this is what lets the native Visual surface edit one rendered block at a time.
    /// </summary>
    public static IReadOnlyList<MarkdownBlock> SplitBlocks(string? markdown)
    {
        var source = NormalizeNewlines(markdown ?? string.Empty);
        var atoms = ScanMath(source);
        var ex = new StringBuilder();
        // (extracted end, shift): original offset = extracted offset + shift of every atom that ends at or before it
        var shifts = new List<(int ExEnd, int Shift)>();
        var last = 0;
        var shift = 0;
        for (var i = 0; i < atoms.Count; i++)
        {
            var a = atoms[i];
            ex.Append(source, last, a.Start - last);
            var placeholder = (a.Block ? BlockOpen : InlineOpen).ToString() + i + (a.Block ? BlockClose : InlineClose);
            ex.Append(placeholder);
            shift += (a.End - a.Start) - placeholder.Length;
            shifts.Add((ex.Length, shift));
            last = a.End;
        }
        ex.Append(source, last, source.Length - last);
        var text = ex.ToString();

        int Map(int p)
        {
            var s = 0;
            foreach (var (exEnd, sh) in shifts)
            {
                if (exEnd <= p) s = sh; else break;
            }
            return p + s;
        }

        var blocks = new List<MarkdownBlock>();
        var fm = SplitFrontmatter(text);
        var offset = 0;
        if (fm.Raw.Length > 0)
        {
            blocks.Add(new MarkdownBlock(Map(0), Map(fm.Raw.Length), "frontmatter"));
            offset = text.Length - fm.Body.Length;
        }

        var lines = fm.Body.Split('\n');
        var starts = new int[lines.Length];
        for (int i = 0, p = offset; i < lines.Length; p += lines[i].Length + 1, i++) starts[i] = p;
        int EndOf(int i) => starts[i] + lines[i].Length;
        void Add(int from, int to, string kind) => blocks.Add(new MarkdownBlock(Map(starts[from]), Map(EndOf(to)), kind));
        var fence = new string(Backtick, 3);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (FencePattern().IsMatch(line) || line.StartsWith(fence, StringComparison.Ordinal))
            {
                var j = i + 1;
                while (j < lines.Length && !(FencePattern().IsMatch(lines[j]) || lines[j].StartsWith(fence, StringComparison.Ordinal))) j++;
                if (j >= lines.Length) j = lines.Length - 1;
                Add(i, j, "code");
                i = j;
                continue;
            }
            if (line.Trim().Length == 0) continue;
            if (HeadingPattern().IsMatch(line)) { Add(i, i, "heading"); continue; }
            if (HorizontalRulePattern().IsMatch(line)) { Add(i, i, "rule"); continue; }
            if (line.Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                var j = i + 2;
                while (j < lines.Length && lines[j].Trim().Length > 0 && lines[j].Contains('|')) j++;
                Add(i, j - 1, "table");
                i = j - 1;
                continue;
            }
            if (QuotePattern().IsMatch(line))
            {
                var j = i;
                while (j + 1 < lines.Length && QuotePattern().IsMatch(lines[j + 1])) j++;
                Add(i, j, "quote");
                i = j;
                continue;
            }
            if (IsListLine(line))
            {
                var j = i;
                while (j + 1 < lines.Length && IsListLine(lines[j + 1]) && !IsRuleOrTable(lines, j + 1)) j++;
                Add(i, j, "list");
                i = j;
                continue;
            }
            Add(i, i, "paragraph");
        }
        return blocks;
    }

    /// <summary>A line that opens or closes fenced code for <see cref="SplitBlocks"/>.</summary>
    internal static bool IsFenceLine(string line) =>
        FencePattern().IsMatch(line) || line.StartsWith(new string(Backtick, 3), StringComparison.Ordinal);

    private static bool IsListLine(string line) =>
        TaskPattern().IsMatch(line) || UnorderedPattern().IsMatch(line) || OrderedPattern().IsMatch(line);

    // inside a list run the renderer still tests rule, table, quote, in that order before the item patterns
    private static bool IsRuleOrTable(string[] lines, int i) =>
        HorizontalRulePattern().IsMatch(lines[i]) ||
        (lines[i].Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]));
}
