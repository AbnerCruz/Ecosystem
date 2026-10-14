using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Urbe.Core.Pages.PageMarkdown;

namespace Urbe.Core.Pages;

/// <summary>
/// The free layout of the 1.8.4's pages (src/pages/free.js): a tree of elements (containers with children, headings,
/// paragraphs, images, buttons…) each with its own style for computer, tablet (≤ 900px) and phone (≤ 600px);
/// normalizeTree validates it and the "free" block writes its HTML and CSS.
/// </summary>
public sealed partial class PageEngine
{
    private sealed record FreeType(string Type, bool Box, JsonArray Content);

    private static readonly Dictionary<string, FreeType> FreeTypes = JsonNode.Parse(PageEngineText.FreeTypesJson)!.AsArray().OfType<JsonObject>()
        .Select(t => new FreeType((string)t["type"]!, (bool)t["box"]!, t["content"]!.AsArray())).ToDictionary(t => t.Type, StringComparer.Ordinal);

    private static readonly Dictionary<string, JsonObject> FreeStyle = JsonNode.Parse(PageEngineText.FreeStyleJson)!.AsArray().OfType<JsonObject>()
        .ToDictionary(f => (string)f["key"]!, StringComparer.Ordinal);

    private const string Len = @"(-?([0-9]+(\.[0-9]+)?|\.[0-9]+)(px|rem|em|%|vw|vh|svh|dvh|ch)?|auto|0|(clamp|min|max|calc)\([A-Za-z0-9_.%,+*/\s()-]{1,80}\))";
    private static readonly Regex Lens = new("^" + Len + @"(\s+" + Len + "){0,3}\\z");
    private static readonly Regex One = new("^" + Len + "\\z");
    private static readonly Regex FreeId = new(@"^[A-Za-z0-9_-]{1,24}\z");

    private static int _nseq;

    /// <summary>nid(): a new element id.</summary>
    private static string Nid()
    {
        _nseq = (_nseq + 1) % 1_000_000;
        var now = Base36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return "n" + now[^Math.Min(4, now.Length)..] + Base36(_nseq) + Base36(Random.Shared.Next(1296, 46656))[1..];
    }

    private static JsonObject CleanStyle(JsonNode? src0, string path, Reporter rep)
    {
        var output = new JsonObject();
        if (src0 is not JsonObject src) return output;
        foreach (var (k, v0) in src)
        {
            var v = v0;
            if (v is null || IsEmptyString(v)) continue;
            if (!FreeStyle.TryGetValue(k, out var f)) { rep.Warn(path + "." + k, "estilo desconhecido, ignorado."); continue; }
            var type = (string)f["type"]!;
            var sv = LegacyCustomize.JsToString(v);
            switch (type)
            {
                case "select":
                {
                    var options = f["options"]!.AsArray().Select(Str).ToList();
                    if (options.IndexOf(sv) > 0) output[k] = sv;
                    else rep.Err(path + "." + k, "valor \"" + sv + "\" inválido; use um de: " + string.Join(", ", options.Skip(1)) + ".");
                    continue;
                }
                case "number":
                {
                    var n = LegacyCustomize.JsToNumber(v);
                    if (double.IsFinite(n)) output[k] = Math.Max(f["min"]!.GetValue<double>(), Math.Min(f["max"]!.GetValue<double>(), n));
                    else rep.Err(path + "." + k, "deve ser um número.");
                    continue;
                }
                case "length":
                {
                    var l = Regex.Replace(LegacyCustomize.JsTrim(sv), @"\s+", " ");
                    if ((Truthy(f["many"]) ? Lens : One).IsMatch(l)) output[k] = l;
                    else rep.Err(path + "." + k, "medida inválida \"" + l + "\" (use px, rem, em, %, vw, vh, auto ou clamp()/min()/max()/calc()).");
                    continue;
                }
                case "color":
                {
                    var c = LegacyCustomize.JsTrim(sv);
                    if (IsColor(c)) output[k] = c;
                    else rep.Err(path + "." + k, "cor inválida \"" + c + "\".");
                    continue;
                }
                case "image":
                {
                    var u = SafeUrl(sv, true);
                    if (u.Length > 0) output[k] = Regex.Replace(u, @"[\s'""()<>\\]", m => "%" + ((int)m.Value[0]).ToString("X2", CultureInfo.InvariantCulture));
                    continue;
                }
            }
            if (k == "className")
            {
                var cn = LegacyCustomize.JsTrim(Regex.Replace(sv, @"[^A-Za-z0-9_\s-]", ""));
                output[k] = cn.Length > 80 ? cn[..80] : cn;
                continue;
            }
            output[k] = sv.Length > 4000 ? sv[..4000] : sv;
        }
        return output;
    }

