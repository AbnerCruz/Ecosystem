using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// The 1.8.4-beta trash (src/core/trash.js TrashStore) and its file .urbe/trash.v2.json as workspace.js writes it:
/// items kept by document id (a later item of the same id takes the place of the first), listed by deletedAt,
/// newest first; the file is JSON.stringify({...export(), version: 2}, null, 1). Items read from the file are kept
/// as they were (their JSON), so deleting a note never rewrites what was already in the trash.
/// </summary>
public sealed class LegacyTrash
{
    /// <summary>.urbe/trash.v2.json, the only trash file the 1.8.4 writes.</summary>
    public const string Path = ".urbe/trash.v2.json";

    /// <summary>.urbe/trash.json of the 1.x, read once when there is no v2 file and never written.</summary>
    public const string PathV1 = ".urbe/trash.json";

    private readonly List<(string Id, JsonObject Item)> _items = [];

    /// <summary>
    /// workspace.js readSide for the trash: the v2 file when present (version 2), otherwise the v1 file (version 1 or
    /// none). Null when there is no trash. A v2 file of another version, or one that is not JSON, is preserved by the
    /// 1.8.4 and nothing is written to it: <paramref name="readOnly"/> is true and no item is loaded.
    /// </summary>
    public static LegacyTrash Read(string? v2Json, string? v1Json, out bool readOnly)
    {
        readOnly = false;
        var trash = new LegacyTrash();
        if (v2Json is not null)
        {
            var root = Parse(v2Json);
            if (root is null || !IsVersion(root, 2)) { readOnly = true; return trash; }
            trash.Import(root);
            return trash;
        }
        if (v1Json is not null && Parse(v1Json) is { } v1 && (IsVersion(v1, 1) || !v1.ContainsKey("version")))
            trash.Import(v1);
        return trash;
    }

    /// <summary>The items, newest first (list()).</summary>
    public IReadOnlyList<JsonObject> Items => List().Select(i => i.Item).ToList();

