using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>A note renamed or moved outside the app, recognized by its content: same id, from → to.</summary>
public sealed record IdentityRename(string Id, string From, string To);

/// <summary>Identical notes that vanished and appeared together: they cannot be told apart and enter as new notes.</summary>
public sealed record IdentityAmbiguity(string Fingerprint, IReadOnlyList<string> From, IReadOnlyList<string> To);

/// <summary>What the reconciliation at the opening found.</summary>
public sealed record IdentityReconcile(IReadOnlyList<IdentityRename> Renames, IReadOnlyList<IdentityAmbiguity> Ambiguous);

/// <summary>
/// The reconciliation at the opening of the 1.8.4 (src/persistence/identity.js reconcile, pair and moveMeta; REQ-042):
/// a note whose path vanished from .urbe/identity.json while a note with the same content (a unique fingerprint on
/// both sides) appeared elsewhere was renamed or moved outside the app (Explorer, Obsidian, a sync tool), so it keeps
/// its id, its house, its region and its assets. The map (.urbe/mapa.json) is adjusted in memory; nothing is written.
/// Proved by csharp/tools/legacy-identity-oracle.mjs.
/// </summary>
public static class LegacyIdentity
{
    /// <summary>A note read from the disk: its path, its content and its id (null when the map has none).</summary>
    public sealed class Item(string path, string content, string? id)
    {
        public string Path { get; } = path;
        public string Content { get; } = content;
        public string? Id { get; set; } = id;
    }