    private static JsonObject CleanContent(string type, JsonNode? src)
    {
        var output = new JsonObject();
        var c = src as JsonObject ?? [];
        foreach (var f in FreeTypes[type].Content.OfType<JsonObject>())
        {
            var key = (string)f["key"]!;
            var v = c[key];
            if (v is null || IsEmptyString(v))
            {
                if (f["default"] is { } d) output[key] = d.DeepClone();
                continue;
            }
            var ft = (string)f["type"]!;
            if (ft == "boolean") output[key] = Truthy(v);
            else if (ft == "select")
            {
                var sv = LegacyCustomize.JsToString(v);
                var opt = f["options"]!.AsArray().FirstOrDefault(o => Str(o) == sv);
                output[key] = opt is not null ? opt.DeepClone() : f["default"]?.DeepClone();
            }
            else output[key] = LegacyCustomize.JsToString(v);
        }
        return output;
    }

    private static JsonObject Make(string type, JsonObject? content = null, JsonObject? style = null, JsonArray? children = null)
    {
        var n = new JsonObject { ["id"] = Nid(), ["type"] = type };
        var c = CleanContent(type, content ?? []);
        if (c.Count > 0) n["content"] = c;
        if (style is not null) n["style"] = style;
        if (FreeTypes[type].Box) n["children"] = children ?? [];
        return n;
    }

    /// <summary>starter(): what a new free layout begins with.</summary>
    private static JsonObject Starter() => Make("box", [], new JsonObject { ["padding"] = "24px 0", ["gap"] = "16px" },
    [
        Make("heading", new JsonObject { ["text"] = "Um título", ["level"] = 2 }),
        Make("text", new JsonObject { ["text"] = "Toque num elemento da prévia para editar. Toque de novo num texto para escrever direto nele." })
    ]);

