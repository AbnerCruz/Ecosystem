using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// The 1.8.4-beta "Versões anteriores" (src/core/history.js RevisionHistory) and its file .urbe/history.v2.json as
/// workspace.js writes it: for each document, the previous text of every change, except changes less than 5 s after
/// the last one (typing), up to 40 per note; a note sent to the trash keeps its last text marked removed. The file is
/// JSON.stringify({version: 2, documents}) with no spaces. Revisions read from the file are kept as they were.
/// </summary>
public sealed class LegacyHistory
{
    public const string Path = ".urbe/history.v2.json";
    public const string PathV1 = ".urbe/history.json";
    public const int Limit = 40;
    public const long WindowMs = 5000;

    public sealed record Revision(long Timestamp, string Path, string Content, bool Removed);

    private readonly List<(string Id, List<JsonObject> Revisions)> _byDoc = [];
    private readonly Dictionary<string, long> _last = new(StringComparer.Ordinal);

    /// <summary>
    /// workspace.js readSide: the v2 file (version 2), else the v1 file (version 1). A v2 file of another version or
    /// that is not JSON is preserved and never written: <paramref name="readOnly"/> is true and nothing is loaded.
    /// </summary>
    public static LegacyHistory Read(string? v2Json, string? v1Json, out bool readOnly)
    {
        readOnly = false;
        var history = new LegacyHistory();
        if (v2Json is not null)
        {
            if (Parse(v2Json) is { } root && IsVersion(root, 2)) history.Import(root);
            else readOnly = true;
            return history;
        }
        if (v1Json is not null && Parse(v1Json) is { } v1 && IsVersion(v1, 1)) history.Import(v1);
        return history;
    }

    /// <summary>
    /// capture (document:updated): the previous text is kept unless the last change of this document was less than
    /// 5 s ago and something is already kept; the time of this change becomes the last one. False when the text did
    /// not change.
    /// </summary>
    public bool Capture(string id, string previousPath, string previousContent, string content, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (string.Equals(previousContent, content, StringComparison.Ordinal)) return false;
        var revisions = Revisions(id);
        bool recent = _last.TryGetValue(id, out var last) && last != 0 && nowMs - last < WindowMs && revisions.Count > 0;
        bool kept = false;
        if (!recent)
        {
            revisions.Add(new JsonObject { ["timestamp"] = nowMs, ["path"] = previousPath, ["content"] = previousContent });
            kept = true;
        }
        if (revisions.Count > Limit) revisions.RemoveRange(0, revisions.Count - Limit);
        _last[id] = nowMs;
        return kept;
    }

    /// <summary>captureRemoved (document:removed): the note's last text, marked removed.</summary>
    public void CaptureRemoved(UrbeDocument d, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(d);
        var revisions = Revisions(d.Id);
        revisions.Add(new JsonObject { ["timestamp"] = nowMs, ["path"] = d.Path, ["content"] = d.Content, ["removed"] = true });
        if (revisions.Count > Limit) revisions.RemoveRange(0, revisions.Count - Limit);
    }

    /// <summary>list(id): newest first.</summary>
    public IReadOnlyList<Revision> List(string id)
    {
        int i = _byDoc.FindIndex(x => x.Id == id);
        if (i < 0) return [];
        return _byDoc[i].Revisions.AsEnumerable().Reverse().Select(r => new Revision(
            Timestamp(r["timestamp"]),
            Str(r["path"]) ?? "", Str(r["content"]) ?? "", r["removed"] is JsonValue v && v.TryGetValue<bool>(out var b) && b)).ToList();
    }

    /// <summary>
    /// gc.js plan (history part): the histories of documents that are neither alive nor in the trash and did not change
    /// for <paramref name="orphanDays"/> days (the last revision's timestamp; none counts as 0).
    /// </summary>
    public IReadOnlyList<(string Id, int Revisions, long? Last)> Orphans(Func<string, bool> known, long nowMs, int orphanDays = 30)
    {
        ArgumentNullException.ThrowIfNull(known);
        var result = new List<(string, int, long?)>();
        foreach (var (id, revisions) in _byDoc)
        {
            if (known(id)) continue;
            long last = revisions.Select(r => Timestamp(r["timestamp"])).DefaultIfEmpty(0).Max();
            if (nowMs - last >= orphanDays * 86_400_000L) result.Add((id, revisions.Count, last == 0 ? null : last));
        }
        return result;
    }

    /// <summary>history.forget(id): the versions kept for a document are dropped.</summary>
    public bool Forget(string id) => _byDoc.RemoveAll(x => x.Id == id) > 0;

    /// <summary>The text of .urbe/history.v2.json.</summary>
    public string Export()
    {
        var documents = new JsonObject();
        foreach (var (id, revisions) in _byDoc)
            documents[id] = new JsonArray(revisions.Select(r => (JsonNode)r.DeepClone()).ToArray());
        return LegacyJson.Stringify(new JsonObject { ["version"] = 2, ["documents"] = documents });
    }

    private List<JsonObject> Revisions(string id)
    {
        int i = _byDoc.FindIndex(x => x.Id == id);
        if (i >= 0) return _byDoc[i].Revisions;
        var list = new List<JsonObject>();
        _byDoc.Add((id, list));
        return list;
    }

    private void Import(JsonObject root)
    {
        _byDoc.Clear();
        _last.Clear();
        if (root["documents"] is not JsonObject documents) return;
        foreach (var (id, node) in documents)
        {
            var revisions = node is JsonArray arr ? arr.OfType<JsonObject>().Select(r => (JsonObject)r.DeepClone()).ToList() : [];
            if (revisions.Count > Limit) revisions.RemoveRange(0, revisions.Count - Limit);
            _byDoc.Add((id, revisions));
        }
    }

    /// <summary>A timestamp kept here (a long) or read from the file (a number).</summary>
    private static long Timestamp(JsonNode? v) => v switch
    {
        JsonValue x when x.TryGetValue<long>(out var l) => l,
        JsonValue x when x.TryGetValue<double>(out var d) => (long)d,
        _ => 0
    };

    private static string? Str(JsonNode? v) => v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;

    private static bool IsVersion(JsonObject root, int version) =>
        root["version"] is JsonValue v && v.TryGetValue<double>(out var d) && d == version;

    private static JsonObject? Parse(string json)
    {
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
