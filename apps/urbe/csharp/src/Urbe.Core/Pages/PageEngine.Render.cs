using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Urbe.Core.Pages.PageMarkdown;

namespace Urbe.Core.Pages;

/// <summary>What a block's render gets (engine.js render's c): the notes, the book, the table of contents and the CSS sink.</summary>
internal sealed class RenderContext
{
    public required PageMarkdownOptions Md { get; init; }
    public required Func<string?, PageNote?> Note { get; init; }
    public required Func<JsonObject, List<PageNote>> Notes { get; init; }
    public required Func<PageNote, string> NoteAnchor { get; init; }
    public required Func<PageNote, int, PageMarkdownOptions> MdNote { get; init; }
    public required List<(string Id, string Anchor, string Title, bool Menu)> Toc { get; init; }
    public required List<BookEntry> BookEntries { get; init; }
    public required string BookStyle { get; init; }
    public required bool Preview { get; init; }
    public required List<string> Css { get; init; }
    public required bool BookFormat { get; init; }
    public bool FreeCssAdded { get; set; }
    public JsonObject Section { get; set; } = [];
}

internal sealed record BookEntry(string Kind, int Num, string Title, string Anchor);

public sealed partial class PageEngine
{
    private static readonly Regex HttpRe = new("^https?:", RegexOptions.IgnoreCase);
    private static string Blank(string u) => HttpRe.IsMatch(u) ? " target=\"_blank\" rel=\"noopener\"" : "";

    private static string S(JsonObject p, string k) => Str(p[k]);
    private static bool T(JsonObject p, string k) => Truthy(p[k]);
    private static IEnumerable<JsonObject> Items(JsonObject p, string k) => (p[k] as JsonArray ?? []).OfType<JsonObject>();

    private static string Head(JsonObject p) =>
        T(p, "title") || T(p, "subtitle")
            ? "<div class=\"sec-head\">" + (T(p, "title") ? "<h2>" + Inline(S(p, "title")) + "</h2>" : "") + (T(p, "subtitle") ? "<p class=\"sub\">" + Inline(S(p, "subtitle")) + "</p>" : "") + "</div>"
            : "";

    private static string Btns(IEnumerable<(string Label, string Url, string Variant)> list)
    {
        var l = list.Where(b => b.Label.Length > 0).ToList();
        if (l.Count == 0) return "";
        return "<div class=\"btns\">" + string.Concat(l.Select(b =>
        {
            var u = SafeUrl(b.Url) is { Length: > 0 } x ? x : "#";
            return "<a class=\"btn btn-" + Esc(b.Variant.Length > 0 ? b.Variant : "primary") + "\" href=\"" + Esc(u) + "\"" + Blank(u) + ">" + Esc(b.Label) + "</a>";
        })) + "</div>";
    }

    private static string Btns(JsonObject p, string k) => Btns(Items(p, k).Select(b => (S(b, "label"), S(b, "url"), S(b, "variant"))));

    private static string Img(string? src, string? alt, string? cls = null)
    {
        var u = SafeUrl(src, true);
        return u.Length > 0 ? "<img" + (cls is not null ? " class=\"" + cls + "\"" : "") + " src=\"" + Esc(u) + "\" alt=\"" + Esc(alt) + "\" loading=\"lazy\">" : "";
    }

    private static string Grid(double n, IEnumerable<string> items, string? cls = null)
    {
        var k = double.IsNaN(n) || n == 0 ? 3 : n;
        return "<div class=\"grid g" + Num(Math.Max(1, Math.Min(4, k))) + (cls is { Length: > 0 } ? " " + cls : "") + "\">" + string.Concat(items) + "</div>";
    }

    private static double Plus(JsonNode? n) => LegacyCustomize.JsToNumber(n);

    private static string Roman(int n)
    {
        (int, string)[] m = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        n = Math.Max(1, n);
        var r = new StringBuilder();
        foreach (var (v, s) in m) while (n >= v) { r.Append(s); n -= v; }
        return r.ToString();
    }

    private static readonly string[] Numerais = ["zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez", "onze", "doze", "treze", "catorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove", "vinte"];

    private static string ChapterLabel(int n, string style)
    {
        if (n == 0 || style == "none") return "";
        if (style == "number") return n.ToString(CultureInfo.InvariantCulture);
        if (style == "roman") return Roman(n);
        if (style == "words") return "Capítulo " + (n < Numerais.Length ? Numerais[n] : n.ToString(CultureInfo.InvariantCulture));
        return "Capítulo " + n.ToString(CultureInfo.InvariantCulture);
    }

    private static string ChapterHtml(RenderContext c, int num, string? anchor, string? title, string? epigraph, string? epigraphAuthor, bool dropCap, string body)
    {
        var lab = ChapterLabel(num, c.BookStyle);
        return "<article class=\"sheet bk-chap" + (dropCap ? " dropcap" : "") + "\"" + (!string.IsNullOrEmpty(anchor) ? " id=\"" + Esc(anchor) + "\"" : "") + ">" +
               "<header class=\"bk-chhead\">" + (lab.Length > 0 ? "<p class=\"bk-chnum\">" + Esc(lab) + "</p>" : "") + (!string.IsNullOrEmpty(title) ? "<h2 class=\"bk-chtitle\">" + Inline(title, c.Md) + "</h2>" : "") +
               (!string.IsNullOrEmpty(epigraph) ? "<blockquote class=\"bk-epi\">" + Markdown(epigraph, c.Md) + (!string.IsNullOrEmpty(epigraphAuthor) ? "<cite>— " + Inline(epigraphAuthor, c.Md) + "</cite>" : "") + "</blockquote>" : "") + "</header>" +
               "<div class=\"prose bk-text\">" + body + "</div></article>";
    }

    private static readonly Regex YouTube = new(@"(?:youtube\.com/(?:watch\?v=|embed/|shorts/)|youtu\.be/)([A-Za-z0-9_-]{6,})");
    private static readonly Regex Vimeo = new(@"vimeo\.com/([0-9]+)");

    private static string VideoHtml(string url, string caption)
    {
        var u = SafeUrl(url);
        if (u.Length == 0) return "<div class=\"missing\">Informe o link do vídeo.</div>";
        Match m;
        var src = "";
        if ((m = YouTube.Match(u)).Success) src = "https://www.youtube-nocookie.com/embed/" + m.Groups[1].Value;
        else if ((m = Vimeo.Match(u)).Success) src = "https://player.vimeo.com/video/" + m.Groups[1].Value;
        var media = src.Length > 0
            ? "<iframe src=\"" + Esc(src) + "\" title=\"" + Esc(caption.Length > 0 ? caption : "Vídeo") + "\" loading=\"lazy\" allow=\"accelerometer; encrypted-media; picture-in-picture; fullscreen\" allowfullscreen></iframe>"
            : "<video src=\"" + Esc(u) + "\" controls preload=\"metadata\"></video>";
        return "<figure class=\"figure s-wide rounded\"><div class=\"ratio\">" + media + "</div>" + (caption.Length > 0 ? "<figcaption>" + Esc(caption) + "</figcaption>" : "") + "</figure>";
    }

    private void On(string type, Func<JsonObject, RenderContext, JsonObject, string> render) => _blocks[type].Render = render;

