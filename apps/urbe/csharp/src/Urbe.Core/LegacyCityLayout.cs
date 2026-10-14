namespace Urbe.Core;

/// <summary>A photo of where every neighbourhood and house is (layout-guard.js snapshot), to undo a reorganization.</summary>
public sealed class LegacyCityLayoutSnapshot
{
    internal sealed record RegionState(string Id, int X, int Y, int W, int H, List<(int X, int Y)>? Cells, string Color, string? ParentId);
    internal sealed record BuildingState(string Id, int X, int Y, string? RegionId);

    internal LegacyCityLayoutSnapshot(List<RegionState> regions, List<BuildingState> buildings)
    {
        Regions = regions;
        Buildings = buildings;
    }

    internal List<RegionState> Regions { get; }
    internal List<BuildingState> Buildings { get; }
}

/// <summary>What urbeReorganizarCidade returns: neighbourhoods drawn, houses placed and neighbourhoods with no room.</summary>
public sealed record LegacyCityReorganization(int Districts, int Houses, int Failures);

/// <summary>
/// "Organizar os bairros" (city.reorganize), ported from app.js urbeReorganizarCidade and world/layout-guard.js:
/// every neighbourhood is drawn again (the heaviest first, each subfolder inside its own) and the houses gather near
/// the centre of their neighbourhood. Notes, folders and links do not change; only where each thing is on the map.
/// </summary>
public sealed partial class LegacyCity
{
    /// <summary>layout-guard.js snapshot(): regions (x, y, w, h, cells, color, parentId) and buildings (x, y, regionId).</summary>
    public LegacyCityLayoutSnapshot SnapshotLayout() => new(
        Regions.Select(r => new LegacyCityLayoutSnapshot.RegionState(r.Id, r.X, r.Y, r.W, r.H, r.CellsNull ? null : [.. r.CellList], r.Color, r.ParentId)).ToList(),
        Buildings.Select(b => new LegacyCityLayoutSnapshot.BuildingState(b.Id, b.X, b.Y, b.RegionId)).ToList());

    /// <summary>layout-guard.js undo(): each neighbourhood and house that still exists goes back to the photo.</summary>
    public void RestoreLayout(LegacyCityLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var byR = snapshot.Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var byB = snapshot.Buildings.ToDictionary(b => b.Id, StringComparer.Ordinal);
        foreach (var r in Regions)
        {
            if (!byR.TryGetValue(r.Id, out var o)) continue;
            r.X = o.X; r.Y = o.Y; r.W = o.W; r.H = o.H;
            r.CellList.Clear();
            r.CellSet.Clear();
            r.CellsNull = o.Cells is null;
            if (o.Cells is not null) foreach (var c in o.Cells) r.Add(c);
            r.Lots = null;
            r.Color = o.Color;
            r.ParentId = o.ParentId;
        }
        foreach (var b in Buildings)
        {
            if (!byB.TryGetValue(b.Id, out var o)) continue;
            b.X = o.X; b.Y = o.Y; b.RegionId = o.RegionId;
        }
        Reindex();
    }

    private static bool IsAttachment(LegacyCityBuilding b) =>
        b.FileTipo is System.Text.Json.Nodes.JsonValue t && t.TryGetValue<string>(out var tipo) && tipo == "anexo";

    /// <summary>urbeCorAutomatica: a colour the city chose (not one the person picked).</summary>
    private static bool AutomaticColor(string c) => BaseColors.Contains(c) || DistrictColors.Contains(c);

    /// <summary>posicaoAdjacenteArquivo: a lot next to the note the file belongs to.</summary>
    private (int X, int Y)? AdjacentFileLot(LegacyCityBuilding owner, LegacyCityRegion? r, string seed)
    {
        const int d = LotGap + 3;
        (int, int)[] cands = [(d, 0), (-d, 0), (0, d), (0, -d), (d, d), (-d, d), (d, -d), (-d, -d)];
        int shift = (int)(LegacyJsMath.Seed(seed) % (uint)cands.Length);
        for (int i = 0; i < cands.Length; i++)
        {
            var (dx, dy) = cands[(i + shift) % cands.Length];
            int x = owner.X + dx, y = owner.Y + dy;
            if (LotIsValid(x, y, new HashSet<string>(), r)) return (x, y);
        }
        return null;
    }

