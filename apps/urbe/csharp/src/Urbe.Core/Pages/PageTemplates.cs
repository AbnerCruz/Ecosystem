using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Pages;

/// <summary>A built-in page template (templates.js LIST): its id, name, icon, description and what it needs.</summary>
public sealed record PageTemplateInfo(string Id, string Name, string Icon, string Description, string? Needs);

/// <summary>
/// The built-in page templates of the 1.8.4 (src/pages/templates.js), generated from the original
/// (csharp/tools/pages-templates-constants.mjs) with marked values that are filled here with the title, folder, note
/// and date; proved by csharp/tools/pages-templates-oracle.mjs. Also the page format reference (schemaText).
/// </summary>
public static class PageTemplates
{
    private const string T = "⟦T⟧", D = "⟦D⟧", L = "⟦L⟧", NT = "⟦NT⟧", NP = "⟦NP⟧";
    private const int MarkYear = 7000;

    private sealed record Entry(PageTemplateInfo Info, Dictionary<string, string> Variants);

    private static readonly Lazy<List<Entry>> All = new(() => JsonNode.Parse(PageTemplatesText.TemplatesJson)!.AsArray().OfType<JsonObject>().Select(t =>
        new Entry(new PageTemplateInfo((string)t["id"]!, (string)t["name"]!, (string)t["icon"]!, (string)t["description"]!, (string?)t["needs"]),
            ((JsonObject)t["variants"]!).ToDictionary(v => v.Key, v => (string)v.Value!))).ToList());

    public static IReadOnlyList<PageTemplateInfo> List() => All.Value.Select(e => e.Info).ToList();

    public static PageTemplateInfo? Get(string? id) => All.Value.FirstOrDefault(e => e.Info.Id == id)?.Info;

    private static string Json(string value) => JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();

