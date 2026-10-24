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

    public string Id { get; private set; } = "graph_" + Guid.NewGuid().ToString("N");
    public string Title { get; private set; } = "Novo grafo";
    public IReadOnlyList<GraphNode> Nodes => _nodes.AsReadOnly();
    public IReadOnlyList<GraphEdge> Edges => _edges.AsReadOnly();

    public static bool IsGraphPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase);

    public static GraphDocument Create(string title) => new() { Title = CleanLabel(title, "Novo grafo") };

    public GraphNode AddText(string? text, double x = 0, double y = 0) =>
        AddNode(GraphNodeKind.Text, text ?? "", null, x, y);

    public GraphNode AddReference(GraphNodeKind kind, string vaultPath, double x = 0, double y = 0, string? referenceId = null)
    {
        if (kind is not (GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder))
            throw new ArgumentOutOfRangeException(nameof(kind), "A referência deve apontar para uma nota, arquivo ou pasta.");
        ValidateTarget(vaultPath);
        var label = Path.GetFileName(vaultPath.TrimEnd('/'));
        return AddNode(kind, label, vaultPath, x, y, referenceId: referenceId);
    }

    public GraphNode AddNode(GraphNodeKind kind, string? label, string? target, double x, double y,
        double width = 190, double height = 84, string? referenceId = null)
    {
        if (target is not null) ValidateTarget(target);
        if (kind is GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder && target is null)
            throw new ArgumentException("Um nó de referência exige um alvo.");
        CheckGeometry(x, y, width, height);
        var node = new GraphNode("node_" + Guid.NewGuid().ToString("N"), kind, CleanLabel(label, ""), target,
            x, y, width, height, referenceId);
        if (_nodes.Count >= 10000) throw new InvalidOperationException("Limite de 10.000 elementos por grafo.");
        _nodes.Add(node);
        return node;
    }

    public GraphEdge Connect(string sourceId, string targetId, string? label = null, string? relation = null,
        bool directed = false)
    {
        if (sourceId == targetId || !_nodes.Any(n => n.Id == sourceId) || !_nodes.Any(n => n.Id == targetId))
            throw new ArgumentException("Selecione dois elementos distintos que pertencem ao grafo.");
        if (_edges.Count >= 30000) throw new InvalidOperationException("Limite de 30.000 conexões por grafo.");
        var edge = new GraphEdge("edge_" + Guid.NewGuid().ToString("N"), sourceId, targetId,
            CleanLabel(label, ""), CleanLabel(relation, ""), directed);
        _edges.Add(edge);
        return edge;
    }

    public bool MoveNode(string id, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 1e7 || Math.Abs(y) > 1e7)
            throw new ArgumentOutOfRangeException(nameof(x));
        var at = _nodes.FindIndex(n => n.Id == id);
        if (at < 0) return false;
        _nodes[at] = _nodes[at] with { X = x, Y = y };
        return true;
    }

    public bool RenameNode(string id, string? text)
    {
        var at = _nodes.FindIndex(n => n.Id == id);
        if (at < 0) return false;
        _nodes[at] = _nodes[at] with { Label = CleanLabel(text, "") };
        return true;
    }

    public bool RemoveNode(string id)
    {
        if (_nodes.RemoveAll(n => n.Id == id) == 0) return false;
        _edges.RemoveAll(e => e.SourceId == id || e.TargetId == id);
        return true;
    }

    public bool RemoveEdge(string id) => _edges.RemoveAll(e => e.Id == id) > 0;
    public GraphNode? Node(string id) => _nodes.FirstOrDefault(n => n.Id == id);

    public string Serialize() => JsonSerializer.Serialize(new Persisted
    {
        Type = TypeName, Version = CurrentVersion, Id = Id, Title = Title,
        Nodes = _nodes, Edges = _edges
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
            data.Nodes is null || data.Edges is null || data.Nodes.Count > 10000 || data.Edges.Count > 30000)
            throw new InvalidDataException("Estrutura de grafo incompleta ou excessiva.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in data.Nodes)
        {
            if (n is null || string.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id) ||
                !Enum.IsDefined(n.Kind) || n.Label is null) throw new InvalidDataException("Nó inválido ou duplicado.");
            CheckGeometry(n.X, n.Y, n.Width, n.Height);
            if (n.Target is not null) ValidateTarget(n.Target);
            if (n.Kind is GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder && n.Target is null)
                throw new InvalidDataException("Referência sem destino.");
        }
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in data.Edges)
            if (e is null || string.IsNullOrWhiteSpace(e.Id) || !edgeIds.Add(e.Id) ||
                !ids.Contains(e.SourceId) || !ids.Contains(e.TargetId) || e.SourceId == e.TargetId ||
                e.Label is null || e.Relation is null)
                throw new InvalidDataException("Conexão inválida ou órfã.");
        var graph = new GraphDocument { Id = data.Id, Title = data.Title };
        graph._nodes.AddRange(data.Nodes);
        graph._edges.AddRange(data.Edges);
        return graph;
    }

    private static string CleanLabel(string? value, string fallback)
    {
        var text = (value ?? "").Trim();
        return text.Length == 0 ? fallback : text.Length > 4000 ? text[..4000] : text;
    }

    private static void CheckGeometry(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height) ||
            Math.Abs(x) > 1e7 || Math.Abs(y) > 1e7 || width < 32 || width > 10000 || height < 24 || height > 10000)
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
    }
}

public enum GraphNodeKind { Text, Note, File, Folder, Group, Shape }

public sealed record GraphNode(string Id, GraphNodeKind Kind, string Label, string? Target,
    double X, double Y, double Width, double Height, string? ReferenceId = null);

/// <summary>Relation is optional semantic metadata; an ordinary drawn line asserts no factual relationship.</summary>
public sealed record GraphEdge(string Id, string SourceId, string TargetId, string Label, string Relation, bool Directed);
