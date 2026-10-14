using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// What the native app adds to the city beyond the 1.8.4 (DEC-0045, UC-41), kept in <c>.urbe/mundo.json</c>. Never in
/// mapa.json: the 1.8.4 rebuilds that file from scratch with only the fields it knows (app.js estadoDesejado) and would
/// drop anything else the next time it saved. It holds how the neighbourhoods are drawn and the decorations placed in the
/// city; the chosen look of each building joins it later, under the same "versao".
/// <code>{ "versao": 1, "muralhas": true, "contornos": "suave", "decoracoes": [ { "id": "d1", "tipo": "poco", "x": 4, "y": 7 } ] }</code>
/// </summary>
public sealed record WorldPrefs(bool Walls = true, string Outlines = WorldPrefs.Soft)
{
    /// <summary>The decorations of the city, in the order they were placed.</summary>
    public IReadOnlyList<WorldDecoration> Decorations { get; init; } = [];

    public bool Equals(WorldPrefs? other) =>
        other is not null && Walls == other.Walls && Outlines == other.Outlines && Decorations.SequenceEqual(other.Decorations);

    public override int GetHashCode() => HashCode.Combine(Walls, Outlines, Decorations.Count);

    /// <summary>The decoration covering tile (x, y), or null.</summary>
    public WorldDecoration? DecorationAt(int x, int y) => Decorations.LastOrDefault(d =>
        WorldDecorations.Find(d.Kind) is { } k && x >= d.X && x < d.X + k.W && y >= d.Y && y < d.Y + k.H);

    /// <summary>A new id for a decoration, not used by any other.</summary>
    public string NextDecorationId()
    {
        int n = 1;
        while (Decorations.Any(d => d.Id == "d" + n)) n++;
        return "d" + n;
    }
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
            var decorations = new List<WorldDecoration>();
            if (o["decoracoes"] is JsonArray list)
                foreach (var item in list)
                    if (item is JsonObject d && d["id"] is JsonValue id && id.TryGetValue<string>(out var dId) && dId.Length > 0
                        && d["tipo"] is JsonValue t && t.TryGetValue<string>(out var kind) && WorldDecorations.Find(kind) is not null
                        && d["x"] is JsonValue jx && jx.TryGetValue<int>(out var x) && d["y"] is JsonValue jy && jy.TryGetValue<int>(out var y)
                        && decorations.TrueForAll(e => e.Id != dId))
                        decorations.Add(new WorldDecoration(dId, kind, x, y));
            return new WorldPrefs(walls, outlines) { Decorations = decorations };
        }
        catch (JsonException) { return Default; }
    }

    /// <summary>The file text (fixed key order, two-space indent, final newline).</summary>
    public string Text()
    {
        var o = new JsonObject { ["versao"] = Version, ["muralhas"] = Walls, ["contornos"] = Outlines };
        if (Decorations.Count > 0)
            o["decoracoes"] = new JsonArray(Decorations.Select(d => (JsonNode)new JsonObject { ["id"] = d.Id, ["tipo"] = d.Kind, ["x"] = d.X, ["y"] = d.Y }).ToArray());
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }
}
