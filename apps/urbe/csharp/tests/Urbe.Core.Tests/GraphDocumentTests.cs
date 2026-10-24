namespace Urbe.Core.Tests;

public sealed class GraphDocumentTests
{
    [Fact]
    public void RoundTripPreservesIdentityPositionsAndTypedReferences()
    {
        var graph = GraphDocument.Create("Dinastia");
        var founder = graph.AddReference(GraphNodeKind.Note, "Personagens/Arther.md", -120, 15);
        var daughter = graph.AddReference(GraphNodeKind.Note, "Personagens/Lyra.md", 180, 60);
        var region = graph.AddReference(GraphNodeKind.Folder, "História", 50, -100);
        graph.Connect(founder.Id, daughter.Id, "pai", "parentOf", directed: true);
        graph.Connect(region.Id, daughter.Id, "aparece em");
        graph.MoveNode(daughter.Id, 210, 72);
        var json = graph.Serialize();
        var restored = GraphDocument.Parse(json);

        Assert.Equal(graph.Id, restored.Id);
        Assert.Equal("Dinastia", restored.Title);
        Assert.Equal(3, restored.Nodes.Count);
        Assert.Equal(2, restored.Edges.Count);
        Assert.Equal("Personagens/Lyra.md", restored.Node(daughter.Id)!.Target);
        Assert.Equal(210, restored.Node(daughter.Id)!.X);
        Assert.Equal("parentOf", restored.Edges[0].Relation);
        Assert.True(restored.Edges[0].Directed);
        Assert.Equal(json, restored.Serialize());
    }

    [Fact]
    public void ACardCanBeRemovedWithoutTouchingItsTarget()
    {
        var graph = GraphDocument.Create("Família");
        var first = graph.AddReference(GraphNodeKind.Note, "Pessoa.md");
        var second = graph.AddText("Anotação");
        graph.Connect(first.Id, second.Id, "conhece");
        Assert.True(graph.RemoveNode(first.Id));
        Assert.Single(graph.Nodes);
        Assert.Empty(graph.Edges);
        Assert.Equal("Anotação", graph.Nodes[0].Label);
        Assert.Equal("Pessoa.md", first.Target);
    }

    [Theory]
    [InlineData("../segredo.md")]
    [InlineData(".urbe/notas.json")]
    [InlineData("/externo.md")]
    [InlineData("x//y")]
    [InlineData("C:/Windows/System32")]
    [InlineData("projeto\\x.md")]
    public void ReferencesCannotEscapeTheVault(string target)
    {
        var graph = GraphDocument.Create("Seguro");
        Assert.Throws<InvalidDataException>(() => graph.AddReference(GraphNodeKind.File, target));
    }

    [Fact]
    public void NoDanglingEdgesOrDuplicateIdsFromDisk()
    {
        var graph = GraphDocument.Create("Incompleto");
        var a = graph.AddText("A");
        var b = graph.AddText("B");
        var edge = graph.Connect(a.Id, b.Id);
        var raw = graph.Serialize();
        Assert.Throws<InvalidDataException>(() => GraphDocument.Parse(raw.Replace(b.Id, "not-a-node")));
        Assert.Throws<InvalidDataException>(() => GraphDocument.Parse(raw.Replace(edge.Id, a.Id).Replace(b.Id, a.Id)));
    }

    [Fact]
    public void UnknownVersionsAndBrokenFilesAreNeverTreatedAsNewGraphs()
    {
        var json = GraphDocument.Create("Meu grafo").Serialize();
        Assert.Throws<NotSupportedException>(() => GraphDocument.Parse(json.Replace("\"version\": 1", "\"version\": 200")));
        Assert.Throws<InvalidDataException>(() => GraphDocument.Parse("{não é json}"));
        Assert.Throws<InvalidDataException>(() => GraphDocument.Parse("{}"));
        Assert.True(GraphDocument.IsGraphPath("Familia.grafo.json"));
        Assert.False(GraphDocument.IsGraphPath("Familia.json"));
    }

    [Fact]
    public void IllegalGeometryAndInvalidLinksAreRefused()
    {
        var graph = GraphDocument.Create("Geometria");
        Assert.Throws<InvalidDataException>(() => graph.AddNode(GraphNodeKind.Shape, "Teste", null, double.NaN, 0));
        var a = graph.AddText("A");
        Assert.Throws<ArgumentException>(() => graph.Connect(a.Id, a.Id));
        Assert.Throws<ArgumentException>(() => graph.Connect(a.Id, "inexistente"));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.MoveNode(a.Id, double.PositiveInfinity, 0));
    }
}
