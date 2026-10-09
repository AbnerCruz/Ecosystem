using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class CityLayoutBuilderTests
{
    [Fact]
    public void EmptyWorkspaceContainsRootDistrictWithoutHouses()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        using var world = new WorldProjection(store);

        var layout = CityLayoutBuilder.Build(world.Snapshot(), knowledge);

        Assert.Single(layout.Districts);
        Assert.Equal("Raiz", layout.Districts[0].Name);
        Assert.Empty(layout.Buildings);
        Assert.Empty(layout.Roads);
        Assert.Equal(0, layout.HiddenNoteCount);
    }

    [Fact]
    public void NotesBecomeHousesAndReciprocalWikilinksBecomeOneRoad()
    {
        var store = new DocumentStore();
        var first = store.Upsert(new DocumentInput
        {
            Path = "Projetos/Alfa.md",
            Content = "[[Beta]]"
        });
        var second = store.Upsert(new DocumentInput
        {
            Path = "Projetos/Beta.md",
            Content = "[[Alfa]]"
        });
        store.Upsert(new DocumentInput
        {
            Path = "Modelos/Modelo.md",
            Content = "# {{Tema}}"
        });

        using var knowledge = new KnowledgeIndex(store);
        using var world = new WorldProjection(store);

        var layout = CityLayoutBuilder.Build(
            world.Snapshot(), knowledge, ["Projetos", "Vazio"]);

        Assert.Equal(2, layout.Buildings.Count);
        Assert.Equal(3, layout.Districts.Count);
        Assert.Single(layout.Roads);
        Assert.Contains(layout.Buildings, house => house.DocumentId == first.Id);
        Assert.Contains(layout.Buildings, house => house.DocumentId == second.Id);
        Assert.Contains(layout.Districts, district => district.Path == "Vazio" && district.NoteCount == 0);

        var road = layout.Roads[0];
        Assert.True(road.X1 != road.X2 || road.Y1 != road.Y2);
    }

    [Fact]
    public void LayoutIsDeterministicAndNeverOverlapsHousesWithinDistrict()
    {
        var store = new DocumentStore();
        foreach (var name in Enumerable.Range(0, 48).Reverse())
            store.Upsert(new DocumentInput { Path = $"Notas/Nota-{name:000}.md" });

        using var knowledge = new KnowledgeIndex(store);
        using var world = new WorldProjection(store);
        var first = CityLayoutBuilder.Build(world.Snapshot(), knowledge);
        var second = CityLayoutBuilder.Build(world.Snapshot(), knowledge);

        Assert.Equal(first.Buildings, second.Buildings);
        Assert.Equal(first.Districts, second.Districts);

        foreach (var group in first.Buildings.GroupBy(house => house.Folder))
        {
            var positions = group.Select(house => (house.X, house.Y)).ToArray();
            Assert.Equal(positions.Length, positions.Distinct().Count());
        }
    }

    [Fact]
    public void LargeVaultIsBoundedAndReportsOmittedHouses()
    {
        var store = new DocumentStore();
        foreach (var name in Enumerable.Range(0, CityLayoutBuilder.MaxNotes + 7))
            store.Upsert(new DocumentInput { Path = $"Nota-{name:000}.md" });

        using var knowledge = new KnowledgeIndex(store);
        using var world = new WorldProjection(store);

        var layout = CityLayoutBuilder.Build(world.Snapshot(), knowledge);

        Assert.Equal(CityLayoutBuilder.MaxNotes, layout.Buildings.Count);
        Assert.Equal(7, layout.HiddenNoteCount);
    }
}
