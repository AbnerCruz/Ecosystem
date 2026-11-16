namespace Urbe.Core.Assistant;

/// <summary>
/// UC-87 / UC-88.4 (owner, PR #952): an agent's memory seen as a graph. A projection of the AssistantStore records,
/// never a second store and never written to the vault: one column per kind, each memory with its standing and
/// source, "substitui" to the version it replaced, "refina" to the general one, "conflito" between open
/// contradictions, and "fonte" to the notes it came from (which open on a tap).
/// </summary>
public static class MemoryGraph
{
    private static readonly string[] Order = ["preference", "decision", "procedure", "goal", "fact", "observation", "episode"];
    private static readonly Dictionary<string, string> Names = new()
    {
        ["preference"] = "Preferências", ["decision"] = "Decisões", ["procedure"] = "Procedimentos", ["goal"] = "Objetivos",
        ["fact"] = "Fatos", ["observation"] = "Observações", ["episode"] = "Execuções"
    };

    /// <summary>The agent's memories: what it can recall now plus the versions they replaced (archived ones stay out).</summary>
    public static IReadOnlyList<AiMemoryRecord> Records(AiConfig cfg, string vault, string agentId) =>
        cfg.MemoryRecords.Where(m => m.State != "archived" &&
            (m.Scope == "global" || m.Vault == vault && (m.Scope == "vault" || m.Scope == "agent" && m.AgentId == agentId))).ToList();

    public static GraphDocument Build(string title, IReadOnlyList<AiMemoryRecord> records,
        IReadOnlyList<(AiMemoryRecord Left, AiMemoryRecord Right)> conflicts)
    {
        var graph = GraphDocument.Create(title);
        var nodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        const double column = 320, row = 130;
        var kinds = Order.Concat(records.Select(r => r.Kind)).Distinct().Where(k => records.Any(r => r.Kind == k)).ToList();
        for (int c = 0; c < kinds.Count; c++)
        {
            graph.AddNode(GraphNodeKind.Text, Names.GetValueOrDefault(kinds[c], kinds[c]), null, c * column, 0, 280, 44);
            double y = 64;
            var inKind = records.Where(r => r.Kind == kinds[c])
                .OrderBy(r => r.State == "active" ? 0 : 1).ThenByDescending(r => r.UpdatedAt).ToList();
            for (int i = 0; i < inKind.Count; i++)
            {
                var m = inKind[i];
                var state = m.State == "active" ? "" : m.State == "superseded" ? " · substituída" : " · " + m.State;
                var label = m.Text + "\n" + AssistantMemoryEngine.Standing(m) + state;
                // tall enough for the memory's own text (about 30 characters a line) and its standing
                double height = 50 + 18 * Math.Ceiling(m.Text.Length / 30.0);
                nodes[m.Id] = graph.AddNode(GraphNodeKind.Text, label, null, c * column, y, 280, height).Id;
                y += height + 36;
            }
        }
        foreach (var m in records)
        {
            foreach (var other in m.RelatedMemoryIds)
                if (nodes.TryGetValue(other, out var to) && records.FirstOrDefault(r => r.Id == other) is { } target)
                    graph.Connect(nodes[m.Id], to, target.State == "superseded" ? "substitui" : "refina", target.State == "superseded" ? "substitui" : "refina");
            foreach (var path in m.RelatedPaths.Take(4))
            {
                if (!notes.TryGetValue(path, out var note))
                    notes[path] = note = graph.AddReference(GraphNodeKind.Note, path, kinds.Count * column + 60, row * (notes.Count + 1)).Id;
                graph.Connect(nodes[m.Id], note, "fonte", "fonte");
            }
        }
        foreach (var (left, right) in conflicts)
            if (nodes.TryGetValue(left.Id, out var a) && nodes.TryGetValue(right.Id, out var b))
                graph.Connect(a, b, "conflito", "conflito");
        return graph;
    }
}
