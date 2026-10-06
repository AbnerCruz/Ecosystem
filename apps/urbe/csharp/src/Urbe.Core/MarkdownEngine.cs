using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public sealed record MarkdownFrontmatterField(string Key, string Value);
public sealed record MarkdownFrontmatter(string Raw, string Body, IReadOnlyList<MarkdownFrontmatterField> Fields);

internal sealed record MarkdownMathAtom(
    int Start, int End, string Tex, string Open, string Close, bool Display, bool Block);

public static partial class MarkdownEngine
{
    private const char InlineOpen = '\uE000';
    private const char InlineClose = '\uE001';
    private const char BlockOpen = '\uE002';
    private const char BlockClose = '\uE003';
    private const char Backtick = '\x60';

    private static readonly IReadOnlyDictionary<string, string> Callouts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["note"]="Nota", ["info"]="Informação", ["tip"]="Dica", ["success"]="Pronto",
            ["question"]="Pergunta", ["warning"]="Atenção", ["danger"]="Perigo", ["bug"]="Erro",
            ["example"]="Exemplo", ["quote"]="Citação", ["abstract"]="Resumo", ["todo"]="A fazer"
        };

    public static string EscapeHtml(string? value)
    {
        var source = value ?? string.Empty;
        var output = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            output.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => ch.ToString()
            });
        }
        return output.ToString();
    }

    public static string SafeUrl(string? url)
    {
        var source = url ?? string.Empty;
        var compact = WhitespaceControlPattern().Replace(
            EntityPattern().Replace(source, string.Empty),
            string.Empty).ToLowerInvariant();

        if (compact.StartsWith("javascript:", StringComparison.Ordinal) ||
            compact.StartsWith("vbscript:", StringComparison.Ordinal) ||
            compact.StartsWith("file:", StringComparison.Ordinal))
            return "#";

        if (compact.StartsWith("data:", StringComparison.Ordinal) &&
            !compact.StartsWith("data:image/", StringComparison.Ordinal))
            return "#";

        return source;
    }

    public static MarkdownFrontmatter SplitFrontmatter(string? markdown)
    {
        var source = NormalizeNewlines(markdown ?? string.Empty);
        if (!source.StartsWith("---\n", StringComparison.Ordinal))
            return new MarkdownFrontmatter(string.Empty, source, Array.Empty<MarkdownFrontmatterField>());

        var end = source.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
            return new MarkdownFrontmatter(string.Empty, source, Array.Empty<MarkdownFrontmatterField>());

        var raw = source[..(end + 4)];
        var body = LeadingNewlines().Replace(source[(end + 4)..], string.Empty);
        var fields = new List<MarkdownFrontmatterField>();
        string? key = null;
        var value = string.Empty;

        foreach (var line in raw.Split('\n').Skip(1).SkipLast(1))
        {
            var field = FrontmatterFieldPattern().Match(line);
            if (field.Success)
            {
                if (key is not null)
                    fields.Add(new MarkdownFrontmatterField(key, value));
                key = field.Groups[1].Value;
                value = field.Groups[2].Value;
                continue;
            }

            var item = FrontmatterListItemPattern().Match(line);
            if (item.Success && key is not null)
                value += (value.Length > 0 ? ", " : string.Empty) + item.Groups[1].Value;
        }

        if (key is not null)
            fields.Add(new MarkdownFrontmatterField(key, value));

        return new MarkdownFrontmatter(raw, body, fields.AsReadOnly());
    }

    public static string InlineMarkdown(string? source)
    {
        var text = EscapeHtml(source ?? string.Empty);
        var stored = new List<string>();
        var code = new Dictionary<int, string>();

        string Store(string html)
        {
            stored.Add(html);
            return "\u0002" + (stored.Count - 1) + "\u0002";
        }

        text = InlineCodePattern().Replace(text, match =>
        {
            var index = stored.Count;
            code[index] = match.Groups[1].Value;
            return Store("<code>" + match.Groups[1].Value + "</code>");
        });

        text = WikiPattern().Replace(text, match =>
        {
            var target = match.Groups[1].Value.Trim();
            var label = match.Groups[2].Success ? match.Groups[2].Value.Trim() : target;
            return Store("<span class=\"wikilink\" data-note-name=\"" +
                         EscapeHtml(target) + "\">" + EscapeHtml(label) + "</span>");
        });

        text = ImagePattern().Replace(text, match =>
        {
            var alt = PlaceholderPattern().Replace(match.Groups[1].Value, nested =>
            {
                var index = int.Parse(nested.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return code.TryGetValue(index, out var raw)
                    ? new string(Backtick, 1) + raw + new string(Backtick, 1)
                    : nested.Value;
            });
            return Store("<img src=\"" + SafeUrl(match.Groups[2].Value) +
                         "\" alt=\"" + alt + "\">");
        });

        text = LinkPattern().Replace(text, match =>
            Store("<a href=\"" + SafeUrl(match.Groups[2].Value) +
                  "\" target=\"_blank\" rel=\"noopener\">" +
                  InlineEmphasis(match.Groups[1].Value) + "</a>"));

        text = InlineEmphasis(text);

        for (var pass = 0; pass < 4 && PlaceholderPattern().IsMatch(text); pass++)
        {
            text = PlaceholderPattern().Replace(text, match =>
            {
                var index = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return index >= 0 && index < stored.Count ? stored[index] : match.Value;
            });
        }

        return text;
    }

    public static string Render(string? markdown)
    {
        var source = markdown ?? string.Empty;
        var atoms = ScanMath(source);
        if (atoms.Count == 0)
            return RenderHeadings(source);

        var extracted = new StringBuilder();
        var last = 0;
        for (var index = 0; index < atoms.Count; index++)
        {
            var atom = atoms[index];
            extracted.Append(source, last, atom.Start - last);
            extracted.Append(atom.Block ? BlockOpen : InlineOpen);
            extracted.Append(index);
            extracted.Append(atom.Block ? BlockClose : InlineClose);
            last = atom.End;
        }
        extracted.Append(source, last, source.Length - last);

        var html = RenderHeadings(extracted.ToString());

        html = Regex.Replace(
            html,
            "<p>\\s*" + Regex.Escape(BlockOpen.ToString()) + "(\\d+)" +
            Regex.Escape(BlockClose.ToString()) + "\\s*</p>",
            match => MathAtomHtml(atoms[int.Parse(match.Groups[1].Value)], true),
            RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            Regex.Escape(BlockOpen.ToString()) + "(\\d+)" + Regex.Escape(BlockClose.ToString()),
            match => MathAtomHtml(atoms[int.Parse(match.Groups[1].Value)], false),
            RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            Regex.Escape(InlineOpen.ToString()) + "(\\d+)" + Regex.Escape(InlineClose.ToString()),
            match => MathAtomHtml(atoms[int.Parse(match.Groups[1].Value)], false),
            RegexOptions.CultureInvariant);

        return html;
    }

    internal static string RawMath(string? open, string? tex, string? close) =>
        (string.IsNullOrEmpty(open) ? "$" : open) +
        (tex ?? string.Empty) +
        (string.IsNullOrEmpty(close) ? "$" : close);

    private static string RenderHeadings(string markdown)
    {
        var lines = NormalizeNewlines(markdown).Split('\n');
        var inCode = false;
        var fence = new string(Backtick, 3);

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(fence, StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
                continue;

            var match = EmptyHeadingPattern().Match(lines[i]);
            if (match.Success)
                lines[i] = match.Groups[1].Value + " \u0001";
        }

        return RenderBlocks(string.Join('\n', lines)).Replace("\u0001", "<br>", StringComparison.Ordinal);
    }

    private static string RenderBlocks(string markdown)
    {
        var fm = SplitFrontmatter(markdown);
        var lines = NormalizeNewlines(fm.Body).Split('\n');
        var output = new StringBuilder(RenderFrontmatter(fm.Fields));
        var inCode = false;
        var code = new List<string>();
        var language = string.Empty;
        var stack = new List<(string Type, int Indent)>();
        var fence = new string(Backtick, 3);

        void CloseLevel()
        {
            var top = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            output.Append("</li></").Append(top.Type).Append('>');
        }

        void CloseList()
        {
            while (stack.Count > 0) CloseLevel();
        }

        void Item(string type, int indent, string html)
        {
            while (stack.Count > 0 && indent < stack[^1].Indent) CloseLevel();
            if (stack.Count > 0 && indent == stack[^1].Indent &&
                !string.Equals(stack[^1].Type, type, StringComparison.Ordinal))
                CloseLevel();

            if (stack.Count == 0 || indent > stack[^1].Indent)
            {
                output.Append('<').Append(type).Append('>');
                stack.Add((type, indent));
            }
            else output.Append("</li>");

            output.Append(html);
        }

        void FlushCode()
        {
            if (!inCode) return;
            output.Append("<pre");
            if (language.Length > 0)
                output.Append(" data-lang=\"").Append(EscapeHtml(language)).Append('"');
            output.Append("><code>").Append(EscapeHtml(string.Join('\n', code))).Append("</code></pre>");
            code.Clear();
            inCode = false;
            language = string.Empty;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            Match match = FencePattern().Match(line);

            if (match.Success || line.StartsWith(fence, StringComparison.Ordinal))
            {
                if (inCode) FlushCode();
                else
                {
                    CloseList();
                    inCode = true;
                    code.Clear();
                    language = match.Success ? match.Groups[1].Value : string.Empty;
                }
                continue;
            }

            if (inCode)
            {
                code.Add(line);
                continue;
            }

            if (line.Trim().Length == 0)
            {
                CloseList();
                continue;
            }

            match = HeadingPattern().Match(line);
            if (match.Success)
            {
                CloseList();
                var level = match.Groups[1].Value.Length;
                output.Append("<h").Append(level).Append('>')
                    .Append(InlineMarkdown(match.Groups[2].Value))
                    .Append("</h").Append(level).Append('>');
                continue;
            }

            if (HorizontalRulePattern().IsMatch(line))
            {
                CloseList();
                output.Append("<hr>");
                continue;
            }

            if (line.Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                CloseList();
                var head = TableCells(line);
                var aligns = TableCells(lines[i + 1]).Select(cell =>
                    Regex.IsMatch(cell, "^:-+:$") ? "center" :
                    Regex.IsMatch(cell, "-:$") ? "right" :
                    Regex.IsMatch(cell, "^:-") ? "left" : string.Empty).ToArray();

                string Cell(string tag, string value, int index)
                {
                    var align = index < aligns.Length ? aligns[index] : string.Empty;
                    var rendered = InlineMarkdown(value);
                    return "<" + tag +
                           (align.Length > 0 ? " style=\"text-align:" + align + "\"" : string.Empty) +
                           ">" + (rendered.Length > 0 ? rendered : "<br>") + "</" + tag + ">";
                }

                output.Append("<table data-md-table=\"1\" data-align=\"")
                    .Append(string.Join(',', aligns)).Append("\"><thead><tr>");
                for (var column = 0; column < head.Count; column++)
                    output.Append(Cell("th", head[column], column));
                output.Append("</tr></thead><tbody>");

                i += 2;
                while (i < lines.Length && lines[i].Trim().Length > 0 && lines[i].Contains('|'))
                {
                    var cells = TableCells(lines[i]);
                    output.Append("<tr>");
                    for (var column = 0; column < head.Count; column++)
                        output.Append(Cell("td", column < cells.Count ? cells[column] : string.Empty, column));
                    output.Append("</tr>");
                    i++;
                }
                i--;
                output.Append("</tbody></table>");
                continue;
            }

            if (QuotePattern().IsMatch(line))
            {
                CloseList();
                var group = new List<string>();
                while (i < lines.Length && QuotePattern().IsMatch(lines[i]))
                {
                    group.Add(QuotePrefixPattern().Replace(lines[i], string.Empty));
                    i++;
                }
                i--;

                var callout = group.Count > 0 ? CalloutPattern().Match(group[0]) : Match.Empty;
                if (callout.Success)
                {
                    var type = callout.Groups[1].Value.ToLowerInvariant();
                    var title = callout.Groups[2].Value;
                    var defaultTitle = Callouts.TryGetValue(type, out var known) ? known : type;
                    var body = string.Join("<br>", group.Skip(1).Select(InlineMarkdown));
                    output.Append("<div class=\"callout callout-").Append(EscapeHtml(type))
                        .Append("\" data-callout=\"").Append(EscapeHtml(type))
                        .Append("\"><div class=\"callout-title\" data-default=\"")
                        .Append(EscapeHtml(defaultTitle)).Append("\">")
                        .Append(InlineMarkdown(title).Length > 0 ? InlineMarkdown(title) : "<br>")
                        .Append("</div><div class=\"callout-body\">")
                        .Append(body.Length > 0 ? body : "<br>")
                        .Append("</div></div>");
                }
                else
                {
                    output.Append("<blockquote>")
                        .Append(string.Join("<br>", group.Select(InlineMarkdown)))
                        .Append("</blockquote>");
                }
                continue;
            }

            match = TaskPattern().Match(line);
            if (match.Success)
            {
                var check = match.Groups[2].Value.Equals("x", StringComparison.OrdinalIgnoreCase)
                    ? " checked" : string.Empty;
                Item("ul", IndentOf(line),
                    "<li class=\"task\"><input type=\"checkbox\"" + check + ">" +
                    InlineMarkdown(match.Groups[3].Value));
                continue;
            }

            match = UnorderedPattern().Match(line);
            if (match.Success)
            {
                Item("ul", IndentOf(line), "<li>" + InlineMarkdown(match.Groups[2].Value));
                continue;
            }

            match = OrderedPattern().Match(line);
            if (match.Success)
            {
                Item("ol", IndentOf(line), "<li>" + InlineMarkdown(match.Groups[3].Value));
                continue;
            }

            CloseList();
            output.Append("<p>").Append(InlineMarkdown(line)).Append("</p>");
        }

        CloseList();
        FlushCode();
        return output.Length == 0 ? "<p><br></p>" : output.ToString();
    }

    private static string RenderFrontmatter(IReadOnlyList<MarkdownFrontmatterField> fields)
    {
        if (fields.Count == 0) return string.Empty;

        var chipKeys = new HashSet<string>(
            new[] { "tags", "tag", "aliases", "alias" },
            StringComparer.Ordinal);

        string ValueHtml(MarkdownFrontmatterField field)
        {
            var raw = field.Value.Trim();
            if (!chipKeys.Contains(field.Key.ToLowerInvariant()))
                return InlineMarkdown(raw.Length == 0 ? "—" : raw);

            var clean = Regex.Replace(raw, @"^\[|\]$", string.Empty);
            var values = clean.Split(',')
                .Select(x => x.Trim().Trim('\'', '"'))
                .Where(x => x.Length > 0)
                .ToArray();

            return values.Length == 0
                ? "—"
                : string.Join(string.Empty,
                    values.Select(x => "<span class=\"frontmatterChip\">" + EscapeHtml(x) + "</span>"));
        }

        return "<section class=\"frontmatterCard\" contenteditable=\"false\" data-frontmatter-card=\"1\">" +
               string.Join(string.Empty, fields.Select(field =>
                   "<div class=\"frontmatterRow\"><span class=\"frontmatterKey\">" +
                   EscapeHtml(field.Key) + "</span><span class=\"frontmatterValue\">" +
                   ValueHtml(field) + "</span></div>")) +
               "</section>";
    }

    private static List<string> TableCells(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|')) text = text[1..];
        if (text.EndsWith('|') && !text.EndsWith("\\|", StringComparison.Ordinal)) text = text[..^1];

        var cells = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
            {
                current.Append('|');
                i++;
            }
            else if (text[i] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(text[i]);
        }
        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static bool IsTableSeparator(string line) =>
        TableSeparatorPattern().IsMatch(line) && line.Contains('-');

    private static int IndentOf(string line) =>
        LeadingWhitespace().Match(line).Value.Replace("\t", "    ", StringComparison.Ordinal).Length;

    private static string InlineEmphasis(string text) =>
        StrikePattern().Replace(
            ItalicUnderscorePattern().Replace(
                ItalicAsteriskPattern().Replace(
                    BoldUnderscorePattern().Replace(
                        BoldAsteriskPattern().Replace(text, "<strong>$1</strong>"),
                        "<strong>$1</strong>"),
                    "<em>$1</em>"),
                "<em>$1</em>"),
            "<del>$1</del>");

    private static List<MarkdownMathAtom> ScanMath(string source)
    {
        var atoms = new List<MarkdownMathAtom>();
        var i = 0;
        var lineStart = 0;
        char? inFence = null;
        var fenceLength = 0;

        bool AtLineStart(int index)
        {
            var j = index - 1;
            while (j >= 0 && (source[j] == ' ' || source[j] == '\t')) j--;
            return j < 0 || source[j] == '\n';
        }

        bool AtLineEnd(int index)
        {
            var j = index;
            while (j < source.Length && (source[j] == ' ' || source[j] == '\t')) j++;
            return j >= source.Length || source[j] == '\n';
        }

        while (i < source.Length)
        {
            if (i == lineStart)
            {
                var sample = source[i..Math.Min(source.Length, i + 40)];
                var fence = CodeFenceScanPattern().Match(sample);
                if (fence.Success)
                {
                    var run = fence.Groups[1].Value;
                    if (inFence is null)
                    {
                        inFence = run[0];
                        fenceLength = run.Length;
                    }
                    else if (run[0] == inFence && run.Length >= fenceLength)
                    {
                        inFence = null;
                        fenceLength = 0;
                    }

                    var nl = source.IndexOf('\n', i);
                    i = nl < 0 ? source.Length : nl + 1;
                    lineStart = i;
                    continue;
                }
            }

            if (inFence is not null)
            {
                var nl = source.IndexOf('\n', i);
                i = nl < 0 ? source.Length : nl + 1;
                lineStart = i;
                continue;
            }

            var ch = source[i];
            if (ch == '\n')
            {
                i++;
                lineStart = i;
                continue;
            }

            if (ch == Backtick)
            {
                var runLength = 1;
                while (i + runLength < source.Length && source[i + runLength] == Backtick) runLength++;
                var token = new string(Backtick, runLength);
                var close = source.IndexOf(token, i + runLength, StringComparison.Ordinal);
                i = close < 0 ? i + runLength : close + runLength;
                continue;
            }

            if (ch == '\\')
            {
                if (i + 1 >= source.Length) { i++; continue; }
                var next = source[i + 1];
                if (next == '$') { i += 2; continue; }

                if (next is '(' or '[')
                {
                    var closeToken = next == '(' ? "\\)" : "\\]";
                    var end = source.IndexOf(closeToken, i + 2, StringComparison.Ordinal);
                    if (end > i + 2)
                    {
                        var tex = source[(i + 2)..end];
                        if (next == '(' && tex.Contains('\n')) { i += 2; continue; }
                        var display = next == '[';
                        atoms.Add(new MarkdownMathAtom(
                            i, end + 2, tex, "\\" + next, closeToken,
                            display, display && AtLineStart(i) && AtLineEnd(end + 2)));
                        i = end + 2;
                        continue;
                    }
                }

                i += 2;
                continue;
            }

            if (ch == '$')
            {
                if (i + 1 < source.Length && source[i + 1] == '$')
                {
                    var end = source.IndexOf("$$", i + 2, StringComparison.Ordinal);
                    while (end > 0 && source[end - 1] == '\\')
                        end = source.IndexOf("$$", end + 2, StringComparison.Ordinal);

                    if (end > i + 2)
                    {
                        var body = source[(i + 2)..end];
                        if (!Regex.IsMatch(body, @"\n[ \t]*\n"))
                        {
                            var open = "$$";
                            var close = "$$";
                            var tex = body;

                            var lead = Regex.Match(body, @"^[ \t]*\n");
                            if (lead.Success)
                            {
                                open += lead.Value;
                                tex = body[lead.Length..];
                            }

                            var trail = Regex.Match(body, @"\n[ \t]*$");
                            if (trail.Success)
                            {
                                close = trail.Value + "$$";
                                tex = tex[..Math.Max(0, tex.Length - trail.Length)];
                            }

                            atoms.Add(new MarkdownMathAtom(
                                i, end + 2, tex, open, close, true,
                                AtLineStart(i) && AtLineEnd(end + 2)));
                            i = end + 2;
                            continue;
                        }
                    }

                    i += 2;
                    continue;
                }

                var next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (next == '\0' || char.IsWhiteSpace(next)) { i++; continue; }

                var j = i + 1;
                var found = -1;
                while (j < source.Length && source[j] != '\n')
                {
                    if (source[j] == '\\') { j += 2; continue; }
                    if (source[j] == '$')
                    {
                        var prev = source[j - 1];
                        var after = j + 1 < source.Length ? source[j + 1] : '\0';
                        if (!char.IsWhiteSpace(prev) && !char.IsDigit(after))
                        {
                            found = j;
                            break;
                        }
                    }
                    j++;
                }

                if (found > i + 1)
                {
                    atoms.Add(new MarkdownMathAtom(
                        i, found + 1, source[(i + 1)..found], "$", "$", false, false));
                    i = found + 1;
                    continue;
                }
            }

            i++;
        }

        return atoms;
    }

    private static string MathAtomHtml(MarkdownMathAtom atom, bool block)
    {
        var attrs = " data-tex=\"" + EscapeHtml(atom.Tex) +
                    "\" data-open=\"" + EscapeHtml(atom.Open) +
                    "\" data-close=\"" + EscapeHtml(atom.Close) +
                    "\" contenteditable=\"false\"";
        var inner = "<code class=\"umath-raw\">" + EscapeHtml(atom.Tex) + "</code>";

        if (block)
            return "<div class=\"umath umath-block\"" + attrs + ">" + inner + "</div>";

        return "<span class=\"umath" + (atom.Display ? " umath-display" : string.Empty) +
               "\"" + attrs + ">" + inner + "</span>";
    }

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    [GeneratedRegex(@"&(?:#\d+|#x[0-9a-fA-F]+|\w+);", RegexOptions.CultureInvariant)]
    private static partial Regex EntityPattern();
    [GeneratedRegex(@"[\s\u0000-\u001f]", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceControlPattern();
    [GeneratedRegex(@"^\n+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNewlines();
    [GeneratedRegex(@"^([A-Za-z0-9_-]+):\s*(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrontmatterFieldPattern();
    [GeneratedRegex(@"^\s*-\s*(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrontmatterListItemPattern();
    [GeneratedRegex(@"\x60([^\x60\n]+)\x60", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodePattern();
    [GeneratedRegex(@"\[\[([^\]|#]+)(?:\|([^\]]+))?\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex WikiPattern();
    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
    [GeneratedRegex(@"\u0002(\d+)\u0002", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
    [GeneratedRegex(@"\*\*([^*\n]+)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex BoldAsteriskPattern();
    [GeneratedRegex(@"__([^_\n]+)__", RegexOptions.CultureInvariant)]
    private static partial Regex BoldUnderscorePattern();
    [GeneratedRegex(@"(?<!\*)\*([^*\n]+)\*(?!\*)", RegexOptions.CultureInvariant)]
    private static partial Regex ItalicAsteriskPattern();
    [GeneratedRegex(@"(?<!_)_([^_\n]+)_(?!_)", RegexOptions.CultureInvariant)]
    private static partial Regex ItalicUnderscorePattern();
    [GeneratedRegex(@"~~([^~\n]+)~~", RegexOptions.CultureInvariant)]
    private static partial Regex StrikePattern();
    [GeneratedRegex(@"^(#{1,6})[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyHeadingPattern();
    [GeneratedRegex(@"^\x60\x60\x60\s*([\w+#.-]*)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FencePattern();
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();
    [GeneratedRegex(@"^ {0,3}([-*_])(?:\s*\1){2,}\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalRulePattern();
    [GeneratedRegex(@"^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TableSeparatorPattern();
    [GeneratedRegex(@"^>\s?", RegexOptions.CultureInvariant)]
    private static partial Regex QuotePattern();
    [GeneratedRegex(@"^>\s?", RegexOptions.CultureInvariant)]
    private static partial Regex QuotePrefixPattern();
    [GeneratedRegex(@"^\[!(\w+)\][+-]?\s*(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex CalloutPattern();
    [GeneratedRegex(@"^([ \t]*)[-*+]\s+\[([ xX])\]\s+(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex(@"^([ \t]*)[-*+]\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex UnorderedPattern();
    [GeneratedRegex(@"^([ \t]*)(\d+)[.)]\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedPattern();
    [GeneratedRegex(@"^[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingWhitespace();
    [GeneratedRegex(@"^ {0,3}(\x60{3,}|~{3,})", RegexOptions.CultureInvariant)]
    private static partial Regex CodeFenceScanPattern();
}