    private void RegisterBlocks()
    {
        On("hero", (p, c, _) => "<div class=\"hero l-" + Esc(S(p, "layout")) + " h-" + Esc(S(p, "height")) + (T(p, "stack") ? " stack-btns" : "") + "\"><div class=\"hero-text\">" +
            (T(p, "eyebrow") ? "<p class=\"eyebrow\">" + Inline(S(p, "eyebrow"), c.Md) + "</p>" : "") + "<h1>" + Inline(S(p, "title"), c.Md) + "</h1>" +
            (T(p, "subtitle") ? "<div class=\"lead\">" + Markdown(S(p, "subtitle"), c.Md) + "</div>" : "") + Btns(p, "buttons") + "</div>" +
            (T(p, "image") ? "<figure class=\"hero-media\">" + Img(S(p, "image"), S(p, "title")) + "</figure>" : "") + "</div>");

        On("text", (p, c, _) => Head(p) + "<div class=\"prose" + (Plus(p["columns"]) == 2 ? " cols2" : "") + "\">" + Markdown(S(p, "markdown"), c.Md) + "</div>");

        On("note", (p, c, _) =>
        {
            var d = c.Note(S(p, "path"));
            if (d is null) return "<div class=\"missing\">Nota não encontrada: " + Esc(T(p, "path") ? S(p, "path") : "(escolha uma nota)") + "</div>";
            var meta = string.Join(" · ", new[] { d.Modified ?? "", string.Join(" ", (d.Tags ?? []).Select(t => "#" + t)) }.Where(x => x.Length > 0));
            return "<article class=\"prose note-article\">" + (T(p, "showTitle") ? "<h1 class=\"note-title\">" + Esc(d.Title) + "</h1>" : "") +
                   (T(p, "showMeta") ? "<p class=\"meta\">" + Esc(meta) + "</p>" : "") + Markdown(StripTitle(d.Content, d.Title, T(p, "showTitle")), c.MdNote(d, 0)) + "</article>";
        });

        On("notes", (p, c, _) =>
        {
            var list = c.Notes(p);
            if (list.Count == 0) return Head(p) + "<div class=\"missing\">Nenhuma nota encontrada para esta coleção.</div>";
            var layout = S(p, "layout");
            var cards = list.Select(d =>
            {
                var href = T(p, "expand") ? "#" + c.NoteAnchor(d) : "";
                return "<a class=\"card note-card\"" + (href.Length > 0 ? " href=\"" + Esc(href) + "\"" : "") + "><h3>" + Esc(d.Title) + "</h3>" +
                       (T(p, "excerpt") ? "<p>" + Esc(Excerpt(d.Content, layout == "list" ? 220 : 140)) + "</p>" : "") +
                       ((d.Tags ?? []).Count > 0 ? "<div class=\"tags\">" + string.Concat(d.Tags!.Take(4).Select(t => "<span>#" + Esc(t) + "</span>")) + "</div>" : "") + "</a>";
            }).ToList();
            double cols = Plus(p["columns"]);
            var body = layout == "list" ? "<div class=\"stack\">" + string.Concat(cards) + "</div>"
                : Grid(layout == "grid" ? Math.Min(4, (double.IsNaN(cols) || cols == 0 ? 3 : cols) + 1) : cols, cards, layout == "grid" ? "compact" : "");
            var full = T(p, "expand")
                ? "<div class=\"note-full\">" + string.Concat(list.Select(d => "<article class=\"prose note-article\" id=\"" + Esc(c.NoteAnchor(d)) + "\"><h2 class=\"note-title\">" + Esc(d.Title) + "</h2>" +
                    Markdown(StripTitle(d.Content, d.Title, true), c.MdNote(d, 1)) + "</article>")) + "</div>"
                : "";
            return Head(p) + body + full;
        });

        On("features", (p, c, _) => Head(p) + Grid(Plus(p["columns"]), Items(p, "items").Select(it =>
            "<div class=\"card feature\">" + (T(it, "icon") ? "<div class=\"ficon\">" + Esc(S(it, "icon")) + "</div>" : "") + "<h3>" + Inline(S(it, "title"), c.Md) + "</h3><p>" + Inline(S(it, "text"), c.Md) + "</p></div>")));

        On("cards", (p, c, _) => Head(p) + Grid(Plus(p["columns"]), Items(p, "items").Select(it =>
        {
            var u = SafeUrl(S(it, "url"));
            var tag = u.Length > 0 ? "a" : "div";
            return "<" + tag + " class=\"card media-card\"" + (u.Length > 0 ? " href=\"" + Esc(u) + "\"" + Blank(u) : "") + ">" +
                   (T(it, "image") ? "<div class=\"card-img\">" + Img(S(it, "image"), S(it, "title")) + "</div>" : "") + "<div class=\"card-body\">" +
                   (T(it, "tag") ? "<span class=\"pill\">" + Esc(S(it, "tag")) + "</span>" : "") + "<h3>" + Inline(S(it, "title"), c.Md) + "</h3>" +
                   (T(it, "text") ? "<p>" + Inline(S(it, "text"), c.Md) + "</p>" : "") + "</div></" + tag + ">";
        })));

        On("gallery", (p, _, _) =>
        {
            var items = Items(p, "items").Where(i => SafeUrl(S(i, "image"), true).Length > 0).ToList();
            if (items.Count == 0) return Head(p) + "<div class=\"missing\">Adicione imagens à galeria.</div>";
            return Head(p) + Grid(Plus(p["columns"]), items.Select(it =>
            {
                var u = SafeUrl(S(it, "image"), true);
                return "<figure class=\"shot\"><a href=\"" + Esc(u) + "\" target=\"_blank\" rel=\"noopener\" data-lightbox>" + Img(u, S(it, "caption")) + "</a>" +
                       (T(it, "caption") ? "<figcaption>" + Esc(S(it, "caption")) + "</figcaption>" : "") + "</figure>";
            }), "gallery");
        });

        On("image", (p, _, _) => T(p, "src") && SafeUrl(S(p, "src"), true).Length > 0
            ? "<figure class=\"figure s-" + Esc(S(p, "size")) + (T(p, "rounded") ? " rounded" : "") + "\">" + Img(S(p, "src"), T(p, "alt") ? S(p, "alt") : S(p, "caption")) +
              (T(p, "caption") ? "<figcaption>" + Esc(S(p, "caption")) + "</figcaption>" : "") + "</figure>"
            : "<div class=\"missing\">Escolha uma imagem.</div>");

        On("video", (p, _, _) => VideoHtml(S(p, "url"), S(p, "caption")));

        On("quote", (p, c, _) => "<figure class=\"bigquote\"><blockquote>" + Inline(S(p, "text"), c.Md) + "</blockquote>" +
            (T(p, "author") ? "<figcaption>" + (T(p, "image") ? Img(S(p, "image"), S(p, "author"), "avatar") : "") + "<span><strong>" + Esc(S(p, "author")) + "</strong>" +
                (T(p, "role") ? "<small>" + Esc(S(p, "role")) + "</small>" : "") + "</span></figcaption>" : "") + "</figure>");

        On("testimonials", (p, c, _) => Head(p) + Grid(Plus(p["columns"]), Items(p, "items").Select(it =>
        {
            var author = S(it, "author");
            var first = author.Length > 0 ? author[..1] : "?";
            return "<figure class=\"card testimonial\"><blockquote>“" + Inline(S(it, "text"), c.Md) + "”</blockquote><figcaption>" +
                   (T(it, "image") ? Img(S(it, "image"), author, "avatar") : "<span class=\"avatar ph\">" + Esc(first) + "</span>") +
                   "<span><strong>" + Esc(author) + "</strong>" + (T(it, "role") ? "<small>" + Esc(S(it, "role")) + "</small>" : "") + "</span></figcaption></figure>";
        })));

        On("stats", (p, _, _) => Head(p) + "<div class=\"stats\">" + string.Concat(Items(p, "items").Select(it =>
            "<div class=\"stat\"><strong>" + Esc(S(it, "value")) + "</strong><span>" + Esc(S(it, "label")) + "</span></div>")) + "</div>");

        On("timeline", (p, c, _) => Head(p) + "<ol class=\"timeline\">" + string.Concat(Items(p, "items").Select(it =>
            "<li><span class=\"when\">" + Esc(S(it, "date")) + "</span><div><h3>" + Inline(S(it, "title"), c.Md) + "</h3>" + (T(it, "text") ? Markdown(S(it, "text"), c.Md) : "") + "</div></li>")) + "</ol>");

        On("faq", (p, c, _) => Head(p) + "<div class=\"faq\">" + string.Concat(Items(p, "items").Select((it, k) =>
            "<details" + (T(p, "openFirst") && k == 0 ? " open" : "") + "><summary>" + Inline(S(it, "q"), c.Md) + "</summary><div class=\"prose\">" + Markdown(S(it, "a"), c.Md) + "</div></details>")) + "</div>");

        On("cta", (p, c, _) => "<div class=\"cta-box\"><div><h2>" + Inline(S(p, "title"), c.Md) + "</h2>" + (T(p, "text") ? "<p>" + Inline(S(p, "text"), c.Md) + "</p>" : "") + "</div>" + Btns(p, "buttons") + "</div>");

        On("columns", (p, c, _) => Head(p) + Grid(Plus(p["columns"]), Items(p, "items").Select(it =>
            "<div class=\"prose col\">" + (T(it, "title") ? "<h3>" + Inline(S(it, "title"), c.Md) + "</h3>" : "") + Markdown(S(it, "markdown"), c.Md) + "</div>")));

        On("pricing", (p, _, _) =>
        {
            var items = Items(p, "items").ToList();
            return Head(p) + Grid(Math.Min(4, Math.Max(1, items.Count)), items.Select(it =>
                "<div class=\"card plan" + (T(it, "highlight") ? " hot" : "") + "\">" + (T(it, "highlight") ? "<span class=\"pill\">Mais escolhido</span>" : "") +
                "<h3>" + Esc(S(it, "name")) + "</h3><div class=\"price\"><strong>" + Esc(S(it, "price")) + "</strong><span>" + Esc(S(it, "period")) + "</span></div>" +
                (T(it, "description") ? "<p>" + Esc(S(it, "description")) + "</p>" : "") + "<ul class=\"checks\">" +
                string.Concat((T(it, "features") ? S(it, "features") : "").Split('\n').Where(x => LegacyCustomize.JsTrim(x).Length > 0).Select(x => "<li>" + Esc(LegacyCustomize.JsTrim(x)) + "</li>")) + "</ul>" +
                Btns([(S(it, "buttonLabel"), S(it, "url"), T(it, "highlight") ? "primary" : "secondary")]) + "</div>"));
        });

        On("contact", (p, c, _) =>
        {
            var b = new List<(string, string, string)>();
            if (T(p, "email")) b.Add(("✉ " + S(p, "email"), "mailto:" + S(p, "email"), "primary"));
            if (T(p, "phone")) b.Add(("☏ " + S(p, "phone"), "tel:" + Regex.Replace(S(p, "phone"), "[^0-9+]", ""), "secondary"));
            var links = Items(p, "links").ToList();
            return "<div class=\"contact\"><h2>" + Inline(S(p, "title"), c.Md) + "</h2>" + (T(p, "text") ? "<p class=\"sub\">" + Inline(S(p, "text"), c.Md) + "</p>" : "") + Btns(b) +
                   (links.Count > 0 ? "<div class=\"links\">" + string.Concat(links.Select(l =>
                   {
                       var u = SafeUrl(S(l, "url"));
                       return u.Length > 0 ? "<a href=\"" + Esc(u) + "\" target=\"_blank\" rel=\"noopener\">" + Esc(T(l, "label") ? S(l, "label") : u) + " ↗</a>" : "";
                   })) + "</div>" : "") + "</div>";
        });

        On("countdown", (p, c, _) => "<div class=\"countdown\" data-until=\"" + Esc(ReplaceFirst(S(p, "date"), " ", "T")) + "\" data-done=\"" + Esc(S(p, "done")) + "\"><h2>" + Inline(S(p, "title"), c.Md) + "</h2><div class=\"cd\">" +
            string.Concat(new[] { "dias", "horas", "min", "seg" }.Select(u => "<div><strong>--</strong><span>" + u + "</span></div>")) + "</div></div>");

        On("code", (p, _, _) => Head(p) + "<pre class=\"code\" data-lang=\"" + Esc(S(p, "language")) + "\"><button class=\"copy\" type=\"button\">Copiar</button><code>" + Esc(S(p, "code")) + "</code></pre>");

        On("toc", (p, c, _) =>
        {
            var items = c.Toc.Where(t => t.Id != Str(c.Section["id"]));
            return "<nav class=\"toc\"><strong>" + Esc(S(p, "title")) + "</strong><ol>" + string.Concat(items.Select(t => "<li><a href=\"#" + Esc(t.Anchor) + "\">" + Esc(t.Title) + "</a></li>")) + "</ol></nav>";
        });

        On("divider", (p, _, _) => S(p, "style") == "wave"
            ? "<svg class=\"wave\" viewBox=\"0 0 1200 40\" preserveAspectRatio=\"none\" aria-hidden=\"true\"><path d=\"M0 20 Q150 0 300 20 T600 20 T900 20 T1200 20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/></svg>"
            : "<div class=\"divider d-" + Esc(S(p, "style")) + "\" role=\"separator\"></div>");

        On("html", (p, _, _) => S(p, "code"));

        On("bookcover", (p, _, _) =>
        {
            var u = SafeUrl(S(p, "image"), true);
            var style = S(p, "style");
            var bg = u.Length > 0 && style == "image" ? " style=\"background-image:url('" + Esc(u).Replace("'", "%27") + "')\"" : "";
            return "<div class=\"sheet bk-full bk-cover s-" + Esc(style) + "\"" + bg + "><div class=\"bk-cover-in\">" + (style == "modern" ? "<span class=\"bk-bar\"></span>" : "") +
                   "<div class=\"bk-cover-top\"><h1>" + Inline(S(p, "title")) + "</h1>" + (T(p, "subtitle") ? "<p class=\"bk-cover-sub\">" + Inline(S(p, "subtitle")) + "</p>" : "") + "</div>" +
                   (u.Length > 0 && style != "image" ? "<figure class=\"bk-cover-img\">" + Img(u, S(p, "title")) + "</figure>" : "") +
                   "<div class=\"bk-cover-bottom\">" + (T(p, "author") ? "<p class=\"bk-author\">" + Esc(S(p, "author")) + "</p>" : "") + (T(p, "publisher") ? "<p class=\"bk-pub\">" + Esc(S(p, "publisher")) + "</p>" : "") + "</div></div></div>";
        });

        On("titlepage", (p, _, _) =>
        {
            var place = string.Join(" · ", new[] { S(p, "place"), S(p, "year") }.Where(x => x.Length > 0));
            return "<div class=\"sheet bk-full bk-front bk-title\"><div class=\"bk-title-top\"><p class=\"bk-author\">" + Esc(S(p, "author")) + "</p></div><div class=\"bk-title-mid\"><h1>" + Inline(S(p, "title")) + "</h1>" +
                   (T(p, "subtitle") ? "<p class=\"bk-title-sub\">" + Inline(S(p, "subtitle")) + "</p>" : "") + "<span class=\"bk-orn\">❦</span></div>" +
                   "<div class=\"bk-title-bottom\">" + (T(p, "publisher") ? "<p class=\"bk-pub\">" + Esc(S(p, "publisher")) + "</p>" : "") + (T(p, "place") || T(p, "year") ? "<p class=\"bk-place\">" + Esc(place) + "</p>" : "") + "</div></div>";
        });

        On("copyright", (p, c, _) => "<div class=\"sheet bk-full bk-front bk-copy\"><div class=\"prose\">" + Markdown(S(p, "markdown"), c.Md) + "</div></div>");

        On("dedication", (p, c, _) => "<div class=\"sheet bk-full bk-front bk-ded k-" + Esc(S(p, "kind")) + "\"><div class=\"bk-ded-in\"><div class=\"prose\">" + Markdown(S(p, "markdown"), c.Md) + "</div>" +
            (T(p, "author") ? "<p class=\"bk-ded-author\">— " + Inline(S(p, "author"), c.Md) + "</p>" : "") + "</div></div>");

        On("booktoc", (p, c, _) =>
        {
            var e = c.BookEntries;
            if (e.Count == 0) return "<div class=\"sheet bk-front bk-toc\"><h2>" + Esc(S(p, "title")) + "</h2><p class=\"missing\">Adicione capítulos para o sumário aparecer.</p></div>";
            return "<div class=\"sheet bk-front bk-toc\"><h2>" + Esc(S(p, "title")) + "</h2><ol>" + string.Concat(e.Select(x =>
            {
                var lab = x.Kind == "part" ? "Parte " + Roman(x.Num) : ChapterLabel(x.Num, c.BookStyle == "words" ? "number" : c.BookStyle);
                return "<li class=\"k-" + x.Kind + "\"><a href=\"#" + Esc(x.Anchor) + "\">" + (lab.Length > 0 ? "<span class=\"n\">" + Esc(Regex.Replace(lab, "^Capítulo ", "")) + "</span>" : "") +
                       "<span class=\"t\">" + Inline(x.Title) + "</span><span class=\"lead\" aria-hidden=\"true\"></span></a></li>";
            })) + "</ol></div>";
        });

        On("part", (p, c, sec) => "<div class=\"sheet bk-full bk-part\"" + (Truthy(sec["_anchor"]) ? " id=\"" + Esc(Str(sec["_anchor"])) + "\"" : "") + "><p class=\"bk-partnum\">Parte " +
            Roman(Truthy(sec["_num"]) ? (int)Plus(sec["_num"]) : 1) + "</p><h2>" + Inline(S(p, "title"), c.Md) + "</h2>" + (T(p, "markdown") ? "<div class=\"prose\">" + Markdown(S(p, "markdown"), c.Md) + "</div>" : "") + "</div>");

        On("chapter", (p, c, sec) =>
        {
            string body;
            var t = S(p, "title");
            if (S(p, "source") == "note")
            {
                var d = c.Note(S(p, "path"));
                if (d is null) return "<div class=\"sheet bk-chap\"><div class=\"missing\">Nota não encontrada: " + Esc(T(p, "path") ? S(p, "path") : "(escolha uma nota)") + "</div></div>";
                if (t.Length == 0) t = d.Title;
                body = Markdown(StripTitle(d.Content, d.Title, true), c.MdNote(d, 2));
            }
            else body = Markdown(S(p, "markdown"), c.Md with { Shift = 2 });
            return ChapterHtml(c, (int)Plus(sec["_num"]), Str(sec["_anchor"]), t, S(p, "epigraph"), S(p, "epigraphAuthor"), T(p, "dropCap"), body);
        });

        On("chapters", (p, c, sec) =>
        {
            var l = sec["_list"] as JsonArray ?? [];
            if (l.Count == 0) return "<div class=\"sheet bk-chap\"><div class=\"missing\">Nenhuma nota na pasta " + Esc(T(p, "folder") ? S(p, "folder") : "(escolha uma pasta)") + ".</div></div>";
            return string.Concat(l.OfType<JsonObject>().Select(x =>
            {
                var d = c.Note(Str(x["path"]))!;
                return ChapterHtml(c, (int)Plus(x["num"]), Str(x["anchor"]), d.Title, null, null, T(p, "dropCap"), Markdown(StripTitle(d.Content, d.Title, true), c.MdNote(d, 2)));
            }));
        });

        On("about", (p, c, _) => "<div class=\"sheet bk-about\"><h2>" + Esc(S(p, "title")) + "</h2>" + (SafeUrl(S(p, "image"), true).Length > 0 ? "<figure class=\"bk-photo\">" + Img(S(p, "image"), S(p, "title")) + "</figure>" : "") +
            "<div class=\"prose\">" + Markdown(S(p, "markdown"), c.Md) + "</div></div>");

        On("colophon", (p, c, _) => "<div class=\"sheet bk-full bk-colophon\"><div class=\"prose\">" + Markdown(S(p, "markdown"), c.Md) + "</div></div>");

        RegisterFree();
    }

