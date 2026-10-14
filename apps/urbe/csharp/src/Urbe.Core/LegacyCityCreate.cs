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

    /// <summary>The neighbourhood colours the 1.8.4 offers (app.js colors).</summary>
    public static IReadOnlyList<string> RegionColors { get; } = ["#5bc2ff", "#e38eff", "#7ee3a0", "#ffd567", "#ff9a88"];

    /// <summary>
    /// openRegionEditDialog → confirmRegion (edit): the folder path of <paramref name="r"/> now and with the name typed
    /// (trimmed; empty is "Região", as the 1.8.4 does). Equal when the name keeps the path. Null when another folder
    /// already has that path: on a real disk two folders cannot share a name.
    /// </summary>
    public (string Name, string Before, string After)? PlanRegionEdit(LegacyCityRegion r, string? typedName)
    {
        ArgumentNullException.ThrowIfNull(r);
        var name = (typedName ?? "").Trim();
        if (name.Length == 0) name = "Região";
        var paths = RegionPaths();
        var before = paths[r.Id];
        var parent = r.ParentId is { } pid && paths.TryGetValue(pid, out var pp) ? pp + "/" : "";
        var after = parent + ArtifactModel.SafeName(name);
        if (string.Equals(after, before, StringComparison.Ordinal)) return (name, before, before);
        if (Regions.Any(x => x != r && string.Equals(paths[x.Id], after, StringComparison.OrdinalIgnoreCase))) return null;
        return (name, before, after);
    }

    /// <summary>
    /// The edit applied (confirmRegion): name, description and colour (#rrggbb, otherwise the old one), and — when the
    /// folder was renamed on disk — every house and attachment under the old path now under the new one
    /// (atualizarFolderPathAoRenomear, here for the files' real paths too). The documents are moved by the caller.
    /// </summary>
    public void ApplyRegionEdit(LegacyCityRegion r, (string Name, string Before, string After) plan, string? description, string? color)
    {
        ArgumentNullException.ThrowIfNull(r);
        r.Name = plan.Name;
        r.Description = description ?? "";
        if (color is not null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) r.Color = color.ToLowerInvariant();
        if (string.Equals(plan.Before, plan.After, StringComparison.Ordinal)) return;
        string Moved(string path) => path == plan.Before ? plan.After
            : path.StartsWith(plan.Before + "/", StringComparison.Ordinal) ? plan.After + path[plan.Before.Length..] : path;
        foreach (var b in Buildings)
        {
            if (b.IsNote) { b.Path = Moved(b.Path); continue; }
            if (b.Files is not JsonArray files) continue;
            foreach (var f in files)
                if (f is JsonObject o)
                    foreach (var key in new[] { "folderPath", "relPath" })
                        if (o[key] is JsonValue v && v.TryGetValue<string>(out var value)) o[key] = Moved(value);
        }
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

public sealed partial class LegacyCity
{
    /// <summary>urbeDentroDe(rid, anc): the neighbourhood is <paramref name="anc"/> or lies inside it.</summary>
    private bool Within(string? regionId, LegacyCityRegion? anc)
    {
        if (anc is null) return false;
        var r = Regions.Find(o => o.Id == regionId);
        for (int g = 0; r is not null && g < 24; g++)
        {
            if (r.Id == anc.Id) return true;
            var p = r.ParentId;
            r = p is not null ? Regions.Find(o => o.Id == p) : null;
        }
        return false;
    }

    /// <summary>v20ResizeDraft(base, handle, t): the rectangle with the dragged side(s) at the tile, at least 3×3.</summary>
    public static (int X, int Y, int W, int H) ResizeDraft((int X, int Y, int W, int H) b, string handle, int tx, int ty)
    {
        int x0 = b.X, y0 = b.Y, x1 = b.X + b.W - 1, y1 = b.Y + b.H - 1;
        if (handle.Contains('w')) x0 = Math.Min(tx, x1 - 2);
        if (handle.Contains('e')) x1 = Math.Max(tx, x0 + 2);
        if (handle.Contains('n')) y0 = Math.Min(ty, y1 - 2);
        if (handle.Contains('s')) y1 = Math.Max(ty, y0 + 2);
        return (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>
    /// v20MascaraPrioritaria(bounds, owner): the new shape of <paramref name="owner"/> inside the rectangle — never
    /// smaller than its own houses with their gap (v20LimitarBoundsAConstrucoes), inside its parent, without water,
    /// other neighbourhoods' protected lots (v20TileProtegidoPorConstrucaoAlheia) or other sub-neighbourhoods
    /// (v20TileEmSubregiaoAlheia); the biggest connected piece plus every piece holding one of its houses.
    /// </summary>
    public LegacyRegionMask ResizeRegionMask(LegacyCityRegion owner, int bx, int by, int bw, int bh)
    {
        ArgumentNullException.ThrowIfNull(owner);
        int x0 = bx, y0 = by, x1 = bx + bw - 1, y1 = by + bh - 1;
        foreach (var c in Buildings)
            if (c.RegionId == owner.Id)
            {
                x0 = Math.Min(x0, c.X - LotGap); y0 = Math.Min(y0, c.Y - LotGap);
                x1 = Math.Max(x1, c.X + c.W - 1 + LotGap); y1 = Math.Max(y1, c.Y + c.H - 1 + LotGap);
            }
        var bounds = (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        var parent = owner.ParentId is { } pid ? Regions.Find(r => r.Id == pid) : null;
        bool Allowed(int x, int y)
        {
            if (x < x0 || y < y0 || x > x1 || y > y1 || IsWater(x, y)) return false;
            if (parent is not null && !RegionHasTile(parent, x, y)) return false;
            foreach (var b in Buildings)
            {
                if (b.RegionId is null || b.RegionId == owner.Id || Within(b.RegionId, owner)) continue;
                if (x >= b.X - LotGap && x < b.X + b.W + LotGap && y >= b.Y - LotGap && y < b.Y + b.H + LotGap) return false;
            }
            foreach (var r in Regions)
            {
                if (r.Id == owner.Id || r.ParentId is null || Within(r.Id, owner) || Within(owner.Id, r)) continue;
                if (RegionHasTile(r, x, y)) return false;
            }
            return true;
        }
        var cand = new List<(int X, int Y)>();
        int dry = 0, total = 0;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            total++;
            if (Allowed(x, y)) { dry++; cand.Add((x, y)); }
        }
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
        // comps.sort((a,b)=>b.length-a.length): a stable sort, the first biggest wins
        var ordered = comps.Select((c, i) => (c, i)).OrderByDescending(p => p.c.Count).ThenBy(p => p.i).Select(p => p.c).ToList();
        var keep = new List<(int X, int Y)>(ordered[0]);
        var required = new HashSet<(int, int)>();
        foreach (var b in Buildings)
            if (b.RegionId == owner.Id)
                for (int y = b.Y; y < b.Y + b.H; y++)
                for (int x = b.X; x < b.X + b.W; x++) required.Add((x, y));
        foreach (var comp in ordered.Skip(1))
            if (comp.Any(required.Contains)) keep.AddRange(comp);
        return new LegacyRegionMask(keep.Distinct().ToList(), dry, total, bounds);
    }

    /// <summary>
    /// v20AplicarMascaraRegiao(r, m): the neighbourhood's cells are the mask, its bounds recalculated, the other
    /// neighbourhoods of the same parent cede those tiles (v20CederTerritorio), holes closed (urbeTaparTodos) and the
    /// index rebuilt. The root houses it now covers are returned for the caller to adopt (absorverConstrucoesDaRaiz),
    /// which moves their files.
    /// </summary>
    public IReadOnlyList<LegacyCityBuilding> ApplyRegionMask(LegacyCityRegion r, LegacyRegionMask m)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(m);
        r.CellList.Clear();
        r.CellSet.Clear();
        r.CellsNull = false;
        foreach (var c in m.Cells) r.Add(c);
        Bounds(r);
        foreach (var old in Regions)
        {
            if (ReferenceEquals(old, r) || old.ParentId != r.ParentId || old.CellsNull || old.CellList.Count == 0) continue;
            int before = old.CellList.Count;
            old.CellList.RemoveAll(c => r.CellSet.Contains(c));
            if (old.CellList.Count == before) continue;
            old.CellSet.Clear();
            foreach (var c in old.CellList) old.CellSet.Add(c);
            Bounds(old);
        }
        TaparTodos();
        Reindex();
        return RootBuildingsUnder(r);
    }
}

public sealed partial class LegacyCity
{
    private static string? RelOf(JsonObject o) => o["relPath"] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;

    /// <summary>The file building holding the attachment at <paramref name="path"/> (by its relPath), or null.</summary>
    public LegacyCityBuilding? AttachmentBuilding(string path) =>
        Buildings.Find(b => !b.IsNote && b.Files is JsonArray files && files.OfType<JsonObject>().Any(o => RelOf(o) == path));

    /// <summary>
    /// renomearAsset / moverAsset after the file moved in the vault: its entry takes the new path (relPath,
    /// folderPath, nome); a building with only that file takes its name and class (b.fileName, limparNome, fileClass)
    /// and, moved to another folder, the neighbourhood and a free lot there (vagaNaRegiao / vagaAleatoria).
    /// </summary>
    public void ApplyAttachmentMove(string from, string to, long nowMs)
    {
        if (AttachmentBuilding(from) is not { Files: JsonArray files } b) return;
        static string Dir(string p) => p.Contains('/') ? p[..p.LastIndexOf('/')] : "";
        var name = to[(to.LastIndexOf('/') + 1)..];
        foreach (var o in files.OfType<JsonObject>().Where(o => RelOf(o) == from))
        {
            o["relPath"] = to;
            o["folderPath"] = Dir(to);
            o["nome"] = name;
        }
        b.Modified = JsonValue.Create(NowDate(nowMs));
        if (files.Count != 1) return;
        b.FileName = JsonValue.Create(name);
        b.Name = System.Text.RegularExpressions.Regex.Replace(name, @"\.[^.]+$", " ").Trim();
        b.FileClass = JsonValue.Create(VaultFileKinds.Classify(name));
        if (Dir(from) == Dir(to)) return;
        var region = Dir(to).Length > 0 ? RegionByPath(Dir(to)) : null;
        double seed = LegacyJsMath.Seed(name + nowMs.ToString(CultureInfo.InvariantCulture));
        var pos = region is not null ? PlaceInRegion(region, seed, new HashSet<string>(StringComparer.Ordinal) { b.Id }) : PlaceLoose(seed, 3, 3);
        if (pos is { } p) { b.X = p.X; b.Y = p.Y; b.RegionId = region?.Id; }
        Reindex();
    }

    /// <summary>removerAsset after the file left the vault: its entry goes, and a building left without files goes too.</summary>
    public void RemoveAttachment(string path)
    {
        if (AttachmentBuilding(path) is not { Files: JsonArray files } b) return;
        foreach (var o in files.OfType<JsonObject>().Where(o => RelOf(o) == path).ToList()) files.Remove(o);
        if (files.Count == 0) Buildings.Remove(b);
        Reindex();
    }
}
