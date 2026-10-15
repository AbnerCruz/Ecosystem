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

    /// <summary>engine.js schemaText(): the page format for the Assistente.</summary>
    public static string SchemaText(int? year = null) =>
        PageTemplatesText.SchemaText.Replace(MarkYear.ToString(CultureInfo.InvariantCulture), (year ?? DateTime.Now.Year).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>"aurora (Aurora, dark), …" for page_schema.</summary>
    public static string ThemesList => PageTemplatesText.ThemesList;
}