    /// <summary>pair(vanished, appeared): the renames by a unique fingerprint, the rest ambiguous.</summary>
    public static (List<IdentityRename> Pairs, List<IdentityAmbiguity> Ambiguous) Pair(
        IEnumerable<(string Id, string Path, string Fingerprint)> vanished, IEnumerable<(string Path, string Fingerprint)> appeared)
    {
        var byOld = new Dictionary<string, List<(string Id, string Path)>>(StringComparer.Ordinal);
        var byNew = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var v in vanished)
        {
            if (string.IsNullOrEmpty(v.Fingerprint)) continue;
            if (!byOld.TryGetValue(v.Fingerprint, out var l)) byOld[v.Fingerprint] = l = [];
            l.Add((v.Id, v.Path));
        }
        foreach (var a in appeared)
        {
            if (string.IsNullOrEmpty(a.Fingerprint)) continue;
            if (!byNew.TryGetValue(a.Fingerprint, out var l)) { byNew[a.Fingerprint] = l = []; order.Add(a.Fingerprint); }
            l.Add(a.Path);
        }
        var pairs = new List<IdentityRename>();
        var ambiguous = new List<IdentityAmbiguity>();
        foreach (var fp in order)
        {
            var news = byNew[fp];
            if (!byOld.TryGetValue(fp, out var olds)) continue;
            if (olds.Count == 1 && news.Count == 1) pairs.Add(new IdentityRename(olds[0].Id, olds[0].Path, news[0]));
            else ambiguous.Add(new IdentityAmbiguity(fp, olds.Select(o => o.Path).ToList(), news.ToList()));
        }
        return (pairs, ambiguous);
    }

    /// <summary>
    /// reconcile(items, meta, identity, onDisk): ids from the sidecar for the same path, then the renames; the map
    /// follows them (moveMeta). <paramref name="items"/> get their ids set.
    /// </summary>
    public static IdentityReconcile Reconcile(IReadOnlyList<Item> items, JsonObject? meta, VaultIdentityState? identity, IReadOnlyCollection<string> onDisk)
    {
        var docs = identity?.Documents ?? new Dictionary<string, VaultIdentityEntry>();
        var present = new HashSet<string>(items.Select(i => i.Path), StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var i in items) if (i.Id is { } id) used.Add(id);
        // 1) the same path: the sidecar gives the id back when the map has none
        var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, e) in docs) byPath[e.Path] = id;
        foreach (var i in items)
            if (i.Id is null && byPath.TryGetValue(i.Path, out var id) && !used.Contains(id)) { i.Id = id; used.Add(id); }
        // 2) a path vanished + a new note with the same content = a rename/move outside
        var vanished = docs.Where(d => !used.Contains(d.Key) && !present.Contains(d.Value.Path)).Select(d => (d.Key, d.Value.Path, d.Value.Fingerprint)).ToList();
        var appeared = items.Where(i => i.Id is null).ToList();
        var (pairs, ambiguous) = Pair(vanished, appeared.Select(a => (a.Path, VaultIdentity.Fingerprint(a.Content))));
        var byNew = appeared.ToDictionary(a => a.Path, StringComparer.Ordinal);
        foreach (var p in pairs) { byNew[p.To].Id = p.Id; used.Add(p.Id); }
        if (meta is not null && pairs.Count > 0) MoveMeta(meta, pairs, onDisk.Count > 0 ? onDisk : present);
        return new IdentityReconcile(pairs, ambiguous);
    }

    private static string DirOf(string p)
    {
        var i = p.LastIndexOf('/');
        return i > 0 ? p[..i] : "";
    }

    /// <summary>
    /// moveMeta: the house, the region and the assets follow the renamed note. A whole folder renamed becomes the same
    /// region (same reg_, same shape) when the old path vanished and the new one has no region. The house keeps its
    /// place only when the note stays in the same quarter; moved to another folder, it keeps id and metadata and gets
    /// a place in the new quarter.
    /// </summary>
    private static void MoveMeta(JsonObject meta, List<IdentityRename> pairs, IReadOnlyCollection<string> onDisk)
    {
        var notas = meta["notas"] as JsonObject;
        var regioes = meta["regioes"] as JsonArray ?? [];
        bool ExistsDir(string dir)
        {
            var pre = dir + "/";
            foreach (var p in onDisk)
                if (p.StartsWith(pre, StringComparison.Ordinal) && !p[pre.Length..].Split('/').Any(x => x.StartsWith('.'))) return true;
            return false;
        }
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var regionPaths = new HashSet<string?>(regioes.Select(r => r is JsonObject o ? Str(o["caminho"]) : null), StringComparer.Ordinal);
        var dirMoves = new List<(string From, string To)>();
        foreach (var p in pairs)
        {
            var a = p.From.Split('/').ToList();
            var b = p.To.Split('/').ToList();
            a.RemoveAt(a.Count - 1);
            b.RemoveAt(b.Count - 1);
            // a folder renamed = equal suffixes; the prefix that differs is what was renamed (A/B/x → C/B/x: A → C)
            while (a.Count > 0 && b.Count > 0 && a[^1] == b[^1]) { a.RemoveAt(a.Count - 1); b.RemoveAt(b.Count - 1); }
            string from = string.Join('/', a), to = string.Join('/', b);
            if (from.Length == 0 || to.Length == 0 || from == to) continue;
            if (!regionPaths.Contains(from) || regionPaths.Contains(to) || ExistsDir(from)) continue;
            var at = dirMoves.FindIndex(m => m.From == from);
            if (at >= 0) dirMoves[at] = (from, to); else dirMoves.Add((from, to));
        }
        string Mv(string path)
        {
            foreach (var (f, t) in dirMoves)
            {
                if (path == f) return t;
                if (path.StartsWith(f + "/", StringComparison.Ordinal)) return t + path[f.Length..];
            }
            return path;
        }
        foreach (var p in pairs)
        {
            if (notas is null || notas[p.From] is not { } node || !LegacyJsTruthy(node) || notas[p.To] is { } taken && LegacyJsTruthy(taken)) continue;
            notas.Remove(p.From);
            if (Mv(DirOf(p.From)) != DirOf(p.To) && node is JsonObject o)
            {
                var copy = (JsonObject)o.DeepClone();
                copy.Remove("x");
                copy.Remove("y");
                node = copy;
            }
            notas[p.To] = node;
        }
        if (dirMoves.Count == 0) return;
        // the renamed folder itself changes its name (the world derives the path from the name); the inner ones only the prefix
        foreach (var r in regioes.OfType<JsonObject>())
        {
            if (Str(r["caminho"]) is not { } caminho) continue;
            var move = dirMoves.FindIndex(m => m.From == caminho);
            if (move >= 0) r["nome"] = dirMoves[move].To.Split('/')[^1];
            r["caminho"] = Mv(caminho);
        }
        void FixFiles(JsonNode? list)
        {
            if (list is not JsonArray arr) return;
            foreach (var f in arr.OfType<JsonObject>())
            {
                if (Str(f["relPath"]) is { Length: > 0 } rel) f["relPath"] = Mv(rel);
                if (Str(f["folderPath"]) is { } folder) f["folderPath"] = Mv(folder);
            }
        }
        if (meta["construcoes"] is JsonArray construcoes)
            foreach (var c in construcoes.OfType<JsonObject>())
            {
                if (Str(c["caminho"]) is { } caminho) c["caminho"] = Mv(caminho);
                FixFiles(c["files"]);
                FixFiles(c["anexos"]);
            }
        if (notas is not null)
            foreach (var (_, n) in notas)
                if (n is JsonObject o) FixFiles(o["anexos"]);
    }

    // notas[p.from] in JS: a present, truthy value (an object; null/false/0/"" are skipped)
    private static bool LegacyJsTruthy(JsonNode node) => node is not JsonValue v ||
        !(v.TryGetValue<bool>(out var b) && !b) && !(v.TryGetValue<double>(out var d) && (d == 0 || double.IsNaN(d))) && !(v.TryGetValue<string>(out var s) && s.Length == 0);
}
