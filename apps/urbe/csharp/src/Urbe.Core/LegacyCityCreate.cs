using System.Text.Json.Nodes;
using System.Globalization;

namespace Urbe.Core;

/// <summary>A region made by drawing it, with the way back (its tiles returned to the neighbourhoods that ceded them).</summary>
public sealed record LegacyDrawnRegion(LegacyCityRegion Region, Action Undo);

/// <summary>The terrain a freehand rectangle selected: the tiles v20MascaraPrioritaria keeps.</summary>
public sealed record LegacyRegionMask(IReadOnlyList<(int X, int Y)> Cells, int Dry, int Total, (int X, int Y, int W, int H) Bounds);

/// <summary>
/// The "Criar" (+) menu rules of the 1.8.4 app.js: criarPastaNoDestino (v21), the drawn region
/// (v20MascaraPrioritaria / v20CederTerritorio / confirmRegion), the placed building
/// (v21ConfirmPlacement). Pure city logic; the client only asks, writes the vault and draws.
/// </summary>
public sealed partial class LegacyCity
{
    /// <summary>v21UniqueRegionName: the typed name, with " (2)", " (3)"… when a neighbourhood has it (case-insensitive).</summary>
    public string UniqueRegionName(string? raw)
    {
        var baseName = (raw ?? "").Trim();
        if (baseName.Length == 0) baseName = "Região";
        var taken = new HashSet<string>(Regions.Select(r => (r.Name ?? "").ToLowerInvariant()), StringComparer.Ordinal);
        if (!taken.Contains(baseName.ToLowerInvariant())) return baseName;
        int n = 2;
        var name = baseName + " (" + n + ")";
        while (taken.Contains(name.ToLowerInvariant())) name = baseName + " (" + ++n + ")";
        return name;
    }

    /// <summary>
    /// criarPastaNoDestino (final v21 definition): the typed name trimmed (empty = nothing), a
    /// unique neighbourhood of quantity 1 seeded by name + Date.now inside the parent (or loose).
    /// Null when there is no free ground ("Não encontrei área livre.").
    /// </summary>
    public LegacyCityRegion? CreateFolder(string? typed, string? parentId, long nowMs)
    {
        var name = (typed ?? "").Trim();
        if (name.Length == 0) return null;
        var parent = parentId is not null ? Regions.Find(r => r.Id == parentId) : null;
        var seed = LegacyJsMath.Seed(name + nowMs.ToString(CultureInfo.InvariantCulture));
        return CreateRegion(UniqueRegionName(name), 1, seed, parent?.Id, null);
    }

    /// <summary>
    /// validarPosicaoMovel(b, x, y): the lot under a carried building. Inside a neighbourhood (the one under the
    /// lot's centre, regiaoDestinoLote) it must be a valid lot of it; outside, no tile may touch a neighbourhood. The
    /// building itself is ignored. Returns the neighbourhood it would land in (null = the root).
    /// </summary>
    public (bool Ok, LegacyCityRegion? Region) ValidMove(LegacyCityBuilding b, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(b);
        var ignore = new HashSet<string>(StringComparer.Ordinal) { b.Id };
        var r = RegionAt(x + b.W / 2, y + b.H / 2);
        if (r is not null) return (LotIsValid(x, y, ignore, r), r);
        for (int yy = y; yy < y + b.H; yy++)
            for (int xx = x; xx < x + b.W; xx++)
                if (RegionAt(xx, yy) is not null) return (false, null);
        return (LotIsValid(x, y, ignore, null), null);
    }

    /// <summary>finalizarMovimentoMundo: the building at its new lot (b.x, b.y, modified = nowDate) and indexar.</summary>
    public void MoveBuildingTo(LegacyCityBuilding b, int x, int y, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.X = x;
        b.Y = y;
        b.Modified = JsonValue.Create(NowDate(nowMs));
        Reindex();
    }

    /// <summary>canHouse: a 3×3 lot is valid at the tile, inside the innermost neighbourhood there.</summary>
    public bool CanHouse(int x, int y) => LotIsValid(x, y, null, RegionAt(x, y));

