using System.Text.Json.Nodes;

namespace Urbe.Core.Pages;

/// <summary>A block of the engine (BLOCKS[type]): its label, icon, group, description and the fields of its props.</summary>
public sealed record PageBlockInfo(string Type, string Label, string Icon, string Group, string Description, JsonArray Fields);

/// <summary>A ready theme (THEMES[key]): its name, its default mode and the palette of that mode.</summary>
public sealed record PageThemeInfo(string Key, string Label, string Mode, JsonObject Palette);

/// <summary>An element type of the free layout (free.js TYPES): label, icon, whether it holds children or text, its content fields.</summary>
public sealed record FreeElementInfo(string Type, string Label, string Icon, bool Box, bool Text, JsonArray Content);

/// <summary>A ready composition of the free layout (free.js PRESETS).</summary>
public sealed record FreePresetInfo(string Key, string Label, string Icon);

/// <summary>
/// What the page studio (src/pages/studio.js) reads from the engine and free.js: the blocks and their fields, the
/// theme/layout/meta/section fields, the ready themes, newSection, and the free layout's element types, style
/// fields, groups and ready compositions, with make/find/walk/clone.
/// </summary>
public sealed partial class PageEngine
{
    public IReadOnlyList<PageBlockInfo> Blocks => _blocks.Values.Select(b => new PageBlockInfo(b.Type, b.Label, b.Icon, b.Group, b.Description, b.Fields)).ToList();

    public PageBlockInfo? BlockInfo(string? type) =>
        type is not null && _blocks.TryGetValue(type, out var b) ? new PageBlockInfo(b.Type, b.Label, b.Icon, b.Group, b.Description, b.Fields) : null;

    public JsonArray ThemeFields => _themeFields;
    public JsonArray LayoutFields => _layoutFields;
    public JsonArray MetaFields => _metaFields;
    public JsonArray SectionFields => _sectionFields;

    public static IReadOnlyList<PageThemeInfo> ThemeList => Themes.Select(t =>
    {
        var o = (JsonObject)t.Value!;
        var mode = (string?)o["mode"] ?? "dark";
        return new PageThemeInfo(t.Key, (string?)o["label"] ?? t.Key, mode, o[mode] as JsonObject ?? []);
    }).ToList();

    /// <summary>newSection(type, props): a new section with a new id, the props filled with the block's defaults.</summary>
    public JsonObject NewSection(string type, JsonObject? props = null)
    {
        if (!_blocks.TryGetValue(type, out var b)) throw new ArgumentException("Bloco desconhecido: " + type);
        return new JsonObject
        {
            ["id"] = Uid("s"), ["type"] = type,
            ["props"] = Fields(b.Fields, props ?? [], "", Noop),
            ["style"] = Fields(_sectionFields, type == "hero" ? new JsonObject { ["padding"] = "l" } : [], "", Noop)
        };
    }

    /// <summary>uid('s'): a new section id.</summary>
    public static string NewSectionId() => Uid("s");

    // ---------------------------------------------------------------- free layout (free.js)

    private static readonly Lazy<List<FreeElementInfo>> FreeInfo = new(() => JsonNode.Parse(PageEngineText.FreeTypesJson)!.AsArray().OfType<JsonObject>()
        .Select(t => new FreeElementInfo((string)t["type"]!, (string)t["label"]!, (string?)t["icon"] ?? "", (bool)t["box"]!, t["text"] is JsonValue v && v.GetValue<bool>(), t["content"]!.AsArray())).ToList());

    private static readonly Lazy<List<(FreePresetInfo Info, JsonObject Node)>> Presets = new(() => JsonNode.Parse(PageEngineText.FreePresetsJson)!.AsArray().OfType<JsonObject>()
        .Select(p => (new FreePresetInfo((string)p["key"]!, (string)p["label"]!, (string)p["icon"]!), (JsonObject)p["node"]!)).ToList());

    public static IReadOnlyList<FreeElementInfo> FreeElements => FreeInfo.Value;
    public static FreeElementInfo? FreeElement(string? type) => FreeInfo.Value.FirstOrDefault(t => t.Type == type);

    /// <summary>free.js STYLE: every style field of an element (key, label, type, group…).</summary>
    public static JsonArray FreeStyleFields { get; } = JsonNode.Parse(PageEngineText.FreeStyleJson)!.AsArray();

    /// <summary>free.js GROUPS: [key, title] of the style groups.</summary>
    public static IReadOnlyList<(string Key, string Title)> FreeGroups { get; } = JsonNode.Parse(PageEngineText.FreeGroupsJson)!.AsArray()
        .Select(g => ((string)g![0]!, (string)g[1]!)).ToList();

    public static IReadOnlyList<FreePresetInfo> FreePresets => Presets.Value.Select(p => p.Info).ToList();

    /// <summary>free.js CONVERTIBLE: the blocks that "Converter em layout livre" accepts.</summary>
    public static IReadOnlyList<string> FreeConvertible { get; } = JsonNode.Parse(PageEngineText.FreeConvertibleJson)!.AsArray().Select(x => (string)x!).ToList();

    /// <summary>PRESETS[key].build(): the composition with new ids.</summary>
    public static JsonObject? BuildPreset(string key) => Presets.Value.FirstOrDefault(p => p.Info.Key == key).Node is { } n ? FreeClone(n) : null;

    /// <summary>make(type, content, style, children).</summary>
    public static JsonObject MakeFree(string type, JsonObject? content = null, JsonObject? style = null, JsonArray? children = null) => Make(type, content, style, children);

    /// <summary>starter(): what a new free layout begins with.</summary>
    public static JsonObject FreeStarter() => Starter();

    /// <summary>walk(n, fn): every element, depth first; fn returns false to stop.</summary>
    public static bool FreeWalk(JsonObject n, Func<JsonObject, JsonObject?, int, bool> fn, JsonObject? parent = null, int depth = 0)
    {
        if (!fn(n, parent, depth)) return false;
        if (n["children"] is JsonArray kids)
            foreach (var k in kids.OfType<JsonObject>().ToList())
                if (!FreeWalk(k, fn, n, depth + 1)) return false;
        return true;
    }

    /// <summary>find(root, id): the element and its parent.</summary>
    public static (JsonObject Node, JsonObject? Parent)? FreeFind(JsonObject? root, string? id)
    {
        if (root is null || id is null) return null;
        (JsonObject, JsonObject?)? hit = null;
        FreeWalk(root, (n, p, _) =>
        {
            if ((string?)n["id"] != id) return true;
            hit = (n, p);
            return false;
        });
        return hit;
    }

    /// <summary>clone(n): a deep copy with new ids.</summary>
    public static JsonObject FreeClone(JsonObject n)
    {
        var copy = (JsonObject)n.DeepClone();
        FreeWalk(copy, (k, _, _) => { k["id"] = Nid(); return true; });
        return copy;
    }
}
