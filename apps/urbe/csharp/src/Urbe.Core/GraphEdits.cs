using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core;

/// <summary>
/// CST-068: the operations an agent sends to change a graph, applied to a copy (the caller writes it only when every
/// operation succeeded). New nodes are named by a "ref" within the call; existing ones by their IDs. New nodes go in
/// calm rows below the existing drawing, never on top of each other.
/// </summary>
public static class GraphEdits
{
    private const double ColumnWidth = 230, RowHeight = 130;
    private const int Columns = 4;

    /// <returns>A short summary of what changed ("3 nós, 2 ligações").</returns>
    /// <param name="idOf">A note's stable id (CST-066: its card follows renames, like one placed by hand).</param>
    public static string Apply(GraphDocument graph, JsonArray operations, Func<string, (GraphNodeKind Kind, string Path)?> resolve, Func<string, string?>? idOf = null)
    {
        var refs = new Dictionary<string, string>(StringComparer.Ordinal);
        double top = graph.Nodes.Count == 0 ? 0 : graph.Nodes.Max(n => n.Y + n.Height) + 60;
        double left = graph.Nodes.Count == 0 ? 0 : graph.Nodes.Min(n => n.X);
        int placed = 0, nodes = 0, edges = 0, renamed = 0, removed = 0, groups = 0;

        string Id(JsonObject op, string key, int index)
        {
            var value = ((string?)op[key] ?? "").Trim();
            if (value.Length == 0) throw new AiToolException("Operação " + index + ": falta \"" + key + "\".");
            if (refs.TryGetValue(value, out var made)) return made;
            if (graph.Node(value) is not null || graph.Edges.Any(e => e.Id == value)) return value;
            throw new AiToolException("Operação " + index + ": \"" + value + "\" não é um nó deste grafo nem um ref criado antes nesta chamada. Use os IDs de read_graph.");
        }

        (double X, double Y) Next()
        {
            var at = (left + placed % Columns * ColumnWidth, top + placed / Columns * RowHeight);
            placed++;
            return at;
        }

        void Remember(JsonObject op, GraphNode node, int index)
        {
            if (((string?)op["ref"])?.Trim() is not { Length: > 0 } name) return;
            if (!refs.TryAdd(name, node.Id)) throw new AiToolException("Operação " + index + ": o ref \"" + name + "\" já foi usado nesta chamada.");
        }

        for (int i = 0; i < operations.Count; i++)
        {
            int index = i + 1;
            if (operations[i] is not JsonObject op) throw new AiToolException("Operação " + index + " inválida.");
            var kind = ((string?)op["op"] ?? "").Trim();
            try
            {
                switch (kind)
                {
                    case "add_text":
                    {
                        var (x, y) = Next();
                        Remember(op, graph.AddText((string?)op["text"] ?? "", x, y), index);
                        nodes++;
                        break;
                    }
                    case "add_note":
                    {
                        var wanted = ((string?)op["path"] ?? "").Trim();
                        var found = resolve(wanted) ?? throw new AiToolException("Operação " + index + ": \"" + wanted + "\" não existe no vault.");
                        var (x, y) = Next();
                        Remember(op, graph.AddReference(found.Kind, found.Path, x, y, found.Kind == GraphNodeKind.Note ? idOf?.Invoke(found.Path) : null), index);
                        nodes++;
                        break;
                    }
                    case "connect":
                        graph.Connect(Id(op, "from", index), Id(op, "to", index), (string?)op["label"], (string?)op["relation"], (bool?)op["directed"] ?? false);
                        edges++;
                        break;
                    case "rename":
                        if (!graph.RenameNode(Id(op, "id", index), (string?)op["text"])) throw new AiToolException("Operação " + index + ": não foi possível renomear.");
                        renamed++;
                        break;
                    case "remove_node":
                        if (!graph.RemoveNode(Id(op, "id", index))) throw new AiToolException("Operação " + index + ": o nó não existe.");
                        removed++;
                        break;
                    case "remove_edge":
                        if (!graph.RemoveEdge(Id(op, "id", index))) throw new AiToolException("Operação " + index + ": a ligação não existe.");
                        removed++;
                        break;
                    case "group":
                    {
                        var ids = (op["ids"] as JsonArray ?? []).Select(x => (string?)x ?? "").Select(x => refs.GetValueOrDefault(x, x)).ToList();
                        if (ids.Count < 1 || ids.Any(x => graph.Node(x) is null)) throw new AiToolException("Operação " + index + ": group precisa de nós deste grafo (ids).");
                        Remember(op, graph.GroupNodes(ids, (string?)op["label"]), index);
                        groups++;
                        break;
                    }
                    default:
                        throw new AiToolException("Operação " + index + ": \"" + kind + "\" não existe. Use add_text, add_note, connect, rename, remove_node, remove_edge ou group.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException)
            {
                throw new AiToolException("Operação " + index + " (" + kind + "): " + ex.Message);
            }
        }
        var parts = new List<string>();
        if (nodes > 0) parts.Add(nodes + (nodes == 1 ? " nó novo" : " nós novos"));
        if (edges > 0) parts.Add(edges + (edges == 1 ? " ligação" : " ligações"));
        if (renamed > 0) parts.Add(renamed + " renomeado(s)");
        if (removed > 0) parts.Add(removed + " removido(s)");
        if (groups > 0) parts.Add(groups + " grupo(s)");
        return string.Join(", ", parts);
    }
}
