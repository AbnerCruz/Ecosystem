using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The colour chosen for each #Tag: (DEC-0045, UC-43), kept in <c>Personalização/tags.json</c> beside tema.json. Not in
/// tema.json: the 1.8.4 rewrites that file with only the keys it knows, and would drop these. The file is plain JSON a
/// person can also edit by hand:
/// <code>{ "$schema": "urbe-tags-1", "tags": { "Todo": "#f1c46e", "Projeto": "#7fd0dc" } }</code>
/// CST-024: besides the colour, a tag can have an icon (an emoji, shown before its name), written as an object; a tag with
/// only a colour stays a string, so files written before read the same:
/// <code>{ "tags": { "Feito": { "cor": "#7fdca6", "icone": "✅" }, "Projeto": "#7fd0dc" } }</code>
/// </summary>
public static partial class TagStyles
{
    public const string FilePath = "Personalização/tags.json";
    public const string Schema = "urbe-tags-1";

    /// <summary>The tags offered first in Personalização, with the app's own colours until the person chooses others.</summary>
    public static IReadOnlyList<string> Defaults { get; } = ["Todo", "Ideia", "Feito", "Aviso", "Dúvida", "Nota"];

    /// <summary>tag → #rrggbb from the file; a missing or broken file, or a bad entry, is simply not used.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string? text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return map;
        try
        {
            if (JsonNode.Parse(text) is JsonObject o && o["tags"] is JsonObject tags)
                foreach (var (tag, value) in tags)
                    if (IsTag(tag) && ColorOf(value) is { } c) map[tag] = c.ToLowerInvariant();
        }
        catch (JsonException) { }
        return map;
    }

    private static string? ColorOf(JsonNode? value) =>
        value is JsonValue v && v.TryGetValue<string>(out var c) && IsColor(c) ? c
        : value is JsonObject o && o["cor"] is JsonValue cv && cv.TryGetValue<string>(out var oc) && IsColor(oc) ? oc : null;

    /// <summary>tag → icon from the file (CST-024); a tag without one, or with something that is not a short emoji, has none.</summary>
    public static IReadOnlyDictionary<string, string> Icons(string? text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return map;
        try
        {
            if (JsonNode.Parse(text) is JsonObject o && o["tags"] is JsonObject tags)
                foreach (var (tag, value) in tags)
                    if (IsTag(tag) && value is JsonObject entry && entry["icone"] is JsonValue iv && iv.TryGetValue<string>(out var icon) && IsIcon(icon)) map[tag] = icon;
        }
        catch (JsonException) { }
        return map;
    }

    /// <summary>An icon is one or two symbols (an emoji, with its modifiers), never text.</summary>
    public static bool IsIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon) || icon.Length > 16 || icon.Any(char.IsWhiteSpace)) return false;
        var symbols = System.Globalization.StringInfo.GetTextElementEnumerator(icon);
        int count = 0;
        while (symbols.MoveNext())
        {
            var element = (string)symbols.Current;
            if (char.IsLetterOrDigit(element, 0) && element.Length == 1) return false;
            count++;
        }
        return count is >= 1 and <= 2;
    }

    /// <summary>The file text for these colours and icons (tags in alphabetical order, two-space indent, final newline).</summary>
    public static string Text(IReadOnlyDictionary<string, string> colors, IReadOnlyDictionary<string, string>? icons = null)
    {
        icons ??= new Dictionary<string, string>();
        var tags = new JsonObject();
        foreach (var tag in colors.Keys.Concat(icons.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            if (!IsTag(tag)) continue;
            var color = colors.TryGetValue(tag, out var c) && IsColor(c) ? c.ToLowerInvariant() : null;
            var icon = icons.TryGetValue(tag, out var i) && IsIcon(i) ? i : null;
            if (icon is null) { if (color is not null) tags[tag] = color; continue; }
            var entry = new JsonObject();
            if (color is not null) entry["cor"] = color;
            entry["icone"] = icon;
            tags[tag] = entry;
        }
        var o = new JsonObject { ["$schema"] = Schema, ["tags"] = tags };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    public static bool IsTag(string? tag) => tag is not null && NoteMarks.TagOf("#" + tag + ":") == tag;

    public static bool IsColor(string? color) => color is not null && ColorRegex().IsMatch(color);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();
}
