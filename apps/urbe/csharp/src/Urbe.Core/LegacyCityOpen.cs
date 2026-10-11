using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// Opening a real vault, as the 1.8.4-beta does it (src/app.js; the last definition wins):
/// abrirCidade (≈4704) → abrirCidade (≈3740: urbePersistence.load, then urbeOpenLegacy =
/// v21OpenCity ≈2495–2498, documentId binding, rebuildRoadNetwork) → urbeCasasNosBairros +
/// urbeTaparTodos. Reads .urbe/mapa.json (v:4, written by estadoDesejado ≈2493); never writes it.
/// </summary>
public sealed partial class LegacyCity
{
    /// <summary>app.js URBE_MUNDO: the world the saved geometry belongs to.</summary>
    public const string WorldVersion = "placas-1";

    private static readonly string[] HouseSprites = ["house1", "house2", "house3"];
    private static readonly CompareInfo Collation = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>app.js camera.z (the zoom); the default is the 1.8.4 start.</summary>
    public double CameraZoom { get; set; } = 1.15;

    /// <summary>
    /// True when the vault's mapa.json was saved for another world (mapa.mundo ≠ URBE_MUNDO) and
    /// there are buildings: the 1.8.4 then reorganizes the whole city (urbeReorganizarCidade),
    /// which is NOT ported yet. The geometry here is the saved one, as read.
    /// </summary>
    public bool WorldReorganizationPending { get; private set; }

    /// <summary>
    /// abrirCidade for a real vault. <paramref name="files"/> are the vault's files in the order the
    /// storage adapter lists them (FS.listar); only editable text outside dot folders becomes a house
    /// (v21IsEditablePath). <paramref name="mapaJson"/> is the text of .urbe/mapa.json (or null).
    /// <paramref name="documentIds"/> gives the DocumentStore id of a path when the vault knows it
    /// (persistence: mapa notas id or identity sidecar); otherwise the mapa id is used.
    /// Binary assets (v21IndexBinary) are not placed: callers pass text files only. File buildings
    /// saved in mapa.construcoes are restored (they take room) but are not notes.
    /// </summary>
    public DocumentStore Open(IReadOnlyList<(string Path, string Content)> files, string? mapaJson,
        IReadOnlyDictionary<string, string>? documentIds = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        using var parsed = ParseMap(mapaJson);
        JsonElement? mapa = parsed?.RootElement;

        // urbePersistence.load: one document per editable file, with the id the vault knows.
        var texts = files.Where(f => ArtifactModel.IsText(f.Path) && !ArtifactModel.IsSystem(f.Path)).ToList();
        var geoNota = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (mapa is { } m0 && m0.TryGetProperty("notas", out var notas) && notas.ValueKind == JsonValueKind.Object)
            foreach (var p in notas.EnumerateObject()) geoNota[p.Name] = p.Value;
        var store = new DocumentStore();
        foreach (var (path, content) in texts)
        {
            string? id = documentIds is not null && documentIds.TryGetValue(path, out var known) ? known : null;
            if (id is null && geoNota.TryGetValue(path, out var gm) && Str(gm, "id") is { Length: > 0 } mid) id = mid;
            store.Upsert(new DocumentInput { Id = id, Path = path, Content = content });
        }

        OpenLegacy(texts.Select(t => t.Path).ToList(), mapa, geoNota);

        // abrirCidade (≈3740): each note house shows the document at its path (urbeBuildingPath).
        var regionPaths = RegionPaths();
        foreach (var b in Buildings)
        {
            if (!b.IsNote) continue;
            if (store.Get(BuildingPath(b, regionPaths)) is { } doc) b.DocumentId = doc.Id;
        }
        // estadoDesejado → urbeReconcileWorld: the house keeps the document it found (by id, then path).
        // (When the computed path differs, 1.8.4 also renames/creates that document on save: a write, not done here.)
        foreach (var b in Buildings)
        {
            if (!b.IsNote) continue;
            var doc = (b.DocumentId is { Length: > 0 } did ? store.Get(did) : null) ?? store.Get(BuildingPath(b, regionPaths));
            if (doc is not null) b.DocumentId = doc.Id;
        }
        using var index = new KnowledgeIndex(store);
        RebuildRoadsByDocument(store, index);

        // abrirCidade (≈4704): every house inside its own neighbourhood; then scheduleRoadRebuild.
        int moved = CasasNosBairros();
        int filled = TaparTodos();
        if (moved + filled > 0) RebuildRoadsByDocument(store, index);
        return store;
    }

