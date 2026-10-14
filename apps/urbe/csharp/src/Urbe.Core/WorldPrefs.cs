using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// What the native app adds to the city beyond the 1.8.4 (DEC-0045, UC-41), kept in <c>.urbe/mundo.json</c>. Never in
/// mapa.json: the 1.8.4 rebuilds that file from scratch with only the fields it knows (app.js estadoDesejado) and would
/// drop anything else the next time it saved. It holds how the neighbourhoods are drawn and the decorations placed in the
/// city, and the look chosen for a building (by the path of its file, since the 1.8.4's building ids are made anew each
/// time the city opens).
/// <code>{ "versao": 1, "muralhas": true, "contornos": "suave", "decoracoes": [ { "id": "d1", "tipo": "poco", "x": 4, "y": 7 } ],
///   "construcoes": { "Projetos/Ideias.md": { "tipo": "tower", "variante": 2, "flores": true } } }</code>
/// </summary>
public sealed record WorldPrefs(bool Walls = true, string Outlines = WorldPrefs.Soft)
{
    /// <summary>The decorations of the city, in the order they were placed.</summary>
    public IReadOnlyList<WorldDecoration> Decorations { get; init; } = [];

    /// <summary>The look chosen for a building, by the vault path of its file (only what was changed from the automatic one).</summary>
    public IReadOnlyDictionary<string, BuildingLook> Looks { get; init; } = new Dictionary<string, BuildingLook>(StringComparer.Ordinal);

    public bool Equals(WorldPrefs? other) =>
        other is not null && Walls == other.Walls && Outlines == other.Outlines && Decorations.SequenceEqual(other.Decorations)
        && Looks.Count == other.Looks.Count && Looks.All(l => other.Looks.TryGetValue(l.Key, out var o) && o == l.Value);

    public override int GetHashCode() => HashCode.Combine(Walls, Outlines, Decorations.Count, Looks.Count);

    /// <summary>These preferences with the look of <paramref name="path"/> set (or, when it is automatic, removed).</summary>
    public WorldPrefs WithLook(string path, BuildingLook look)
    {
        var looks = new Dictionary<string, BuildingLook>(Looks, StringComparer.Ordinal);
        if (look.IsAutomatic) looks.Remove(path); else looks[path] = look;
        return this with { Looks = looks };
    }

    /// <summary>These preferences with the looks of moved files following them (old path → new path); null when none moved.</summary>
    public WorldPrefs? WithMoved(IReadOnlyDictionary<string, string> moves)
    {
        if (!moves.Keys.Any(Looks.ContainsKey)) return null;
        var looks = new Dictionary<string, BuildingLook>(StringComparer.Ordinal);
        foreach (var (path, look) in Looks) looks[moves.TryGetValue(path, out var to) ? to : path] = look;
        return this with { Looks = looks };
    }

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
            var looks = new Dictionary<string, BuildingLook>(StringComparer.Ordinal);
            if (o["construcoes"] is JsonObject buildings)
                foreach (var (path, value) in buildings)
                    if (path.Length > 0 && value is JsonObject l)
                    {
                        var look = new BuildingLook(
                            l["tipo"] is JsonValue lk && lk.TryGetValue<string>(out var k) && LegacyWorldBuildings.BuildingKinds.Contains(k) ? k : null,
                            l["variante"] is JsonValue lv && lv.TryGetValue<int>(out var n) && n is >= 0 and < BuildingLook.Variants ? n : null,
                            l["flores"] is JsonValue lf && lf.TryGetValue<bool>(out var f) ? f : null);
                        if (!look.IsAutomatic) looks[path] = look;
                    }
            return new WorldPrefs(walls, outlines) { Decorations = decorations, Looks = looks };
        }
        catch (JsonException) { return Default; }
    }

    /// <summary>The file text (fixed key order, two-space indent, final newline).</summary>
    public string Text()
    {
        var o = new JsonObject { ["versao"] = Version, ["muralhas"] = Walls, ["contornos"] = Outlines };
        if (Decorations.Count > 0)
            o["decoracoes"] = new JsonArray(Decorations.Select(d => (JsonNode)new JsonObject { ["id"] = d.Id, ["tipo"] = d.Kind, ["x"] = d.X, ["y"] = d.Y }).ToArray());
        if (Looks.Count > 0)
        {
            var buildings = new JsonObject();
            foreach (var (path, look) in Looks.OrderBy(l => l.Key, StringComparer.Ordinal))
            {
                var l = new JsonObject();
                if (look.Kind is { } k) l["tipo"] = k;
                if (look.Variant is { } v) l["variante"] = v;
                if (look.Flowers is { } f) l["flores"] = f;
                buildings[path] = l;
            }
            o["construcoes"] = buildings;
        }
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }
}

/// <summary>
/// The look chosen for one building (DEC-0045, UC-41: "ao clicar em uma construção nós podemos editar o visual dela"): its
/// trade among the 1.8.4's sprites (<see cref="LegacyWorldBuildings.BuildingKinds"/>), its variant and its flowers. What is
/// null stays as the 1.8.4 chooses it (the trade by extension, the variant and flowers by name).
/// </summary>
public sealed record BuildingLook(string? Kind = null, int? Variant = null, bool? Flowers = null)
{
    /// <summary>The variants each sprite has.</summary>
    public const int Variants = 3;

    public bool IsAutomatic => Kind is null && Variant is null && Flowers is null;
}
