using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// UC-72 (DEC-0052, ADD-0029): where each note shines in the Constelação, a night sky to stroll through the notes. The
/// positions are worked out once and kept in .urbe/constelacao.json, a file derived from the notes that says so (NN-001:
/// the notes stay the authority; a note gone leaves the sky, a new one gets a place, nobody else moves). Each project
/// (top folder, or the root) is a region; its notes fill it on a sunflower spiral, one free seat after the other. Never
/// .urbe/mapa.json nor .urbe/mundo.json.
/// </summary>
public sealed class Constellation
{
    public const string Path = ".urbe/constelacao.json";
    public const int Version = 1;
    public const string Source = "Posições da Constelação (UC-72), derivadas das notas. As notas continuam a única autoridade; apagar este arquivo só faz o céu ser desenhado de novo.";

    /// <summary>The distance between two neighbouring stars of a region.</summary>
    public const double Step = 26;
    /// <summary>The distance between the centres of two regions.</summary>
    public const double RegionStep = 300;

    private static readonly double Golden = Math.PI * (3 - Math.Sqrt(5));

    private readonly Dictionary<string, (double X, double Y)> _stars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (double X, double Y, int Seats)> _regions = new(StringComparer.Ordinal);

    /// <summary>A star: the note's id and where it is.</summary>
    public sealed record Star(string Id, double X, double Y);

    /// <summary>A region: the project ("" = the root), its centre and how many seats it has given.</summary>
    public sealed record Region(string Project, double X, double Y, int Seats);

    public IReadOnlyList<Star> Stars => [.. _stars.Select(p => new Star(p.Key, p.Value.X, p.Value.Y))];

    public IReadOnlyList<Region> Regions => [.. _regions.Select(p => new Region(p.Key, p.Value.X, p.Value.Y, p.Value.Seats))];

    /// <summary>Where the note is, or null.</summary>
    public (double X, double Y)? Of(string id) => _stars.TryGetValue(id, out var p) ? p : null;

    /// <summary>
    /// The sky of these notes (id and path): the ones gone leave, the new ones take the next seat of their project's
    /// region (a new project gets the next region); the others stay where they are. True when anything changed.
    /// </summary>
    public bool Update(IEnumerable<(string Id, string Path)> notes)
    {
        var list = notes.ToList();
        var ids = list.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        bool changed = false;
        foreach (var gone in _stars.Keys.Where(id => !ids.Contains(id)).ToList()) { _stars.Remove(gone); changed = true; }
        // new notes in a stable order: by project, then path
        foreach (var n in list.Where(n => !_stars.ContainsKey(n.Id)).OrderBy(n => ProjectOf(n.Path), StringComparer.Ordinal).ThenBy(n => n.Path, StringComparer.Ordinal))
        {
            var project = ProjectOf(n.Path);
            if (!_regions.TryGetValue(project, out var region))
            {
                // the next region on a wide spiral around the middle of the sky
                int k = _regions.Count;
                double r = k == 0 ? 0 : RegionStep * Math.Sqrt(k);
                region = (Math.Round(Math.Cos(k * Golden) * r, 2), Math.Round(Math.Sin(k * Golden) * r, 2), 0);
            }
            int seat = region.Seats;
            double d = Step * Math.Sqrt(seat + .5);
            _stars[n.Id] = (Math.Round(region.X + Math.Cos(seat * Golden) * d, 2), Math.Round(region.Y + Math.Sin(seat * Golden) * d, 2));
            _regions[project] = (region.X, region.Y, seat + 1);
            changed = true;
        }
        return changed;
    }

    /// <summary>The project of a note: its top folder, or "" at the root.</summary>
    public static string ProjectOf(string path)
    {
        int slash = path.IndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    public string Serialize()
    {
        var stars = new JsonObject();
        foreach (var (id, p) in _stars.OrderBy(p => p.Key, StringComparer.Ordinal)) stars[id] = new JsonArray(p.X, p.Y);
        var regions = new JsonObject();
        foreach (var (project, r) in _regions.OrderBy(p => p.Key, StringComparer.Ordinal)) regions[project] = new JsonObject { ["x"] = r.X, ["y"] = r.Y, ["lugares"] = r.Seats };
        var root = new JsonObject { ["versao"] = Version, ["fonte"] = Source, ["regioes"] = regions, ["estrelas"] = stars };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>Reads the file; one of a newer version (or unreadable) is marked <paramref name="future"/> and never written over.</summary>
    public static Constellation Parse(string? json, out bool future)
    {
        future = false;
        var sky = new Constellation();
        if (string.IsNullOrWhiteSpace(json)) return sky;
        JsonObject root;
        try { root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { future = true; return sky; }
        if (root["versao"] is not JsonValue v || !v.TryGetValue<int>(out var version) || version < 1) { future = true; return sky; }
        future = version > Version;
        static double? Num(JsonNode? n) => n is JsonValue x && x.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : null;
        if (root["regioes"] is JsonObject regions)
            foreach (var (project, node) in regions)
                if (node is JsonObject o && Num(o["x"]) is { } x && Num(o["y"]) is { } y)
                    sky._regions[project] = (x, y, o["lugares"] is JsonValue s && s.TryGetValue<int>(out var seats) && seats >= 0 ? seats : 0);
        if (root["estrelas"] is JsonObject stars)
            foreach (var (id, node) in stars)
                if (node is JsonArray { Count: 2 } a && Num(a[0]) is { } x && Num(a[1]) is { } y) sky._stars[id] = (x, y);
        return sky;
    }

    /// <summary>True for a file this version may write over.</summary>
    public static bool IsConstellation(string json)
    {
        Parse(json, out var future);
        return !future;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{_stars.Count} estrelas em {_regions.Count} regiões");
}
