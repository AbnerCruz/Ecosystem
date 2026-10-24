namespace Urbe.Core.Tests;

public sealed class GraphAuthoringTests
{
    [Fact]
    public void UndoRedoRestoresNodeGeometryAndConnections()
    {
        var graph = GraphDocument.Create("Exemplo");
        var first = graph.AddText("Primeiro", 10, 20);
        var second = graph.AddText("Segundo", 100, 200);
        graph.Connect(first.Id, second.Id, "relaciona");
        Assert.True(graph.MoveNode(first.Id, 110, 120));
        Assert.True(graph.Undo());
        Assert.Equal(10, graph.Node(first.Id)!.X);
        Assert.True(graph.CanRedo);
        Assert.True(graph.Redo());
        Assert.Equal(110, graph.Node(first.Id)!.X);
        Assert.Single(graph.Edges);
        Assert.True(graph.CanUndo);
    }

    [Fact]
    public void AWholeDragIsOneUndoStep()
    {
        var graph = GraphDocument.Create("Desenho");
        var node = graph.AddText("Mover");
        graph.BeginDrag();
        graph.MoveNode(node.Id, 40, 0, recordHistory: false);
        graph.MoveNode(node.Id, 80, 30, recordHistory: false);
        Assert.True(graph.Undo());
        Assert.Equal(0, graph.Node(node.Id)!.X);
        Assert.Equal(0, graph.Node(node.Id)!.Y);
    }

    [Fact]
    public void GroupMovesChildrenAndDeletingItKeepsReferencedFiles()
    {
        var graph = GraphDocument.Create("Projeto");
        var note = graph.AddReference(GraphNodeKind.Note, "Personagens/A.md", 0, 0, "doc-A");
        var text = graph.AddText("Observação", 240, 0);
        var group = graph.GroupNodes([note.Id, text.Id], "Núcleo");
        Assert.Equal(group.Id, graph.Node(note.Id)!.ParentId);
        graph.MoveNode(group.Id, group.X + 40, group.Y + 20);
        Assert.Equal(40, graph.Node(note.Id)!.X);
        Assert.Equal(280, graph.Node(text.Id)!.X);
        Assert.True(graph.RemoveNode(group.Id));
        Assert.Null(graph.Node(note.Id)!.ParentId);
        Assert.Equal("Personagens/A.md", graph.Node(note.Id)!.Target);
        Assert.Equal("doc-A", graph.Node(note.Id)!.ReferenceId);
    }

    [Fact]
    public void DrawnStrokeRoundTripsAndCanBeErasedAndUndone()
    {
        var graph = GraphDocument.Create("Croqui");
        var stroke = graph.AddStroke([new GraphPoint(0, 0), new GraphPoint(16, 22), new GraphPoint(30, 20)], 3);
        var restored = GraphDocument.Parse(graph.Serialize());
        Assert.Equal(3, restored.Strokes.Single().Points.Length);
        Assert.True(restored.RemoveStroke(stroke.Id));
        Assert.Empty(restored.Strokes);
        Assert.True(restored.Undo());
        Assert.Single(restored.Strokes);
        Assert.Throws<InvalidDataException>(() => graph.AddStroke([new GraphPoint(double.NaN, 2), new GraphPoint(1, 1)]));
    }

    [Fact]
    public void StructureProjectionRespectsPathsAndExplicitWikiLinks()
    {
        static UrbeDocument Doc(string id, string path) => new(id, path, Path.GetFileNameWithoutExtension(path), "",
            new Dictionary<string, string>(), [], [], null, null, 1);
        var a = Doc("a", "Livro/Capitulo.md");
        var b = Doc("b", "Livro/Personagem.md");
        var other = Doc("c", "Diário/Sol.md");
        var graph = GraphProjection.FromVault([a, b, other], ["Livro/Mapa.png", "Diário/img.jpg"],
            ["Livro", "Diário"], id => id == "a" ? ["b"] : [], "Livro");
        Assert.Equal("Estrutura de Livro", graph.Title);
        Assert.Equal(4, graph.Nodes.Count);
        Assert.DoesNotContain(graph.Nodes, n => n.Target is not null && n.Target.StartsWith("Diário/"));
        Assert.Contains(graph.Edges, e => e.Relation == "wikilink" && e.Directed);
        Assert.Equal(3, graph.Edges.Count(e => e.Relation == "contains"));
        Assert.Contains(graph.Nodes, n => n.ReferenceId == "a");
        Assert.Equal(graph.Nodes.Count, GraphDocument.Parse(graph.Serialize()).Nodes.Count);
    }

    [Fact]
    public void StructureProjectionRefusesExcessiveCanvasWithoutSilentlyHidingFiles()
    {
        var docs = Array.Empty<UrbeDocument>();
        var files = Enumerable.Range(0, 6001).Select(n => $"Arquivo_{n}.png");
        Assert.Throws<InvalidDataException>(() =>
            GraphProjection.FromVault(docs, files, [], _ => []));
    }
}
