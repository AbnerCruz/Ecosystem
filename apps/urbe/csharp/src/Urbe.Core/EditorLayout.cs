using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// UC-61 (16/10/2026: "barra do editor e menu / configuráveis — quais botões e em que ordem"): Personalização/editor.json,
/// {"versao": 1, "barra": ["bold", "italic", "|", "h1"], "menu": ["h1", "list", "task"]}. Unknown ids are left out; a list
/// missing (or a file that cannot be read) keeps the default. Made only when the person asks for it.
/// </summary>
public static class EditorLayout
{
    public const string Path = "Personalização/editor.json";

    /// <summary>The bar and the "/" menu as the file asks (null: the default), keeping only the ids that exist.</summary>
    public static (IReadOnlyList<string>? Bar, IReadOnlyList<string>? Menu) Parse(string? json, IReadOnlyCollection<string> barIds, IReadOnlyCollection<string> menuIds)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null);
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root) return (null, null);
            static List<string>? List(JsonNode? node, IReadOnlyCollection<string> known, bool separators) =>
                node is JsonArray a
                    ? [.. a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null).OfType<string>()
                        .Where(s => known.Contains(s) || (separators && s == "|"))]
                    : null;
            var bar = List(root["barra"], barIds, true);
            var menu = List(root["menu"], menuIds, false)?.Distinct().ToList();
            // a bar without buttons, or separators side by side, are tidied
            if (bar is not null)
            {
                var tidy = new List<string>();
                foreach (var id in bar)
                    if (id == "|" ? tidy.Count > 0 && tidy[^1] != "|" : !tidy.Contains(id)) tidy.Add(id);
                if (tidy.Count > 0 && tidy[^1] == "|") tidy.RemoveAt(tidy.Count - 1);
                bar = tidy;
            }
            return (bar, menu);
        }
        catch (JsonException) { return (null, null); }
    }

    /// <summary>The file's model: the default bar and menu, written out so they can be reordered or cut.</summary>
    public static string Template(IReadOnlyList<string> bar, IReadOnlyList<string> menu)
    {
        var root = new JsonObject
        {
            ["versao"] = 1,
            ["barra"] = new JsonArray([.. bar.Select(x => (JsonNode)x)]),
            ["menu"] = new JsonArray([.. menu.Select(x => (JsonNode)x)])
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>
    /// The file with the bar replaced (CST-019, from Personalização → Editor): the "/" menu and anything else in it stay;
    /// a null bar removes "barra", so the default comes back. A file that is not valid JSON is started again.
    /// </summary>
    public static string WithBar(string? json, IReadOnlyList<string>? bar)
    {
        JsonObject root;
        try { root = !string.IsNullOrWhiteSpace(json) && JsonNode.Parse(json) is JsonObject o ? o : new JsonObject { ["versao"] = 1 }; }
        catch (JsonException) { root = new JsonObject { ["versao"] = 1 }; }
        if (bar is null) root.Remove("barra");
        else root["barra"] = new JsonArray([.. bar.Select(x => (JsonNode)x)]);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>The "/" menu in the person's order (only those items), or as it is.</summary>
    public static IReadOnlyList<LegacyEditorTools.SlashItem> Arrange(IReadOnlyList<LegacyEditorTools.SlashItem> items, IReadOnlyList<string>? menu)
    {
        if (menu is null) return items;
        var rank = menu.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return [.. items.Where(x => rank.ContainsKey(x.Act)).OrderBy(x => rank[x.Act])];
    }
}
