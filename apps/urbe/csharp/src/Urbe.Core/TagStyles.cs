using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The colour chosen for each #Tag: (DEC-0045, UC-43), kept in <c>Personalização/tags.json</c> beside tema.json. Not in
/// tema.json: the 1.8.4 rewrites that file with only the keys it knows, and would drop these. The file is plain JSON a
/// person can also edit by hand:
/// <code>{ "$schema": "urbe-tags-1", "tags": { "Todo": "#f1c46e", "Projeto": "#7fd0dc" } }</code>
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
                    if (IsTag(tag) && value is JsonValue v && v.TryGetValue<string>(out var c) && IsColor(c)) map[tag] = c.ToLowerInvariant();
        }
        catch (JsonException) { }
        return map;
    }

    /// <summary>The file text for these colours (tags in alphabetical order, two-space indent, final newline).</summary>
    public static string Text(IReadOnlyDictionary<string, string> colors)
    {
        var tags = new JsonObject();
        foreach (var (tag, color) in colors.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (IsTag(tag) && IsColor(color)) tags[tag] = color.ToLowerInvariant();
        var o = new JsonObject { ["$schema"] = Schema, ["tags"] = tags };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    public static bool IsTag(string? tag) => tag is not null && NoteMarks.TagOf("#" + tag + ":") == tag;

    public static bool IsColor(string? color) => color is not null && ColorRegex().IsMatch(color);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();
}
