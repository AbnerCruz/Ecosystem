using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-068: an agent creates and changes a graph with a short list of operations, through the approval.</summary>
public sealed class AssistantGraphEditTests
{
    private sealed class Graphs : IAssistantGraphs
    {
        public readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase);
        public string? Refuse;
        public IReadOnlyList<string> ListPaths() => [.. Files.Keys];
        public Task<string?> ReadAsync(string path) => Task.FromResult(Files.GetValueOrDefault(path));
        public Task WriteAsync(string path, string json, bool create)
        {
            if (Refuse is not null) throw new AiToolException(Refuse);
            if (create == Files.ContainsKey(path)) throw new InvalidOperationException("create mismatch");
            Files[path] = json;
            return Task.CompletedTask;
        }
    }

    private sealed class Host(Graphs graphs) : IAssistantHost
    {
        public DocumentStore Documents { get; } = Make();
        private static DocumentStore Make()
        {
            var d = new DocumentStore();
            d.Upsert(new() { Path = "Personagens/Ana.md", Content = "# Ana" });
            d.Upsert(new() { Path = "Personagens/Bruno.md", Content = "# Bruno" });
            return d;
        }
        public KnowledgeIndex Knowledge => new(Documents);
        public int ReadChars => 16000;
        public IAssistantGraphs? Graphs => graphs;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => throw new NotSupportedException();
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static JsonObject Input(string path, string ops, string? title = null)
    {
        var o = new JsonObject { ["path"] = path, ["operations"] = JsonNode.Parse(ops) };
        if (title is not null) o["title"] = title;
        return o;
    }

    private const string Family = """
        [
          {"op":"add_note","ref":"ana","path":"Personagens/Ana.md"},
          {"op":"add_note","ref":"bruno","path":"Personagens/Bruno"},
          {"op":"add_text","ref":"casa","text":"Casa de Avis"},
          {"op":"connect","from":"ana","to":"bruno","label":"irmãos"},
          {"op":"connect","from":"casa","to":"ana","relation":"linhagem","directed":true}
        ]
        """;

    [Fact]
    public async Task ANewGraphIsCreatedWithNotesTextsAndConnections()
    {
        var graphs = new Graphs();
        var tool = AssistantGraphTools.Edit;
        var output = await tool.Run(Input("Mapas/Família.grafo.json", Family, "Família real"), new Host(graphs));
        Assert.Contains("Criado: Mapas/Família.grafo.json", output.Content);
        var g = GraphDocument.Parse(graphs.Files["Mapas/Família.grafo.json"]);
        Assert.Equal("Família real", g.Title);
        Assert.Equal(3, g.Nodes.Count);
        Assert.Equal("Personagens/Bruno.md", g.Nodes.Single(n => n.Label.StartsWith("Bruno")).Target);
        Assert.Equal(2, g.Edges.Count);
        Assert.Contains(g.Edges, e => e.Directed && e.Relation == "linhagem");
        // new nodes do not pile up on one spot
        Assert.Equal(3, g.Nodes.Select(n => (n.X, n.Y)).Distinct().Count());
    }

    [Fact]
    public async Task AnExistingGraphChangesByItsIdsAndKeepsTheRest()
    {
        var graphs = new Graphs();
        await AssistantGraphTools.Edit.Run(Input("Família.grafo.json", Family), new Host(graphs));
        var before = GraphDocument.Parse(graphs.Files["Família.grafo.json"]);
        var casa = before.Nodes.Single(n => n.Label == "Casa de Avis");
        var link = before.Edges.Single(e => e.Label == "irmãos");
        var ops = "[{\"op\":\"rename\",\"id\":\"" + casa.Id + "\",\"text\":\"Dinastia de Avis\"},{\"op\":\"remove_edge\",\"id\":\"" + link.Id + "\"},{\"op\":\"add_text\",\"ref\":\"n\",\"text\":\"Nova\"},{\"op\":\"connect\",\"from\":\"n\",\"to\":\"" + casa.Id + "\"}]";
        var output = await AssistantGraphTools.Edit.Run(Input("Família.grafo.json", ops), new Host(graphs));
        Assert.Contains("Alterado", output.Content);
        var after = GraphDocument.Parse(graphs.Files["Família.grafo.json"]);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal("Dinastia de Avis", after.Node(casa.Id)!.Label);
        Assert.DoesNotContain(after.Edges, e => e.Id == link.Id);
        Assert.Equal(4, after.Nodes.Count);
        Assert.Equal(before.Node(casa.Id)!.X, after.Node(casa.Id)!.X);
    }

    [Fact]
    public async Task TheApprovalShowsTheGraphAsTextNotJson()
    {
        var preview = await AssistantGraphTools.Edit.PreviewAsync!(Input("Família.grafo.json", Family), new Host(new Graphs()));
        Assert.Null(preview.Before);
        // the labels the canvas shows for a reference (the file name)
        Assert.Contains("[nota] Ana.md → Personagens/Ana.md", preview.After);
        Assert.Contains("Ana.md — Bruno.md: irmãos", preview.After);
        Assert.Contains("Casa de Avis → Ana.md [linhagem]", preview.After);
        Assert.DoesNotContain("\"nodes\"", preview.After);
    }

    [Theory]
    [InlineData("""[{"op":"add_note","path":"Personagens/Nadia.md"}]""", "não existe")]
    [InlineData("""[{"op":"connect","from":"x","to":"y"}]""", "x")]
    [InlineData("""[{"op":"voar"}]""", "voar")]
    [InlineData("""[]""", "Nenhuma operação")]
    [InlineData("""[{"op":"add_text","text":"x"}]""", "fora do vault", "../fora.grafo.json")]
    [InlineData("""[{"op":"add_text","text":"x"}]""", "fora do vault", ".urbe/x.grafo.json")]
    public async Task AWrongOperationChangesNothingAndSaysWhich(string ops, string message, string path = "G.grafo.json")
    {
        var graphs = new Graphs();
        var error = await Assert.ThrowsAsync<AiToolException>(() => AssistantGraphTools.Edit.Run(Input(path, ops), new Host(graphs)));
        Assert.Contains(message, error.Message);
        Assert.Empty(graphs.Files);
    }

    [Fact]
    public async Task AGraphTheAppCannotTakeNowIsNotOverwritten()
    {
        var graphs = new Graphs();
        await AssistantGraphTools.Edit.Run(Input("G.grafo.json", Family), new Host(graphs));
        var kept = graphs.Files["G.grafo.json"];
        graphs.Refuse = "O grafo está aberto com alterações não salvas.";
        var error = await Assert.ThrowsAsync<AiToolException>(() => AssistantGraphTools.Edit.Run(Input("G.grafo.json", """[{"op":"add_text","text":"x"}]"""), new Host(graphs)));
        Assert.Contains("não salvas", error.Message);
        Assert.Equal(kept, graphs.Files["G.grafo.json"]);
        Assert.Equal("write", AssistantGraphTools.Edit.Access);
    }
}
