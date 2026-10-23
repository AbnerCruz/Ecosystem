using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>
/// UC-62 slice 1 (DEC-0055, PAGES-PLAN.md): the "Notas" section, which already lists notes by folder, tag, choice or
/// recency (1.8.4), also lists them by the vault's links — the notes that link to a note, the notes a note links to —
/// and by a text search, and a folder may leave its subfolders out. The page keeps only the query; the list is worked
/// out at each render, so it follows the notes (NN-001). On only with <see cref="UrbeMarks"/>: the oracles' pages stay
/// the 1.8.4's.
/// </summary>
public sealed partial class PageEngine
{
    public static readonly string[] VaultSources = ["links", "linked", "search"];

    /// <summary>UC-62 slice 4: a text the preview lets one write in place (one line, no Markdown, no marks).</summary>
    public static bool PlainText(string text) => PageEditMarks.Plain(text);

    private static readonly Regex WikiLinkRe = new(@"\[\[([^\]\|#\n]+)", RegexOptions.Compiled);

    /// <summary>The "Notas" section's fields with the vault's queries (once).</summary>
    private void AddVaultQueries()
    {
        if (!_blocks.TryGetValue("notes", out var notes)) return;
        var fields = notes.Fields;
        if (fields.OfType<JsonObject>().Any(f => (string?)f["key"] == "query")) return;
        if (fields.OfType<JsonObject>().FirstOrDefault(f => (string?)f["key"] == "source") is { } source &&
            source["options"] is JsonArray options && source["labels"] is JsonArray labels)
        {
            // JsonValue.Create, not Add<string>: no reflection (the Android build has none)
            foreach (var (option, label) in new[] { ("links", "Ligam a uma nota"), ("linked", "Ligadas por uma nota"), ("search", "Busca") })
            {
                options.Add((JsonNode?)JsonValue.Create(option));
                labels.Add((JsonNode?)JsonValue.Create(label));
            }
        }
        int at = fields.Select((f, i) => ((string?)f?["key"], i)).FirstOrDefault(x => x.Item1 == "paths").i + 1;
        fields.Insert(at, new JsonObject { ["key"] = "subfolders", ["label"] = "Incluir subpastas", ["type"] = "boolean", ["default"] = true });
        fields.Insert(at + 1, new JsonObject { ["key"] = "note", ["label"] = "Nota das ligações", ["type"] = "note" });
        fields.Insert(at + 2, new JsonObject { ["key"] = "query", ["label"] = "Buscar", ["type"] = "text" });
    }

    /// <summary>The wikilink targets of a text, in order, once each.</summary>
    public static IReadOnlyList<string> WikiTargets(string? text) =>
        [.. WikiLinkRe.Matches(text ?? "").Select(m => m.Groups[1].Value.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The notes of a links or search query: its note's backlinks, its links, or the notes with the text.</summary>
    private static List<PageNote> VaultQuery(JsonObject p, List<PageNote> all, Func<string?, PageNote?> noteOf)
    {
        var source = S(p, "source");
        if (source == "search")
        {
            var q = JsText.Lower(S(p, "query").Trim());
            return q.Length == 0 ? [] : [.. all.Where(d => JsText.Lower(d.Title).Contains(q, StringComparison.Ordinal) || JsText.Lower(d.Content).Contains(q, StringComparison.Ordinal))];
        }
        if (noteOf(S(p, "note")) is not { } target) return [];
        if (source == "linked")
            return [.. WikiTargets(target.Content).Select(noteOf).OfType<PageNote>().Where(d => d.Path != target.Path).DistinctBy(d => d.Path)];
        // the links of every note are resolved through one index of titles and paths, not one search per link
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var d in all)
        {
            byName.TryAdd(JsText.Lower(d.Path), d.Path);
            byName.TryAdd(JsText.Lower(MdExt.Replace(d.Path, "")), d.Path);
            byName.TryAdd(JsText.Lower(d.Title), d.Path);
        }
        bool LinksToTarget(PageNote d) => WikiTargets(d.Content).Any(t => byName.TryGetValue(JsText.Lower(t), out var path) && path == target.Path);
        return [.. all.Where(d => d.Path != target.Path && LinksToTarget(d))];
    }
}
