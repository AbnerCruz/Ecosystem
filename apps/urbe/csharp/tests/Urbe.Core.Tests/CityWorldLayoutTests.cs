using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class CityWorldLayoutTests
{
    [Fact]
    public void OverflowNotesAreVisibleOutsideFirstEightRows()
    {
        var sources = Enumerable.Range(0, 76)
            .Select(i => File($"Nota-{i:D3}.md", "texto")).ToArray();
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read(sources));

        var beyond = CityWorldLayout.Visible(session.ExplorerEntries(),
            session.World, 0, 8, 8, 4, false);
        Assert.Contains(beyond, cell => cell.Row >= 8 && cell.Entry is not null);
        Assert.Equal(12, beyond.Count);
        Assert.DoesNotContain(beyond, cell => cell.Entry is null);
    }

    [Fact]
    public void SavedWorldCoordinateIsVisibleOnlyInsideCamera()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([File("Minha.md", "conteúdo")]));

        Assert.True(session.PlaceHouseInWorld("Minha.md", 73, -12));
        var here = CityWorldLayout.Visible(session.ExplorerEntries(),
            session.World, 65, -16, 16, 16, false);
        Assert.Contains(here, c => c.Column == 73 && c.Row == -12 &&
            c.Entry?.Path == "Minha.md");
        var away = CityWorldLayout.Visible(session.ExplorerEntries(),
            session.World, 0, 0, 8, 8, false);
        Assert.DoesNotContain(away, c => c.Entry?.Path == "Minha.md");
    }

    [Fact]
    public void WorldPlacementPersistsAndDoesNotChangeLegacyBoundary()
    {
        var input = new[]
        {
            File("Bairro/A.md", "A"),
            File("Bairro/B.md", "B"),
            File(".urbe/mapa.json", """{"v":4,"custom":{"keep":42}}""")
        };
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read(input), ["Bairro"]);
        Assert.False(session.PlaceHouse("Bairro/A.md", 12, 11));
        Assert.True(session.PlaceHouseInWorld("Bairro/A.md", 12, 11));
        Assert.False(session.PlaceHouseInWorld("Bairro/B.md", 12, 11));
        Assert.True(session.PlaceHouseInWorld("Bairro/B.md", -12, 14));
        Assert.True(session.HasSpatialChanges);

        var plan = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = input,
            Documents = session.Documents.List()
                .Select(d => new VaultWriteDocument(d.Id, d.Path, d.Content)).ToArray(),
            MetadataJson = session.World.Metadata().ToJsonString(),
            AppVersion = "urbe-csharp-beta"
        });
        Assert.True(plan.Writable);
        var json = JsonNode.Parse(plan.Files[".urbe/mapa.json"].Bytes.Span)!;
        Assert.Equal(42, json["custom"]!["keep"]!.GetValue<int>());
        Assert.Equal(12, json["notas"]!["Bairro/A.md"]!["x"]!.GetValue<int>());
        Assert.Equal(-12, json["notas"]!["Bairro/B.md"]!["x"]!.GetValue<int>());

        using var reopened = new WorkspaceSession();
        reopened.Load(VaultReader.Read(plan.Files.Values), ["Bairro"]);
        Assert.Equal(-12, reopened.World.ProjectDocument("Bairro/B.md")?.X);
        Assert.Equal(14, reopened.World.ProjectDocument("Bairro/B.md")?.Y);
    }

    [Fact]
    public void HouseCreationOutsideInitialLotsCreatesOnePhysicalMarkdownFile()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([File("Bairro/A.md", "A")]), ["Bairro"]);
        var created = session.CreateHouseInWorld("Nova", "Bairro", 80, -6);
        Assert.NotNull(created);
        Assert.Equal(80, session.World.ProjectDocument(created.Path)?.X);
        Assert.Equal(-6, session.World.ProjectDocument(created.Path)?.Y);
        Assert.Null(session.CreateHouseInWorld("Falhou", "Bairro", 80, -6));
        Assert.Single(session.Documents.List().Where(d => d.Title == "Nova"));
    }

    [Fact]
    public void OnlyOccupiedTilesRenderUntilBuildingMode()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([File("A.md", "A")]));
        var passive = CityWorldLayout.Visible(session.ExplorerEntries(),
            session.World, 0, 0, 24, 20, false);
        var editable = CityWorldLayout.Visible(session.ExplorerEntries(),
            session.World, 0, 0, 24, 20, true);
        Assert.Single(passive);
        Assert.Equal(480, editable.Count);
        Assert.Equal("A.md", editable[0].Entry?.Path);
        Assert.Equal(479, editable.Count(c => c.IsEmpty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CityWorldLayout.Visible(session.ExplorerEntries(),
                session.World, 0, 0, 49, 1, false));
    }

    private static VaultFile File(string path, string value) =>
        new(path, Encoding.UTF8.GetBytes(value));
}
