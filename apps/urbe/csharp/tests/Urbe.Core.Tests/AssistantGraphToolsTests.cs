using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantGraphToolsTests
{
    private sealed class GraphVault(string path, string json) : IAssistantGraphs
    {
        public IReadOnlyList<string> ListPaths() => [path];
        public Task<string?> ReadAsync(string requested) =>
            Task.FromResult<string?>(requested == path ? json : null);
    }

    private sealed class Host(IAssistantGraphs? graphs) : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; } = new(new DocumentStore());
        public IAssistantGraphs? Graphs => graphs;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public int ReadChars => 5000;
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) => throw new NotSupportedException();
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static AiTool Tool(string name) => Assert.Single(AssistantGraphTools.All.Where(t => t.Name == name));

    [Fact]
    public async Task AgentsReadAuthorDefinedConnectionsWithStableReferences()
    {
        var graph = GraphDocument.Create("Linhas genealógicas");
        var mother = graph.AddReference(GraphNodeKind.Note, "Historia/Mãe.md", 0, 0, "doc-a");
        var child = graph.AddReference(GraphNodeKind.Note, "Historia/Filha.md", 200, 0, "doc-b");
        graph.Connect(mother.Id, child.Id, "mãe", "parentOf", directed: true);
        var host = new Host(new GraphVault("Historia/Familia.grafo.json", graph.Serialize()));

        var available = await Tool("list_graphs").Run(new JsonObject(), host);
        Assert.Contains("Historia/Familia.grafo.json", available.Content);
        var read = await Tool("read_graph").Run(new JsonObject { ["path"] = "Historia/Familia.grafo.json" }, host);
        Assert.Contains("doc-a", read.Content);
        Assert.Contains("parentOf", read.Content);
        Assert.Contains("não são provas", read.Content);
        Assert.Contains(mother.Id, read.Content);
        Assert.Contains(child.Id, read.Content);
    }

    [Fact]
    public async Task MissingAndFutureGraphFilesAreRejectedWithoutWrites()
    {
        var graph = GraphDocument.Create("Incompatível");
        var future = graph.Serialize().Replace("\"version\": 1", "\"version\": 999", StringComparison.Ordinal);
        var host = new Host(new GraphVault("Futuro.grafo.json", future));
        await Assert.ThrowsAsync<AiToolException>(() => Tool("read_graph").Run(
            new JsonObject { ["path"] = "Futuro.grafo.json" }, host));
        await Assert.ThrowsAsync<AiToolException>(() => Tool("read_graph").Run(
            new JsonObject { ["path"] = "../fora.grafo.json" }, host));
        var empty = await Tool("list_graphs").Run(new JsonObject(), new Host(null));
        Assert.Contains("Não há grafos", empty.Content);
        Assert.All(AssistantGraphTools.All, t => Assert.Equal("read", t.Access));
    }

    [Fact]
    public async Task PaginatedGraphHasExplicitNextOffsets()
    {
        var graph = GraphDocument.Create("Dez itens");
        for (var i = 0; i < 10; i++) graph.AddText("Item " + i);
        var host = new Host(new GraphVault("Mapa.grafo.json", graph.Serialize()));
        var result = await Tool("read_graph").Run(new JsonObject
        {
            ["path"] = "Mapa.grafo.json", ["limit"] = 2, ["node_offset"] = 4
        }, host);
        Assert.Contains("Item 4", result.Content);
        Assert.Contains("Item 5", result.Content);
        Assert.DoesNotContain("Item 6", result.Content);
        Assert.Contains("node_offset=6", result.Content);
    }
}
