using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>The Markdown options of src/pages/engine.js (o.wikilink, o.shift, o.idPrefix).</summary>
public sealed record PageMarkdownOptions(Func<string, string?, string>? Wikilink = null, int Shift = 0, string IdPrefix = "")
{
    /// <summary>UrbeMath when the app has it (markdown(): formulas drawn, the rest as Markdown); null = text as written.</summary>
    public Func<string, bool, string>? Math { get; init; }
}

/// <summary>
/// The Markdown of the 1.8.4's pages (engine.js inline/markdownRaw/list/plain/excerpt/slug/stripTitle): GFM essentials,
/// [[wikilinks]] and callouts. JavaScript regex semantics are kept: \d and \w are ASCII, $ is the end of the text.
/// Formulas ($…$) are left as text: the 1.8.4 renders them with KaTeX only when its math module is loaded.
/// </summary>
public static class PageMarkdown
{
    private static readonly Regex InlineCode = new("`([^`\\n]+)`");
    private static readonly Regex EmbedLink = new(@"!\[\[([^\]]+)\]\]");
    private static readonly Regex ImageRe = new(@"!\[([^\]]*)\]\(([^)\s]+)(?:\s+""([^""]*)"")?\)");
    private static readonly Regex Wiki = new(@"\[\[([^\]|#]+)(?:#[^\]|]*)?(?:\|([^\]]+))?\]\]");
    private static readonly Regex LinkRe = new(@"\[([^\]]+)\]\(([^)\s]+)(?:\s+""([^""]*)"")?\)");
    private static readonly Regex AutoLink = new(@"<(https?://[^>\s]+)>");
    private static readonly Regex Strong1 = new(@"\*\*([^*]+)\*\*");
    private static readonly Regex Strong2 = new("__([^_]+)__");
    private static readonly Regex Em1 = new(@"(^|[^*])\*([^*\s][^*]*)\*");
    private static readonly Regex Em2 = new(@"(^|[\s(])_([^_\s][^_]*)_(?=[\s).,!?:;]|\z)");
    private static readonly Regex Del = new("~~([^~]+)~~");
    private static readonly Regex Mark = new("==([^=]+)==");
    private static readonly Regex Held = new("\u0000([0-9]+)\u0001");
    private static readonly Regex Http = new("^https?:", RegexOptions.IgnoreCase);

    /// <summary>esc(v): String(v) with &amp; &lt; &gt; " ' escaped.</summary>
    public static string Esc(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        var sb = new StringBuilder(v.Length + 8);
        foreach (var c in v)
            sb.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&quot;", '\'' => "&#39;", _ => c.ToString() });
        return sb.ToString();
    }

    private static readonly Regex Control = new("[\u0000-\u001f]");
    private static readonly Regex DataUrl = new("^data:", RegexOptions.IgnoreCase);
    private static readonly Regex DataImage = new(@"^data:image/(png|jpe?g|gif|webp|svg\+xml|avif);base64,[a-z0-9+/=\s]+\z", RegexOptions.IgnoreCase);
    private static readonly Regex Scheme = new(@"^[a-z][A-Za-z0-9_+.-]*:", RegexOptions.IgnoreCase);
    private static readonly Regex AllowedScheme = new("^(https?:|mailto:|tel:)", RegexOptions.IgnoreCase);

    /// <summary>safeUrl(u, image): '' for a control character, a non-image data: URL or a scheme other than http(s)/mailto/tel.</summary>
    public static string SafeUrl(string? u, bool image = false)
    {
        u = LegacyCustomize.JsTrim(u ?? "");
        if (u.Length == 0 || Control.IsMatch(u)) return "";
        if (DataUrl.IsMatch(u)) return image && DataImage.IsMatch(u) ? u : "";
        if (Scheme.IsMatch(u) && !AllowedScheme.IsMatch(u)) return "";
        return u;
    }

    private static readonly Regex NotLetterOrNumber = new(@"[^\p{L}\p{N}]+");
    private static readonly Regex EdgeDashes = new("^-+|-+$");