    private static string Dates(string text, DateTime now)
    {
        var inv = CultureInfo.InvariantCulture;
        var mark = new DateTime(MarkYear, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        // event: Date.now()+30 days as aaaa-mm-dd (and dd/mm/aaaa); article: today as dd/mm/aaaa; then the years
        var in30 = now.AddDays(30);
        text = text.Replace(mark.AddDays(30).ToString("yyyy-MM-dd", inv), in30.ToString("yyyy-MM-dd", inv), StringComparison.Ordinal)
            .Replace(mark.AddDays(30).ToString("dd/MM/yyyy", inv), in30.ToString("dd/MM/yyyy", inv), StringComparison.Ordinal)
            .Replace(mark.ToString("dd/MM/yyyy", inv), now.ToString("dd/MM/yyyy", inv), StringComparison.Ordinal);
        foreach (var back in new[] { 0, 2, 3, 5, 6, 8 })
            text = text.Replace((MarkYear - back).ToString(inv), (now.Year - back).ToString(inv), StringComparison.Ordinal);
        return text;
    }

    /// <summary>build(id, {title, folder, note}) as pages/ai-tools.js calls it, normalized; null for an unknown id.</summary>
    public static JsonObject? Build(PageEngine engine, string id, string? title, string? folder, (string Title, string Path)? note, DateTime? now = null)
    {
        var e = All.Value.FirstOrDefault(x => x.Info.Id == id);
        if (e is null) return null;
        bool hasTitle = !string.IsNullOrEmpty(title) || note is not null, hasFolder = !string.IsNullOrEmpty(folder) && e.Info.Needs == "folder", hasNote = note is not null && e.Info.Needs == "note";
        // with a note and no title, the note's title is the title (c.title = c.title || d.title)
        var t = !string.IsNullOrEmpty(title) ? title : note?.Title ?? "";
        if (!e.Variants.TryGetValue("t" + (hasTitle ? 1 : 0) + "f" + (hasFolder ? 1 : 0) + "n" + (hasNote ? 1 : 0), out var json))
            json = e.Variants["t" + (hasTitle ? 1 : 0) + "f0n0"];
        var f = folder ?? "";
        var last = f.Contains('/') ? f[(f.LastIndexOf('/') + 1)..] : f;
        json = json.Replace(Json(D + "/" + L), Json(f), StringComparison.Ordinal).Replace(Json(L), Json(last), StringComparison.Ordinal)
            .Replace(Json(NT), Json(note?.Title ?? ""), StringComparison.Ordinal).Replace(Json(NP), Json(note?.Path ?? ""), StringComparison.Ordinal)
            .Replace(Json(T), Json(t), StringComparison.Ordinal);
        json = Dates(json, now ?? DateTime.Now);
        return engine.Normalize(JsonNode.Parse(json)).Spec;
    }

    private static readonly string[] OptionalInBook = ["copyright", "dedication", "about", "colophon", "part"];

    // limparCampos: the same blocks without the sample texts (a title becomes its field's label)
    private static JsonObject Clear(JsonArray fields, JsonObject props)
    {
        var o = new JsonObject();
        foreach (var f in fields.OfType<JsonObject>())
        {
            var key = (string)f["key"]!;
            var type = (string)f["type"]!;
            props.TryGetPropertyValue(key, out var v);
            switch (type)
            {
                case "list":
                    o[key] = v is JsonArray { Count: > 0 } arr && arr[0] is JsonObject first ? new JsonArray(Clear(f["fields"]!.AsArray(), first)) : new JsonArray();
                    break;
                case "text":
                    if (key == "title") o[key] = (string?)f["label"];
                    else if (key is "label" or "value" or "icon") { if (props.ContainsKey(key)) o[key] = v?.DeepClone(); }
                    else o[key] = "";
                    break;
                case "markdown" or "textarea":
                    o[key] = key is "markdown" or "subtitle" or "text" ? "Escreva aqui." : "";
                    break;
                case "image" or "url":
                    o[key] = "";
                    break;
                default:
                    if (props.ContainsKey(key)) o[key] = v?.DeepClone();
                    break;
            }
        }
        return o;
    }

    // encurtar: lists cut to 2 items (buttons to 1)
    private static JsonObject Shorten(JsonArray fields, JsonObject props)
    {
        var o = new JsonObject();
        foreach (var f in fields.OfType<JsonObject>())
        {
            var key = (string)f["key"]!;
            if (!props.TryGetPropertyValue(key, out var v)) continue;
            if ((string?)f["type"] == "list" && v is JsonArray arr)
                o[key] = new JsonArray(arr.Take(key == "buttons" ? 1 : 2).Select(it => (JsonNode)(it is JsonObject io ? Shorten(f["fields"]!.AsArray(), io) : it?.DeepClone()!)).ToArray());
            else o[key] = v?.DeepClone();
        }
        return o;
    }

    /// <summary>
    /// variant(spec, mode) of templates.js: "full" as it is, "simple" (the main parts, short lists, one button) or
    /// "skeleton" (the same blocks and look without the sample texts), normalized.
    /// </summary>
    public static JsonObject Variant(PageEngine engine, JsonObject spec, string? mode)
    {
        if (string.IsNullOrEmpty(mode) || mode == "full") return spec;
        var s = (JsonObject)spec.DeepClone();
        var sections = s["sections"] as JsonArray ?? [];
        if (mode == "simple")
        {
            bool book = s["layout"] is JsonObject l && (string?)l["format"] == "book";
            var kept = (book ? sections.OfType<JsonObject>().Where(x => !OptionalInBook.Contains((string?)x["type"])).Take(6) : sections.OfType<JsonObject>().Take(4))
                .Select(x => (JsonObject)x.DeepClone()).ToList();
            foreach (var x in kept)
                if (engine.BlockInfo((string?)x["type"]) is { } b && x["props"] is JsonObject props) x["props"] = Shorten(b.Fields, props);
            s["sections"] = new JsonArray(kept.ToArray<JsonNode>());
            if (s["layout"] is JsonObject layout) { layout["navCta"] = ""; layout["navCtaUrl"] = ""; layout["progress"] = false; }
        }
        else if (mode == "skeleton")
            foreach (var x in sections.OfType<JsonObject>())
                if (engine.BlockInfo((string?)x["type"]) is { } b && x["props"] is JsonObject props) x["props"] = Clear(b.Fields, props);
        return engine.Normalize(s).Spec;
    }

    /// <summary>engine.js schemaText(): the page format for the Assistente.</summary>
    public static string SchemaText(int? year = null) =>
        PageTemplatesText.SchemaText.Replace(MarkYear.ToString(CultureInfo.InvariantCulture), (year ?? DateTime.Now.Year).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>"aurora (Aurora, dark), …" for page_schema.</summary>
    public static string ThemesList => PageTemplatesText.ThemesList;
}
