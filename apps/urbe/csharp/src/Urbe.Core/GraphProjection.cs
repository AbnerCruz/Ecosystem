namespace Urbe.Core;

/// <summary>
/// Read-only projection of the real vault hierarchy and resolved wiki links. The returned graph
/// is an editable snapshot, not the filesystem authority: moving a card never moves a folder.
/// Callers may export/save the snapshot only after obtaining the user's intent.
/// </summary>
public static class GraphProjection
{
    public static GraphDocument FromVault(
        IEnumerable<UrbeDocument> documents,
        IEnumerable<string> filePaths,
        IEnumerable<string> directoryPaths,
        Func<string, IEnumerable<string>> linkedDocumentIds,
        string? scope = null,
        bool links = true)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(directoryPaths);
        ArgumentNullException.ThrowIfNull(linkedDocumentIds);
        var root = (scope ?? "").Trim('/');
        if (root.StartsWith(".", StringComparison.Ordinal) ||
            root.Split('/').Any(segment => segment is "." or ".."))
            throw new InvalidDataException("Escopo inválido.");

        bool Inside(string path) => root.Length == 0 ||
            string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        bool Valid(string path) => path.Length > 0 && !path.StartsWith("/", StringComparison.Ordinal) &&
            !path.Contains('\\') && !path.Contains(':') && path.Split('/').All(s =>
                s.Length > 0 && s is not "." and not ".." && !s.StartsWith(".", StringComparison.Ordinal));
        var docs = documents.Where(d => Valid(d.Path) && Inside(d.Path))
            .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var files = filePaths.Where(p => Valid(p) && Inside(p) && !GraphDocument.IsGraphPath(p))
            .Except(docs.Select(d => d.Path), StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Parents(string path)
        {
            var position = path.LastIndexOf('/');
            while (position > 0)
            {
                var folder = path[..position];
                if (Inside(folder)) dirs.Add(folder);
                position = folder.LastIndexOf('/');
            }
        }
        foreach (var d in docs) Parents(d.Path);
        foreach (var file in files) Parents(file);
        foreach (var dir in directoryPaths.Where(Valid).Where(Inside))
        {
            dirs.Add(dir);
            Parents(dir);
        }
        if (root.Length > 0) dirs.Add(root);
        var items = new List<(string Path, GraphNodeKind Kind, string? Id)>();
        items.AddRange(dirs.Select(p => (p, GraphNodeKind.Folder, (string?)null)));
        items.AddRange(docs.Select(d => (d.Path, GraphNodeKind.Note, (string?)d.Id)));
        items.AddRange(files.Select(p => (p, GraphNodeKind.File, (string?)null)));
        // The renderer culls offscreen objects. Do not reject a vault based on an arbitrary
        // item count: users may select narrower scopes for convenience, never as a requirement.
        items.Sort((a, b) =>
        {
            var byPath = StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
            return byPath != 0 ? byPath : a.Kind.CompareTo(b.Kind);
        });
        var graph = GraphDocument.Create(root.Length == 0 ? "Estrutura do vault" : "Estrutura de " + root);
        var byPath = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var byNoteId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var node = graph.AddReference(item.Kind, item.Path, 0, 0, item.Id, recordHistory: false);
            byPath[item.Path] = node;
            if (item.Id is not null) byNoteId[item.Id] = node;
        }
        // Blocks, not one long column: each folder sits left of its files and notes laid out as a near-square grid,
        // its subfolders below them; the top-level blocks are packed in shelves towards a 16:9 view. A vault of
        // thousands of notes reads as a map instead of a line hundreds of screens tall.
        var children = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var cut = item.Path.LastIndexOf('/');
            var parent = cut < 0 ? "" : item.Path[..cut];
            if (!byPath.ContainsKey(parent)) parent = "";
            if (!children.TryGetValue(parent, out var siblings)) children[parent] = siblings = [];
            siblings.Add(item.Path);
        }
        const double Col = 266, Row = 116;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // the leaves as a grid about as wide as tall (a card is ~2.3 times wider than high), then each subfolder's block
        (double W, double H) Contents(List<string> kids, double x, double y)
        {
            var leaves = kids.Where(k => children.GetValueOrDefault(k) is not { Count: > 0 }).ToList();
            int cols = leaves.Count == 0 ? 0 : (int)Math.Max(1, Math.Ceiling(Math.Sqrt(leaves.Count * Row / Col)));
            int rows = cols == 0 ? 0 : (leaves.Count + cols - 1) / cols;
            for (int i = 0; i < leaves.Count; i++) Put(leaves[i], x + i % cols * Col, y + i / cols * Row);
            double w = cols * Col, h = rows * Row;
            foreach (var folder in kids.Except(leaves))
            {
                var (bw, bh) = Block(folder, x, y + h);
                w = Math.Max(w, bw);
                h += bh;
            }
            return (w, h);
        }
        (double W, double H) Block(string path, double x, double y)
        {
            Put(path, x, y);
            var (w, h) = Contents(children.GetValueOrDefault(path) ?? [], x + Col, y);
            return (Col + w, Math.Max(Row, h) + Row / 2);
        }
        void Put(string path, double x, double y)
        {
            if (!visited.Add(path)) throw new InvalidDataException("Hierarquia com ciclo.");
            graph.MoveNode(byPath[path].Id, x, y, recordHistory: false);
        }
        var top = children.GetValueOrDefault("") ?? [];
        var topLeaves = top.Where(k => children.GetValueOrDefault(k) is not { Count: > 0 }).ToList();
        double shelfX = 0, shelfY = 0, shelfH = 0;
        if (topLeaves.Count > 0) { var (_, h) = Contents(topLeaves, 0, 0); shelfY = h + Row; }
        var blocks = top.Except(topLeaves).ToList();
        // the shelf width that makes the whole roughly 16:9, from the blocks' total area
        double area = items.Count * Col * Row * 1.4;
        double shelf = Math.Max(Col * 4, Math.Sqrt(area * 16 / 9));
        foreach (var folder in blocks)
        {
            var (w, h) = Block(folder, shelfX, shelfY);
            if (shelfX > 0 && shelfX + w > shelf)
            {
                // too wide for this shelf: the block starts the next one
                visited.RemoveWhere(p => p == folder || p.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
                shelfY += shelfH + Row;
                shelfX = 0;
                shelfH = 0;
                (w, h) = Block(folder, shelfX, shelfY);
            }
            shelfX += w + Col / 2;
            shelfH = Math.Max(shelfH, h);
        }

        foreach (var item in items)
        {
            var end = item.Path.LastIndexOf('/');
            if (end < 0) continue;
            var parent = item.Path[..end];
            if (byPath.TryGetValue(parent, out var parentNode) &&
                byPath.TryGetValue(item.Path, out var childNode))
                graph.Connect(parentNode.Id, childNode.Id, relation: "contains",
                    directed: true, recordHistory: false);
        }
        if (links)
            foreach (var note in docs)
            {
                if (!byNoteId.TryGetValue(note.Id, out var from)) continue;
                foreach (var toId in linkedDocumentIds(note.Id).Distinct(StringComparer.Ordinal))
                    if (byNoteId.TryGetValue(toId, out var to) && from.Id != to.Id)
                        graph.Connect(from.Id, to.Id, relation: "wikilink",
                            directed: true, recordHistory: false);
            }
        return graph;
    }
}
