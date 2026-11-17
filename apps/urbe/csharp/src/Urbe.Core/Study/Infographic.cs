using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core.Study;

/// <summary>
/// #956: a note drawn as a vector infographic (SVG): its title, the numbers it states as highlights, and one card per
/// section with the section's first sentence. Derived from the note and remade from it; the note stays the source.
/// </summary>
public static partial class Infographic
{
    private const int Width = 1080, Margin = 64, Gap = 24, Columns = 2;
    private const int CardWidth = (Width - 2 * Margin - Gap) / Columns;

    public sealed record Section(string Title, string Summary);
    public sealed record Figure(string Value, string Label);

    public static (string Title, IReadOnlyList<Figure> Figures, IReadOnlyList<Section> Sections) Read(string markdown, string fallbackTitle)
    {
        var blocks = MarkdownEngine.SplitBlocks(markdown);
        string title = fallbackTitle;
        var sections = new List<Section>();
        var figures = new List<Figure>();
        string? open = null;
        foreach (var raw in blocks)
        {
            var block = markdown[raw.Start..raw.End].Trim();
            if (Heading().Match(block) is { Success: true } h)
            {
                var text = Plain(h.Groups[2].Value);
                if (h.Groups[1].Length == 1 && sections.Count == 0 && open is null) title = text;
                else { if (open is not null) sections.Add(new(open, "")); open = text; }
                continue;
            }
            if (block.Length == 0 || block.StartsWith("```", StringComparison.Ordinal)) continue;
            var plain = Plain(block);
            foreach (Match f in Number().Matches(plain))
                if (figures.Count < 3 && figures.All(x => x.Value != f.Value.Trim()))
                    figures.Add(new(f.Value.Trim(), string.Join(' ', plain[(f.Index + f.Length)..].Split('.', '!', '?', ';')[0]
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5))));
            if (open is not null) { sections.Add(new(open, FirstSentence(plain))); open = null; }
        }
        if (open is not null) sections.Add(new(open, ""));
        return (title, figures, sections.Take(8).ToList());
    }

    /// <summary>One drawn piece: a rounded rectangle (Text null) or a line of text at a baseline.</summary>
    // integers only, so the SVG never takes the device culture's decimal comma
    public sealed record Shape(int X, int Y, int W, int H, string Fill, string? Text = null, int Size = 0,
        bool Bold = false, bool Centered = false, string? Stroke = null);

    /// <summary>The layout both outputs draw: the SVG here, the PNG in the app.</summary>
    public static (int Width, int Height, string Title, IReadOnlyList<Shape> Shapes) Layout(string markdown, string fallbackTitle)
    {
        var (title, figures, sections) = Read(markdown, fallbackTitle);
        var shapes = new List<Shape>();
        var titleLines = Wrap(title, 30);
        for (int i = 0; i < titleLines.Count; i++) shapes.Add(new(Margin, Margin + 52 + i * 64, 0, 0, Ink, titleLines[i], 54, Bold: true));
        int y = Margin + titleLines.Count * 64 + 24;
        if (figures.Count > 0)
        {
            int fw = (Width - 2 * Margin - (figures.Count - 1) * Gap) / figures.Count;
            for (int i = 0; i < figures.Count; i++)
            {
                int x = Margin + i * (fw + Gap);
                var (value, label) = figures[i];
                shapes.Add(new(x, y, fw, 170, Accent));
                shapes.Add(new(x + fw / 2, y + 88, 0, 0, "#FFFFFF", value, value.Length > 8 ? 40 : 60, Bold: true, Centered: true));
                var words = Wrap(label, fw / 13).Take(2).ToList();
                for (int l = 0; l < words.Count; l++) shapes.Add(new(x + fw / 2, y + 126 + l * 26, 0, 0, "#DCE8E3", words[l], 20, Centered: true));
            }
            y += 170 + Gap;
        }
        int[] column = [y, y];
        foreach (var s in sections)
        {
            int c = column[0] <= column[1] ? 0 : 1, x = Margin + c * (CardWidth + Gap), top = column[c];
            var head = Wrap(s.Title, 30).Take(2).ToList();
            var lines = Wrap(s.Summary, 40).Take(5).ToList();
            int h = 40 + head.Count * 34 + lines.Count * 30 + 24;
            shapes.Add(new(x, top, CardWidth, h, "#FFFFFF", Stroke: "#E2DED5"));
            shapes.Add(new(x, top + 24, 6, 34, Accent));
            for (int i = 0; i < head.Count; i++) shapes.Add(new(x + 28, top + 52 + i * 34, 0, 0, Ink, head[i], 28, Bold: true));
            for (int i = 0; i < lines.Count; i++) shapes.Add(new(x + 28, top + 60 + head.Count * 34 + i * 30, 0, 0, "#4A4F55", lines[i], 22));
            column[c] += h + Gap;
        }
        return (Width, Math.Max(column[0], column[1]) + Margin - Gap, title, shapes);
    }

    public const string Paper = "#F7F5F0";
    private const string Ink = "#1F2328", Accent = "#2F5D50";

    public static string Svg(string markdown, string fallbackTitle)
    {
        var (width, height, title, shapes) = Layout(markdown, fallbackTitle);
        var svg = new StringBuilder($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" font-family="Inter, Roboto, 'Segoe UI', sans-serif">""");
        svg.Append($"""<title>{Esc(title)}</title><rect width="{width}" height="{height}" fill="{Paper}"/>""");
        foreach (var s in shapes)
            svg.Append(s.Text is null
                ? $"""<rect x="{s.X}" y="{s.Y}" width="{s.W}" height="{s.H}" rx="{(s.W < 10 ? 3 : 20)}" fill="{s.Fill}"{(s.Stroke is null ? "" : $" stroke=\"{s.Stroke}\"")}/>"""
                : $"""<text x="{s.X}" y="{s.Y}" font-size="{s.Size}"{(s.Bold ? " font-weight=\"700\"" : "")} fill="{s.Fill}"{(s.Centered ? " text-anchor=\"middle\"" : "")}>{Esc(s.Text)}</text>""");
        return svg.Append("</svg>").ToString();
    }

    private static string FirstSentence(string text)
    {
        var m = Sentence().Match(text);
        var s = m.Success ? m.Value.Trim() : text;
        return s.Length > 200 ? s[..199].TrimEnd() + "…" : s;
    }

    private static string Plain(string markdown) =>
        Space().Replace(Markup().Replace(Link().Replace(Wiki().Replace(markdown, m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value), "$1"), ""), " ").Trim();

    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width) { lines.Add(line.ToString()); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) lines.Add(line.ToString());
        return lines;
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);

    [GeneratedRegex(@"^(#{1,3})\s+(.+)$")]
    private static partial Regex Heading();
    [GeneratedRegex(@"(?<![\w.,])\d{1,3}(?:[.,]\d+)*\s?(?:%|mil\b|milhões\b|bilhões\b|km\b|kg\b|anos\b)|(?<![\w.,])\d{4}(?![\w.,])")]
    private static partial Regex Number();
    [GeneratedRegex(@"^.+?[.!?](?=\s|$)")]
    private static partial Regex Sentence();
    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]")]
    private static partial Regex Wiki();
    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Link();
    [GeneratedRegex(@"[*_`~=]{1,3}|^\s*(?:>+|[-+]|#+)\s+", RegexOptions.Multiline)]
    private static partial Regex Markup();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();
}
