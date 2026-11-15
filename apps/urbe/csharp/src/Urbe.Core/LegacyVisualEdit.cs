using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The tool actions of the 1.8.4 Visual editor (v23PrefixarBloco / v23Envolver in app.js, the selection bubble and the
/// "/" menu in visual-tools.js) expressed on the Markdown source of one block. The original acts on the DOM of a
/// contenteditable (execCommand); the native Visual surface edits one block's Markdown at a time, so the same
/// transformations are applied to that text. Unit-tested (LegacyVisualEditTests); the DOM original has no pure oracle.
/// </summary>
public static partial class LegacyVisualEdit
{
    private const string Ws = @"[\t\n\v\f\r    -     　﻿]";

    [GeneratedRegex(@"^(#{1,6})" + Ws + "+")] private static partial Regex HeadingPrefix();
    [GeneratedRegex(@"^>" + Ws + "?")] private static partial Regex QuotePrefix();
    [GeneratedRegex(@"^[ \t]*(?:[-*+]|\d+[.)])" + Ws + @"+(?:\[[ xX]\]" + Ws + "+)?")] private static partial Regex ListPrefix();
    [GeneratedRegex(@"^" + Ws + @"*(https?://|mailto:)\S+", RegexOptions.IgnoreCase)] private static partial Regex UrlRule();

    /// <summary>The prompt validation of the "Link externo" tool.</summary>
    public static bool IsValidUrl(string value) => UrlRule().IsMatch(value);

    private static (int Start, int End) LineAt(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var start = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var end = text.IndexOf('\n', caret);
        return (start, end < 0 ? text.Length : end);
    }

    private static string StripBlock(string line)
    {
        var m = HeadingPrefix().Match(line);
        if (m.Success) return line[m.Length..];
        m = QuotePrefix().Match(line);
        if (m.Success) return line[m.Length..];
        m = ListPrefix().Match(line);
        return m.Success ? line[m.Length..] : line;
    }

    /// <summary>v23PrefixarBloco(prefix) for "# ", "## ", "### ", "> ", "- ", "- [ ] " on the line of the caret.</summary>
    public static LegacyEditorTools.TextEdit ToggleBlock(string text, int caret, string prefix)
    {
        var (s, e) = LineAt(text, caret);
        var line = text[s..e];
        var inList = ListPrefix().IsMatch(line);
        string replaced;
        var range = (Start: s, End: e);
        if (HeadingMarks().Match(prefix) is { Success: true } h)
        {
            var n = h.Groups[1].Value.Length;
            var current = HeadingPrefix().Match(line);
            var content = StripBlock(line);
            replaced = current.Success && current.Groups[1].Length == n ? content : new string('#', n) + " " + content;
        }
        else if (prefix.StartsWith('>'))
        {
            // a quote is one block: every line of the group changes together
            range = QuoteGroup(text, s, e);
            var group = text[range.Start..range.End].Split('\n');
            replaced = QuotePrefix().IsMatch(line)
                ? string.Join('\n', group.Select(l => QuotePrefix().Replace(l, "")))
                : "> " + StripBlock(line);
            if (!QuotePrefix().IsMatch(line)) range = (s, e);
        }
        else
        {
            var task = prefix.Contains('[');
            replaced = inList ? StripBlock(line) : (task ? "- [ ] " : "- ") + StripBlock(line);
        }
        var next = text[..range.Start] + replaced + text[range.End..];
        var end = range.Start + replaced.Length;
        // the caret goes to the end of the block (setCaretEnd); a quote group ends at its last line
        return new LegacyEditorTools.TextEdit(next, end, end);
    }

    [GeneratedRegex(@"^(#{1,6})" + Ws + "$")] private static partial Regex HeadingMarks();

    private static (int Start, int End) QuoteGroup(string text, int s, int e)
    {
        int start = s, end = e;
        while (start > 0)
        {
            var prevStart = text.LastIndexOf('\n', start - 2) + 1;
            if (!QuotePrefix().IsMatch(text[prevStart..(start - 1)])) break;
            start = prevStart;
        }
        while (end < text.Length)
        {
            var nextEnd = text.IndexOf('\n', end + 1);
            if (nextEnd < 0) nextEnd = text.Length;
            if (!QuotePrefix().IsMatch(text[(end + 1)..nextEnd])) break;
            end = nextEnd;
        }
        return (start, end);
    }

    /// <summary>execCommand("bold"/"italic"): wraps the selection with the marker, or removes it when already applied.</summary>
    public static LegacyEditorTools.TextEdit ToggleWrap(string text, int selectionStart, int selectionEnd, string marker)
    {
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        // Markdown only reads a mark that touches the text: "**palavra **" stays as stars. The spaces a selection
        // takes at its ends (a double tap often takes the space after the word) stay outside the marks.
        if (b > a && text.AsSpan(a, b - a).Trim().Length > 0)
        {
            while (a < b && char.IsWhiteSpace(text[a])) a++;
            while (b > a && char.IsWhiteSpace(text[b - 1])) b--;
        }
        // a selection over several lines: each line gets its own marks (a mark never crosses a line break)
        if (text.AsSpan(a, b - a).Contains('\n'))
            return EachLine(text, a, b, marker);
        var m = marker.Length;
        if (a >= m && b + m <= text.Length && text.AsSpan(a - m, m).SequenceEqual(marker) && text.AsSpan(b, m).SequenceEqual(marker) && (marker.Length > 1 || !IsDouble(text, a - 1, b)))
            return new(text[..(a - m)] + text[a..b] + text[(b + m)..], a - m, b - m);
        if (b - a >= 2 * m && text.AsSpan(a, m).SequenceEqual(marker) && text.AsSpan(b - m, m).SequenceEqual(marker))
            return new(text[..a] + text[(a + m)..(b - m)] + text[b..], a, b - 2 * m);
        return new(text[..a] + marker + text[a..b] + marker + text[b..], a + m, b + m);
    }

    private static bool InsideWord(string text, int a, int b)
    {
        while (a < b && char.IsWhiteSpace(text[a])) a++;
        while (b > a && char.IsWhiteSpace(text[b - 1])) b--;
        return a > 0 && char.IsLetterOrDigit(text[a - 1]) || b < text.Length && char.IsLetterOrDigit(text[b]);
    }

    private static LegacyEditorTools.TextEdit EachLine(string text, int a, int b, string marker)
    {
        var sb = new System.Text.StringBuilder(text.Length + 16);
        sb.Append(text, 0, a);
        var lines = text[a..b].Split('\n');
        // all of them marked already: the marks go; otherwise every line gets them
        static string Prefix(string core) => System.Text.RegularExpressions.Regex.Match(core, @"^(#{1,6}\s+|>\s?|[-*+]\s+(\[[ xX]\]\s+)?|\d+[.)]\s+)").Value;
        bool allMarked = lines.Select(l => l.Trim()).Where(c => c.Length > 0).Select(c => c[Prefix(c).Length..]).Where(body => body.Length > 0)
            .All(body => body.Length >= 2 * marker.Length && body.StartsWith(marker, StringComparison.Ordinal) && body.EndsWith(marker, StringComparison.Ordinal));
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            var line = lines[i];
            var core = line.Trim();
            if (core.Length == 0) { sb.Append(line); continue; }
            int lead = line.Length - line.TrimStart().Length, trail = line.Length - line.TrimEnd().Length;
            // a list, quote or heading mark stays outside: "- **item**", not "**- item**"
            var prefix = Prefix(core);
            var body = core[prefix.Length..];
            sb.Append(line, 0, lead).Append(prefix);
            if (body.Length == 0) { }
            else if (allMarked) sb.Append(body, marker.Length, body.Length - 2 * marker.Length);
            else sb.Append(marker).Append(body).Append(marker);
            sb.Append(line, line.Length - trail, trail);
        }
        var changed = sb.Length - a;
        sb.Append(text, b, text.Length - b);
        return new(sb.ToString(), a, a + changed);
    }

    // "_" next to another "_" is the bold form of the other syntax, not italic
    private static bool IsDouble(string text, int open, int close) =>
        (open > 0 && text[open - 1] == text[open]) || (close + 1 < text.Length && text[close + 1] == text[close]);

    /// <summary>
    /// Inline code in the running text. The 1.8.4 inserted &lt;code&gt;selection or "código"&lt;/code&gt;; the owner did not want
    /// the placeholder word (DEC-0045, UC-37): with no selection the caret goes between two backticks to type the code,
    /// a selection becomes code, and a selection that already is code goes back to plain text.
    /// </summary>
    public static LegacyEditorTools.TextEdit InlineCode(string text, int selectionStart, int selectionEnd)
    {
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        if (a == b) return new(text[..a] + "``" + text[a..], a + 1, a + 1);
        return ToggleWrap(text, a, b, "`");
    }

    /// <summary>
    /// The bold, italic and code buttons (UC-55): a button that shows as on (<see cref="LegacyEditorTools.EnclosingMark"/>)
    /// takes its marks off, even with only the caret inside them; otherwise the marks go in (<see cref="ToggleWrap"/>,
    /// <see cref="InlineCode"/>). The tap always does what the lit button says.
    /// </summary>
    public static LegacyEditorTools.TextEdit ToggleMark(string text, int selectionStart, int selectionEnd, string id)
    {
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        if (LegacyEditorTools.EnclosingMark(text, a, b, id) is var (open, close, m))
            return new(text[..open] + text[(open + m)..close] + text[(close + m)..], a - m, b - m);
        return id switch
        {
            "code" => InlineCode(text, a, b),
            // "_" inside a word is not read as italic ("pala_vra_"): there the star is used
            "italic" => ToggleWrap(text, a, b, InsideWord(text, a, b) ? "*" : "_"),
            _ => ToggleWrap(text, a, b, "**"),
        };
    }

    /// <summary>A %%comment%% (DEC-0045, UC-43): the selection becomes one (or stops being one); with none, the caret goes
    /// between the marks to write it.</summary>
    public static LegacyEditorTools.TextEdit Comment(string text, int selectionStart, int selectionEnd)
    {
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        if (a == b) return new(text[..a] + "%%  %%" + text[a..], a + 3, a + 3);
        return ToggleWrap(text, a, b, "%%");
    }

    /// <summary>The "/" menu's Bloco de código (DEC-0045, UC-37): an empty fenced block with the caret inside it.</summary>
    public static LegacyEditorTools.TextEdit CodeBlock(string text, int caret)
    {
        var at = Math.Clamp(caret, 0, text.Length);
        int lineStart = text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        if (at == 0) lineStart = 0;
        int lineEnd = text.IndexOf('\n', at);
        if (lineEnd < 0) lineEnd = text.Length;
        var line = text[lineStart..lineEnd];
        // "/" at the start of an empty block: the typed "/query" goes, the block becomes the fence
        var keep = line.TrimStart().StartsWith('/') ? "" : line;
        var fence = (keep.Length > 0 ? keep + "\n" : "") + "```\n\n```";
        var caretAt = lineStart + fence.Length - 4;
        return new(text[..lineStart] + fence + text[lineEnd..], caretAt, caretAt);
    }

    /// <summary>CST-069: the "/" menu's Gráfico de função — a ```grafico block with an example, the caret on the function.</summary>
    public static LegacyEditorTools.TextEdit Plot(string text, int caret)
    {
        var at = Math.Clamp(caret, 0, text.Length);
        int lineStart = at == 0 ? 0 : text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        int lineEnd = text.IndexOf('\n', at);
        if (lineEnd < 0) lineEnd = text.Length;
        var line = text[lineStart..lineEnd];
        var keep = line.TrimStart().StartsWith('/') ? "" : line;
        var head = (keep.Length > 0 ? keep + "\n" : "") + "```" + FunctionPlot.Language + "\ny = ";
        const string example = "sin(x)";
        var block = head + example + "\nx: -6..6\n```";
        int from = lineStart + head.Length;
        return new(text[..lineStart] + block + text[lineEnd..], from, from + example.Length);
    }

    /// <summary>CST-206: the "/" menu's Cálculo — a ```calculo block with an example, the caret on its first line.</summary>
    public static LegacyEditorTools.TextEdit Calc(string text, int caret)
    {
        var at = Math.Clamp(caret, 0, text.Length);
        int lineStart = at == 0 ? 0 : text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        int lineEnd = text.IndexOf('\n', at);
        if (lineEnd < 0) lineEnd = text.Length;
        var line = text[lineStart..lineEnd];
        var keep = line.TrimStart().StartsWith('/') ? "" : line;
        var head = (keep.Length > 0 ? keep + "\n" : "") + "```calculo\n";
        const string example = "preco = 12,5";
        var block = head + example + "\npreco * 4\n```";
        int from = lineStart + head.Length;
        return new(text[..lineStart] + block + text[lineEnd..], from, from + example.Length);
    }

    /// <summary>CST-207: the "/" menu's Dados — a ```dados block naming a source, the caret on the path.</summary>
    public static LegacyEditorTools.TextEdit Data(string text, int caret)
    {
        var at = Math.Clamp(caret, 0, text.Length);
        int lineStart = at == 0 ? 0 : text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        int lineEnd = text.IndexOf('\n', at);
        if (lineEnd < 0) lineEnd = text.Length;
        var line = text[lineStart..lineEnd];
        var keep = line.TrimStart().StartsWith('/') ? "" : line;
        var head = (keep.Length > 0 ? keep + "\n" : "") + "```dados\nfonte: ";
        const string example = "Planilhas/arquivo.csv";
        var block = head + example + "\nresumo\n```";
        int from = lineStart + head.Length;
        return new(text[..lineStart] + block + text[lineEnd..], from, from + example.Length);
    }

    /// <summary>CST-174: the "/" menu's Questionário — a ```quiz block with one example question.</summary>
    public static LegacyEditorTools.TextEdit Quiz(string text, int caret)
    {
        var at = Math.Clamp(caret, 0, text.Length);
        int lineStart = at == 0 ? 0 : text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        int lineEnd = text.IndexOf('\n', at);
        if (lineEnd < 0) lineEnd = text.Length;
        var line = text[lineStart..lineEnd];
        var keep = line.TrimStart().StartsWith('/') ? "" : line;
        var head = (keep.Length > 0 ? keep + "\n" : "") + "```quiz\n? ";
        const string example = "Escreva a pergunta";
        var block = head + example + "\n- [x] alternativa certa\n- [ ] alternativa errada\n> Explicação da resposta.\n```";
        int from = lineStart + head.Length;
        return new(text[..lineStart] + block + text[lineEnd..], from, from + example.Length);
    }

    /// <summary>The bubble's wikilink button: the trimmed selection becomes [[selection]]; null when empty.</summary>
    public static LegacyEditorTools.TextEdit? WikiOfSelection(string text, int selectionStart, int selectionEnd)
    {
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        var t = LegacyCustomize.JsTrim(text[a..b]);
        if (t.Length == 0) return null;
        var link = "[[" + t + "]]";
        return new(text[..a] + link + text[b..], a + link.Length, a + link.Length);
    }

    /// <summary>The "Link externo" tool after the URL prompt: [selection or url](url).</summary>
    public static LegacyEditorTools.TextEdit? Link(string text, int selectionStart, int selectionEnd, string url)
    {
        if (!IsValidUrl(url)) return null;
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        var u = LegacyCustomize.JsTrim(url);
        var label = a == b ? u : text[a..b];
        var md = "[" + label + "](" + u + ")";
        return new(text[..a] + md + text[b..], a + md.Length, a + md.Length);
    }

    /// <summary>The "/" menu choice: the block is cleared, then the tool runs on the empty block. Rule is handled by the caller.</summary>
    public static LegacyEditorTools.TextEdit? Slash(string act) => act switch
    {
        "h1" => ToggleBlock("", 0, "# "),
        "h2" => ToggleBlock("", 0, "## "),
        "h3" => ToggleBlock("", 0, "### "),
        "list" => ToggleBlock("", 0, "- "),
        "task" => ToggleBlock("", 0, "- [ ] "),
        "quote" => ToggleBlock("", 0, "> "),
        "noteLink" => new("[[", 2, 2),
        _ => null,
    };
}
