using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>A problem found while normalizing a page (engine.js rep.err / rep.warn).</summary>
public sealed record PageIssue(string Path, string Message);

/// <summary>normalize(input): the complete page and what was wrong with it.</summary>
public sealed record PageNormalized(JsonObject Spec, IReadOnlyList<PageIssue> Errors, IReadOnlyList<PageIssue> Warnings);

/// <summary>A note as the page engine reads it (the documents service of the 1.8.4: path, title, content, modified, tags).</summary>
public sealed record PageNote(string Path, string Title, string Content, string? Modified = null, IReadOnlyList<string>? Tags = null);

/// <summary>The notes a page can show (documents.get / documents.list).</summary>
public interface IPageNotes
{
    PageNote? Get(string path);
    IReadOnlyList<PageNote> List();
}

/// <summary>
/// The page engine of the Urbe 1.8.4 (src/pages/engine.js), ported: a page is a .page.json file (meta, theme, layout
/// and sections); <see cref="Normalize"/> validates it field by field, <see cref="Compact"/> keeps only what differs
/// from the defaults and <see cref="Render"/> writes the complete, standalone HTML of the page — the same HTML the
/// 1.8.4 previews and exports. The literal stylesheets and the engine's data come from the original
/// (PageEngineText.g.cs, csharp/tools/pages-engine-constants.mjs); proved by csharp/tools/pages-engine-oracle.mjs.
/// </summary>
public sealed partial class PageEngine
{
    public const int Version = 1;

    private sealed class Reporter(List<PageIssue>? errors, List<PageIssue>? warnings)
    {
        public void Err(string path, string message) => errors?.Add(new PageIssue(path, message));
        public void Warn(string path, string message) => warnings?.Add(new PageIssue(path, message));
    }

    private static readonly Reporter Noop = new(null, null);

    internal sealed class Block
    {
        public required string Type { get; init; }
        public required string Label { get; init; }
        public required string Group { get; init; }
        public required JsonArray Fields { get; init; }
        public string Icon { get; init; } = "";
        public string Description { get; init; } = "";
        public Func<JsonObject, RenderContext, JsonObject, string> Render { get; set; } = (_, _, _) => "";
    }

    internal static readonly JsonObject Fonts = (JsonObject)JsonNode.Parse(PageEngineText.FontsJson)!;
    internal static readonly JsonObject Themes = (JsonObject)JsonNode.Parse(PageEngineText.ThemesJson)!;

    private readonly Dictionary<string, Block> _blocks = new(StringComparer.Ordinal);
    private readonly JsonArray _themeFields, _layoutFields, _metaFields, _sectionFields;

    /// <summary>The engine with the defaults that carry a year (titlepage.year, copyright.markdown) in <paramref name="year"/>
    /// (the 1.8.4 takes new Date().getFullYear() when it loads).</summary>
    public PageEngine(int? year = null)
    {
        var y = (year ?? DateTime.Now.Year).ToString(CultureInfo.InvariantCulture);
        var blocks = PageEngineText.BlocksJson.Replace("\"" + PageEngineText.GeneratedYear + "\"", "\"" + y + "\"", StringComparison.Ordinal)
            .Replace("Copyright © " + PageEngineText.GeneratedYear, "Copyright © " + y, StringComparison.Ordinal)
            .Replace("Copyright \\u00a9 " + PageEngineText.GeneratedYear, "Copyright \\u00a9 " + y, StringComparison.Ordinal);
        foreach (var b in JsonNode.Parse(blocks)!.AsArray().OfType<JsonObject>())
        {
            var type = (string)b["type"]!;
            _blocks[type] = new Block { Type = type, Label = (string)b["label"]!, Group = (string)b["group"]!, Fields = b["fields"]!.AsArray(),
                Icon = (string?)b["icon"] ?? "", Description = (string?)b["description"] ?? "" };
        }
        _themeFields = JsonNode.Parse(PageEngineText.ThemeFieldsJson)!.AsArray();
        _layoutFields = JsonNode.Parse(PageEngineText.LayoutFieldsJson)!.AsArray();
        _metaFields = JsonNode.Parse(PageEngineText.MetaFieldsJson)!.AsArray();
        _sectionFields = JsonNode.Parse(PageEngineText.SectionFieldsJson)!.AsArray();
        RegisterBlocks();
    }

