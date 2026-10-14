using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// What the native app adds to the city beyond the 1.8.4 (DEC-0045, UC-41), kept in <c>.urbe/mundo.json</c>. Never in
/// mapa.json: the 1.8.4 rebuilds that file from scratch with only the fields it knows (app.js estadoDesejado) and would
/// drop anything else the next time it saved. This first version holds how the neighbourhoods are drawn; decorations and
/// the chosen look of each building join it later, under the same "versao".
/// <code>{ "versao": 1, "muralhas": true, "contornos": "suave" }</code>
/// </summary>
public sealed record WorldPrefs(bool Walls = true, string Outlines = WorldPrefs.Soft)
{
    public const string FilePath = ".urbe/mundo.json";
    public const int Version = 1;
    /// <summary>Sub-neighbourhoods with a faint outline (the default: the owner found the 1.8.4's too strong).</summary>
    public const string Soft = "suave";
    /// <summary>The outlines as the 1.8.4 drew them.</summary>
    public const string Strong = "forte";
    /// <summary>No outline on sub-neighbourhoods.</summary>
    public const string Hidden = "oculto";

    public static WorldPrefs Default { get; } = new();

    /// <summary>The preferences in the file; a missing, broken or newer file gives the defaults (a newer one is not written over).</summary>
    public static WorldPrefs Parse(string? text, out bool newer)
    {
        newer = false;
        if (string.IsNullOrWhiteSpace(text)) return Default;
        try
        {
            if (JsonNode.Parse(text) is not JsonObject o) return Default;
            if (o["versao"] is JsonValue v && v.TryGetValue<int>(out var version) && version > Version) { newer = true; return Default; }
            bool walls = o["muralhas"] is JsonValue w && w.TryGetValue<bool>(out var b) ? b : Default.Walls;
            var outlines = o["contornos"] is JsonValue c && c.TryGetValue<string>(out var s) && s is Soft or Strong or Hidden ? s : Default.Outlines;
            return new WorldPrefs(walls, outlines);
        }
        catch (JsonException) { return Default; }
    }

    /// <summary>The file text (fixed key order, two-space indent, final newline).</summary>
    public string Text() =>
        new JsonObject { ["versao"] = Version, ["muralhas"] = Walls, ["contornos"] = Outlines }
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
}
