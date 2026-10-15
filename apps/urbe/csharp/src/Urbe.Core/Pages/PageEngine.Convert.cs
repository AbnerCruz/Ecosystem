using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>
/// "Converter em layout livre" of the studio (free.js fromBlock/paragraphs/cleanNull/fixVarColors): a closed block
/// (text, hero, quote, image, the book's parts, chapter, note) becomes a free layout in which every title and paragraph
/// is a piece of its own. Proved by csharp/tools/pages-convert-oracle.mjs (ids aside).
/// </summary>
public sealed partial class PageEngine
{
    private static JsonObject St(params (string Key, JsonNode? Value)[] kv)
    {
        var o = new JsonObject();
        foreach (var (k, v) in kv) o[k] = v;
        return o;
    }

    private static JsonObject Ct(params (string Key, JsonNode? Value)[] kv) => St(kv);

    private static JsonArray Kids(IEnumerable<JsonObject?> list) => new(list.Where(x => x is not null).Select(x => (JsonNode)x!).ToArray());

    private static string P(JsonObject p, string k) => p[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : p[k] is null ? "" : LegacyCustomize.JsToString(p[k]);

    /// <summary>paragraphs(md): each paragraph a piece — a heading one level down, a divider, a quote, an image or text.</summary>
    public static List<JsonObject> FreeParagraphs(string? md)
    {
        var output = new List<JsonObject>();
        foreach (var par in Regex.Split(Regex.Replace(md ?? "", "\r\n?", "\n"), "\n{2,}"))
        {
            var t = par.Trim();
            if (t.Length == 0) continue;
            var m = Regex.Match(t, @"^(#{1,6})\s+(.+)$", RegexOptions.Singleline);
            if (m.Success && !t.Contains('\n')) output.Add(Make("heading", Ct(("text", m.Groups[2].Value), ("level", Math.Min(6, m.Groups[1].Length + 1)))));
            else if (Regex.IsMatch(t, @"^(\*\s*){3}$|^-{3,}$")) output.Add(Make("divider"));
            else if (Regex.IsMatch(t, @"^>\s?") && !Regex.IsMatch(t, @"^>\s*\[!")) output.Add(Make("quote", Ct(("text", Regex.Replace(t, @"^>\s?", "", RegexOptions.Multiline)))));
            else if (Regex.IsMatch(t, @"^!\[[^\]]*\]\([^)]+\)$"))
            {
                var im = Regex.Match(t, @"^!\[([^\]]*)\]\(([^)\s]+)");
                output.Add(Make("image", Ct(("src", im.Groups[2].Value), ("alt", im.Groups[1].Value))));
            }
            else output.Add(Make("text", Ct(("text", t))));
        }
        return output;
    }

    /// <summary>fromBlock(sec, {note}): the free layout of a closed block, or null when it does not convert.</summary>
    public static JsonObject? FreeFromBlock(JsonObject sec, Func<string?, PageNote?>? note = null)
    {
        var p = sec["props"] as JsonObject ?? [];
        var t = (string?)sec["type"];
        JsonObject? n = t switch
        {
            "text" => Make("box", [], St(("gap", "14px")), Kids((P(p, "title").Length > 0 ? [Make("heading", Ct(("text", P(p, "title")), ("level", 2)))] : new List<JsonObject?>()).Concat(FreeParagraphs(P(p, "markdown"))))),
            "hero" => Hero(p),
            "quote" => Make("box", [], [], Kids([Make("quote", Ct(("text", P(p, "text")), ("author", string.Join(", ", new[] { P(p, "author"), P(p, "role") }.Where(x => x.Length > 0)))))])),
            "image" => Make("box", [], [], Kids([Make("image", Ct(("src", P(p, "src") is { Length: > 0 } src ? src : P(p, "image")), ("alt", P(p, "alt") is { Length: > 0 } alt ? alt : P(p, "caption")), ("caption", P(p, "caption"))))])),
            "bookcover" => Make("box", Ct(("tag", "header")), St(("minHeight", "100%"), ("padding", "12% 10%"), ("gap", "14px"), ("textAlign", "center"), ("align", "center"), ("bg", "#7a2e1f"), ("color", "#f6efe2")), Kids(
            [
                Make("heading", Ct(("text", P(p, "title")), ("level", 1)), St(("size", "2.6em"))),
                P(p, "subtitle").Length > 0 ? Make("text", Ct(("text", "*" + P(p, "subtitle") + "*"))) : null,
                Make("spacer", [], St(("css", "flex:1"))),
                Make("text", Ct(("text", P(p, "author"))), St(("transform", "uppercase"), ("spacing", ".18em"))),
                P(p, "publisher").Length > 0 ? Make("text", Ct(("text", P(p, "publisher"))), St(("size", ".78em"), ("transform", "uppercase"), ("spacing", ".14em"), ("opacity", .75))) : null
            ])),
            "titlepage" => Make("box", [], St(("minHeight", "100%"), ("gap", "14px"), ("textAlign", "center"), ("align", "center"), ("justify", "between")), Kids(
            [
                Make("text", Ct(("text", P(p, "author"))), St(("transform", "uppercase"), ("spacing", ".18em"))),
                Make("box", [], St(("gap", "8px"), ("align", "center")), Kids([Make("heading", Ct(("text", P(p, "title")), ("level", 1))), P(p, "subtitle").Length > 0 ? Make("text", Ct(("text", "*" + P(p, "subtitle") + "*"))) : null])),
                Make("text", Ct(("text", string.Join("\n\n", new[] { P(p, "publisher"), string.Join(" · ", new[] { P(p, "place"), P(p, "year") }.Where(x => x.Length > 0)) }.Where(x => x.Length > 0)))), St(("size", ".85em")))
            ])),
            "dedication" or "copyright" or "colophon" or "about" or "part" => Make("box", [], St(("gap", "12px"), ("justify", t is "copyright" or "colophon" ? "end" : "center"), ("minHeight", "100%"),
                    ("textAlign", t is "dedication" or "part" or "colophon" ? "center" : null)),
                Kids((P(p, "title").Length > 0 ? [Make("heading", Ct(("text", (t == "part" ? "Parte " + P(sec, "_num") + " · " : "") + P(p, "title")), ("level", 2)))] : new List<JsonObject?>())
                    .Concat(FreeParagraphs(P(p, "markdown"))).Concat(P(p, "author").Length > 0 ? [Make("text", Ct(("text", "— " + P(p, "author"))))] : []))),
            "chapter" => Chapter(p, note),
            "note" when note is not null => NoteBox(p, note),
            _ => null
        };
        return n is null ? null : CleanNull(n);
    }

    private static JsonObject Hero(JsonObject p)
    {
        var kids = new List<JsonObject?>();
        if (P(p, "eyebrow").Length > 0) kids.Add(Make("text", Ct(("text", P(p, "eyebrow"))), St(("transform", "uppercase"), ("spacing", ".12em"), ("size", ".85rem"), ("weight", "700"))));
        kids.Add(Make("heading", Ct(("text", P(p, "title")), ("level", 1))));
        if (P(p, "subtitle").Length > 0) kids.AddRange(FreeParagraphs(P(p, "subtitle")));
        if (P(p, "image").Length > 0) kids.Add(Make("image", Ct(("src", P(p, "image")), ("alt", P(p, "title"))), St(("radius", "16px"))));
        var bs = (p["buttons"] as JsonArray ?? []).OfType<JsonObject>().Where(b => P(b, "label").Length > 0).ToList();
        if (bs.Count > 0)
            kids.Add(Make("box", [], St(("display", "row"), ("gap", "12px"), ("minCol", "160px")),
                Kids(bs.Select(b => Make("button", Ct(("label", P(b, "label")), ("url", P(b, "url")), ("variant", P(b, "variant"))))))));
        var layout = P(p, "layout");
        var height = P(p, "height");
        return Make("box", Ct(("tag", "header")), St(("gap", "18px"), ("align", layout == "center" ? "center" : "stretch"), ("textAlign", layout == "center" ? "center" : null),
            ("minHeight", height == "screen" ? "100vh" : height == "tall" ? "70vh" : null), ("justify", "center")), Kids(kids));
    }

    private static JsonObject Chapter(JsonObject p, Func<string?, PageNote?>? note)
    {
        var d = note?.Invoke(P(p, "path"));
        var body = P(p, "source") == "note" && d is not null ? PageMarkdown.StripTitle(d.Content, d.Title, true) : P(p, "markdown");
        var title = P(p, "title").Length > 0 ? P(p, "title") : d?.Title is { Length: > 0 } dt ? dt : "Capítulo";
        var kids = new List<JsonObject?> { Make("heading", Ct(("text", title), ("level", 2)), St(("textAlign", "center"), ("margin", "8% 0 6%"))) };
        if (P(p, "epigraph").Length > 0) kids.Add(Make("quote", Ct(("text", P(p, "epigraph")), ("author", P(p, "epigraphAuthor")))));
        kids.AddRange(FreeParagraphs(body));
        return Make("box", Ct(("tag", "article")), St(("gap", "14px")), Kids(kids));
    }

    private static JsonObject NoteBox(JsonObject p, Func<string?, PageNote?> note)
    {
        var d = note(P(p, "path"));
        var kids = new List<JsonObject?>();
        if (d is not null && PageEngine.Truthy(p["showTitle"])) kids.Add(Make("heading", Ct(("text", d.Title), ("level", 1))));
        kids.AddRange(FreeParagraphs(d is not null ? PageMarkdown.StripTitle(d.Content, d.Title, true) : ""));
        return Make("box", Ct(("tag", "article")), St(("gap", "14px")), Kids(kids));
    }

    /// <summary>cleanNull + fixVarColors: null styles removed; a bg that is not a plain color goes to the element's css.</summary>
    private static JsonObject CleanNull(JsonObject n)
    {
        if (n["style"] is JsonObject s)
            foreach (var k in s.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList()) s.Remove(k);
        if (n["children"] is JsonArray kids) foreach (var k in kids.OfType<JsonObject>()) CleanNull(k);
        return FixVarColors(n);
    }

    private static JsonObject FixVarColors(JsonObject n)
    {
        foreach (var b in new[] { "style", "tablet", "mobile" })
        {
            if (n[b] is not JsonObject s || s["bg"] is not JsonValue v || !v.TryGetValue<string>(out var bg) || bg.Length == 0 || ColorRe.IsMatch(bg)) continue;
            s["css"] = (s["css"] is JsonValue c && c.TryGetValue<string>(out var css) && css.Length > 0 ? css + ";" : "") + "background:" + bg;
            s.Remove("bg");
        }
        if (n["children"] is JsonArray kids) foreach (var k in kids.OfType<JsonObject>()) FixVarColors(k);
        return n;
    }
}
