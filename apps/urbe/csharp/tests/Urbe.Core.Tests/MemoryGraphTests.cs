using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class MemoryGraphTests
{
    [Fact]
    public void TheGraphShowsKindsVersionsRefinementsSourcesAndOnlyThisAgent()
    {
        var cfg = new AiConfig();
        void Say(string text) => AssistantMemoryEngine.CaptureUserStatements(cfg, "v", "alice", text, "conversation:c");
        Say("Gosto de strogonoff.");
        Say("Não gosto de strogonoff.");
        Say("Não gosto de strogonoff de porco.");
        AssistantMemoryEngine.ObserveFinishedRun(cfg, "v", "alice", "r1", ["Receitas.md"], "organize as receitas");
        AssistantMemoryEngine.CaptureUserStatements(cfg, "v", "bruno", "Prefiro chá a café.", "conversation:b");
        var records = MemoryGraph.Records(cfg, "v", "alice");
        var graph = GraphDocument.Parse(MemoryGraph.Build("Memória de alice", records, AssistantMemoryEngine.PotentialConflicts(cfg, "v", "alice")).Serialize());
        Assert.Contains(graph.Nodes, n => n.Label == "Preferências");
        Assert.Contains(graph.Nodes, n => n.Label == "Execuções");
        Assert.Contains(graph.Nodes, n => n.Label.StartsWith("Gosto de strogonoff.", StringComparison.Ordinal) && n.Label.EndsWith("substituída", StringComparison.Ordinal));
        Assert.Contains(graph.Edges, e => e.Relation == "substitui");
        Assert.Contains(graph.Edges, e => e.Relation == "refina");
        Assert.Contains(graph.Nodes, n => n.Kind == GraphNodeKind.Note && n.Target == "Receitas.md");
        Assert.DoesNotContain(graph.Nodes, n => n.Label.Contains("chá", StringComparison.Ordinal));
    }
}