    /// <summary>
    /// trash.trash(id): the document as it is now ({...d, properties, tags, links}), deletedAt=Date.now() and its
    /// path, then removed from the store. Null when the store has no such document.
    /// </summary>
    public JsonObject? Trash(DocumentStore store, string idOrPath, long nowMs, object? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.Get(idOrPath) is not { } d) return null;
        var item = Item(d, nowMs);
        Set(d.Id, item);
        store.Remove(d.Id, metadata ?? "trash");
        return item;
    }

    /// <summary>
    /// trash.restore(id): the document back in the store at its original path ({...item.document, id, path}); refused
    /// when a document already has that path ("Já existe um documento em …"). Null when the trash has no such item.
    /// </summary>
    public UrbeDocument? Restore(DocumentStore store, string id)
    {
        ArgumentNullException.ThrowIfNull(store);
        int i = _items.FindIndex(x => x.Id == id);
        if (i < 0) return null;
        var item = _items[i].Item;
        var doc = item["document"] as JsonObject ?? new JsonObject();
        var path = Str(item["originalPath"]) ?? Str(doc["path"]) ?? "";
        if (store.Get(path) is not null) throw new InvalidOperationException("Já existe um documento em " + path);
        var restored = store.Upsert(new DocumentInput
        {
            Id = Str(doc["id"]) ?? id,
            Path = path,
            Title = Str(doc["title"]),
            Content = Str(doc["content"]) ?? "",
            Properties = doc["properties"] is JsonObject props ? props.ToDictionary(p => p.Key, p => Str(p.Value) ?? "") : null,
            Tags = doc["tags"] is JsonArray tags ? tags.Select(t => Str(t) ?? "").ToList() : null,
            Links = doc["links"] is JsonArray links ? links.Select(t => Str(t) ?? "").ToList() : null,
            Created = Str(doc["created"]),
            Modified = Str(doc["modified"])
        }, "trash.restore");
        _items.RemoveAt(i);
        return restored;
    }

    /// <summary>trash.purge(id): the item leaves the trash for good.</summary>
    public bool Purge(string id)
    {
        int i = _items.FindIndex(x => x.Id == id);
        if (i < 0) return false;
        _items.RemoveAt(i);
        return true;
    }

    /// <summary>trash.clear() (Esvaziar).</summary>
    public void Clear() => _items.Clear();

    /// <summary>The trash as it would be after <paramref name="change"/>, without changing this one.</summary>
    public string ExportAfter(Action<LegacyTrash> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var copy = new LegacyTrash();
        copy._items.AddRange(_items.Select(x => (x.Id, (JsonObject)x.Item.DeepClone())));
        change(copy);
        return copy.Export();
    }

    private static string? Str(JsonNode? v) => v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;

    /// <summary>The text of .urbe/trash.v2.json once <paramref name="d"/> is trashed at <paramref name="nowMs"/>, without
    /// changing this trash (what must be on disk before the document's file is removed).</summary>
    /// <summary>UC-65: the trash as it will be with all of <paramref name="docs"/> in it (one copy for the whole batch).</summary>
    public string ExportWith(IEnumerable<UrbeDocument> docs, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(docs);
        var copy = new LegacyTrash();
        copy._items.AddRange(_items.Select(x => (x.Id, (JsonObject)x.Item.DeepClone())));
        foreach (var d in docs) copy.Set(d.Id, Item(d, nowMs));
        return copy.Export();
    }

    public string ExportWith(UrbeDocument d, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(d);
        var copy = new LegacyTrash();
        copy._items.AddRange(_items.Select(x => (x.Id, (JsonObject)x.Item.DeepClone())));
        copy.Set(d.Id, Item(d, nowMs));
        return copy.Export();
    }

    /// <summary>{document:{...d, properties:{...}, tags:[...], links:[...]}, deletedAt, originalPath}.</summary>
    private static JsonObject Item(UrbeDocument d, long nowMs)
    {
        var properties = new JsonObject();
        foreach (var p in d.Properties) properties[p.Key] = p.Value;
        var tags = new JsonArray();
        foreach (var t in d.Tags) tags.Add(t);
        var links = new JsonArray();
        foreach (var l in d.Links) links.Add(l);
        return new JsonObject
        {
            ["document"] = new JsonObject
            {
                ["id"] = d.Id,
                ["path"] = d.Path,
                ["title"] = d.Title,
                ["content"] = d.Content,
                ["properties"] = properties,
                ["tags"] = tags,
                ["links"] = links,
                ["created"] = d.Created,
                ["modified"] = d.Modified,
                ["revision"] = d.Revision
            },
            ["deletedAt"] = nowMs,
            ["originalPath"] = d.Path
        };
    }

    /// <summary>The text of .urbe/trash.v2.json the 1.8.4 writes for this trash.</summary>
    public string Export()
    {
        var items = new JsonArray();
        foreach (var (_, item) in List()) items.Add(item.DeepClone());
        return LegacyJson.Stringify(new JsonObject { ["version"] = 2, ["items"] = items }, 1);
    }

    private void Import(JsonObject root)
    {
        _items.Clear();
        if (root["items"] is not JsonArray items) return;
        foreach (var node in items)
            if (node is JsonObject item && item["document"] is JsonObject doc && Pages.PageEngine.Truthy(doc["id"]))
                Set(JsString(doc["id"]), (JsonObject)item.DeepClone());
    }

    /// <summary>Map.set: an id already there keeps its place and takes the new item.</summary>
    private void Set(string id, JsonObject item)
    {
        int i = _items.FindIndex(x => x.Id == id);
        if (i >= 0) _items[i] = (id, item);
        else _items.Add((id, item));
    }

    /// <summary>list(): sorted by deletedAt, newest first (Array.prototype.sort is stable).</summary>
    private IEnumerable<(string Id, JsonObject Item)> List() =>
        _items.Select((x, i) => (x, i)).OrderByDescending(p => DeletedAt(p.x.Item)).ThenBy(p => p.i).Select(p => p.x);

    /// <summary>b.deletedAt-a.deletedAt with a missing or non-numeric value as NaN (the comparator then says 0).</summary>
    private static double DeletedAt(JsonObject item) => item["deletedAt"] switch
    {
        JsonValue v when v.TryGetValue<long>(out var l) => l,
        JsonValue v when v.TryGetValue<double>(out var d) => d,
        _ => 0
    };

    private static bool IsVersion(JsonObject root, int version) =>
        root["version"] is JsonValue v && v.TryGetValue<double>(out var d) && d == version;

    private static JsonObject? Parse(string json)
    {
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private static string JsString(JsonNode? v) =>
        v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : LegacyJson.Stringify(v);
}
