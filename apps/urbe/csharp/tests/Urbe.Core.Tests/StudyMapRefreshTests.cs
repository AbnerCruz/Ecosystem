using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Core.Tests;

/// <summary>
/// #956 acceptance 4: the study map is the person's to edit. Making it again from the sources updates what comes from
/// them and keeps what the person added or arranged (relations, notes on the canvas, drawings, positions).
/// </summary>
public sealed class StudyMapRefreshTests
{
    private static readonly StudySession Session = new() { Title = "Física", Objectives = ["Leis de Newton"] };

    [Fact]
    public void RemakingKeepsThePersonsEditsAndUpdatesWhatComesFromTheSources()
    {
        var sources = new List<(string, string, string?)> { ("d1", "Newton.md", "# Newton\n\n## Inércia\n"), ("d2", "Atrito.md", "# Atrito\n") };
        var map = StudyMap.Build(Session, sources);
        var newton = map.Nodes.Single(n => n.Target == "Newton.md");
        var atrito = map.Nodes.Single(n => n.Target == "Atrito.md");
        // the person's work on the map
        map.MoveNode(newton.Id, 900, 50);
        map.Connect(newton.Id, atrito.Id, "explica", "explica");
        var idea = map.AddText("Pergunta para a aula: por que o atrito existe?", 100, 700);
        map.Connect(idea.Id, atrito.Id, null, "pergunta");
        map.AddStroke([new GraphPoint(0, 0), new GraphPoint(10, 10)]);

        // the sources changed: a new heading in Newton, a heading gone from nowhere, a third source
        sources[0] = ("d1", "Newton.md", "# Newton\n\n## Inércia\n## Ação e reação\n");
        sources.Add(("d3", "Energia.md", "# Energia\n"));
        var remade = StudyMap.Refresh(GraphDocument.Parse(map.Serialize()), Session, sources);

        var newNewton = remade.Nodes.Single(n => n.Target == "Newton.md");
        var newAtrito = remade.Nodes.Single(n => n.Target == "Atrito.md");
        Assert.Equal((900d, 50d), (newNewton.X, newNewton.Y));                                     // arrangement kept
        Assert.Contains(remade.Edges, e => e.Relation == "explica" && e.SourceId == newNewton.Id && e.TargetId == newAtrito.Id);
        var keptIdea = remade.Nodes.Single(n => n.Label.StartsWith("Pergunta para a aula", StringComparison.Ordinal));
        Assert.Contains(remade.Edges, e => e.Relation == "pergunta" && e.SourceId == keptIdea.Id);
        Assert.Single(remade.Strokes);
        Assert.Contains(remade.Nodes, n => n.Label == "Ação e reação");                             // new topic from the note
        Assert.Contains(remade.Nodes, n => n.Target == "Energia.md");                                 // new source
        Assert.Single(remade.Nodes, n => n.Target == "Newton.md");                                    // nothing doubled
    }

    [Fact]
    public void ATopicGoneFromTheNoteLeavesTheMapButThePersonsOwnStays()
    {
        var sources = new List<(string, string, string?)> { ("d1", "Newton.md", "# Newton\n\n## Inércia\n") };
        var map = StudyMap.Build(Session, sources);
        map.AddText("Minha anotação", 0, 500);
        sources[0] = ("d1", "Newton.md", "# Newton\n");
        var remade = StudyMap.Refresh(map, Session, sources);
        Assert.DoesNotContain(remade.Nodes, n => n.Label == "Inércia");
        Assert.Contains(remade.Nodes, n => n.Label == "Minha anotação");
        Assert.Equal(StudyMap.Build(Session, sources).Nodes.Count, StudyMap.Refresh(null, Session, sources).Nodes.Count);
    }
}