    /// <summary>
    /// urbeReorganizarCidade(): the neighbourhoods are drawn again from the centre of the city on land, the heaviest
    /// first and each subfolder inside its parent; then the notes (by name) and the files (next to their note) are
    /// placed again. The caller takes the <see cref="SnapshotLayout"/> before, rebuilds the streets and saves the map.
    /// </summary>
    public LegacyCityReorganization Reorganize((double X, double Y)? centre = null)
    {
        var regs = Regions.ToList();
        var houses = Buildings.ToList();
        var items = regs.ToDictionary(r => r.Id, _ => 0, StringComparer.Ordinal);
        var children = regs.ToDictionary(r => r.Id, _ => new List<LegacyCityRegion>(), StringComparer.Ordinal);
        var c0 = centre ?? CentreOnLand(CityCentre());
        foreach (var r in regs)
        {
            if (r.ParentId is { } pid && children.TryGetValue(pid, out var list)) list.Add(r);
            else r.ParentId = null;
        }
        LegacyCityRegion? ById(string? id) => id is null ? null : regs.Find(x => x.Id == id);
        foreach (var b in houses)
        {
            if (IsAttachment(b)) continue;
            for (var r = ById(b.RegionId); r is not null; r = ById(r.ParentId)) items[r.Id]++;
        }
        double Weight(LegacyCityRegion r) => items[r.Id] + children[r.Id].Aggregate(0.0, (s, f) => s + 2 + Weight(f) * .25);

        foreach (var r in regs)
        {
            r.CellList.Clear();
            r.CellSet.Clear();
            r.CellsNull = false;
            r.W = 0; r.H = 0;
            r.Lots = null;
        }
        Buildings.Clear();
        int failures = 0;

        void Build(LegacyCityRegion r, LegacyCityRegion? parent, bool first)
        {
            int area = DistrictArea(Weight(r));
            double seed = LegacyJsMath.Seed("bairro:" + r.Id);
            var m = MaskFor(seed, area, first ? c0 : null, parent);
            if (m is null && parent is not null)
            {
                ExpandRegion(parent, area + 40);
                m = MaskFor(seed + 1, area, null, parent);
            }
            if (m is null) { failures++; return; }
            foreach (var c in m.Cells) r.Add(c);
            Bounds(r);
            if (AutomaticColor(r.Color)) r.Color = parent is not null ? ChildColor(r) : RootColor(r);
            Reindex();
            // Array.prototype.sort is stable: equal weights keep the order of world.regions
            foreach (var f in children[r.Id].OrderByDescending(Weight).ToList()) Build(f, r, false);
        }
        var roots = regs.Where(r => r.ParentId is null).OrderByDescending(Weight).ToList();
        for (int i = 0; i < roots.Count; i++) Build(roots[i], null, i == 0);

        // houses: notes first (by name, to stay stable), then files
        void Put(LegacyCityBuilding b, (int X, int Y)? pos)
        {
            if (pos is { } p) { b.X = p.X; b.Y = p.Y; }
            Buildings.Add(b);
            b.IndexedX = b.X; b.IndexedY = b.Y;
        }
        LegacyCityRegion? Drawn(string? id) => id is null ? null : regs.Find(x => x.Id == id && x.W != 0);
        foreach (var b in houses.Where(b => !IsAttachment(b)).OrderBy(b => b.Name, Comparer<string>.Create(JsText.LocaleCompare)).ToList())
        {
            var r = Drawn(b.RegionId);
            if (b.RegionId is not null && r is null) b.RegionId = null;
            double s = LegacyJsMath.Seed(b.Id);
            Put(b, r is not null ? PlaceInRegion(r, s, new HashSet<string>()) : PlaceLoose(s, b.W, b.H, c0));
        }
        foreach (var b in houses.Where(IsAttachment).ToList())
        {
            var owner = b.ParentNoteId is { } pn ? Buildings.Find(x => x.Id == pn) : null;
            var r = Drawn(b.RegionId);
            double s = LegacyJsMath.Seed(b.Id);
            Put(b, (owner is not null ? AdjacentFileLot(owner, r, b.Id) : null)
                   ?? (r is not null ? PlaceInRegion(r, s, new HashSet<string>()) : PlaceLoose(s, b.W, b.H, c0)));
        }
        Reindex();
        return new LegacyCityReorganization(regs.Count - failures, Buildings.Count, failures);
    }

    /// <summary>
    /// urbeValidarBairros(): the rules of the neighbourhoods — a subfolder inside its parent (with a margin), siblings
    /// not touching, every house wholly inside its own neighbourhood and no two houses overlapping.
    /// </summary>
    public IReadOnlyList<string> ValidateDistricts()
    {
        var errors = new List<string>();
        (int, int)[] n8 = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];
        foreach (var r in Regions)
        {
            if (r.W == 0) continue;
            r.Celulas();
            var parent = Parent(r);
            if (parent is not null)
            {
                parent.Celulas();
                int outside = r.CellList.Count(k => !parent.CellSet.Contains(k) || n8.Any(d => !parent.CellSet.Contains((k.X + d.Item1, k.Y + d.Item2))));
                if (outside != 0) errors.Add(r.Name + ": " + outside + " tile(s) fora da margem de " + parent.Name);
            }
            foreach (var o in Regions)
            {
                if (o == r || o.W == 0 || o.ParentId != r.ParentId || string.CompareOrdinal(o.Id, r.Id) < 0) continue;
                o.Celulas();
                if (r.CellList.Any(k => o.CellSet.Contains(k) || n8.Any(d => o.CellSet.Contains((k.X + d.Item1, k.Y + d.Item2)))))
                    errors.Add(r.Name + " encosta em " + o.Name);
            }
        }
        for (int i = 0; i < Buildings.Count; i++)
        {
            var b = Buildings[i];
            var r = b.RegionId is { } id ? Regions.Find(x => x.Id == id) : null;
            bool done = false;
            for (int y = b.Y; y < b.Y + b.H && !done; y++)
            for (int x = b.X; x < b.X + b.W; x++)
                if (RegionAt(x, y)?.Id != r?.Id) { errors.Add("casa " + b.Name + " fora do bairro " + (r?.Name ?? "raiz")); done = true; break; }
            for (int j = i + 1; j < Buildings.Count; j++)
            {
                var o = Buildings[j];
                if (b.X < o.X + o.W && b.X + b.W > o.X && b.Y < o.Y + o.H && b.Y + b.H > o.Y) errors.Add("casas sobrepostas: " + b.Name + " e " + o.Name);
            }
        }
        return errors;
    }
}