    private static string ReplaceFirst(string s, string a, string b)
    {
        int i = s.IndexOf(a, StringComparison.Ordinal);
        return i < 0 ? s : s[..i] + b + s[(i + a.Length)..];
    }

    // ---------------------------------------------------------------- CSS

    private static JsonObject FontOf(string k, string fallback) =>
        (k.Length > 0 && Fonts[k] is JsonObject f ? f : null) ?? (Fonts[fallback] as JsonObject) ?? (JsonObject)Fonts["inter"]!;

    private static JsonObject ThemeOf(JsonObject t) => Themes[Str(t["preset"])] as JsonObject ?? (JsonObject)Themes["aurora"]!;

    private static (string Mode, string Dark, string Light) Palette(JsonObject t)
    {
        var pr = ThemeOf(t);
        var mode = T(t, "mode") ? S(t, "mode") : Str(pr["mode"]);
        var over = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in new[] { "bg", "surface", "text", "muted", "primary", "accent" }) if (T(t, k)) over[k] = S(t, k);
        string V(JsonObject p)
        {
            string G(string k) => over.TryGetValue(k, out var v) ? v : Str(p[k]);
            return "--bg:" + G("bg") + ";--surface:" + G("surface") + ";--text:" + G("text") + ";--muted:" + G("muted") + ";--primary:" + G("primary") + ";--accent:" + G("accent") +
                   ";--border:" + G("border") + ";--on-primary:" + G("onPrimary") + ";";
        }
        return (mode, V((JsonObject)pr["dark"]!), V((JsonObject)pr["light"]!));
    }

    /// <summary>cssSafe(c): the person's CSS as it is, only it can never close the &lt;style&gt; tag.</summary>
    public static string CssSafe(string? c) => (c ?? "").Replace("<", "\\3C ");

    private static string Fixed(double v, int digits) => v.ToString("F" + digits, CultureInfo.InvariantCulture);

    private static string Css(JsonObject s)
    {
        var t = (JsonObject)s["theme"]!;
        var pr = ThemeOf(t);
        var p = Palette(t);
        var prFonts = (JsonObject)pr["fonts"]!;
        var hf = FontOf(S(t, "headingFont"), Str(prFonts["heading"]));
        var bf = FontOf(S(t, "bodyFont"), Str(prFonts["body"]));
        double r = t["radius"] is { } rad ? Plus(rad) : Plus(pr["radius"]);
        var bg = T(t, "background") ? S(t, "background") : Str(pr["background"]);
        double sp = S(t, "spacing") switch { "compact" => .7, "comfortable" => 1, "airy" => 1.35, _ => 1 };
        var sh = S(t, "shadow") switch
        {
            "none" => "none", "soft" => "0 1px 2px rgba(0,0,0,.06),0 8px 28px -12px rgba(0,0,0,.28)", "strong" => "0 2px 4px rgba(0,0,0,.1),0 22px 50px -16px rgba(0,0,0,.5)", _ => "none"
        };
        var vars = ":root{" + (p.Mode == "dark" ? p.Dark : p.Light) + "--r:" + Num(r) + "px;--rs:" + Num(LegacyJsMath.Round(r * .6)) + "px;--w:" + (T(t, "width") ? Str(t["width"]) : "1120") + "px;--sp:" + Num(sp) +
                   ";--shadow:" + sh + ";--fh:" + Str(hf["stack"]) + ";--fb:" + Str(bf["stack"]) + ";--fs:" + (T(t, "scale") ? Str(t["scale"]) : "1") + ";color-scheme:" + (p.Mode == "dark" ? "dark" : "light") + "}" +
                   (p.Mode == "auto" ? "@media (prefers-color-scheme:dark){:root{" + p.Dark + "color-scheme:dark}}" : "") +
                   ":root[data-theme=dark]{" + p.Dark + "color-scheme:dark}:root[data-theme=light]{" + p.Light + "color-scheme:light}";
        var bgs = bg switch
        {
            "gradient" => PageEngineText.Background_gradient, "mesh" => PageEngineText.Background_mesh, "dots" => PageEngineText.Background_dots, "grid" => PageEngineText.Background_grid, _ => ""
        };
        var ex = new StringBuilder();
        if (T(t, "border")) ex.Append(":root,:root[data-theme]{--border:" + S(t, "border") + "}");
        if (T(t, "headingWeight")) ex.Append("h1,h2,h3,h4{font-weight:" + S(t, "headingWeight") + "}");
        if (S(t, "headingCase") == "upper") ex.Append("h1,h2,h3{text-transform:uppercase;letter-spacing:.04em}");
        else if (S(t, "headingCase") == "small") ex.Append("h1,h2,h3{font-variant:small-caps;letter-spacing:.02em}");
        if (t["headingSpacing"] is { } hs && !IsEmptyString(hs)) ex.Append("h1,h2,h3,h4{letter-spacing:" + Num(Plus(hs)) + "em}");
        if (T(t, "headingScale") && Plus(t["headingScale"]) != 1)
        {
            var k = Plus(t["headingScale"]);
            ex.Append("h1{font-size:clamp(" + Fixed(2.2 * k, 2) + "rem," + Fixed(5.2 * k, 2) + "vw," + Fixed(4.2 * k, 2) + "rem)}h2{font-size:clamp(" + Fixed(1.6 * k, 2) + "rem," + Fixed(3.2 * k, 2) + "vw," +
                      Fixed(2.5 * k, 2) + "rem)}h3{font-size:" + Fixed(1.2 * k, 2) + "rem}");
        }
        if (T(t, "lineHeight")) ex.Append("body{line-height:" + Num(Plus(t["lineHeight"])) + "}");
        ex.Append(S(t, "buttonStyle") switch
        {
            "pill" => ".btn{border-radius:999px}", "square" => ".btn{border-radius:0}",
            "outline" => ".btn-primary{background:transparent;color:var(--primary);border-color:var(--primary);box-shadow:none}.btn-primary:hover{background:var(--primary);color:var(--on-primary)}",
            "soft" => ".btn-primary{background:color-mix(in srgb,var(--primary) 16%,transparent);color:var(--primary);box-shadow:none}.btn-primary:hover{background:var(--primary);color:var(--on-primary)}",
            _ => ""
        });
        ex.Append(S(t, "cardStyle") switch
        {
            "outlined" => ".card{box-shadow:none}", "flat" => ".card{box-shadow:none;border-color:transparent;background:color-mix(in srgb,var(--text) 5%,var(--bg))}",
            "glass" => ".card{background:color-mix(in srgb,var(--surface) 55%,transparent);-webkit-backdrop-filter:blur(14px);backdrop-filter:blur(14px)}", _ => ""
        });
        ex.Append(S(t, "linkStyle") switch
        {
            "plain" => ".prose a,.lead a{text-decoration:none}",
            "highlight" => ".prose a,.lead a{text-decoration:none;background:color-mix(in srgb,var(--primary) 18%,transparent);border-radius:3px;padding:0 .15em;color:var(--text)}", _ => ""
        });
        return vars + PageEngineText.BaseCss + bgs + (T(t, "animations") ? PageEngineText.AnimCss : "") + PageEngineText.SectionCss + ex + (T(t, "css") ? CssSafe(S(t, "css")) : "");
    }

    private static readonly Dictionary<string, (double W, double H)> PageSizes = new(StringComparer.Ordinal)
    {
        ["a5"] = (148, 210), ["6x9"] = (152.4, 228.6), ["pocket"] = (110, 180), ["a4"] = (210, 297), ["letter"] = (215.9, 279.4)
    };

    private static readonly Dictionary<string, double[]> Margins = new(StringComparer.Ordinal)
    {
        ["narrow"] = [14, 16, 16, 12], ["normal"] = [18, 21, 20, 15], ["wide"] = [24, 27, 26, 20]
    };

    private static string BookCss(JsonObject s, bool book)
    {
        var L = (JsonObject)s["layout"]!;
        var (W, H) = PageSizes.TryGetValue(S(L, "pageSize"), out var sz) ? sz : PageSizes["a5"];
        double k = W > 200 ? 1.2 : W < 120 ? .85 : 1;
        var m = (Margins.TryGetValue(S(L, "margins"), out var mm) ? mm : Margins["normal"]).Select(v => LegacyJsMath.Round(v * k * 10) / 10).ToArray();
        double mt = m[0], mb = m[1], mi = m[2], mo = m[3];
        string Pct(double v) => Fixed(v / W * 100, 3) + "%";
        static string Q(string t) => "\"" + Regex.Replace(Regex.Replace(t, "[\\\\\"]", "\\$0"), "[\n\r]", " ") + "\"";
        var css = new StringBuilder();
        css.Append(".sheet{--pw:" + Num(W) + ";--ph:" + Num(H) + "}");
        css.Append(".bk-wrap{container-type:inline-size;display:flex;flex-direction:column;align-items:center;gap:22px;width:100%}");
        css.Append(".sheet{position:relative;width:min(" + Num(W) + "mm,100%);aspect-ratio:" + Num(W) + "/" + Num(H) + ";padding:" + Pct(mt) + " " + Pct(mo) + " " + Pct(mb) + " " + Pct(mi) +
                   ";background:var(--surface);color:var(--text);box-shadow:0 1px 2px rgba(0,0,0,.08),0 14px 40px -18px rgba(0,0,0,.45);font-size:clamp(13px,calc(min(" + Num(W) +
                   "mm,100cqw)*.0285),17.5px);line-height:1.55;text-align:left;overflow-wrap:break-word}");
        css.Append(PageEngineText.BookSheet);
        bool indent = T(L, "indent");
        css.Append(".sheet .prose{max-width:none;font-size:1em}.sheet .prose p{margin:0" + (indent ? "" : " 0 .9em") + "}" + (indent ? ".sheet .prose p+p{text-indent:1.4em}" : ""));
        if (T(L, "justify")) css.Append(".sheet .prose p,.sheet .prose li{text-align:justify;hyphens:auto;-webkit-hyphens:auto}");
        css.Append(PageEngineText.BookProse);
        if (book)
        {
            css.Append(PageEngineText.BookScreen);
            css.Append("@page{size:" + Num(W) + "mm " + Num(H) + "mm;margin:" + Num(mt) + "mm " + Num(mo) + "mm " + Num(mb) + "mm " + Num(mi) + "mm" +
                       (T(L, "pageNumbers") ? ";@bottom-center{content:counter(page);font:9pt var(--fb);color:#555}" : "") +
                       (T(L, "runningHead") ? ";@top-center{content:" + Q(S(L, "runningHead")) + ";font:italic 8.5pt var(--fb);color:#666}" : "") + "}");
            css.Append("@page :left{margin-left:" + Num(mo) + "mm;margin-right:" + Num(mi) + "mm}@page :right{margin-left:" + Num(mi) + "mm;margin-right:" + Num(mo) + "mm}");
            css.Append(PageEngineText.BookNamedPages);
            css.Append(PageEngineText.BookPrint);
            css.Append(".sheet.flow{break-before:auto;page-break-before:auto}.bk-full{height:" + Fixed(H - mt - mb - .5, 1) + "mm;break-inside:avoid}");
            css.Append(".bk-cover{page:cover;width:" + Num(W) + "mm;height:" + Num(H) + "mm!important;margin:0}.bk-front{page:front}.bk-chap{page:chapter}.bk-part{page:part}.bk-bleed{page:cover;width:" + Num(W) +
                       "mm;height:" + Num(H) + "mm!important;margin:0}" + (T(L, "recto") ? ".bk-chap,.bk-part{break-before:right}" : ""));
            css.Append(PageEngineText.BookPrintEnd);
        }
        return css.ToString();
    }

    // ---------------------------------------------------------------- render

    private static readonly Regex NoteText = new(@"\.(md|markdown|txt)\z", RegexOptions.IgnoreCase);
    private static readonly Regex MdExt = new(@"\.md\z", RegexOptions.IgnoreCase);
    private static readonly Regex HasSheet = new("class=\"sheet");
    private static readonly Regex Braces = new("[{}]");
    private static readonly Regex ClassName = new(@"[^A-Za-z0-9_\s-]");

    /// <summary>encodeURIComponent: UTF-8, everything but A–Z a–z 0–9 - _ . ! ~ * ' ( ) escaped.</summary>
    private static string EncodeUriComponent(string s)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(s))
        {
            char c = (char)b;
            if (b < 128 && (char.IsAsciiLetterOrDigit(c) || "-_.!~*'()".Contains(c))) sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>render(input) of a .page.json text (an invalid text renders the blank page, as normalize does).</summary>
    public string Render(string json, IPageNotes? documents = null, bool preview = false)
    {
        JsonNode? raw;
        try { raw = JsonNode.Parse(json); }
        catch (System.Text.Json.JsonException) { raw = null; }
        return Render(raw ?? Blank(), documents, preview);
    }

    /// <summary>render(input, ctx): the complete HTML of the page; with <paramref name="preview"/> the studio's selection script and hidden sections.</summary>
    public string Render(JsonNode? input, IPageNotes? documents = null, bool preview = false)
    {
        if (!UrbeMarks) return RenderPage(input, documents, preview);
        var marks = new PageCommentMarks(show: preview);
        if (!preview) return marks.Finish(RenderPage(marks.Page(input), marks.Notes(documents), preview));
        // UC-62 slice 4: in the studio preview the plain texts of the sections are written in place
        var edits = new PageEditMarks(this);
        return edits.Finish(marks.Finish(RenderPage(marks.Page(edits.Page(input)), marks.Notes(documents), preview)));
    }

    private string RenderPage(JsonNode? input, IPageNotes? documents, bool preview)
    {
        var s = Normalize(input).Spec;
        var docs = documents;
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        string NoteAnchor(PageNote d) => "nota-" + Slug(d.Title);
        PageNote? NoteOf(string? path)
        {
            if (string.IsNullOrEmpty(path) || docs is null) return null;
            var p = LegacyCustomize.JsTrim(path);
            var bare = JsText.Lower(MdExt.Replace(p, ""));
            return docs.Get(p) ?? docs.Get(p + ".md") ?? docs.List().FirstOrDefault(d => JsText.Lower(d.Title) == bare);
        }
        var sections = s["sections"]!.AsArray().OfType<JsonObject>().ToList();
        static bool Hidden(JsonObject sec) => Truthy(((JsonObject)sec["style"]!)["hidden"]);
        List<PageNote> NotesFor(JsonObject p)
        {
            if (docs is null) return [];
            var all = docs.List().Where(d => NoteText.IsMatch(d.Path)).ToList();
            var source = S(p, "source");
            List<PageNote> output;
            var byLocale = Comparer<string>.Create(JsText.LocaleCompare);
            if (source == "folder")
            {
                var f = JsText.Lower(Regex.Replace(T(p, "folder") ? S(p, "folder") : "", "^/+|/+$", ""));
                output = all.Where(d => f.Length == 0 || JsText.Lower(d.Path).StartsWith(f + "/", StringComparison.Ordinal)).ToList();
                // UC-62: only the folder itself, not its subfolders (an Urbe field; the 1.8.4 pages never set it)
                if (p["subfolders"] is JsonValue sub && sub.TryGetValue<bool>(out var deep) && !deep)
                    output = output.Where(d => d.Path.LastIndexOf('/') is var i && JsText.Lower(i < 0 ? "" : d.Path[..i]) == f).ToList();
            }
            else if (source == "tag")
            {
                var tg = JsText.Lower(Regex.Replace(T(p, "tag") ? S(p, "tag") : "", "^#", ""));
                output = all.Where(d => (d.Tags ?? []).Any(t => JsText.Lower(t) == tg)).ToList();
            }
            else if (source == "list")
                output = (T(p, "paths") ? S(p, "paths") : "").Split('\n').Select(NoteOf).Where(d => d is not null).Select(d => d!).ToList();
            else if (source is "links" or "linked" or "search")
                output = VaultQuery(p, all, NoteOf);
            else output = all.OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => JsText.LocaleCompare(b, a))).ToList();
            if (source is not ("list" or "recent"))
            {
                var sort = S(p, "sort");
                output = sort == "modified" ? output.OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => JsText.LocaleCompare(b, a))).ToList()
                    : sort == "path" ? output.OrderBy(d => d.Path, byLocale).ToList()
                    : output.OrderBy(d => d.Title, byLocale).ToList();
            }
            var limit = Plus(p["limit"]);
            int n = double.IsNaN(limit) || limit == 0 ? 24 : (int)Math.Truncate(limit);
            return n >= 0 ? output.Take(n).ToList() : output.Take(Math.Max(0, output.Count + n)).ToList();
        }
        var included = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sec in sections)
        {
            if (Hidden(sec)) continue;
            var props = (JsonObject)sec["props"]!;
            var type = Str(sec["type"]);
            if (type == "notes" && T(props, "expand")) foreach (var d in NotesFor(props)) included[JsText.Lower(d.Title)] = NoteAnchor(d);
            if (type == "chapters" && T(props, "folder"))
                foreach (var d in NotesFor(new JsonObject { ["source"] = "folder", ["folder"] = Clone(props["folder"]), ["sort"] = Clone(props["sort"]), ["limit"] = 200 }))
                    included[JsText.Lower(d.Title)] = NoteAnchor(d);
        }
        string Wikilink(string target, string? label)
        {
            var key = JsText.Lower(MdExt.Replace(target, "").Split('/')[^1]);
            return included.TryGetValue(key, out var a)
                ? "<a class=\"wikilink\" href=\"#" + Esc(a) + "\">" + Esc(!string.IsNullOrEmpty(label) ? label : target) + "</a>"
                : "<span class=\"wikilink\">" + Esc(!string.IsNullOrEmpty(label) ? label : target.Split('/')[^1]) + "</span>";
        }
        var md = new PageMarkdownOptions(Wikilink) { Math = MathHtml };
        var toc = new List<(string Id, string Anchor, string Title, bool Menu)>();
        foreach (var sec in sections)
        {
            if (Hidden(sec)) continue;
            var props = (JsonObject)sec["props"]!;
            var st = (JsonObject)sec["style"]!;
            var t = T(props, "title") ? S(props, "title") : Str(sec["type"]) == "note" ? NoteOf(S(props, "path"))?.Title ?? "" : "";
            var a = T(st, "anchor") ? Slug(S(st, "anchor")) : t.Length > 0 ? Slug(t) : "";
            if (a.Length > 0) { while (anchors.Contains(a)) a += "-2"; anchors.Add(a); }
            sec["_anchor"] = a;
            if (t.Length > 0 && a.Length > 0) toc.Add((Str(sec["id"]), a, T(st, "anchor") ? S(st, "anchor") : t, T(st, "menu")));
        }
        var entries = new List<BookEntry>();
        int chN = 0, ptN = 0;
        bool usesBook = false;
        foreach (var sec in sections)
        {
            if (Hidden(sec)) continue;
            var type = Str(sec["type"]);
            var props = (JsonObject)sec["props"]!;
            if (_blocks.TryGetValue(type, out var b) && b.Group == "Livro") usesBook = true;
            if (type == "part")
            {
                sec["_num"] = ++ptN;
                if (!Truthy(sec["_anchor"])) sec["_anchor"] = "parte-" + ptN;
                entries.Add(new BookEntry("part", ptN, S(props, "title"), Str(sec["_anchor"])));
            }
            else if (type == "chapter")
            {
                var t = T(props, "title") ? S(props, "title") : S(props, "source") == "note" ? NoteOf(S(props, "path"))?.Title ?? "" : "";
                sec["_num"] = T(props, "numbered") ? ++chN : 0;
                if (!Truthy(sec["_anchor"])) sec["_anchor"] = "capitulo-" + (chN != 0 ? chN.ToString(CultureInfo.InvariantCulture) : Str(sec["id"]));
                entries.Add(new BookEntry("chapter", (int)Plus(sec["_num"]), t, Str(sec["_anchor"])));
            }
            else if (type == "chapters")
            {
                var list = T(props, "folder") ? NotesFor(new JsonObject { ["source"] = "folder", ["folder"] = Clone(props["folder"]), ["sort"] = Clone(props["sort"]), ["limit"] = 200 }) : [];
                var arr = new JsonArray();
                foreach (var d in list)
                {
                    var x = new JsonObject { ["path"] = d.Path, ["num"] = ++chN, ["anchor"] = NoteAnchor(d) };
                    entries.Add(new BookEntry("chapter", chN, d.Title, NoteAnchor(d)));
                    arr.Add(x);
                }
                sec["_list"] = arr;
            }
        }
        var L = (JsonObject)s["layout"]!.DeepClone();
        var M = (JsonObject)s["meta"]!;
        var theme = (JsonObject)s["theme"]!;
        bool book = S(L, "format") == "book";
        var secCss = new List<string>();
        var c = new RenderContext
        {
            Md = md, Note = NoteOf, Notes = NotesFor, NoteAnchor = NoteAnchor, MdNote = (d, shift) => new PageMarkdownOptions(Wikilink, shift, NoteAnchor(d) + "-") { Math = MathHtml },
            Toc = toc, BookEntries = entries, BookStyle = S(L, "chapterStyle"), Preview = preview, Css = secCss, BookFormat = book
        };
        var bodyParts = new List<string>();
        foreach (var sec in sections.Where(x => !Hidden(x) || preview))
        {
            var b = _blocks[Str(sec["type"])];
            var st = (JsonObject)sec["style"]!;
            var id = Str(sec["id"]);
            c.Section = sec;
            string html;
            try { html = b.Render((JsonObject)sec["props"]!, c, sec); }
            catch (Exception e)
            { html = "<div class=\"missing\">Erro ao montar “" + Esc(b.Label) + "”: " + Esc(e.Message) + "</div>"; }
            var className = T(st, "className") ? " " + Esc(ClassName.Replace(S(st, "className"), "")) : "";
            var anchor = Str(sec["_anchor"]);
            if (book)
            {
                if (!HasSheet.IsMatch(html)) html = "<div class=\"sheet flow\">" + html + "</div>";
                if (T(st, "css"))
                {
                    var bsel = "[data-s=\"" + id + "\"]";
                    var bc = CssSafe(S(st, "css"));
                    secCss.Add(Braces.IsMatch(bc) ? bc.Replace("&", bsel) : bsel + " .sheet{" + bc + "}");
                }
                bodyParts.Add("<section class=\"bk-wrap b-" + Str(sec["type"]) + (T(st, "hidden") ? " is-hidden" : "") + className + "\" data-s=\"" + Esc(id) + "\"" +
                              (anchor.Length > 0 && Str(sec["type"]) is not ("part" or "chapter") ? " id=\"" + Esc(anchor) + "\"" : "") + (preview ? " data-sid=\"" + Esc(id) + "\"" : "") + ">" + html + "</section>");
                continue;
            }
            if (HasSheet.IsMatch(html)) html = "<div class=\"bk-wrap\">" + html + "</div>";
            var animation = S(st, "animation");
            bool anim = animation == "none" ? false : animation.Length > 0 || (T(theme, "animations") && Str(sec["type"]) != "hero");
            var cls = "sec b-" + Str(sec["type"]) + " p-" + S(st, "padding") + " w-" + S(st, "width") + " a-" + S(st, "align") + (S(st, "background") != "none" ? " bg-" + S(st, "background") : "") +
                      (anim ? " rv" + (animation.Length > 0 && animation != "up" ? " an-" + animation : "") : "") + (T(st, "boxed") ? " boxed" : "") + (S(st, "minHeight") != "none" ? " mh-" + S(st, "minHeight") : "") +
                      (T(st, "textColor") ? " own-text" : "") + (T(st, "hidden") ? " is-hidden" : "") + className;
            var sty = new List<string>();
            if (S(st, "background") == "image" && SafeUrl(S(st, "image"), true).Length > 0) sty.Add("background-image:url('" + Esc(SafeUrl(S(st, "image"), true)).Replace("'", "%27") + "')");
            if (T(st, "bgColor")) sty.Add("background:" + Esc(S(st, "bgColor")));
            if (T(st, "textColor")) sty.Add("--text:" + Esc(S(st, "textColor")) + ";--muted:color-mix(in srgb," + Esc(S(st, "textColor")) + " 72%,transparent);color:" + Esc(S(st, "textColor")));
            var bgi = sty.Count > 0 ? " style=\"" + string.Join(";", sty) + "\"" : "";
            if (T(st, "css"))
            {
                var sel = "[data-s=\"" + id + "\"]";
                var c0 = CssSafe(S(st, "css"));
                secCss.Add(Braces.IsMatch(c0) ? c0.Replace("&", sel) : sel + "{" + c0 + "}");
            }
            bodyParts.Add("<section class=\"" + cls + "\" data-s=\"" + Esc(id) + "\"" + (anchor.Length > 0 ? " id=\"" + Esc(anchor) + "\"" : "") + (preview ? " data-sid=\"" + Esc(id) + "\"" : "") + bgi +
                          "><div class=\"wrap\">" + html + "</div></section>");
        }
        var body = string.Join("\n", bodyParts);
        var menu = toc.Where(t => t.Menu).ToList();
        if (book) { L["nav"] = false; L["footer"] = ""; L["progress"] = false; L["backToTop"] = false; L["themeToggle"] = false; }
        var nav = T(L, "nav")
            ? "<header class=\"nav" + (T(L, "sticky") ? " sticky" : "") + "\"><div class=\"wrap\"><a class=\"brand\" href=\"#top\">" + (T(M, "icon") ? "<span>" + Esc(S(M, "icon")) + "</span>" : "") +
              Esc(T(L, "brand") ? S(L, "brand") : S(M, "title")) + "</a><input type=\"checkbox\" id=\"nav-t\" aria-hidden=\"true\"><label class=\"nav-burger\" for=\"nav-t\" aria-label=\"Menu\"><span></span></label><nav class=\"nav-links\">" +
              string.Concat(menu.Select(t => "<a href=\"#" + Esc(t.Anchor) + "\">" + Esc(t.Title) + "</a>")) +
              (T(L, "navCta") ? "<a class=\"btn btn-primary\" href=\"" + Esc(SafeUrl(S(L, "navCtaUrl")) is { Length: > 0 } nu ? nu : "#") + "\">" + Esc(S(L, "navCta")) + "</a>" : "") + "</nav></div></header>"
            : "";
        var foot = T(L, "footer") ? "<footer class=\"foot\"><div class=\"wrap\">" + Markdown(S(L, "footer"), md) + "</div></footer>" : "";
        var pr = ThemeOf(theme);
        var prFonts = (JsonObject)pr["fonts"]!;
        var fontList = new List<JsonObject>();
        foreach (var f in new[] { FontOf(S(theme, "headingFont"), Str(prFonts["heading"])), FontOf(S(theme, "bodyFont"), Str(prFonts["body"])) })
            if (T(f, "g") && !fontList.Contains(f)) fontList.Add(f);
        var fonts = fontList.Select(f => "family=" + S(f, "g")).ToList();
        var mode = T(theme, "mode") ? S(theme, "mode") : Str(pr["mode"]);
        var iconSvg = "data:image/svg+xml," + EncodeUriComponent("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><text y=\".9em\" font-size=\"90\">" +
                                                               Regex.Replace(T(M, "icon") ? S(M, "icon") : "✦", "[<>&]", "") + "</text></svg>");
        var image = S(M, "image");
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"" + Esc(T(M, "lang") ? S(M, "lang") : "pt-BR") + "\" class=\"" + (mode == "dark" ? "dark-default" : mode == "light" ? "light-default" : "") + (book ? " book" : "") +
                  "\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1,viewport-fit=cover\">\n<title>" + Esc(S(M, "title")) + "</title>\n");
        if (T(M, "description")) sb.Append("<meta name=\"description\" content=\"" + Esc(S(M, "description")) + "\">\n<meta property=\"og:description\" content=\"" + Esc(S(M, "description")) + "\">\n");
        sb.Append("<meta property=\"og:title\" content=\"" + Esc(S(M, "title")) + "\">\n");
        if (image.Length > 0 && SafeUrl(image, true).Length > 0 && !image.StartsWith("data:", StringComparison.Ordinal)) sb.Append("<meta property=\"og:image\" content=\"" + Esc(SafeUrl(image, true)) + "\">\n");
        sb.Append("<meta name=\"generator\" content=\"Urbe\">\n");
        if (T(M, "head")) sb.Append(Regex.Replace(S(M, "head"), "</head", "", RegexOptions.IgnoreCase) + "\n");
        sb.Append("<link rel=\"icon\" href=\"" + Esc(iconSvg) + "\">\n");
        if (fonts.Count > 0)
            sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\"><link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>\n<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?" +
                      string.Join("&", fonts) + "&display=swap\">\n");
        if ((body + foot).Contains("class=\"katex", StringComparison.Ordinal))
            sb.Append("<link rel=\"stylesheet\" href=\"" + (preview ? "http://localhost/vendor/katex/katex.min.css" : "https://cdn.jsdelivr.net/npm/katex@0.16.47/dist/katex.min.css") + "\">\n");
        sb.Append("<style>" + Css(s) + (usesBook || book ? BookCss(s, book) : "") + string.Concat(secCss) + (preview ? PageEngineText.PreviewCss + ".is-hidden{opacity:.35}" : "") + "</style>\n</head>\n<body id=\"top\">\n");
        if (T(L, "progress")) sb.Append("<div class=\"progress\" aria-hidden=\"true\"></div>");
        sb.Append(nav + "<main>\n" + body + "\n</main>\n" + foot);
        if (T(L, "themeToggle"))
            sb.Append("<button class=\"fab fab-theme\" type=\"button\" aria-label=\"Alternar tema claro/escuro\"><svg width=\"20\" height=\"20\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><path d=\"M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z\"/></svg></button>");
        if (T(L, "backToTop"))
            sb.Append("<button class=\"fab fab-top\" type=\"button\" aria-label=\"Voltar ao topo\"><svg width=\"20\" height=\"20\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M12 19V5M5 12l7-7 7 7\"/></svg></button>");
        if (book && !preview) sb.Append("<button class=\"fab-print\" type=\"button\" onclick=\"print()\">Imprimir ou salvar PDF</button>");
        sb.Append("\n<script>" + PageEngineText.Runtime + (preview ? PageEngineText.Preview : "") + "</script>\n</body>\n</html>\n");
        return sb.ToString();
    }
}
