using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Core.Tests;

public sealed class StudyMapTests
{
    [Fact]
    public void TheMapHasTheSessionTheSourcesTheirTopicsAndCitations()
    {
        var session = new StudySession { Title = "Física", Objectives = ["Leis de Newton"] };
        var graph = StudyMap.Build(session,
        [
            ("d1", "Newton.md", "# Newton\n\n## Inércia\n\n## Força\n\nVer [[Galileu]]."),
            ("d2", "Galileu.md", "# Galileu\n"),
            ("file:Livro.pdf", "Livro.pdf", null)
        ]);
        var json = graph.Serialize();
        var back = GraphDocument.Parse(json);
        Assert.Contains(back.Nodes, n => n.Kind == GraphNodeKind.Text && n.Label.StartsWith("Física", StringComparison.Ordinal));
        Assert.Contains(back.Nodes, n => n.Kind == GraphNodeKind.Note && n.Target == "Newton.md");
        Assert.Contains(back.Nodes, n => n.Kind == GraphNodeKind.File && n.Target == "Livro.pdf");
        Assert.Contains(back.Nodes, n => n.Label == "Inércia");
        var newton = back.Nodes.Single(n => n.Target == "Newton.md").Id;
        var galileu = back.Nodes.Single(n => n.Target == "Galileu.md").Id;
        Assert.Contains(back.Edges, e => e.SourceId == newton && e.TargetId == galileu && e.Relation == "cita");
        Assert.Equal(3, back.Edges.Count(e => e.Relation == "fonte"));
    }
}
