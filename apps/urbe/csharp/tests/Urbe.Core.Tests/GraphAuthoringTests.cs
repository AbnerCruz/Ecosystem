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

    /// <summary>
    /// A vault of 3,000 notes in 30 folders read as one column 350,000 px tall (seen in the desktop app, 18/11/2026).
    /// Now each folder is a block with its notes in a grid beside it, packed towards 16:9; no two cards share a place.
    /// </summary>
    [Fact]
    public void AVaultStructureOfThousandsOfNotesIsAMapNotAColumn()
    {
        var docs = Enumerable.Range(0, 3000).Select(i => new UrbeDocument("n" + i, $"Projeto {i / 100:00}/Nota {i:0000}.md", "N", "",
            new Dictionary<string, string>(), [], [], null, null, 1)).ToList();
        docs.Add(new UrbeDocument("solta", "Solta.md", "S", "", new Dictionary<string, string>(), [], [], null, null, 1));
        docs.Add(new UrbeDocument("fundo", "Projeto 00/Sub/Funda.md", "F", "", new Dictionary<string, string>(), [], [], null, null, 1));
        var graph = GraphProjection.FromVault(docs, [], [], _ => []);
        double w = graph.Nodes.Max(n => n.X) - graph.Nodes.Min(n => n.X), h = graph.Nodes.Max(n => n.Y) - graph.Nodes.Min(n => n.Y);
        Assert.InRange(w / h, 0.8, 3.5);
        Assert.Equal(graph.Nodes.Count, graph.Nodes.Select(n => (n.X, n.Y)).Distinct().Count());
        var folder = graph.Nodes.Single(n => n.Target == "Projeto 07");
        var notes = graph.Nodes.Where(n => n.Target?.StartsWith("Projeto 07/") == true).ToList();
        Assert.All(notes, n => Assert.True(n.X > folder.X && n.Y >= folder.Y));   // beside and below its folder
        Assert.True(notes.Max(n => n.X) - notes.Min(n => n.X) > 1000);   // a grid, not a column
        var sub = graph.Nodes.Single(n => n.Target == "Projeto 00/Sub");
        Assert.True(graph.Nodes.Single(n => n.Target == "Projeto 00/Sub/Funda.md").X > sub.X);
    }

    [Fact]
    public void StructureProjectionDoesNotDiscardFilesAboveAnInventedQuota()
    {
        var docs = Array.Empty<UrbeDocument>();
        var files = Enumerable.Range(0, 6001).Select(n => $"Arquivo_{n}.png");
        var graph = GraphProjection.FromVault(docs, files, [], _ => []);
        Assert.Equal(6001, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Target == "Arquivo_6000.png");
    }

    [Fact]
    public void HistoryHasNoArtificialEightyEditCeiling()
    {
        var graph = GraphDocument.Create("Histórico");
        var n = graph.AddText("Posição original");
        for (var i = 1; i <= 125; i++) graph.MoveNode(n.Id, i, 0);
        for (var i = 0; i < 125; i++) Assert.True(graph.Undo());
        Assert.Equal(0, graph.Node(n.Id)!.X);
        for (var i = 0; i < 125; i++) Assert.True(graph.Redo());
        Assert.Equal(125, graph.Node(n.Id)!.X);
    }

    [Fact]
    public void ManyNodesAndConnectionsCanBeSavedAndOpenedWithoutNumberQuotas()
    {
        var graph = GraphDocument.Create("Grande");
        var first = graph.AddText("Inicial");
        for (var i = 0; i < 10500; i++)
        {
            var node = graph.AddText("Item " + i, i * 4, i);
            if (i < 10) graph.Connect(first.Id, node.Id);
        }
        Assert.Equal(10501, graph.Nodes.Count);
        var restored = GraphDocument.Parse(graph.Serialize());
        Assert.Equal(graph.Nodes.Count, restored.Nodes.Count);
        Assert.NotNull(restored.Node(first.Id));
    }
}