    /// <summary>
    /// v21ConfirmPlacement (new building): the note typed in "Configurar arquivo" built exactly
    /// at the chosen tile, in the neighbourhood under it. Null when the lot is not valid.
    /// </summary>
    public (LegacyCityBuilding House, string Content)? CreateNoteAt(int x, int y, string typedName) =>
        CreateNoteAt(x, y, typedName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>v21ConfirmPlacement (new building) at <paramref name="nowMs"/>: tags:[], created/modified = nowDate(),
    /// aiLocal:{instructions:'',memory:'',artifacts:[]}, as the 1.8.4 builds it.</summary>
    public (LegacyCityBuilding House, string Content)? CreateNoteAt(int x, int y, string typedName, long nowMs)
    {
        if (!CanHouse(x, y)) return null;
        var region = RegionAt(x, y);
        var full = UniqueNoteName(typedName, region?.Id);
        var (name, ext) = SplitNoteName(full);
        var folder = region is not null ? RegionPaths().GetValueOrDefault(region.Id, region.Name) : "";
        var path = (folder.Length > 0 ? folder + "/" : "") + name + ext;
        var b = new LegacyCityBuilding(_newId("b"), path, name, region?.Id, x, y, "house" + (Buildings.Count % 3 + 1));
        var today = NowDate(nowMs);
        b.Tags = new JsonArray();
        b.Created = JsonValue.Create(today);
        b.Modified = JsonValue.Create(today);
        b.AiLocal = DefaultAiLocal();
        Buildings.Add(b);
        Reindex();
        return (b, ext is ".md" or ".markdown" ? "# " : "");
    }

    /// <summary>
    /// "Copiar" of a note house at a tile. The 1.8.4 (v21ConfirmPlacement 'copy') clones the house onto the SAME document
    /// and writes no copy (csharp/tools/legacy-copy-oracle.mjs); by the owner's decision (ROADMAP UC-19, 12/10/2026)
    /// the native copy is a real one: the clone's name follows the 1.8.4 (the name typed in "Configurar cópia", the
    /// source's extension when it has none, v21UniqueName in the neighbourhood under the tile, separarNomeExtNota),
    /// with the source's sprite, tags and aiLocal, created/modified = nowDate() and no attachments; the caller writes a
    /// NEW file with the source's text and gives it its own document. Null when the lot is not valid.
    /// </summary>
    public LegacyCityBuilding? CopyNoteAt(LegacyCityBuilding source, int x, int y, string typedName, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsNote || !CanHouse(x, y)) return null;
        var region = RegionAt(x, y);
        var requested = (typedName ?? "").Trim();
        if (requested.Length > 0 && !TextRe.IsMatch(requested)) requested += ExtNota(source);
        var unique = UniqueNoteName(requested.Length > 0 ? requested : FullNoteName(source), region?.Id);
        var (name, ext) = SplitNoteName(unique, ExtNota(source));
        var folder = region is not null ? RegionPaths().GetValueOrDefault(region.Id, region.Name) : "";
        var b = new LegacyCityBuilding(_newId("b"), (folder.Length > 0 ? folder + "/" : "") + name + ext, name, region?.Id, x, y, source.Sprite);
        var today = NowDate(nowMs);
        b.Tags = source.Tags?.DeepClone() ?? new JsonArray();
        b.Created = JsonValue.Create(today);
        b.Modified = JsonValue.Create(today);
        b.Anexos = new JsonArray();
        b.AiLocal = source.AiLocal?.DeepClone();
        Buildings.Add(b);
        Reindex();
        return b;
    }

