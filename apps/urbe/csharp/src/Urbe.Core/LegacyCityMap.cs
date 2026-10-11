using System.Globalization;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// The text of .urbe/mapa.json exactly as the 1.8.4-beta writes it, from the city, the documents and
/// the camera. Ported from the definitions in effect in src/app.js (the last one wins):
/// <list type="number">
/// <item>estadoDesejado base (≈2493): v:4, app, version, mundo, salvo, camera, regioes (stable ids,
/// caminhosRegioes, cells), notas by building, construcoes (relBinUnico, metaAssetLimpa);</item>
/// <item>wrapper "mapa.json só é gravado quando muda de verdade" (≈2640): "salvo" keeps the previous
/// instant while the rest of that map is unchanged, and moves to the end of the object;</item>
/// <item>wrapper urbeLegacyDesired (≈3655): urbeReconcileWorld, then notas rebuilt from the
/// DocumentStore ({id,x,y,sprite,tags,anexos,criado,modificado,aiLocal});</item>
/// <item>urbePersistence.metadataProvider (≈3673) → workspace.js desired: JSON.stringify(map,null,1).</item>
/// </list>
/// One instance per open vault (the v22 "salvo" memory lives for the session, like app.js).
/// </summary>
public sealed class LegacyCityMap
{
    /// <summary>.urbe/mapa.json.</summary>
    public const string Path = ".urbe/mapa.json";

    /// <summary>app.js APP_NOME.</summary>
    public const string AppName = "Urbe";

    /// <summary>window.UrbeCore.version of the reference product (V21_VERSION).</summary>
    public const string ReferenceVersion = "1.8.4-beta";

    private readonly Func<string, string> _freshId;
    private string? _key, _salvo; // v22MapaChave, v22MapaSalvo

    /// <param name="freshId">UrbeStableIds.fresh(prefix) for a region/file building that has no stable id yet.</param>
    public LegacyCityMap(Func<string, string>? freshId = null)
    {
        _freshId = freshId ?? WorldStableIds.Fresh;
    }

    /// <summary>
    /// estadoDesejado().arquivos.get('.urbe/mapa.json') at <paramref name="nowMs"/> (Date.now / new Date).
    /// Returns null, with <paramref name="refusal"/>, when the 1.8.4 would first create or move a note
    /// FILE (urbeReconcileWorld: a house without document, or a document whose path is not the house's):
    /// those writes are not ported, and a map naming paths that are not on disk must not be written.
    /// </summary>
    public string? Produce(LegacyCity city, DocumentStore store, double cameraX, double cameraY, double cameraZoom,
        long nowMs, string version, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(version);
        var today = LegacyCity.NowDate(nowMs);
        var iso = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        // urbeReconcileWorld
        var regions = city.RegionPaths();
        foreach (var b in city.Buildings)
        {
            if (!b.IsNote) continue;
            var path = LegacyCity.BuildingPathOf(b, regions);
            var doc = (b.DocumentId is { Length: > 0 } did ? store.Get(did) : null) ?? store.Get(path);
            if (doc is not null && b.RegionId is null && doc.Path.Contains('/')) path = doc.Path;
            if (doc is null)
            {
                refusal = "A casa \"" + b.Name + "\" não tem documento (a 1.8.4 criaria " + path + ").";
                return null;
            }
            if (b.DocumentId is { Length: > 0 } && doc.Path != path)
            {
                refusal = "A 1.8.4 moveria " + doc.Path + " para " + path + "; mover arquivos ainda não foi portado.";
                return null;
            }
            b.DocumentId = doc.Id;
        }

        // estadoDesejado base: the map as the buildings describe it
        var baseMap = BaseMap(city, regions, cameraX, cameraY, cameraZoom, today, iso, version);

        // v22: "salvo" stays while the rest is the same; it is re-added last
        var o = (JsonObject)JsonNode.Parse(LegacyJson.Stringify(baseMap, 1))!;
        var salvo = (string?)o["salvo"];
        o.Remove("salvo");
        var key = LegacyJson.Stringify(o);
        if (_key == key && _salvo is not null) salvo = _salvo;
        else { _key = key; _salvo = salvo; }
        o["salvo"] = salvo;

        // urbeLegacyDesired: notas from the DocumentStore
        var map = (JsonObject)JsonNode.Parse(LegacyJson.Stringify(o, 1))!;
        var byId = new Dictionary<string, LegacyCityBuilding>(StringComparer.Ordinal);
        foreach (var b in city.Buildings)
            if (b.IsNote && b.DocumentId is { Length: > 0 } id) byId[id] = b;
        var notas = new JsonObject();
        foreach (var doc in store.List())
        {
            byId.TryGetValue(doc.Id, out var b);
            var tags = new JsonArray();
            foreach (var t in doc.Tags) tags.Add(t);
            notas[doc.Path] = new JsonObject
            {
                ["id"] = doc.Id,
                ["x"] = b?.X ?? 0,
                ["y"] = b?.Y ?? 0,
                ["sprite"] = b is not null ? b.Sprite : "house1",
                ["tags"] = tags,
                ["anexos"] = b is not null ? Anexos(b.Anexos) : new JsonArray(),
                ["criado"] = !string.IsNullOrEmpty(doc.Created) ? doc.Created : Truthy(b?.Created) ? b!.Created!.DeepClone() : today,
                ["modificado"] = !string.IsNullOrEmpty(doc.Modified) ? doc.Modified : Truthy(b?.Modified) ? b!.Modified!.DeepClone() : today,
                ["aiLocal"] = Truthy(b?.AiLocal) ? b!.AiLocal!.DeepClone() : null
            };
        }
        map["notas"] = notas;
        refusal = null;
        return LegacyJson.Stringify(map, 1);
    }