    /// <summary>The block types, in the 1.8.4's order.</summary>
    public IReadOnlyList<string> BlockTypes => [.. _blocks.Keys];

    /// <summary>isPagePath: a .page.json or .template.json file.</summary>
    public static bool IsPagePath(string? p) => Regex.IsMatch(p ?? "", @"\.(page|template)\.json\z", RegexOptions.IgnoreCase);

    // ---------------------------------------------------------------- JS values

    public static bool Truthy(JsonNode? n) => n switch
    {
        null => false,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => LegacyCustomize.JsToNumber(v) is var d && d != 0 && !double.IsNaN(d),
        _ => true
    };

    /// <summary>String(v) for a value that is there; '' for null/undefined (esc semantics).</summary>
    internal static string Str(JsonNode? n) => n is null ? "" : LegacyCustomize.JsToString(n);

    internal static string Num(double d) => LegacyCustomize.JsNumber(d);

    internal static bool IsObj(JsonNode? n) => n is JsonObject;

    private static JsonNode? Clone(JsonNode? n) => n?.DeepClone();

    private static bool IsString(JsonNode? n, string s) => n is JsonValue v && v.TryGetValue<string>(out var x) && x == s;

    private static bool IsEmptyString(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var x) && x.Length == 0;

    private static readonly Regex ColorRe = new(@"^(#[0-9a-f]{3,8}|(rgb|hsl)a?\([0-9\s.,%/+-]+\)|transparent|currentColor|[a-z]{3,20})\z", RegexOptions.IgnoreCase);

    /// <summary>COLOR_RE.</summary>
    public static bool IsColor(string v) => ColorRe.IsMatch(v);

    // ---------------------------------------------------------------- fields

    private JsonNode? Coerce(JsonObject f, JsonNode? v, string path, Reporter rep)
    {
        var t = (string)f["type"]!;
        var def = f["default"];
        if (t == "tree") return CoerceTree(v, path, rep);
        if (v is null || IsEmptyString(v))
        {
            if (t == "list") return Clone(def) ?? new JsonArray();
            if (t == "boolean") return def is not null ? Clone(def) : JsonValue.Create(false);
            if (Truthy(f["required"]) && v is null) rep.Err(path, "\"" + Str(f["label"]) + "\" é obrigatório.");
            return def is not null ? Clone(def) : t == "number" ? null : JsonValue.Create("");
        }
        switch (t)
        {
            case "boolean":
                if (v is JsonValue bv && bv.TryGetValue<bool>(out var b)) return JsonValue.Create(b);
                if (IsString(v, "true") || IsString(v, "false")) return JsonValue.Create(IsString(v, "true"));
                rep.Err(path, "deve ser true ou false.");
                return JsonValue.Create(Truthy(def));
            case "number":
            {
                var n = LegacyCustomize.JsToNumber(v);
                if (!double.IsFinite(n)) { rep.Err(path, "deve ser um número."); return Clone(def); }
                double min = f["min"] is { } mi ? mi.GetValue<double>() : -1e9, max = f["max"] is { } ma ? ma.GetValue<double>() : 1e9;
                return JsonValue.Create(Math.Max(min, Math.Min(max, n)));
            }
            case "select":
            {
                var sv = LegacyCustomize.JsToString(v);
                var options = f["options"]!.AsArray();
                var opt = options.FirstOrDefault(o => Str(o) == sv);
                if (opt is null)
                {
                    rep.Err(path, "valor \"" + sv + "\" inválido; use um de: " + string.Join(", ", options.Where(o => !IsEmptyString(o)).Select(Str)) + ".");
                    return Clone(def);
                }
                return Clone(opt);
            }
            case "color":
            {
                var cv = LegacyCustomize.JsTrim(LegacyCustomize.JsToString(v));
                if (!IsColor(cv)) { rep.Err(path, "cor inválida \"" + cv + "\" (use #rrggbb, rgb() ou hsl())."); return JsonValue.Create(""); }
                return JsonValue.Create(cv);
            }
            case "list":
            {
                if (v is not JsonArray arr) { rep.Err(path, "deve ser uma lista."); return Clone(def) ?? new JsonArray(); }
                int maxItems = f["max"] is { } mx && mx.GetValue<double>() is var md && md != 0 ? (int)md : 60;
                var output = new JsonArray();
                int k = 0;
                foreach (var item0 in arr.Take(maxItems))
                {
                    var item = item0;
                    if (item is not JsonObject) { rep.Err(path + "[" + k + "]", "cada item deve ser um objeto."); item = new JsonObject(); }
                    output.Add(Fields(f["fields"]!.AsArray(), item, path + "[" + k + "]", rep));
                    k++;
                }
                return output;
            }
        }
        if (v is JsonObject or JsonArray) { rep.Err(path, "deve ser texto."); return Truthy(def) ? Clone(def) : JsonValue.Create(""); }
        return JsonValue.Create(LegacyCustomize.JsToString(v));
    }

