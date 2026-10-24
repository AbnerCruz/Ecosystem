using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core;

/// <summary>
/// A user-authored visual graph. Unlike the derived Constelação, this JSON file is the authority
/// for positions, labels and explicit relationships. A reference points to a vault object, never owns it.
/// A card removed from a graph MUST NOT delete a vault file.
/// </summary>
public sealed class GraphDocument
{
    public const int CurrentVersion = 1;
    public const string FileSuffix = ".grafo.json";
    public const string TypeName = "urbe.graph";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];
    private readonly List<GraphStroke> _strokes = [];
    // Delta-based history: an edit stores only the entities it touched, never a full JSON
    // copy of the canvas. No fixed action quota: memory grows with actual edits.
    private sealed record Edit(Action Undo, Action Redo);
    private readonly Stack<Edit> _undo = new();
    private readonly Stack<Edit> _redo = new();
    private readonly Dictionary<string, int> _nodeIndex = new(StringComparer.Ordinal);
    private bool _dragActive;
    private readonly Dictionary<string, GraphNode> _dragBefore = new(StringComparer.Ordinal);

    public IReadOnlyList<GraphStroke> Strokes => _strokes.AsReadOnly();
    public bool CanUndo => _undo.Count > 0 || _dragActive && _dragBefore.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    private void Record(Action undo, Action redo)
    {
        _undo.Push(new Edit(undo, redo));
        _redo.Clear();
    }

    private int IndexOfNode(string id) => _nodeIndex.TryGetValue(id, out var index) ? index : -1;
    private void ReindexNodes()
    {
        _nodeIndex.Clear();
        for (var i = 0; i < _nodes.Count; i++) _nodeIndex[_nodes[i].Id] = i;
    }

    private void PutNode(GraphNode node)
    {
        var at = IndexOfNode(node.Id);
        if (at >= 0) _nodes[at] = node;
        else { _nodeIndex[node.Id] = _nodes.Count; _nodes.Add(node); }
    }

    private void DeleteNode(string id)
    {
        var at = IndexOfNode(id);
        if (at < 0) return;
        _nodes.RemoveAt(at);
        _nodeIndex.Remove(id);
        // Reindex only the shifted suffix. Removing the last-added node during undo
        // is O(1), instead of forcing a scan of the entire graph.
        for (var i = at; i < _nodes.Count; i++) _nodeIndex[_nodes[i].Id] = i;
    }

    private void PutEdge(GraphEdge edge)
    {
        var at = _edges.FindIndex(e => e.Id == edge.Id);
        if (at >= 0) _edges[at] = edge;
        else _edges.Add(edge);
    }

    private void PutStroke(GraphStroke stroke)
    {
        var at = _strokes.FindIndex(s => s.Id == stroke.Id);
        if (at >= 0) _strokes[at] = stroke;
        else _strokes.Add(stroke);
    }

    private void ApplyNodes(IReadOnlyDictionary<string, GraphNode?> change)
    {
        // Two passes ensure parent groups are restored before children.
        foreach (var pair in change.Where(p => p.Value is null)) DeleteNode(pair.Key);
        foreach (var pair in change.Where(p => p.Value is not null)) PutNode(pair.Value!);
    }

    private void RecordNodes(
        IReadOnlyDictionary<string, GraphNode?> before,
        IReadOnlyDictionary<string, GraphNode?> after,
        IReadOnlyList<GraphEdge>? removedEdges = null)
    {
        Record(
            () =>
            {
                ApplyNodes(before);
                if (removedEdges is not null) foreach (var edge in removedEdges) PutEdge(edge);
            },
            () =>
            {
                ApplyNodes(after);
                if (removedEdges is not null)
                    foreach (var edge in removedEdges) _edges.RemoveAll(e => e.Id == edge.Id);
            });
    }

    public bool Undo()
    {
        EndDrag();
        if (!_undo.TryPop(out var edit)) return false;
        edit.Undo();
        _redo.Push(edit);
        return true;
    }

    public bool Redo()
    {
        EndDrag();
        if (!_redo.TryPop(out var edit)) return false;
        edit.Redo();
        _undo.Push(edit);
        return true;
    }

    /// <summary>Collects only the moving node (and children of its group) for a single undo step.</summary>
    public void BeginDrag()
    {
        EndDrag();
        _dragActive = true;
    }

    public void EndDrag()
    {
        if (!_dragActive) return;
        _dragActive = false;
        if (_dragBefore.Count == 0) return;
        var before = _dragBefore.ToDictionary(x => x.Key, x => (GraphNode?)x.Value, StringComparer.Ordinal);
        _dragBefore.Clear();
        var after = before.Keys.ToDictionary(id => id, id => (GraphNode?)Node(id), StringComparer.Ordinal);
        if (before.Keys.Any(id => !Equals(before[id], after[id]))) RecordNodes(before, after);
    }

    public string Id { get; private set; } = "graph_" + Guid.NewGuid().ToString("N");
    public string Title { get; private set; } = "Novo grafo";
    public IReadOnlyList<GraphNode> Nodes => _nodes.AsReadOnly();
    public IReadOnlyList<GraphEdge> Edges => _edges.AsReadOnly();

    public static bool IsGraphPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase);

    public static GraphDocument Create(string title) => new() { Title = CleanLabel(title, "Novo grafo") };

    public GraphNode AddText(string? text, double x = 0, double y = 0) =>
        AddNode(GraphNodeKind.Text, text ?? "", null, x, y);

    public GraphNode AddReference(GraphNodeKind kind, string vaultPath, double x = 0, double y = 0, string? referenceId = null, bool recordHistory = true)
    {
        if (kind is not (GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder))
            throw new ArgumentOutOfRangeException(nameof(kind), "A referência deve apontar para uma nota, arquivo ou pasta.");
        ValidateTarget(vaultPath);
        var label = Path.GetFileName(vaultPath.TrimEnd('/'));
        return AddNode(kind, label, vaultPath, x, y, referenceId: referenceId, recordHistory: recordHistory);
    }

    public GraphNode AddNode(GraphNodeKind kind, string? label, string? target, double x, double y,
        double width = 190, double height = 84, string? referenceId = null, bool recordHistory = true)
    {
        if (target is not null) ValidateTarget(target);
        if (kind is GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder && target is null)
            throw new ArgumentException("Um nó de referência exige um alvo.");
        CheckGeometry(x, y, width, height);
        var node = new GraphNode("node_" + Guid.NewGuid().ToString("N"), kind, CleanLabel(label, ""), target,
            x, y, width, height, referenceId);
        if (recordHistory) RecordNodes(
            new Dictionary<string, GraphNode?> { [node.Id] = null },
            new Dictionary<string, GraphNode?> { [node.Id] = node });
        PutNode(node);
        return node;
    }

    public GraphEdge Connect(string sourceId, string targetId, string? label = null, string? relation = null,
        bool directed = false, bool recordHistory = true)
    {
        if (sourceId == targetId || IndexOfNode(sourceId) < 0 || IndexOfNode(targetId) < 0)
            throw new ArgumentException("Selecione dois elementos distintos que pertencem ao grafo.");
        var edge = new GraphEdge("edge_" + Guid.NewGuid().ToString("N"), sourceId, targetId,
            CleanLabel(label, ""), CleanLabel(relation, ""), directed);
        if (recordHistory) Record(
            () => _edges.RemoveAll(e => e.Id == edge.Id),
            () => PutEdge(edge));
        _edges.Add(edge);
        return edge;
    }

    public bool MoveNode(string id, double x, double y, bool recordHistory = true)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x));
        if (recordHistory) EndDrag();
        var at = IndexOfNode(id);
        if (at < 0) return false;
        var original = _nodes[at];
        if (original.X == x && original.Y == y) return false;
        var affected = original.Kind == GraphNodeKind.Group
            ? _nodes.Where(n => n.Id == id || n.ParentId == id).ToArray()
            : [original];
        var before = affected.ToDictionary(n => n.Id, n => (GraphNode?)n, StringComparer.Ordinal);
        if (_dragActive && !recordHistory)
            foreach (var node in affected) _dragBefore.TryAdd(node.Id, node);
        var dx = x - original.X;
        var dy = y - original.Y;
        foreach (var node in affected)
        {
            var replacement = node.Id == id ? node with { X = x, Y = y }
                : node with { X = node.X + dx, Y = node.Y + dy };
            PutNode(replacement);
        }
        if (recordHistory)
        {
            var after = affected.ToDictionary(n => n.Id, n => (GraphNode?)Node(n.Id), StringComparer.Ordinal);
            RecordNodes(before, after);
        }
        return true;
    }

    public bool RenameNode(string id, string? text)
    {
        var at = IndexOfNode(id);
        if (at < 0) return false;
        var next = CleanLabel(text, "");
        var old = _nodes[at];
        if (old.Label == next) return false;
        var updated = old with { Label = next };
        RecordNodes(
            new Dictionary<string, GraphNode?> { [id] = old },
            new Dictionary<string, GraphNode?> { [id] = updated });
        PutNode(updated);
        return true;
    }

    public bool RemoveNode(string id)
    {
        var node = Node(id);
        if (node is null) return false;
        var affected = _nodes.Where(n => n.Id == id || n.ParentId == id).ToArray();
        var before = affected.ToDictionary(n => n.Id, n => (GraphNode?)n, StringComparer.Ordinal);
        var after = affected.ToDictionary(n => n.Id,
            n => n.Id == id ? null : (GraphNode?)(n with { ParentId = null }), StringComparer.Ordinal);
        var edges = _edges.Where(e => e.SourceId == id || e.TargetId == id).ToArray();
        RecordNodes(before, after, edges);
        ApplyNodes(after);
        foreach (var edge in edges) _edges.Remove(edge);
        return true;
    }

    public bool RemoveEdge(string id)
    {
        var edge = _edges.FirstOrDefault(e => e.Id == id);
        if (edge is null) return false;
        Record(() => PutEdge(edge), () => _edges.RemoveAll(e => e.Id == id));
        _edges.Remove(edge);
        return true;
    }

    public bool ResizeNode(string id, double width, double height)
    {
        var at = IndexOfNode(id);
        if (at < 0) return false;
        var old = _nodes[at];
        CheckGeometry(old.X, old.Y, width, height);
        if (old.Width == width && old.Height == height) return false;
        var updated = old with { Width = width, Height = height };
        RecordNodes(
            new Dictionary<string, GraphNode?> { [id] = old },
            new Dictionary<string, GraphNode?> { [id] = updated });
        PutNode(updated);
        return true;
    }

    public GraphNode GroupNodes(IEnumerable<string> ids, string? label = null)
    {
        var selected = ids.Distinct(StringComparer.Ordinal).Select(Node).ToArray();
        if (selected.Length < 2 || selected.Any(n => n is null || n.Kind == GraphNodeKind.Group || n.ParentId is not null))
            throw new ArgumentException("Selecione pelo menos dois elementos livres, sem grupos aninhados.");
        var items = selected.Select(n => n!).ToArray();
        var x = items.Min(n => n.X) - 20;
        var y = items.Min(n => n.Y) - 38;
        var width = items.Max(n => n.X + n.Width) - x + 20;
        var height = items.Max(n => n.Y + n.Height) - y + 20;
        CheckGeometry(x, y, width, height);
        var group = AddNode(GraphNodeKind.Group, label ?? "Grupo", null, x, y, width, height,
            recordHistory: false);
        var before = items.ToDictionary(n => n.Id, n => (GraphNode?)n, StringComparer.Ordinal);
        before.Add(group.Id, null);
        var after = items.ToDictionary(n => n.Id,
            n => (GraphNode?)(n with { ParentId = group.Id }), StringComparer.Ordinal);
        after.Add(group.Id, group);
        foreach (var node in items) PutNode(node with { ParentId = group.Id });
        RecordNodes(before, after);
        return group;
    }

    public GraphStroke AddStroke(IEnumerable<GraphPoint> points, double width = 2.2)
    {
        var list = points.ToArray();
        if (list.Length < 2 || !double.IsFinite(width) || width <= 0 ||
            list.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            throw new InvalidDataException("Traço precisa de coordenadas finitas e espessura positiva.");
        var stroke = new GraphStroke("stroke_" + Guid.NewGuid().ToString("N"), list, width);
        Record(() => _strokes.RemoveAll(s => s.Id == stroke.Id), () => PutStroke(stroke));
        _strokes.Add(stroke);
        return stroke;
    }

    public bool RemoveStroke(string id)
    {
        var stroke = _strokes.FirstOrDefault(s => s.Id == id);
        if (stroke is null) return false;
        Record(() => PutStroke(stroke), () => _strokes.RemoveAll(s => s.Id == id));
        _strokes.Remove(stroke);
        return true;
    }

    public GraphNode? Node(string id) => IndexOfNode(id) is var at && at >= 0 ? _nodes[at] : null;

    public string Serialize() => JsonSerializer.Serialize(new Persisted
    {
        Type = TypeName, Version = CurrentVersion, Id = Id, Title = Title,
        Nodes = _nodes, Edges = _edges, Strokes = _strokes
    }, Json) + "\n";

    /// <summary>
    /// Never silently replaces a damaged or future file with an empty graph. The UI must refuse writes on failure.
    /// </summary>
    public static GraphDocument Parse(string raw)
    {
        Persisted? data;
        try { data = JsonSerializer.Deserialize<Persisted>(raw, Json); }
        catch (JsonException e) { throw new InvalidDataException("Grafo JSON inválido.", e); }
        if (data is null || data.Type != TypeName || data.Version < 1)
            throw new InvalidDataException("Este arquivo não é um grafo Urbe válido.");
        if (data.Version > CurrentVersion)
            throw new NotSupportedException("Este grafo foi criado por uma versão mais recente do Urbe.");
        if (string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.Title) ||
            data.Nodes is null || data.Edges is null)
            throw new InvalidDataException("Estrutura de grafo incompleta.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in data.Nodes)
        {
            if (n is null || string.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id) ||
                !Enum.IsDefined(n.Kind) || n.Label is null) throw new InvalidDataException("Nó inválido ou duplicado.");
            CheckGeometry(n.X, n.Y, n.Width, n.Height);
            if (n.ParentId == n.Id) throw new InvalidDataException("Um grupo não pode ser pai de si próprio.");
            if (n.Target is not null) ValidateTarget(n.Target);
            if (n.Kind is GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder && n.Target is null)
                throw new InvalidDataException("Referência sem destino.");
        }
        foreach (var node in data.Nodes)
            if (node.ParentId is { } parent && (!ids.Contains(parent) ||
                data.Nodes.All(n => n.Id != parent || n.Kind != GraphNodeKind.Group) ||
                node.Kind == GraphNodeKind.Group))
                throw new InvalidDataException("Grupo inválido ou não encontrado.");
        var strokes = data.Strokes ?? [];
        var strokeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stroke in strokes)
            if (stroke is null || string.IsNullOrWhiteSpace(stroke.Id) || !strokeIds.Add(stroke.Id) ||
                !double.IsFinite(stroke.Width) || stroke.Width <= 0 ||
                stroke.Points is null || stroke.Points.Length < 2 ||
                stroke.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
                throw new InvalidDataException("Traço inválido.");
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in data.Edges)
            if (e is null || string.IsNullOrWhiteSpace(e.Id) || !edgeIds.Add(e.Id) ||
                !ids.Contains(e.SourceId) || !ids.Contains(e.TargetId) || e.SourceId == e.TargetId ||
                e.Label is null || e.Relation is null)
                throw new InvalidDataException("Conexão inválida ou órfã.");
        var graph = new GraphDocument { Id = data.Id, Title = data.Title };
        graph._nodes.AddRange(data.Nodes);
        graph._edges.AddRange(data.Edges);
        graph._strokes.AddRange(strokes);
        graph.ReindexNodes();
        return graph;
    }

    private static string CleanLabel(string? value, string fallback)
    {
        var text = (value ?? "").Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static void CheckGeometry(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height) ||
            width <= 0 || height <= 0)
            throw new InvalidDataException("Geometria de nó inválida.");
    }

    private static void ValidateTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':') ||
            path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".." || segment.StartsWith('.')))
            throw new InvalidDataException("Referência fora do vault ou insegura.");
    }

    private sealed class Persisted
    {
        public string Type { get; set; } = "";
        public int Version { get; set; }
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public List<GraphNode>? Nodes { get; set; }
        public List<GraphEdge>? Edges { get; set; }
        public List<GraphStroke>? Strokes { get; set; }
    }
}

public enum GraphNodeKind { Text, Note, File, Folder, Group, Shape }

public sealed record GraphNode(string Id, GraphNodeKind Kind, string Label, string? Target,
    double X, double Y, double Width, double Height, string? ReferenceId = null, string? ParentId = null);

/// <summary>Relation is optional semantic metadata; an ordinary drawn line asserts no factual relationship.</summary>
public sealed record GraphEdge(string Id, string SourceId, string TargetId, string Label, string Relation, bool Directed);

public sealed record GraphPoint(double X, double Y);
public sealed record GraphStroke(string Id, GraphPoint[] Points, double Width);