    private static JsonDocument? ParseMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var doc = JsonDocument.Parse(json.TrimStart('\uFEFF')); // TextDecoder drops the BOM
            // v21OpenCity: Object.keys(pm.meta).length ? mapa : null
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().Any()) return doc;
            doc.Dispose();
        }
        catch (JsonException) { }
        return null;
    }

    private static string? Str(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>JS `g.n||fallback` for a numeric field.</summary>
    private static int NumOr(JsonElement o, string name, int fallback) => Num(o, name) is { } v && v != 0 ? (int)v : fallback;

    private static string DirOf(string rel)
    {
        int i = rel.LastIndexOf('/');
        return i < 0 ? "" : rel[..i];
    }

    /// <summary>v21Stem: the file name without its text extension, made safe.</summary>
    private static string Stem(string name)
    {
        var s = Regex.Replace(name.Trim(), @"[\\/]", "-");
        var m = Regex.Match(s, @"\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$", RegexOptions.IgnoreCase);
        if (m.Success) s = s[..^m.Length];
        return ArtifactModel.SafeName(s.Length > 0 ? s : "Nota");
    }

    /// <summary>urbeBuildingPath: folder of the neighbourhood + safe name + extNota.</summary>
    private static string BuildingPath(LegacyCityBuilding b, IReadOnlyDictionary<string, string> regions)
    {
        var dir = b.RegionId is not null && regions.TryGetValue(b.RegionId, out var d) ? d : "";
        return (dir.Length > 0 ? dir + "/" : "") + ArtifactModel.SafeName(b.Name) + b.Ext.ToLowerInvariant() switch
        {
            var e when Regex.IsMatch(e, @"\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$") => e,
            _ => ".md"
        };
    }

    /// <summary>urbeOrigemPelasCasas: a neighbourhood recreated without a saved shape is born where its houses were.</summary>
    private static (double X, double Y)? OrigemPelasCasas(string cam, IReadOnlyList<string> rels, IReadOnlyDictionary<string, JsonElement> geo)
    {
        double sx = 0, sy = 0;
        int n = 0;
        foreach (var rel in rels)
        {
            var d = DirOf(rel);
            if (d != cam && !d.StartsWith(cam + "/", StringComparison.Ordinal)) continue;
            if (geo.TryGetValue(rel, out var g) && Num(g, "x") is { } x && Num(g, "y") is { } y) { sx += x + 1; sy += y + 1; n++; }
        }
        return n != 0 ? (LegacyJsMath.Round(sx / n), LegacyJsMath.Round(sy / n)) : null;
    }

    /// <summary>v21OpenCity (≈2495–2498), for the text files of the vault.</summary>
    private void OpenLegacy(List<string> texts, JsonElement? mapa, Dictionary<string, JsonElement> geoNota)
    {
        Regions.Clear();
        Buildings.Clear();
        _roads.Clear();
        _roadOrder.Clear();
        _componentId = null;
        Links.Clear();
        WorldReorganizationPending = false;

        var geoReg = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var caminhos = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void AddPath(string c) { if (seen.Add(c)) caminhos.Add(c); }
        if (mapa is { } m && m.TryGetProperty("regioes", out var regioes) && regioes.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in regioes.EnumerateArray())
                if (Str(r, "caminho") is { } c) geoReg[c] = r;
            foreach (var r in regioes.EnumerateArray())
                if (Str(r, "caminho") is { Length: > 0 } c) AddPath(c);
        }
        foreach (var rel in texts)
            for (var d = DirOf(rel); d.Length > 0; d = DirOf(d)) AddPath(d);

        // shallow first, then String.prototype.localeCompare (ICU collation), stable
        var ordenados = caminhos
            .OrderBy(c => c.Split('/').Length)
            .ThenBy(c => c, Comparer<string>.Create((a, b) => Collation.Compare(a, b, CompareOptions.None)))
            .ToList();
        var regPorCaminho = new Dictionary<string, LegacyCityRegion?>(StringComparer.Ordinal);
        foreach (var cam in ordenados)
        {
            var paiCam = DirOf(cam);
            var pai = paiCam.Length > 0 && regPorCaminho.TryGetValue(paiCam, out var pr) ? pr : null;
            JsonElement? g = geoReg.TryGetValue(cam, out var ge) ? ge : null;
            var nomeR = g is { } g0 && Str(g0, "nome") is { Length: > 0 } nome ? nome : cam.Split('/')[^1];
            int quantos = texts.Count(r => r.StartsWith(cam + "/", StringComparison.Ordinal)) +
                          ordenados.Count(c => c.StartsWith(cam + "/", StringComparison.Ordinal)) * 2;
            if (quantos == 0) quantos = 1;
            LegacyCityRegion? r;
            if (g is { } gs && Num(gs, "x") is { } gx && Num(gs, "w") is > 0)
            {
                var cor = Str(gs, "cor");
                r = new LegacyCityRegion(_newId("r"), nomeR, pai?.Id,
                    !string.IsNullOrEmpty(cor) ? cor : BaseColors[Regions.Count % BaseColors.Length])
                {
                    X = (int)gx,
                    Y = (int)(Num(gs, "y") ?? 0),
                    W = (int)Num(gs, "w")!.Value,
                    H = (int)(Num(gs, "h") ?? 0)
                };
                if (gs.TryGetProperty("cells", out var cells) && cells.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cells.EnumerateArray())
                    {
                        var k = c.GetString() ?? "";
                        int j = k.IndexOf(',');
                        r.Add((int.Parse(k[..j], CultureInfo.InvariantCulture), int.Parse(k[(j + 1)..], CultureInfo.InvariantCulture)));
                    }
                }
                else r.CellsNull = true;
                Regions.Add(r);
            }
            else
            {
                r = CreateRegion(nomeR, quantos, LegacyJsMath.Seed(cam + quantos.ToString(CultureInfo.InvariantCulture)), pai?.Id,
                    OrigemPelasCasas(cam, texts, geoNota));
            }
            regPorCaminho[cam] = r;
        }

        // notes: saved place first; the others look for a lot after all saved houses are down
        var semLugar = new List<(LegacyCityBuilding B, LegacyCityRegion? R, string Rel)>();
        foreach (var rel in texts)
        {
            var cam = DirOf(rel);
            var r = cam.Length > 0 && regPorCaminho.TryGetValue(cam, out var rr) ? rr : null;
            JsonElement? g = geoNota.TryGetValue(rel, out var ge) ? ge : null;
            var sprite = g is { } gsp && Str(gsp, "sprite") is { Length: > 0 } sp ? sp : HouseSprites[LegacyJsMath.Seed(rel) % 3];
            var bid = _newId("b");
            var name = Stem(rel.Split('/')[^1]);
            if (g is { } gp && Num(gp, "x") is { } x)
            {
                var b = new LegacyCityBuilding(bid, rel, name, r?.Id, (int)x, (int)(Num(gp, "y") ?? 0), sprite)
                {
                    DocumentId = Str(gp, "id") is { Length: > 0 } did ? did : null
                };
                Buildings.Add(b);
            }
            else
            {
                semLugar.Add((new LegacyCityBuilding(bid, rel, name, r?.Id, 0, 0, sprite)
                {
                    DocumentId = g is { } gd && Str(gd, "id") is { Length: > 0 } did ? did : null
                }, r, rel));
            }
        }
        foreach (var (b, r, rel) in semLugar)
        {
            // posicaoAleatoriaNaRegiao (final, no growth) or vagaAleatoria
            var pos = r is not null ? PlaceInRegion(r, new HashSet<string>()) : PlaceLoose(LegacyJsMath.Seed(rel));
            b.X = pos?.X ?? 0;
            b.Y = pos?.Y ?? 0;
            b.IndexedX = b.X;
            b.IndexedY = b.Y;
            Buildings.Add(b);
        }
        // file buildings saved in the map (drawn as files in 1.8.4; here they only take their room)
        if (mapa is { } mc && mc.TryGetProperty("construcoes", out var construcoes) && construcoes.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in construcoes.EnumerateArray())
            {
                var cam = Str(g, "caminho");
                var r = !string.IsNullOrEmpty(cam) && regPorCaminho.TryGetValue(cam, out var rr) ? rr : null;
                var name = Str(g, "name") is { Length: > 0 } n1 ? n1 : Str(g, "fileName") is { Length: > 0 } n2 ? n2 : "Arquivo";
                var sprite = Str(g, "sprite") is { Length: > 0 } s ? s : "file-" + (Str(g, "fileClass") is { Length: > 0 } fc ? fc : "other");
                Buildings.Add(new LegacyCityBuilding(_newId("b"), "", name, r?.Id, NumOr(g, "x", 0), NumOr(g, "y", 0), sprite,
                    isNote: false, w: NumOr(g, "w", 3), h: NumOr(g, "h", 3)));
            }
        }

        // camera={...camera,...mapa.camera}; camera.z=clamp(camera.z,.22,2.8)
        if (mapa is { } mk && mk.TryGetProperty("camera", out var cam0) && cam0.ValueKind == JsonValueKind.Object)
        {
            if (Num(cam0, "x") is { } cx) CameraX = cx;
            if (Num(cam0, "y") is { } cy) CameraY = cy;
            if (Num(cam0, "z") is { } cz) CameraZoom = cz;
        }
        CameraZoom = Math.Clamp(CameraZoom, .22, 2.8);

        // mapa.mundo !== URBE_MUNDO: urbeReorganizarCidade (not ported; flagged)
        if (mapa is { } mw && Str(mw, "mundo") != WorldVersion && Buildings.Count > 0) WorldReorganizationPending = true;

        // marcarIndice(); indexar(); urbeCasasNosBairros()+urbeTaparTodos() with no streets yet.
        // (The rebuildRoadNetwork that follows here is fully replaced by the one in abrirCidade ≈3740:
        // it clears the streets first and nothing between the two reads them.)
        Reindex();
        CasasNosBairros();
        TaparTodos();
    }

    /// <summary>
    /// rebuildRoadNetwork with linksFromContent/buildingByLinkName as in 1.8.4: the house's document is
    /// docs.get(b.documentId), the target house is the first one whose documentId is the target's id.
    /// </summary>
    private void RebuildRoadsByDocument(DocumentStore store, KnowledgeIndex index)
    {
        static string NormName(string s) => LegacyJsMath.Slug(Regex.Replace(s, @"\.md$", "", RegexOptions.IgnoreCase)).ToLowerInvariant();
        var all = store.List();
        LegacyCityBuilding? ByLinkName(string name)
        {
            var doc = all.FirstOrDefault(d => NormName(d.Title) == name || NormName(d.Path) == name);
            return doc is null ? null : Buildings.Find(b => b.DocumentId == doc.Id);
        }
        RebuildRoads(b =>
        {
            // docs.get(b.documentId||b.path||b.name): the legacy building has no path field
            var doc = store.Get(b.DocumentId is { Length: > 0 } id ? id : b.Name);
            return doc is null ? [] : index.Links(doc.Id).Select(d => ByLinkName(NormName(d.Title))).ToList();
        });
    }

    // ------------------------------------------------------------------ neighbourhood rule

    private LegacyCityRegion? RegionById(string? id) => id is null ? null : Regions.Find(o => o.Id == id);

    /// <summary>urbeDentroDe: the neighbourhood is anc or lies inside it.</summary>
    private bool DentroDe(string? rid, LegacyCityRegion? anc)
    {
        if (anc is null) return false;
        var r = RegionById(rid);
        for (int g = 0; r is not null && g < 24; g++)
        {
            if (r.Id == anc.Id) return true;
            r = RegionById(r.ParentId);
        }
        return false;
    }

    private HashSet<string> Ancestral(LegacyCityRegion r)
    {
        var ancestral = new HashSet<string>(StringComparer.Ordinal);
        for (var a = r; a is not null; a = RegionById(a.ParentId))
        {
            ancestral.Add(a.Id);
            if (ancestral.Count > 24) break;
        }
        return ancestral;
    }

    /// <summary>(o._cellSet||new Set(o.cells)).has(k), with o.cells null being an empty set.</summary>
    private static bool OwnsCell(LegacyCityRegion o, (int X, int Y) k) => !o.CellsNull && o.CellSet.Contains(k);

    private bool InsideBuilding(int x, int y, Func<LegacyCityBuilding, bool> exempt)
    {
        foreach (var b in Buildings)
            if (x >= b.X && x < b.X + b.W && y >= b.Y && y < b.Y + b.H && !exempt(b)) return true;
        return false;
    }

    /// <summary>urbeTaparBuracos: closed holes of a neighbourhood become part of it. Returns the tiles added.</summary>
    private int TaparBuracos(LegacyCityRegion r)
    {
        if (r.Celulas().Count == 0) return 0;
        int x0 = r.X - 1, y0 = r.Y - 1, x1 = r.X + r.W, y1 = r.Y + r.H, W = x1 - x0 + 1, H = y1 - y0 + 1;
        var fora = new byte[W * H];
        var pai = RegionById(r.ParentId);
        pai?.Celulas();
        var ancestral = Ancestral(r);
        var fila = new Stack<(int X, int Y)>();
        for (int x = 0; x < W; x++) { fila.Push((x, 0)); fila.Push((x, H - 1)); }
        for (int y = 0; y < H; y++) { fila.Push((0, y)); fila.Push((W - 1, y)); }
        while (fila.Count > 0)
        {
            var (x, y) = fila.Pop();
            if (x < 0 || y < 0 || x >= W || y >= H) continue;
            int i = y * W + x;
            if (fora[i] != 0) continue;
            if (r.CellSet.Contains((x + x0, y + y0))) continue;
            fora[i] = 1;
            fila.Push((x + 1, y)); fila.Push((x - 1, y)); fila.Push((x, y + 1)); fila.Push((x, y - 1));
        }
        int add = 0;
        for (int y = 1; y < H - 1; y++)
        for (int x = 1; x < W - 1; x++)
        {
            int i = y * W + x;
            var k = (x + x0, y + y0);
            if (fora[i] != 0 || r.CellSet.Contains(k)) continue;
            if (IsWater(k.Item1, k.Item2)) continue;
            if (pai is not null && !pai.CellSet.Contains(k)) continue;
            if (Regions.Any(o => o != r && !ancestral.Contains(o.Id) && !DentroDe(o.Id, r) && OwnsCell(o, k))) continue;
            if (InsideBuilding(k.Item1, k.Item2, b => b.RegionId is not null && DentroDe(b.RegionId, r))) continue;
            r.Add(k);
            add++;
        }
        if (add != 0) Reindex();
        return add;
    }

    private int Nivel(LegacyCityRegion r)
    {
        int n = 0;
        var p = r;
        while (p is not null && p.ParentId is not null && n < 24) { p = RegionById(p.ParentId); n++; }
        return n;
    }

    private static readonly (int X, int Y)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>
    /// urbeTaparTodos: what a sub-neighbourhood has and its parent lacks goes to the parent (deepest
    /// first), the 1-tile teeth of the seam are smoothed, then every neighbourhood's holes are closed.
    /// </summary>
    public int TaparTodos()
    {
        int total = 0;
        foreach (var f in Regions.OrderByDescending(Nivel).ToList())
        {
            if (f.ParentId is null || f.CellsNull || f.CellList.Count == 0) continue;
            var p = RegionById(f.ParentId);
            if (p is null) continue;
            p.Celulas();
            int add = 0;
            foreach (var k in f.CellList.ToList())
            {
                if (p.CellSet.Contains(k)) continue;
                if (IsWater(k.X, k.Y)) continue;
                if (Regions.Any(o => o != p && o != f && !DentroDe(o.Id, p) && !DentroDe(p.Id, o) && OwnsCell(o, k))) continue;
                p.Add(k);
                add++;
            }
            for (int volta = 0; add != 0 && volta < 3; volta++)
            {
                var novos = new List<(int X, int Y)>();
                var novosSet = new HashSet<(int X, int Y)>();
                foreach (var (x, y) in p.CellList)
                foreach (var d in Four)
                {
                    int nx = x + d.X, ny = y + d.Y;
                    var nk = (nx, ny);
                    if (p.CellSet.Contains(nk) || novosSet.Contains(nk) || IsWater(nx, ny)) continue;
                    int viz = Four.Count(e => p.CellSet.Contains((nx + e.X, ny + e.Y)));
                    if (viz < 3) continue;
                    var pp = RegionById(p.ParentId);
                    if (pp is not null && !OwnsCell(pp, nk)) continue;
                    if (Regions.Any(o => o != p && !DentroDe(o.Id, p) && !DentroDe(p.Id, o) && OwnsCell(o, nk))) continue;
                    if (InsideBuilding(nx, ny, b => b.RegionId is not null && DentroDe(b.RegionId, p))) continue;
                    novos.Add(nk);
                    novosSet.Add(nk);
                }
                if (novos.Count == 0) break;
                foreach (var k in novos) p.Add(k);
                add += novos.Count;
            }
            if (add != 0)
            {
                Bounds(p); // v20RecalcularBounds
                total += add;
            }
        }
        if (total != 0) Reindex();
        foreach (var r in Regions.OrderBy(Nivel).ToList())
            if (!r.CellsNull && r.CellList.Count > 0) total += TaparBuracos(r);
        return total;
    }

    /// <summary>urbeDistanciaAoBairro.</summary>
    private static double DistanciaAoBairro(LegacyCityRegion r, int x, int y)
    {
        double m = 1e9;
        foreach (var (cx, cy) in r.Celulas())
        {
            double dx = cx - x, dy = cy - y, d = dx * dx + dy * dy;
            if (d < m) m = d;
        }
        return Math.Sqrt(m);
    }

    /// <summary>urbeAbsorverEmVolta: the neighbourhood takes the ground around (x, y), inside its parent.</summary>
    private int AbsorverEmVolta(LegacyCityRegion r, int x, int y, double raio)
    {
        r.Celulas();
        var pai = RegionById(r.ParentId);
        pai?.Celulas();
        var ancestral = Ancestral(r);
        int add = 0, R = (int)Math.Ceiling(raio);
        for (int yy = y - R; yy <= y + R; yy++)
        for (int xx = x - R; xx <= x + R; xx++)
        {
            if ((xx - x) * (xx - x) + (yy - y) * (yy - y) > raio * raio) continue;
            var k = (xx, yy);
            if (r.CellSet.Contains(k) || IsWater(xx, yy) || (pai is not null && !pai.CellSet.Contains(k))) continue;
            bool dono = Regions.Any(o =>
            {
                if (o == r || ancestral.Contains(o.Id) || o.CellsNull) return false;
                for (var d = o; d is not null; d = RegionById(d.ParentId))
                    if (d == r) return false;
                return o.CellSet.Contains(k);
            });
            if (dono) continue;
            r.Add(k);
            add++;
        }
        if (add != 0)
        {
            Bounds(r);
            Reindex();
        }
        return add;
    }

    /// <summary>
    /// urbeCasasNosBairros: every note house stays inside its own neighbourhood. A house left
    /// outside comes back in (holes closed, the neighbourhood absorbs it, or a new lot, growing if
    /// needed). Returns how many houses changed.
    /// </summary>
    public int CasasNosBairros()
    {
        int movidas = 0;
        for (int i = 0; i < Buildings.Count; i++)
        {
            var b = Buildings[i];
            if (!b.IsNote || b.RegionId is null) continue;
            var r = RegionById(b.RegionId);
            if (r is null) continue;
            r.Celulas();
            int cx = b.X + b.W / 2, cy = b.Y + b.H / 2;
            if (r.CellSet.Contains((cx, cy))) continue; // the centre of the house is in the neighbourhood
            var cadeia = new List<LegacyCityRegion>();
            var c0 = r;
            for (int g0 = 0; c0 is not null && g0++ < 24; c0 = RegionById(c0.ParentId)) cadeia.Insert(0, c0);
            int tapou = 0;
            foreach (var rr in cadeia) tapou += TaparBuracos(rr);
            if (tapou != 0 && r.CellSet.Contains((cx, cy))) { movidas++; continue; }
            if (DistanciaAoBairro(r, cx, cy) <= 5)
            {
                int absorveu = 0;
                foreach (var rr in cadeia) absorveu += AbsorverEmVolta(rr, cx, cy, 3.2);
                foreach (var rr in cadeia) TaparBuracos(rr);
                if (absorveu != 0 && r.CellSet.Contains((cx, cy))) { movidas++; continue; }
            }
            var ign = new HashSet<string>(StringComparer.Ordinal) { b.Id };
            var key = (b.DocumentId is { Length: > 0 } did ? did : b.Id) + ":volta";
            var pos = PlaceInRegion(r, LegacyJsMath.Seed(key), ign);
            for (int t = 0; pos is null && t < 3; t++)
            {
                if (ExpandRegion(r, 40) == 0) break;
                pos = PlaceInRegion(r, LegacyJsMath.Seed(key + t.ToString(CultureInfo.InvariantCulture)), ign);
            }
            if (pos is { } p) { b.X = p.X; b.Y = p.Y; movidas++; }
        }
        if (movidas != 0) Reindex();
        return movidas;
    }
}
