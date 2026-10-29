using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-066: a graph's cards follow the notes, files and folders they point to when these are renamed or moved in the
/// Urbe — the path, and the name when the card still showed the old one. A card renamed by the person keeps its name.
/// </summary>
public sealed class GraphRetargetTests
{
    [Fact]
    public void ARenamedNoteMovesItsCardAndItsDefaultName()
    {
        var g = GraphDocument.Create("Mapa");
        var note = g.AddReference(GraphNodeKind.Note, "Projetos/Plano.md", referenceId: "d1");
        var other = g.AddReference(GraphNodeKind.Note, "Projetos/Outra.md");
        Assert.Equal(1, g.Retarget("Projetos/Plano.md", "Projetos/Plano final.md"));
        var moved = g.Node(note.Id)!;
        Assert.Equal(("Projetos/Plano final.md", "Plano final.md", "d1"), (moved.Target, moved.Label, moved.ReferenceId));
        Assert.Equal("Projetos/Outra.md", g.Node(other.Id)!.Target);
    }

    [Fact]
    public void ACardThePersonNamedKeepsItsName()
    {
        var g = GraphDocument.Create("Mapa");
        var note = g.AddReference(GraphNodeKind.Note, "Plano.md");
        g.RenameNode(note.Id, "O plano do ano");
        g.Retarget("Plano.md", "Arquivo/Plano.md");
        Assert.Equal(("Arquivo/Plano.md", "O plano do ano"), (g.Node(note.Id)!.Target, g.Node(note.Id)!.Label));
    }

    [Fact]
    public void AMovedFolderTakesEverythingInsideIt()
    {
        var g = GraphDocument.Create("Mapa");
        var folder = g.AddReference(GraphNodeKind.Folder, "Droerni");
        var inside = g.AddReference(GraphNodeKind.File, "Droerni/Mapas/mapa.png");
        var near = g.AddReference(GraphNodeKind.Note, "Droerni antigo/nota.md");   // only a common start, not inside
        Assert.Equal(2, g.Retarget("Droerni", "Mundos/Droerni"));
        Assert.Equal(("Mundos/Droerni", "Droerni"), (g.Node(folder.Id)!.Target, g.Node(folder.Id)!.Label));
        Assert.Equal("Mundos/Droerni/Mapas/mapa.png", g.Node(inside.Id)!.Target);
        Assert.Equal("Droerni antigo/nota.md", g.Node(near.Id)!.Target);
    }

    [Fact]
    public void ACaseOnlyRenameIsFollowedAndNothingElseChanges()
    {
        var g = GraphDocument.Create("Mapa");
        var note = g.AddReference(GraphNodeKind.Note, "plano.md");
        var before = g.Serialize();
        Assert.Equal(0, g.Retarget("Outra.md", "Mais.md"));
        Assert.Equal(before, g.Serialize());
        Assert.Equal(1, g.Retarget("plano.md", "Plano.md"));
        Assert.Equal(("Plano.md", "Plano.md"), (g.Node(note.Id)!.Target, g.Node(note.Id)!.Label));
    }
}
