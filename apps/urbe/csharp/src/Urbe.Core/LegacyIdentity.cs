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
/// its id (and with it its history, its place in compositions and its dates). Nothing is written. The 1.8.4 also moved
/// the note's house in the city map (moveMeta); the city left the app (ADD-0028).
/// Proved by csharp/tools/legacy-identity-oracle.mjs.
/// </summary>
public static class LegacyIdentity
{
    /// <summary>A note read from the disk: its path, its content and its id (null when the vault has none for it).</summary>
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
    /// reconcile(items, meta, identity, onDisk) without the map: ids from the sidecar for the same path, then the
    /// renames. <paramref name="items"/> get their ids set.
    /// </summary>
    public static IdentityReconcile Reconcile(IReadOnlyList<Item> items, VaultIdentityState? identity)
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
        return new IdentityReconcile(pairs, ambiguous);
    }
}