    /// <summary>
    /// v20MascaraPrioritaria (no owner): inside the bounds, the dry tiles that are not the
    /// protected lots of a neighbourhood's house nor inside a sub-neighbourhood; only the biggest
    /// connected piece is kept (4-neighbours, breadth-first, in the original order).
    /// </summary>
    public LegacyRegionMask DrawRegionMask(int bx, int by, int bw, int bh)
    {
        bool Allowed(int x, int y)
        {
            if (x < bx || y < by || x >= bx + bw || y >= by + bh || IsWater(x, y)) return false;
            foreach (var b in Buildings)
                if (b.RegionId is not null && x >= b.X - LotGap && x < b.X + b.W + LotGap && y >= b.Y - LotGap && y < b.Y + b.H + LotGap) return false;
            foreach (var r in Regions)
                if (r.ParentId is not null && RegionHasTile(r, x, y)) return false;
            return true;
        }
        var cand = new List<(int X, int Y)>();
        int dry = 0, total = 0;
        for (int y = by; y < by + bh; y++)
        for (int x = bx; x < bx + bw; x++)
        {
            total++;
            if (Allowed(x, y)) { dry++; cand.Add((x, y)); }
        }
        var bounds = (bx, by, bw, bh);
        if (cand.Count == 0) return new LegacyRegionMask([], dry, total, bounds);
        var seen = new HashSet<(int, int)>();
        var comps = new List<List<(int X, int Y)>>();
        foreach (var seed in cand)
        {
            if (seen.Contains(seed)) continue;
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(seed);
            var cells = new List<(int X, int Y)>();
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (!seen.Add(c)) continue;
                if (!Allowed(c.X, c.Y)) continue;
                cells.Add(c);
                queue.Enqueue((c.X + 1, c.Y)); queue.Enqueue((c.X - 1, c.Y)); queue.Enqueue((c.X, c.Y + 1)); queue.Enqueue((c.X, c.Y - 1));
            }
            if (cells.Count > 0) comps.Add(cells);
        }
        var keep = comps.Select((c, i) => (c, i)).OrderByDescending(p => p.c.Count).ThenBy(p => p.i).First().c;
        return new LegacyRegionMask(keep, dry, total, bounds);
    }

    /// <summary>
    /// The drawn region of the 1.8.4 ("Nova região" dialog confirmed): the mask of the rectangle
    /// becomes a root neighbourhood with the typed name (made unique), description and colour;
    /// v20CederTerritorio takes its tiles from the other root neighbourhoods. Null when no ground.
    /// Root houses under the new region are NOT adopted here (see <see cref="RootBuildingsUnder"/>).
    /// </summary>
    public LegacyDrawnRegion? CreateDrawnRegion(string? name, string? description, string? color, int bx, int by, int bw, int bh)
    {
        var m = DrawRegionMask(bx, by, bw, bh);
        if (m.Cells.Count == 0) return null;
        var typed = (name ?? "").Trim();
        var r = new LegacyCityRegion(_newId("r"), UniqueRegionName(typed.Length == 0 ? "Região" : typed), null,
            string.IsNullOrEmpty(color) ? "#4db7ff" : color)
        {
            X = m.Bounds.X, Y = m.Bounds.Y, W = m.Bounds.W, H = m.Bounds.H, Description = description ?? ""
        };
        foreach (var c in m.Cells.Distinct()) r.Add(c);
        Regions.Add(r);
        var ceded = new List<(LegacyCityRegion Region, List<(int X, int Y)> Cells, int X, int Y, int W, int H)>();
        // v20CederTerritorio: the same-parent neighbourhoods with a cell list lose the new tiles
        foreach (var old in Regions.ToList())
        {
            if (ReferenceEquals(old, r) || old.ParentId is not null || old.CellsNull || old.CellList.Count == 0) continue;
            int before = old.CellList.Count;
            var snapshot = (old, old.CellList.ToList(), old.X, old.Y, old.W, old.H);
            old.CellList.RemoveAll(c => r.CellSet.Contains(c));
            if (old.CellList.Count != before)
            {
                old.CellSet.Clear();
                foreach (var c in old.CellList) old.CellSet.Add(c);
                Bounds(old);
                ceded.Add(snapshot);
            }
        }
        return new LegacyDrawnRegion(r, () =>
        {
            Regions.Remove(r);
            foreach (var (region, cells, x, y, w, h) in ceded)
            {
                region.CellList.Clear(); region.CellSet.Clear();
                foreach (var c in cells) region.Add(c);
                region.X = x; region.Y = y; region.W = w; region.H = h;
            }
        });
    }

    /// <summary>absorverConstrucoesDaRaiz (count only): root buildings with a tile in the region.</summary>
    public IReadOnlyList<LegacyCityBuilding> RootBuildingsUnder(LegacyCityRegion r) =>
        Buildings.Where(b => b.RegionId is null && Touches(b, r)).ToList();

    private static bool Touches(LegacyCityBuilding b, LegacyCityRegion r)
    {
        for (int y = b.Y; y < b.Y + b.H; y++)
        for (int x = b.X; x < b.X + b.W; x++)
            if (RegionHasTile(r, x, y)) return true;
        return false;
    }

    /// <summary>The default colour of the "Nova região" dialog: colors[regions.length % 5].</summary>
    public string NextRegionColor() => BaseColors[Regions.Count % BaseColors.Length];
}