    /// <summary>slug(s): accents dropped, lower case, anything but letters and numbers a dash, at most 60, or "secao".</summary>
    public static string Slug(string? s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d) if (c < '̀' || c > 'ͯ') sb.Append(c);
        var t = EdgeDashes.Replace(NotLetterOrNumber.Replace(JsText.Lower(sb.ToString()), "-"), "");
        if (t.Length > 60) t = t[..60];
        return t.Length > 0 ? t : "secao";
    }

    /// <summary>inline(s, o): code, embeds, images, [[links]], links and autolinks held, the rest escaped and emphasised.</summary>
    public static string Inline(string? s, PageMarkdownOptions? o = null)
    {
        o ??= new PageMarkdownOptions();
        var keep = new List<string>();
        string Hold(string h) { keep.Add(h); return "\u0000" + (keep.Count - 1).ToString(CultureInfo.InvariantCulture) + "\u0001"; }
        s ??= "";
        s = InlineCode.Replace(s, m => Hold("<code>" + Esc(m.Groups[1].Value) + "</code>"));
        s = EmbedLink.Replace(s, m =>
        {
            var parts = m.Groups[1].Value.Split('|');
            return Hold(o.Wikilink is { } w ? w(LegacyCustomize.JsTrim(parts[0]), parts.Length > 1 ? parts[1] : null) : Esc(m.Groups[1].Value));
        });
        s = ImageRe.Replace(s, m =>
        {
            var u = SafeUrl(m.Groups[2].Value, true);
            var title = m.Groups[3].Success ? m.Groups[3].Value : "";
            return u.Length > 0 ? Hold("<img src=\"" + Esc(u) + "\" alt=\"" + Esc(m.Groups[1].Value) + "\"" + (title.Length > 0 ? " title=\"" + Esc(title) + "\"" : "") + " loading=\"lazy\">") : "";
        });
        s = Wiki.Replace(s, m =>
        {
            string? label = m.Groups[2].Success ? LegacyCustomize.JsTrim(m.Groups[2].Value) : null;
            return Hold(o.Wikilink is { } w ? w(LegacyCustomize.JsTrim(m.Groups[1].Value), label) : Esc(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value));
        });
        s = LinkRe.Replace(s, m =>
        {
            var u = SafeUrl(m.Groups[2].Value);
            return Hold(u.Length > 0 ? "<a href=\"" + Esc(u) + "\"" + (Http.IsMatch(u) ? " target=\"_blank\" rel=\"noopener\"" : "") + ">" + Inline(m.Groups[1].Value, o) + "</a>" : Esc(m.Groups[1].Value));
        });
        s = AutoLink.Replace(s, m => Hold("<a href=\"" + Esc(m.Groups[1].Value) + "\" target=\"_blank\" rel=\"noopener\">" + Esc(m.Groups[1].Value) + "</a>"));
        s = Esc(s);
        s = Strong1.Replace(s, "<strong>$1</strong>");
        s = Strong2.Replace(s, "<strong>$1</strong>");
        s = Em1.Replace(s, "$1<em>$2</em>");
        s = Em2.Replace(s, "$1<em>$2</em>");
        s = Del.Replace(s, "<del>$1</del>");
        s = Mark.Replace(s, "<mark>$1</mark>");
        return Held.Replace(s, m => keep[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)]);
    }

    private static readonly Regex TrailingSpace = new(@"\s+\z");
    private static readonly Regex TwoSpacesEnd = new(" {2,}\\z");
    private static readonly Regex Fence = new(@"^(\s*)(`{3,}|~{3,})\s*([A-Za-z0-9_+-]*)");
    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.+?)\s*#*\s*\z");
    private static readonly Regex Rule = new(@"^\s{0,3}([-*_])(\s*\1){2,}\s*\z");
    private static readonly Regex Quote = new(@"^\s*>");
    private static readonly Regex QuoteStrip = new(@"^\s*>\s?");
    private static readonly Regex Callout = new(@"^\[!([A-Za-z0-9_]+)\][+-]?\s*(.*)\z");
    private static readonly Regex TableRow = new(@"^\s*\|.*\|\s*\z");
    private static readonly Regex TableSep = new(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*\z");
    private static readonly Regex TableEdges = new(@"^\||\|\z");
    private static readonly Regex ListItem = new(@"^\s*([-*+]|[0-9]+[.)])\s+");
    private static readonly Regex Continuation = new(@"^\s{2,}\S");

    private const char OpenI = '\uE000', CloseI = '\uE001', OpenB = '\uE002', CloseB = '\uE003';

    /// <summary>
    /// markdown(src, o) of engine.js: with UrbeMath, the formulas are swapped for markers that survive the Markdown, then
    /// restored drawn (math/core.js extract/restore: a block formula alone in a paragraph is a block); else markdownRaw.
    /// </summary>
    public static string Markdown(string? src, PageMarkdownOptions? o = null)
    {
        if (o?.Math is not { } math) return MarkdownRaw(src, o);
        var s = src ?? "";
        var items = MathEditing.Scan(s);
        if (items.Count == 0) return MarkdownRaw(src, o);
        var text = new System.Text.StringBuilder();
        int last = 0;
        for (int k = 0; k < items.Count; k++)
        {
            var it = items[k];
            text.Append(s, last, it.Start - last).Append(it.Block ? OpenB : OpenI).Append(k.ToString(CultureInfo.InvariantCulture)).Append(it.Block ? CloseB : CloseI);
            last = it.End;
        }
        text.Append(s, last, s.Length - last);
        var html = MarkdownRaw(text.ToString(), o with { Math = null });
        string Atom(int k, bool block)
        {
            if (k < 0 || k >= items.Count) return "";
            var it = items[k];
            var attrs = " data-tex=\"" + Esc(it.Tex) + "\" data-open=\"" + Esc(it.Open) + "\" data-close=\"" + Esc(it.Close) + "\"";
            return block ? "<div class=\"umath umath-block\"" + attrs + ">" + math(it.Tex, true) + "</div>"
                : "<span class=\"umath" + (it.Display ? " umath-display" : "") + "\"" + attrs + ">" + math(it.Tex, it.Display) + "</span>";
        }
        int N(Match m) => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        html = Regex.Replace(html, "<p>\\s*" + OpenB + "(\\d+)" + CloseB + "\\s*</p>", m => Atom(N(m), true));
        html = Regex.Replace(html, OpenB + "(\\d+)" + CloseB, m => Atom(N(m), false));
        return Regex.Replace(html, OpenI + "(\\d+)" + CloseI, m => Atom(N(m), false));
    }

    /// <summary>markdownRaw(src, o): front matter skipped, blocks in the 1.8.4's order.</summary>
    public static string MarkdownRaw(string? src, PageMarkdownOptions? o = null)
    {
        o ??= new PageMarkdownOptions();
        var lines = (src ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>();
        int i = 0;
        if (lines[0] == "---")
        {
            int fm = Array.IndexOf(lines, "---", 1);
            if (fm > 0) i = fm + 1;
        }
        var buf = new List<string>();
        void Flush()
        {
            if (buf.Count == 0) return;
            output.Add("<p>" + string.Join(" ", buf.Select((l, k) => Inline(TrailingSpace.Replace(l, ""), o) + (k < buf.Count - 1 && TwoSpacesEnd.IsMatch(l) ? "<br>" : ""))) + "</p>");
            buf.Clear();
        }
        while (i < lines.Length)
        {
            var line = lines[i];
            Match m;
            if (LegacyCustomize.JsTrim(line).Length == 0) { Flush(); i++; continue; }
            if ((m = Fence.Match(line)).Success)
            {
                Flush();
                var fence = m.Groups[2].Value;
                var lang = m.Groups[3].Value;
                var code = new List<string>();
                i++;
                while (i < lines.Length && !LegacyCustomize.JsTrim(lines[i]).StartsWith(fence, StringComparison.Ordinal)) code.Add(lines[i++]);
                i++;
                // CST-069: a ```grafico block is its graph on the page too (an SVG drawn from the text, no script)
                if (FunctionPlot.IsPlotLanguage(lang)) output.Add("<figure class=\"figure urbe-plot\">" + FunctionPlot.Parse(string.Join("\n", code)).ToSvg() + "</figure>");
                else output.Add("<pre class=\"code\"" + (lang.Length > 0 ? " data-lang=\"" + Esc(lang) + "\"" : "") + "><code>" + Esc(string.Join("\n", code)) + "</code></pre>");
                continue;
            }
            if ((m = Heading.Match(line)).Success)
            {
                Flush();
                int lvl = Math.Clamp(m.Groups[1].Value.Length + o.Shift, 1, 6);
                var id = Slug(m.Groups[2].Value);
                output.Add("<h" + lvl + " id=\"" + Esc(o.IdPrefix + id) + "\">" + Inline(m.Groups[2].Value, o) + "</h" + lvl + ">");
                i++;
                continue;
            }
            if (Rule.IsMatch(line)) { Flush(); output.Add("<hr>"); i++; continue; }
            if (Quote.IsMatch(line))
            {
                Flush();
                var q = new List<string>();
                while (i < lines.Length && Quote.IsMatch(lines[i])) q.Add(QuoteStrip.Replace(lines[i++], "", 1));
                var call = q.Count > 0 ? Callout.Match(q[0]) : Match.Empty;
                if (call.Success)
                {
                    var kind = JsText.Lower(call.Groups[1].Value);
                    var title = call.Groups[2].Value.Length > 0 ? call.Groups[2].Value : call.Groups[1].Value;
                    output.Add("<aside class=\"callout callout-" + Esc(kind) + "\"><strong class=\"callout-title\">" + Inline(title, o) + "</strong>" + Markdown(string.Join("\n", q.Skip(1)), o) + "</aside>");
                }
                else output.Add("<blockquote>" + Markdown(string.Join("\n", q), o) + "</blockquote>");
                continue;
            }
            if (TableRow.IsMatch(line) && i + 1 < lines.Length && TableSep.IsMatch(lines[i + 1]))
            {
                Flush();
                static List<string> Cells(string l) => TableEdges.Replace(LegacyCustomize.JsTrim(l), "").Split('|').Select(c => LegacyCustomize.JsTrim(c)).ToList();
                var head = Cells(line);
                var aligns = Cells(lines[i + 1]).Select(c => Regex.IsMatch(c, @"^:-+:\z") ? "center" : Regex.IsMatch(c, @"-:\z") ? "right" : "").ToList();
                i += 2;
                var rows = new List<List<string>>();
                while (i < lines.Length && TableRow.IsMatch(lines[i])) rows.Add(Cells(lines[i++]));
                string Al(int k) => k < aligns.Count && aligns[k].Length > 0 ? " style=\"text-align:" + aligns[k] + "\"" : "";
                output.Add("<div class=\"table\"><table><thead><tr>" + string.Concat(head.Select((c, k) => "<th" + Al(k) + ">" + Inline(c, o) + "</th>")) + "</tr></thead><tbody>" +
                    string.Concat(rows.Select(r => "<tr>" + string.Concat(head.Select((_, k) => "<td" + Al(k) + ">" + Inline(k < r.Count ? r[k] : "", o) + "</td>")) + "</tr>")) + "</tbody></table></div>");
                continue;
            }
            if (ListItem.IsMatch(line))
            {
                Flush();
                var items = new List<string>();
                while (i < lines.Length && (ListItem.IsMatch(lines[i]) || (Continuation.IsMatch(lines[i]) && items.Count > 0))) items.Add(lines[i++]);
                output.Add(List(items, o));
                continue;
            }
            buf.Add(line);
            i++;
        }
        Flush();
        return string.Join("\n", output);
    }

    private static readonly Regex Indent = new(@"^\s*");
    private static readonly Regex Ordered = new(@"^\s*[0-9]+[.)]");
    private static readonly Regex Item = new(@"^\s*([-*+]|[0-9]+[.)])\s+(.*)\z");
    private static readonly Regex Task = new(@"^\[([ xX])\]\s+(.*)\z");

    private static string List(List<string> lines, PageMarkdownOptions o)
    {
        static int Ind(string l) => Indent.Match(l).Value.Replace("\t", "  ").Length;
        int @base = Ind(lines[0]);
        bool ordered = Ordered.IsMatch(lines[0]);
        var items = new List<(string Text, List<string> Sub)>();
        foreach (var l in lines)
        {
            int d = Ind(l);
            var m = Item.Match(l);
            if (m.Success && d <= @base + 1) items.Add((m.Groups[2].Value, []));
            else if (items.Count > 0) items[^1].Sub.Add(l);
        }
        var tag = ordered ? "ol" : "ul";
        return "<" + tag + ">" + string.Concat(items.Select(it =>
        {
            var task = Task.Match(it.Text);
            var sub = it.Sub.Where(x => LegacyCustomize.JsTrim(x).Length > 0).ToList();
            var inner = task.Success
                ? "<span class=\"task" + (task.Groups[1].Value != " " ? " done" : "") + "\"><input type=\"checkbox\" disabled" + (task.Groups[1].Value != " " ? " checked" : "") + "> " + Inline(task.Groups[2].Value, o) + "</span>"
                : Inline(it.Text, o);
            if (sub.Count > 0)
            {
                if (ListItem.IsMatch(sub[0])) inner += List(sub, o);
                else inner += "<br>" + string.Join("<br>", sub.Select(x => Inline(LegacyCustomize.JsTrim(x), o)));
            }
            return "<li" + (task.Success ? " class=\"task-item\"" : "") + ">" + inner + "</li>";
        })) + "</" + tag + ">";
    }

    /// <summary>plain(md): the text of a note without its Markdown (for excerpts).</summary>
    public static string Plain(string? md)
    {
        var t = md ?? "";
        t = Regex.Replace(t, @"^---[\s\S]*?\n---\n?", "");
        t = Regex.Replace(t, @"^\s*#\s+.*\n?", "");
        t = Regex.Replace(t, @"\[![A-Za-z0-9_-]+\][+-]?", "");
        t = Regex.Replace(t, @"^\s*[-*+]\s+\[[ xX]\]\s+", "", RegexOptions.Multiline);
        t = Regex.Replace(t, @"```[\s\S]*?```", " ");
        t = Regex.Replace(t, @"!\[[^\]]*\]\([^)]*\)", "");
        t = Regex.Replace(t, @"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]", m => m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : m.Groups[1].Value);
        t = Regex.Replace(t, @"\[([^\]]+)\]\([^)]*\)", "$1");
        t = Regex.Replace(t, @"^#+\s+", "", RegexOptions.Multiline);
        t = Regex.Replace(t, "[*_`>~=#|-]+", " ");
        t = Regex.Replace(t, @"\s+", " ");
        return LegacyCustomize.JsTrim(t);
    }

    /// <summary>excerpt(md, n): plain text up to n characters, cut at a word, with "…".</summary>
    public static string Excerpt(string? md, int n)
    {
        var t = Plain(md);
        return t.Length > n ? Regex.Replace(t[..n], @"\s+\S*\z", "") + "…" : t;
    }

    /// <summary>stripTitle(content, title, shown): the note's first "# Title" is dropped when the page shows the title.</summary>
    public static string StripTitle(string? content, string? title, bool shown)
    {
        if (!shown) return content ?? "";
        var t = content ?? "";
        var m = Regex.Match(Regex.Replace(t, @"^---[\s\S]*?\n---\n?", ""), @"^\s*#\s+(.+)\n?");
        if (m.Success && JsText.Lower(LegacyCustomize.JsTrim(m.Groups[1].Value)) == JsText.Lower(LegacyCustomize.JsTrim(title ?? "")))
            return Regex.Replace(t, @"^(---[\s\S]*?\n---\n?)?\s*#\s+.+\n?", "$1");
        return t;
    }
}