    /// <summary>normalizeTree(v): valid types, unique ids, only containers have children, depth and size limited.</summary>
    private static JsonNode CoerceTree(JsonNode? v, string path, Reporter rep)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        JsonObject? Node(JsonNode? x0, string p, int depth)
        {
            if (x0 is not JsonObject x) { rep.Err(p, "cada elemento deve ser um objeto {type, …}."); return null; }
            var typeName = x["type"] is null ? "undefined" : Str(x["type"]);
            if (!FreeTypes.TryGetValue(typeName, out var type)) { rep.Err(p + ".type", "tipo \"" + typeName + "\" não existe. Tipos: " + string.Join(", ", FreeTypes.Keys) + "."); return null; }
            if (++count > 1500) { rep.Err(p, "elementos demais (máximo 1500)."); return null; }
            var id = x["id"] is JsonValue iv && iv.TryGetValue<string>(out var sid) && FreeId.IsMatch(sid) && !ids.Contains(sid) ? sid : Nid();
            ids.Add(id);
            var o = new JsonObject { ["id"] = id, ["type"] = type.Type };
            var c = CleanContent(type.Type, x["content"]);
            if (c.Count > 0) o["content"] = c;
            var s = CleanStyle(x["style"], p + ".style", rep);
            if (s.Count > 0) o["style"] = s;
            foreach (var b in new[] { "tablet", "mobile" })
            {
                var r = CleanStyle(x[b], p + "." + b, rep);
                if (r.Count > 0) o[b] = r;
            }
            if (type.Box)
            {
                var kids = x["children"] as JsonArray ?? [];
                if (depth > 24) { rep.Err(p, "aninhamento fundo demais."); kids = []; }
                var output = new JsonArray();
                int i = 0;
                foreach (var k in kids) if (Node(k, p + ".children[" + i++ + "]", depth + 1) is { } n) output.Add(n);
                o["children"] = output;
            }
            else if (x["children"] is JsonArray ch && ch.Count > 0 || x["children"] is JsonValue cv && Str(cv).Length > 0)
                rep.Warn(p + ".children", "só containers têm filhos; ignorados.");
            return o;
        }
        if (v is null || IsEmptyString(v)) return Starter();
        var root = Node(v, path, 0);
        if (root is null) return Starter();
        if (Str(root["type"]) != "box") root = new JsonObject { ["id"] = Nid(), ["type"] = "box", ["children"] = new JsonArray(root) };
        return root;
    }

    // ---------------------------------------------------------------- CSS and HTML of each element

    private static readonly Dictionary<string, string> Just = new(StringComparer.Ordinal)
    {
        ["start"] = "flex-start", ["center"] = "center", ["end"] = "flex-end", ["between"] = "space-between", ["around"] = "space-around", ["evenly"] = "space-evenly"
    };

    private static string FreeShadow(string k) => k switch
    {
        "none" => PageEngineText.FreeShadow_none, "soft" => PageEngineText.FreeShadow_soft, "strong" => PageEngineText.FreeShadow_strong, "glow" => PageEngineText.FreeShadow_glow, _ => "undefined"
    };

    private static string Decl(JsonObject? s, bool box)
    {
        if (s is null) return "";
        var d = new List<string>();
        string G(string k) => S(s, k);
        bool H(string k) => T(s, k);
        var display = G("display");
        if (display == "none") d.Add("display:none");
        else if (box && display == "row") d.Add("display:flex;flex-direction:row;flex-wrap:wrap");
        else if (box && display == "grid")
            d.Add("display:grid;grid-template-columns:" + (H("columns") ? "repeat(" + G("columns") + ",minmax(0,1fr))" : "repeat(auto-fit,minmax(min(100%," + (H("minCol") ? G("minCol") : "220px") + "),1fr))"));
        else if (box && display == "stack") d.Add("display:flex;flex-direction:column;flex-wrap:nowrap");
        if (box && display is not ("grid" or "row" or "stack" or "none") && (s["columns"] is not null || H("minCol")))
            d.Add("grid-template-columns:" + (H("columns") ? "repeat(" + G("columns") + ",minmax(0,1fr))" : "repeat(auto-fit,minmax(min(100%," + G("minCol") + "),1fr))"));
        if (H("gap")) d.Add("gap:" + G("gap"));
        if (H("minCol")) d.Add("--fx-basis:" + G("minCol"));
        if (H("justify")) d.Add("justify-content:" + (Just.TryGetValue(G("justify"), out var j) ? j : "undefined"));
        if (H("align")) d.Add("align-items:" + (Just.TryGetValue(G("align"), out var a) ? a : G("align")));
        if (H("self")) d.Add("align-self:" + (Just.TryGetValue(G("self"), out var se) ? se : G("self")));
        if (s["order"] is not null) d.Add("order:" + G("order"));
        if (H("width")) d.Add("width:" + (G("width") == "full" ? "100%" : G("width")) + ";flex-basis:" + (G("width") == "auto" ? "auto" : G("width")) + ";flex-grow:0");
        if (H("maxWidth")) d.Add("max-width:" + G("maxWidth"));
        if (H("minHeight")) d.Add("min-height:" + G("minHeight"));
        if (H("height")) d.Add("height:" + G("height"));
        if (H("aspect")) d.Add("aspect-ratio:" + G("aspect"));
        if (H("fit")) d.Add("--fx-fit:" + G("fit"));
        if (H("padding")) d.Add("padding:" + G("padding"));
        if (H("margin")) d.Add("margin:" + G("margin"));
        var bgs = new List<string>();
        if (H("bg") && H("bg2")) bgs.Add("linear-gradient(" + (s["angle"] is not null ? G("angle") : "135") + "deg," + G("bg") + "," + G("bg2") + ")");
        if (H("bgImage")) bgs.Add("url('" + Regex.Replace(G("bgImage"), @"[\s'""()<>\\]", m => EncodeUriComponent(m.Value)) + "') center/cover no-repeat");
        if (bgs.Count > 0) d.Add("background:" + string.Join(",", bgs) + (H("bg") && !H("bg2") ? " " + G("bg") : ""));
        else if (H("bg")) d.Add("background:" + G("bg"));
        if (H("color")) d.Add("color:" + G("color") + ";--text:" + G("color") + ";--muted:color-mix(in srgb," + G("color") + " 72%,transparent)");
        if (H("border")) d.Add("border:" + G("border") + " " + (H("borderStyle") ? G("borderStyle") : "solid") + " " + (H("borderColor") ? G("borderColor") : "var(--border)"));
        else
        {
            if (H("borderStyle")) d.Add("border-style:" + G("borderStyle"));
            if (H("borderColor")) d.Add("border-color:" + G("borderColor"));
        }
        if (H("radius")) d.Add("border-radius:" + G("radius"));
        if (H("shadow")) d.Add("box-shadow:" + FreeShadow(G("shadow")));
        if (s["opacity"] is not null) d.Add("opacity:" + G("opacity"));
        if (H("font")) d.Add("font-family:" + (G("font") == "heading" ? "var(--fh)" : G("font") == "body" ? "var(--fb)" : Str(((JsonObject)Fonts[G("font")]!)["stack"])));
        if (H("size")) d.Add("font-size:" + G("size"));
        if (H("weight")) d.Add("font-weight:" + G("weight"));
        if (s["lineHeight"] is not null) d.Add("line-height:" + G("lineHeight"));
        if (H("spacing")) d.Add("letter-spacing:" + G("spacing"));
        if (H("textAlign")) d.Add("text-align:" + G("textAlign"));
        if (H("transform")) d.Add("text-transform:" + G("transform"));
        if (H("italic")) d.Add("font-style:" + (G("italic") == "yes" ? "italic" : "normal"));
        return string.Join(";", d);
    }

    private static void NodeCss(JsonObject n, (List<string> Base, List<string> Tablet, List<string> Mobile) output, string pre)
    {
        var sel = ".fx-" + pre + Str(n["id"]);
        bool box = FreeTypes[Str(n["type"])].Box;
        var styles = new[] { n["style"] as JsonObject, n["tablet"] as JsonObject, n["mobile"] as JsonObject };
        var b = Decl(styles[0], box);
        if (b.Length > 0) output.Base.Add(sel + "{" + b + "}");
        if (styles[1] is not null && Decl(styles[1], box) is { Length: > 0 } t) output.Tablet.Add(sel + "{" + t + "}");
        if (styles[2] is not null && Decl(styles[2], box) is { Length: > 0 } m) output.Mobile.Add(sel + "{" + m + "}");
        List<string> Dst(int i) => i == 0 ? output.Base : i == 1 ? output.Tablet : output.Mobile;
        for (int i = 0; i < 3; i++)
        {
            if (styles[i] is not { } s || !box) continue;
            var display = S(s, "display");
            if (display == "row") Dst(i).Add(sel + ">*{flex:1 1 var(--fx-basis,220px)}");
            else if (display is "stack" or "grid") Dst(i).Add(sel + ">*{flex:0 1 auto}");
        }
        for (int i = 0; i < 3; i++)
        {
            if (styles[i] is not { } s || !T(s, "css")) continue;
            var c = CssSafe(S(s, "css"));
            Dst(i).Add(Braces.IsMatch(c) ? c.Replace("&", sel) : sel + "{" + c + "}");
        }
        foreach (var k in (n["children"] as JsonArray ?? []).OfType<JsonObject>()) NodeCss(k, output, pre);
    }

    private static readonly Regex HeadingMarks = new(@"[*_`\[\]#>|~=]");
    private static readonly Regex TextMarks = new(@"[*_`\[\]#>|~=-]");
    private static readonly Regex ListMarker = new(@"^\s*([-*+]|[0-9]+[.)])\s+");

    private static string RenderNode(JsonObject n, RenderContext c, bool prev, string pre)
    {
        var ct = n["content"] as JsonObject ?? [];
        var style = n["style"] as JsonObject;
        var cls = "fx fx-" + pre + Str(n["id"]) + (style is not null && T(style, "className") ? " " + Esc(S(style, "className")) : "");
        var at = " class=\"" + cls + "\"";
        var da = prev ? " data-nid=\"" + Esc(Str(n["id"])) + "\"" : "";
        string C(string k) => S(ct, k);
        switch (Str(n["type"]))
        {
            case "box":
            {
                var tag = T(ct, "tag") ? C("tag") : "div";
                var inner = string.Concat((n["children"] as JsonArray ?? []).OfType<JsonObject>().Select(k => RenderNode(k, c, prev, pre)));
                var u = T(ct, "link") ? SafeUrl(C("link")) : "";
                if (u.Length > 0) return "<a href=\"" + Esc(u) + "\" class=\"" + cls + " fx-box\"" + da + ">" + inner + "</a>";
                return "<" + tag + " class=\"" + cls + " fx-box\"" + da + ">" + inner + "</" + tag + ">";
            }
            case "heading":
            {
                var level = Plus(ct["level"]);
                int lv = (int)Math.Max(1, Math.Min(6, double.IsNaN(level) || level == 0 ? 2 : level));
                bool plain = !HeadingMarks.IsMatch(C("text"));
                return "<h" + lv + at + da + (prev && plain ? " data-edit=\"1\"" : "") + ">" + Inline(C("text"), c.Md) + "</h" + lv + ">";
            }
            case "text":
            {
                bool pl = !TextMarks.IsMatch(C("text"));
                return "<div class=\"" + cls + " fx-text\"" + da + (prev && pl ? " data-edit=\"1\"" : "") + ">" + Markdown(C("text"), c.Md) + "</div>";
            }
            case "image":
            {
                var src = SafeUrl(C("src"), true);
                var im = src.Length > 0 ? "<img src=\"" + Esc(src) + "\" alt=\"" + Esc(C("alt")) + "\" loading=\"lazy\">" : "<div class=\"missing\">Escolha uma imagem</div>";
                var lk = T(ct, "link") ? SafeUrl(C("link")) : "";
                return "<figure class=\"" + cls + " fx-img\"" + da + ">" + (lk.Length > 0 ? "<a href=\"" + Esc(lk) + "\">" + im + "</a>" : im) + (T(ct, "caption") ? "<figcaption>" + Inline(C("caption"), c.Md) + "</figcaption>" : "") + "</figure>";
            }
            case "button":
            {
                var bu = SafeUrl(C("url")) is { Length: > 0 } x ? x : "#";
                return "<a class=\"" + cls + " btn btn-" + Esc(T(ct, "variant") ? C("variant") : "primary") + "\" href=\"" + Esc(bu) + "\"" + Blank(bu) + da + ">" + Esc(C("label")) + "</a>";
            }
            case "list":
            {
                var tg = T(ct, "ordered") ? "ol" : "ul";
                return "<" + tg + " class=\"" + cls + " fx-list\"" + da + ">" + string.Concat(C("items").Split('\n').Where(x => LegacyCustomize.JsTrim(x).Length > 0)
                    .Select(x => "<li>" + Inline(ListMarker.Replace(x, "", 1), c.Md) + "</li>")) + "</" + tg + ">";
            }
            case "quote": return "<blockquote class=\"" + cls + " fx-quote\"" + da + ">" + Markdown(C("text"), c.Md) + (T(ct, "author") ? "<cite>— " + Inline(C("author"), c.Md) + "</cite>" : "") + "</blockquote>";
            case "icon": return "<span class=\"" + cls + " fx-icon\"" + da + ">" + Esc(C("emoji")) + "</span>";
            case "divider": return "<hr class=\"" + cls + " fx-divider\"" + da + ">";
            case "spacer": return "<div class=\"" + cls + " fx-spacer\"" + da + " aria-hidden=\"true\"></div>";
            case "video": return "<div class=\"" + cls + " fx-video-wrap\"" + da + ">" + (T(ct, "url") ? VideoHtml(C("url"), "") : "<div class=\"missing\">Cole o link de um vídeo</div>") + "</div>";
            case "note":
            {
                var d = c.Note(C("path"));
                if (d is null) return "<div class=\"" + cls + " missing\"" + da + ">Nota não encontrada: " + Esc(T(ct, "path") ? C("path") : "(escolha uma nota)") + "</div>";
                return "<div class=\"" + cls + " fx-text\"" + da + ">" + (T(ct, "showTitle") ? "<h2>" + Esc(d.Title) + "</h2>" : "") + Markdown(StripTitle(d.Content, d.Title, true), c.MdNote(d, T(ct, "showTitle") ? 1 : 0)) + "</div>";
            }
            case "html": return "<div class=\"" + cls + "\"" + da + ">" + C("code") + "</div>";
            case "table": return "<div class=\"" + cls + " fx-table\"" + da + ">" + Markdown(C("md"), c.Md) + "</div>";
            case "code": return "<pre class=\"" + cls + " code\"" + (T(ct, "lang") ? " data-lang=\"" + Esc(C("lang")) + "\"" : "") + da + "><button type=\"button\" class=\"copy\">Copiar</button><code>" + Esc(C("code")) + "</code></pre>";
            case "formula": return "<div class=\"" + cls + " fx-formula\"" + da + ">" + Markdown("$$" + C("tex").Replace("$$", "") + "$$", c.Md) + "</div>";
            case "badge": return "<span class=\"" + cls + " fx-badge\"" + da + (prev ? " data-edit=\"1\"" : "") + ">" + Esc(C("text")) + "</span>";
            case "embed":
            {
                var eu = SafeUrl(C("url"));
                if (eu.Length == 0 || !eu.StartsWith("https:", StringComparison.OrdinalIgnoreCase)) return "<div class=\"" + cls + " missing\"" + da + ">Cole um endereço https:// para incorporar</div>";
                return "<div class=\"" + cls + " fx-embed\"" + da + "><iframe src=\"" + Esc(eu) + "\" title=\"" + Esc(C("title")) +
                       "\" loading=\"lazy\" referrerpolicy=\"no-referrer\" sandbox=\"allow-scripts allow-same-origin allow-popups allow-forms\" allowfullscreen></iframe></div>";
            }
            case "pagebreak": return "<div class=\"" + cls + " fx-break\"" + da + " aria-hidden=\"true\"></div>";
        }
        return "";
    }

    private static readonly Regex NotIdChar = new(@"[^A-Za-z0-9_-]");

    private void RegisterFree() => On("free", (p, c, sec) =>
    {
        var root = p["root"] as JsonObject ?? Starter();
        var pre = (Truthy(c.Section["id"]) ? NotIdChar.Replace(Str(c.Section["id"]), "") : "x") + "-";
        var output = (Base: new List<string>(), Tablet: new List<string>(), Mobile: new List<string>());
        NodeCss(root, output, pre);
        var first = !c.FreeCssAdded;
        c.FreeCssAdded = true;
        c.Css.Add((first ? PageEngineText.FreeCss : "") + string.Concat(output.Base) + (output.Tablet.Count > 0 ? "@media (max-width:900px){" + string.Concat(output.Tablet) + "}" : "") +
                  (output.Mobile.Count > 0 ? "@media (max-width:600px){" + string.Concat(output.Mobile) + "}" : ""));
        var html = "<div class=\"fx-free\">" + RenderNode(root, c, c.Preview, pre) + "</div>";
        if (c.BookFormat)
        {
            var sheet = S(p, "sheet");
            return "<div class=\"sheet" + (sheet == "flow" ? " flow" : " bk-free") + (sheet == "full" ? " bk-full bk-bleed" : "") + "\">" + html + "</div>";
        }
        return html;
    });
}
