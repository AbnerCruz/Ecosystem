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
        // Prevent quadratic memory use on huge vaults and make the scope restriction explicit.
        if (items.Count > 6000)
            throw new InvalidDataException("Escopo grande demais para um só canvas. Escolha uma pasta menor.");
        items.Sort((a, b) =>
        {
            var byPath = StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
            return byPath != 0 ? byPath : a.Kind.CompareTo(b.Kind);
        });
        var graph = GraphDocument.Create(root.Length == 0 ? "Estrutura do vault" : "Estrutura de " + root);
        var byPath = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var byNoteId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        int row = 0;
        foreach (var item in items)
        {
            var depth = item.Path.Count(ch => ch == '/') - (root.Length > 0 ? root.Count(ch => ch == '/') + 1 : 0);
            depth = Math.Max(0, depth);
            var node = graph.AddReference(item.Kind, item.Path, depth * 265, row++ * 112, item.Id, recordHistory: false);
            byPath[item.Path] = node;
            if (item.Id is not null) byNoteId[item.Id] = node;
        }
        // Tidy hierarchy: leaves take unique rows; folder parents sit at their children's
        // vertical midpoint. This avoids long crossing lines produced by a flat list layout.
        var children = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var cut = item.Path.LastIndexOf('/');
            var parent = cut < 0 ? "" : item.Path[..cut];
            if (!byPath.ContainsKey(parent)) parent = "";
            if (!children.TryGetValue(parent, out var siblings))
            {
                siblings = [];
                children[parent] = siblings;
            }
            siblings.Add(item.Path);
        }
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int leaf = 0;
        double Place(string path, int depth)
        {
            if (!visited.Add(path)) throw new InvalidDataException("Hierarquia com ciclo.");
            var child = children.GetValueOrDefault(path);
            double y;
            if (child is { Count: > 0 })
            {
                double first = 0, last = 0;
                for (var index = 0; index < child.Count; index++)
                {
                    var childY = Place(child[index], depth + 1);
                    if (index == 0) first = childY;
                    last = childY;
                }
                y = (first + last) / 2;
            }
            else y = (leaf++) * 116;
            graph.MoveNode(byPath[path].Id, depth * 266, y, recordHistory: false);
            return y;
        }
        foreach (var path in children.GetValueOrDefault("") ?? [])
            Place(path, 0);
        foreach (var item in items)
            if (!visited.Contains(item.Path)) Place(item.Path, 0);

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