    private JsonObject BaseMap(LegacyCity city, Dictionary<string, string> cam, double cx, double cy, double cz,
        string today, string iso, string version)
    {
        string Dir(string? regionId) => regionId is not null && cam.TryGetValue(regionId, out var d) ? d : "";

        var notas = new JsonObject();
        var usados = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in city.Buildings)
        {
            if (!b.IsNote) continue;
            var dir = Dir(b.RegionId);
            var nome = ArtifactModel.SafeName(b.Name);
            var ext = LegacyCity.ExtNota(b);
            var rel = (dir.Length > 0 ? dir + "/" : "") + nome + ext;
            int n = 2;
            while (usados.Contains(JsLower(rel))) rel = (dir.Length > 0 ? dir + "/" : "") + nome + " (" + (n++).ToString(CultureInfo.InvariantCulture) + ")" + ext;
            usados.Add(JsLower(rel));
            notas[rel] = new JsonObject
            {
                ["x"] = b.X,
                ["y"] = b.Y,
                ["sprite"] = b.Sprite,
                ["tags"] = Truthy(b.Tags) ? b.Tags!.DeepClone() : new JsonArray(),
                ["anexos"] = Anexos(b.Anexos),
                ["criado"] = Truthy(b.Created) ? b.Created!.DeepClone() : today,
                ["modificado"] = Truthy(b.Modified) ? b.Modified!.DeepClone() : today,
                ["aiLocal"] = Truthy(b.AiLocal) ? b.AiLocal!.DeepClone() : null
            };
        }