    private JsonObject Fields(JsonArray list, JsonNode? src0, string path, Reporter rep)
    {
        var output = new JsonObject();
        var src = src0 as JsonObject ?? new JsonObject();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in list.OfType<JsonObject>())
        {
            var key = (string)f["key"]!;
            known.Add(key);
            src.TryGetPropertyValue(key, out var value);
            output[key] = Coerce(f, value, path + "." + key, rep);
        }
        foreach (var kv in src)
            if (!known.Contains(kv.Key)) rep.Warn(path + "." + kv.Key, "propriedade desconhecida, ignorada.");
        return output;
    }

    private static int _seq;

    /// <summary>uid(p): a new section id (only for sections without a valid one).</summary>
    private static string Uid(string p)
    {
        _seq = (_seq + 1) % 1_000_000;
        var now = Base36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return p + "_" + now[^Math.Min(5, now.Length)..] + Base36(_seq) + Base36(Random.Shared.Next(46656, 1679616))[1..];
    }

    private static string Base36(long v)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (v == 0) return "0";
        var s = "";
        while (v > 0) { s = digits[(int)(v % 36)] + s; v /= 36; }
        return s;
    }

    private static readonly Regex SectionId = new(@"^[A-Za-z0-9_-]{1,40}\z");

    /// <summary>normalize(input): a JSON text or object; everything missing gets its default, everything wrong is reported.</summary>
    public PageNormalized Normalize(JsonNode? input) => Normalize(input, null);

    /// <summary>normalize(input) of a .page.json text.</summary>
    public PageNormalized Normalize(string json)
    {
        JsonNode? raw;
        try { raw = JsonNode.Parse(json); }
        catch (JsonException e) { return new PageNormalized(Blank(), [new PageIssue("(json)", "JSON inválido: " + e.Message)], []); }
        return Normalize(raw, null);
    }

    private PageNormalized Normalize(JsonNode? raw, object? _)
    {
        var errors = new List<PageIssue>();
        var warnings = new List<PageIssue>();
        var rep = new Reporter(errors, warnings);
        if (raw is not JsonObject o) return new PageNormalized(Blank(), [new PageIssue("(raiz)", "A página precisa ser um objeto JSON.")], []);
        var s = new JsonObject { ["version"] = Version, ["kind"] = "urbe-page" };
        var kind = o["kind"];
        if (Truthy(kind) && !IsString(kind, "urbe-page") && !IsString(kind, "urbe-template")) rep.Warn("kind", "esperado \"urbe-page\".");
        if (IsString(kind, "urbe-template")) s["kind"] = "urbe-template";
        if (Truthy(o["template"]) && o["template"] is JsonObject tpl)
            s["template"] = new JsonObject
            {
                ["name"] = Truthy(tpl["name"]) ? LegacyCustomize.JsToString(tpl["name"]) : "",
                ["description"] = Truthy(tpl["description"]) ? LegacyCustomize.JsToString(tpl["description"]) : ""
            };
        s["meta"] = Fields(_metaFields, o["meta"], "meta", rep);
        var th = o["theme"] as JsonObject ?? new JsonObject();
        if (Truthy(th["preset"]) && !Themes.ContainsKey(Str(th["preset"])))
        {
            rep.Err("theme.preset", "tema \"" + Str(th["preset"]) + "\" não existe; use um de: " + string.Join(", ", Themes.Select(k => k.Key)) + ".");
            var copy = (JsonObject)th.DeepClone();
            copy["preset"] = "aurora";
            th = copy;
        }
        s["theme"] = Fields(_themeFields, th, "theme", rep);
        s["layout"] = Fields(_layoutFields, o["layout"], "layout", rep);
        JsonArray secs;
        if (o["sections"] is JsonArray a) secs = a;
        else
        {
            if (o["sections"] is not null) rep.Err("sections", "deve ser uma lista de seções.");
            secs = [];
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var outSecs = new JsonArray();
        int i = 0;
        foreach (var sec0 in secs.Take(200))
        {
            var p = "sections[" + i++ + "]";
            if (sec0 is not JsonObject sec) { rep.Err(p, "cada seção deve ser um objeto {type, props}."); continue; }
            var type = sec.TryGetPropertyValue("type", out var tv) ? (tv is null ? "null" : Str(tv)) : "undefined";
            if (!_blocks.TryGetValue(type, out var b))
            {
                rep.Err(p + ".type", "tipo \"" + type + "\" não existe. Tipos: " + string.Join(", ", _blocks.Keys) + ".");
                continue;
            }
            var id = sec["id"] is JsonValue iv && iv.TryGetValue<string>(out var sid) && SectionId.IsMatch(sid) && !ids.Contains(sid) ? sid : Uid("s");
            ids.Add(id);
            foreach (var kv in sec)
                if (kv.Key is not ("id" or "type" or "props" or "style")) rep.Warn(p + "." + kv.Key, "propriedade desconhecida (use props ou style).");
            outSecs.Add(new JsonObject
            {
                ["id"] = id, ["type"] = type,
                ["props"] = Fields(b.Fields, sec["props"], p + ".props", rep),
                ["style"] = Fields(_sectionFields, sec["style"], p + ".style", rep)
            });
        }
        s["sections"] = outSecs;
        return new PageNormalized(s, errors, warnings);
    }

    private static JsonNode? DefaultOf(JsonObject f)
    {
        if (f["default"] is { } d) return d;
        return (string)f["type"]! switch { "boolean" => JsonValue.Create(false), "list" => new JsonArray(), "number" => null, _ => JsonValue.Create("") };
    }

    private static JsonObject CompactFields(JsonArray list, JsonObject obj)
    {
        var output = new JsonObject();
        foreach (var f in list.OfType<JsonObject>())
        {
            var key = (string)f["key"]!;
            if (!obj.TryGetPropertyValue(key, out var v)) continue;
            var d = DefaultOf(f);
            if ((string)f["type"]! == "tree") { output[key] = Clone(v); continue; }
            if (LegacyCustomize.Stringify(v) == LegacyCustomize.Stringify(d)) continue;
            if ((v is null || IsEmptyString(v)) && (d is null || IsEmptyString(d))) continue;
            output[key] = Clone(v);
        }
        return output;
    }

    /// <summary>compact(spec): only what differs from the defaults (normalize completes it back).</summary>
    public JsonObject Compact(JsonNode? spec)
    {
        var n = Normalize(spec).Spec;
        var o = new JsonObject { ["version"] = Version, ["kind"] = Clone(n["kind"]) };
        if (n["template"] is { } tpl) o["template"] = Clone(tpl);
        foreach (var (name, list) in new[] { ("meta", _metaFields), ("theme", _themeFields), ("layout", _layoutFields) })
        {
            var c = CompactFields(list, (JsonObject)n[name]!);
            if (c.Count > 0) o[name] = c;
        }
        var secs = new JsonArray();
        foreach (var sec in n["sections"]!.AsArray().OfType<JsonObject>())
        {
            var r = new JsonObject { ["id"] = Clone(sec["id"]), ["type"] = Clone(sec["type"]) };
            var pr = CompactFields(_blocks[(string)sec["type"]!].Fields, (JsonObject)sec["props"]!);
            var sy = CompactFields(_sectionFields, (JsonObject)sec["style"]!);
            if (pr.Count > 0) r["props"] = pr;
            if (sy.Count > 0) r["style"] = sy;
            secs.Add(r);
        }
        o["sections"] = secs;
        return o;
    }

    /// <summary>blank(): an empty page with every default.</summary>
    public JsonObject Blank() => new()
    {
        ["version"] = Version, ["kind"] = "urbe-page",
        ["meta"] = Fields(_metaFields, new JsonObject(), "", Noop),
        ["theme"] = Fields(_themeFields, new JsonObject(), "", Noop),
        ["layout"] = Fields(_layoutFields, new JsonObject(), "", Noop),
        ["sections"] = new JsonArray()
    };
}
