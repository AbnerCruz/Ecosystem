using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-71 (ADD-0029): the local graph of a note — who it cites, who cites it, and where each one sits.</summary>
public sealed class LinkGraphTests
{
    private static (DocumentStore Store, KnowledgeIndex Index) Vault(params (string Path, string Content)[] notes)
    {
        var store = new DocumentStore();
        var index = new KnowledgeIndex(store);
        store.ReplaceAll(notes.Select(n => new DocumentInput { Id = "doc_" + n.Path, Path = n.Path, Content = n.Content }).ToList(), "test");
        return (store, index);
    }

    private static readonly (string, string)[] Notes =
    [
        ("Centro.md", "# Centro\n\n[[Amiga]] [[Citada]] [[Centro]]\n"),
        ("Pessoas/Amiga.md", "# Amiga\n\n[[Centro]]\n"),
        ("Citada.md", "# Citada\n"),
        ("Diário/Leitor.md", "# Leitor\n\n[[Centro]] [[Amiga]]\n"),
        ("Solta.md", "# Solta\n")
    ];

    [Fact]
    public void TheNeighboursAreGroupedByHowTheyLinkAndTheNoteItselfIsLeftOut()
    {
        var (store, index) = Vault(Notes);
        using var _ = index;
        var graph = LinkGraph.Around(store, index, "Centro.md")!;
        Assert.Equal(("Centro", ""), (graph.Title, graph.Folder));
        Assert.Equal(
            [("Amiga", LinkRelation.Mutual, "Pessoas"), ("Citada", LinkRelation.Cites, ""), ("Leitor", LinkRelation.CitedBy, "Diário")],
            graph.Neighbours.Select(n => (n.Title, n.Relation, n.Folder)));
        // Amiga is linked to Centro and Leitor
        Assert.Equal(2, graph.Neighbours.Single(n => n.Title == "Amiga").Degree);
        Assert.Equal(0, graph.Hidden);
    }

    [Fact]
    public void MutualNeighboursSitInsideAndTheOthersAround()
    {
        var (store, index) = Vault(Notes);
        using var _ = index;
        var graph = LinkGraph.Around(store, index, "Centro.md")!;
        double Radius(string title) { var n = graph.Placed.Single(p => p.Title == title); return Math.Round(Math.Sqrt(n.X * n.X + n.Y * n.Y), 4); }
        Assert.Equal(LinkGraph.InnerRing, Radius("Amiga"));
        Assert.Equal(LinkGraph.OuterRing, Radius("Citada"));
        Assert.Equal(LinkGraph.OuterRing, Radius("Leitor"));
    }

    [Fact]
    public void ThePlacesDoNotDependOnTheOrderTheNotesWereRead()
    {
        var (a, ia) = Vault(Notes);
        var (b, ib) = Vault(Notes.Reverse().ToArray());
        using (ia) using (ib)
            Assert.Equal(
                LinkGraph.Around(a, ia, "Centro.md")!.Placed.Select(n => (n.Id, n.X, n.Y)),
                LinkGraph.Around(b, ib, "Centro.md")!.Placed.Select(n => (n.Id, n.X, n.Y)));
    }

    [Fact]
    public void OnlyTheMostConnectedArePlacedAndEveryOneIsListed()
    {
        var notes = new List<(string, string)> { ("Centro.md", "# Centro\n\n" + string.Join(" ", Enumerable.Range(1, 30).Select(i => $"[[N{i}]]"))) };
        notes.AddRange(Enumerable.Range(1, 30).Select(i => ($"N{i}.md", $"# N{i}\n" + (i <= 3 ? "[[Centro]] [[N10]]" : ""))));
        var (store, index) = Vault(notes.ToArray());
        using var _ = index;
        var graph = LinkGraph.Around(store, index, "Centro.md", max: 12)!;
        Assert.Equal(30, graph.Neighbours.Count);
        Assert.Equal(12, graph.Placed.Count);
        Assert.Equal(18, graph.Hidden);
        // the mutual ones and N10 (linked from three more notes) are the most connected
        foreach (var t in new[] { "N1", "N2", "N3", "N10" }) Assert.Contains(graph.Placed, n => n.Title == t);
    }

    [Fact]
    public void ANoteWithoutLinksHasAnEmptyGraphAndAnUnknownNoteHasNone()
    {
        var (store, index) = Vault(Notes);
        using var _ = index;
        Assert.Empty(LinkGraph.Around(store, index, "Solta.md")!.Neighbours);
        Assert.Null(LinkGraph.Around(store, index, "Nada.md"));
    }

    [Fact]
    public void TheHashIsStable() => Assert.Equal(2166136261u, LinkGraph.Hash(""));
}