        var construcoes = new JsonArray();
        var usadosBin = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in city.Buildings)
        {
            if (b.IsNote) continue;
            var fallbackDir = Dir(b.RegionId);
            var filesOut = new JsonArray();
            foreach (var a in Truthy(b.Files) ? Items(b.Files!) : [])
            {
                var folderPath = a is JsonObject ao && ao["folderPath"] is JsonValue fv && fv.TryGetValue<string>(out var fp) ? fp : null;
                var dir = folderPath ?? fallbackDir;
                var nome = FirstTruthyString(Prop(a, "nome"), b.FileName) ?? b.Name;
                filesOut.Add(MetaAssetLimpa(a, RelBinUnico(dir, nome, usadosBin)));
            }
            var pai = b.ParentNoteId is not null ? city.Buildings.Find(n => n.Id == b.ParentNoteId) : null;
            var fileClass = Truthy(b.FileClass) ? b.FileClass!.DeepClone() : JsonValue.Create("other");
            construcoes.Add(new JsonObject
            {
                ["id"] = Ensure(b),
                ["parentId"] = pai?.DocumentId is { Length: > 0 } pd ? pd : null,
                ["tipo"] = b.FileTipo?.DeepClone(),
                ["fileClass"] = fileClass,
                ["name"] = b.Name,
                ["fileName"] = Truthy(b.FileName) ? b.FileName!.DeepClone() : null,
                ["caminho"] = fallbackDir,
                ["x"] = b.X,
                ["y"] = b.Y,
                ["w"] = b.W != 0 ? b.W : 3,
                ["h"] = b.H != 0 ? b.H : 3,
                ["description"] = Truthy(b.FileDescription) ? b.FileDescription!.DeepClone() : "",
                ["sprite"] = b.Sprite.Length > 0 ? b.Sprite : "file-" + JsString(fileClass),
                ["parentNoteName"] = b.ParentNoteId is not null ? (pai?.Name is { Length: > 0 } pn ? pn : null) : null,
                ["files"] = filesOut,
                ["anexos"] = filesOut.DeepClone(),
                ["created"] = Truthy(b.Created) ? b.Created!.DeepClone() : today,
                ["modified"] = Truthy(b.Modified) ? b.Modified!.DeepClone() : today
            });
        }

        var regioes = new JsonArray();
        foreach (var r in city.Regions)
        {
            var pr = r.ParentId is not null ? city.Regions.Find(x => x.Id == r.ParentId) : null;
            JsonArray? cells = null;
            if (!r.CellsNull)
            {
                cells = new JsonArray();
                foreach (var (x, y) in r.CellList) cells.Add(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture));
            }
            regioes.Add(new JsonObject
            {
                ["id"] = Ensure(r),
                ["parentId"] = pr is not null ? Ensure(pr) : null,
                ["caminho"] = cam.TryGetValue(r.Id, out var c) ? c : null,
                ["nome"] = r.Name,
                ["cor"] = r.Color,
                ["x"] = r.X,
                ["y"] = r.Y,
                ["w"] = r.W,
                ["h"] = r.H,
                ["cells"] = cells,
                ["descricao"] = r.Description ?? ""
            });
        }

        return new JsonObject
        {
            ["v"] = 4,
            ["app"] = AppName,
            ["version"] = version,
            ["mundo"] = LegacyCity.WorldVersion,
            ["salvo"] = iso,
            ["camera"] = new JsonObject { ["x"] = cx, ["y"] = cy, ["z"] = cz },
            ["regioes"] = regioes,
            ["notas"] = notas,
            ["construcoes"] = construcoes
        };
    }

    /// <summary>UrbeStableIds.ensure(r,'reg').</summary>
    private string Ensure(LegacyCityRegion r)
    {
        if (!WorldStableIds.Valid("reg", r.Uid)) r.Uid = _freshId("reg");
        return r.Uid!;
    }

    /// <summary>UrbeStableIds.ensure(b,'ast').</summary>
    private string Ensure(LegacyCityBuilding b)
    {
        if (!WorldStableIds.Valid("ast", b.Uid)) b.Uid = _freshId("ast");
        return b.Uid!;
    }

    /// <summary>(b.anexos||[]).map(metaAnexoNota).</summary>
    private static JsonArray Anexos(JsonNode? anexos)
    {
        var list = new JsonArray();
        if (!Truthy(anexos)) return list;
        foreach (var a in Items(anexos!))
        {
            var o = Spread(a);
            o.Remove("_blob"); o.Remove("file"); o.Remove("handle"); o.Remove("dataUrl");
            list.Add(o);
        }
        return list;
    }

    /// <summary>metaAssetLimpa(a, rel): {...a, relPath:rel, folderPath:dirDe(rel)} without the in-memory fields.</summary>
    private static JsonObject MetaAssetLimpa(JsonNode? a, string rel)
    {
        var o = Spread(a);
        o["relPath"] = rel;
        int i = rel.LastIndexOf('/');
        o["folderPath"] = i < 0 ? "" : rel[..i];
        o.Remove("_blob"); o.Remove("file"); o.Remove("handle"); o.Remove("dataUrl");
        return o;
    }

    /// <summary>relBinUnico(dir, nome, usados): a unique (case-insensitive) relative path for a file.</summary>
    private static string RelBinUnico(string dir, string? nome, HashSet<string> usados)
    {
        var seguro = ArtifactModel.SafeName(string.IsNullOrEmpty(nome) ? "arquivo" : nome);
        int i = seguro.LastIndexOf('.');
        string stem = i > 0 ? seguro[..i] : seguro, ext = i > 0 ? seguro[i..] : "";
        int n = 1;
        var basename = seguro;
        var rel = (dir.Length > 0 ? dir + "/" : "") + basename;
        while (usados.Contains(JsLower(rel)))
        {
            n++;
            basename = stem + " (" + n.ToString(CultureInfo.InvariantCulture) + ")" + ext;
            rel = (dir.Length > 0 ? dir + "/" : "") + basename;
        }
        usados.Add(JsLower(rel));
        return rel;
    }

    /// <summary>{...a}: own enumerable properties (a string spreads its characters; other primitives nothing).</summary>
    private static JsonObject Spread(JsonNode? a)
    {
        var o = new JsonObject();
        switch (a)
        {
            case JsonObject src:
                foreach (var p in src) o[p.Key] = p.Value?.DeepClone();
                break;
            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++) o[i.ToString(CultureInfo.InvariantCulture)] = arr[i]?.DeepClone();
                break;
            case JsonValue v when v.TryGetValue<string>(out var s):
                for (int i = 0; i < s.Length; i++) o[i.ToString(CultureInfo.InvariantCulture)] = s[i].ToString();
                break;
        }
        return o;
    }

    private static IEnumerable<JsonNode?> Items(JsonNode node) => node is JsonArray arr ? arr : [];

    private static JsonNode? Prop(JsonNode? a, string name) => a is JsonObject o ? o[name] : null;

    private static string? FirstTruthyString(params JsonNode?[] values)
    {
        foreach (var v in values)
            if (Truthy(v)) return JsString(v);
        return null;
    }

    private static string JsString(JsonNode? v) =>
        v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : LegacyJson.Stringify(v);

    /// <summary>String.prototype.toLowerCase for the case-insensitive sets of app.js.</summary>
    private static string JsLower(string s) => s.ToLowerInvariant();

    /// <summary>JS truthiness of a JSON value.</summary>
    internal static bool Truthy(JsonNode? node) => node switch
    {
        null => false,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<System.Text.Json.JsonElement>(out var e) => LegacyCity.Truthy(e),
        JsonValue v when v.TryGetValue<double>(out var d) => d != 0 && !double.IsNaN(d),
        JsonValue v when v.TryGetValue<int>(out var i) => i != 0,
        _ => true
    };
}
