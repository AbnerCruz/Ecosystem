using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class CityTileLayoutTests
{
    [Fact]
    public void MovingHousePersistsThroughCanonicalVaultMapAndReopensAtSameTile()
    {
        var files = new[]
        {
            File("Centro/A.md", "A"),
            File("Centro/B.md", "B"),
            File(".urbe/mapa.json",
                """{"v":4,"custom":{"preserve":true},"notas":{"Centro/A.md":{"sprite":"house4","extra":"kept"}}}""")
        };
        using var session = new WorkspaceSession();
        var original = VaultReader.Read(files);
        session.Load(original, ["Centro"]);

        Assert.False(session.HasUnsavedChanges);
        Assert.True(session.PlaceHouse("Centro/A.md", 6, 4));
        Assert.True(session.HasSpatialChanges);
        Assert.True(session.HasUnsavedChanges);

        var metadata = session.World.Metadata().ToJsonString();
        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = session.Documents.List()
                .Select(document => new VaultWriteDocument(
                    document.Id, document.Path, document.Content)).ToArray(),
            MetadataJson = metadata,
            AppVersion = "urbe-csharp-beta"
        });

        Assert.True(result.Writable);
        var map = JsonNode.Parse(result.Files[".urbe/mapa.json"].Bytes.Span)!;
        Assert.True(map["custom"]!["preserve"]!.GetValue<bool>());
        Assert.Equal("kept", map["notas"]!["Centro/A.md"]!["extra"]!.GetValue<string>());
        Assert.Equal("house4", map["notas"]!["Centro/A.md"]!["sprite"]!.GetValue<string>());
        Assert.Equal(6, map["notas"]!["Centro/A.md"]!["x"]!.GetValue<int>());
        Assert.Equal(4, map["notas"]!["Centro/A.md"]!["y"]!.GetValue<int>());

        using var reopened = new WorkspaceSession();
        reopened.Load(VaultReader.Read(result.Files.Values), ["Centro"]);
        var position = reopened.World.ProjectDocument("Centro/A.md");
        Assert.Equal(6, position?.X);
        Assert.Equal(4, position?.Y);
        Assert.False(reopened.HasUnsavedChanges);
        Assert.False(reopened.HasSpatialChanges);
        Assert.Contains(CityTileLayout.Build(reopened.ExplorerEntries("Centro"), reopened.World),
            tile => tile.Column == 6 && tile.Row == 4 &&
                tile.Entry?.Path == "Centro/A.md");

        session.MarkSaved();
        Assert.False(session.HasUnsavedChanges);
        Assert.False(session.HasSpatialChanges);
    }

    [Fact]
    public void LayoutIsBoundedDeterministicAndDoesNotOverlapSavedHouses()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([
            File("A.md", "A"),
            File("B.md", "B"),
            File("C.md", "C")
        ]), ["BairroVazio"]);

        Assert.True(session.PlaceHouse("B.md", 7, 7));
        var one = CityTileLayout.Build(session.ExplorerEntries(), session.World);
        var two = CityTileLayout.Build(session.ExplorerEntries(), session.World);

        Assert.Equal(64, one.Count);
        Assert.Equal(one.Select(t => t.Entry?.Path),
            two.Select(t => t.Entry?.Path));
        Assert.Equal(one.Count, one.Select(t => (t.Column, t.Row)).Distinct().Count());
        Assert.Equal("B.md", one.Single(t => t.Column == 7 && t.Row == 7).Entry?.Path);
        Assert.Contains(one, tile => tile.Entry?.Path == "BairroVazio");
        Assert.False(session.PlaceHouse("C.md", 7, 7));
        Assert.True(session.PlaceHouse("C.md", 3, 5));
        Assert.True(session.HasUnsavedChanges);
        Assert.False(session.PlaceHouse("C.md", -1, 0));
        Assert.False(session.PlaceHouse("C.md", 2, 8));
        Assert.False(session.PlaceHouse("BairroVazio", 1, 1));
    }

    [Fact]
    public void BuildAtTappedLotCreatesMarkdownAndPersistsPositionTogether()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([
            File("Bairro/Existente.md", "antigo")
        ]), ["Bairro"]);

        var note = session.CreateHouseAt("Casa nova", "Bairro", 5, 3);
        Assert.NotNull(note);
        Assert.Equal("Bairro/Casa nova.md", note!.Path);
        Assert.Equal(5, session.World.ProjectDocument(note.Path)?.X);
        Assert.Equal(3, session.World.ProjectDocument(note.Path)?.Y);
        Assert.True(session.HasUnsavedChanges);
        Assert.True(session.HasSpatialChanges);

        var count = session.Documents.List().Count;
        Assert.Null(session.CreateHouseAt("Não criar", "Bairro", 5, 3));
        Assert.Null(session.CreateHouseAt("Não criar", "Bairro", -1, 3));
        Assert.Null(session.CreateHouseAt("Não criar", "Bairro", 0, 8));
        Assert.Equal(count, session.Documents.List().Count);
        Assert.DoesNotContain(session.Paths, path => path.Contains("Não criar", StringComparison.Ordinal));

        var saved = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = [File("Bairro/Existente.md", "antigo")],
            Documents = session.Documents.List().Select(document =>
                new VaultWriteDocument(document.Id, document.Path, document.Content)).ToArray(),
            MetadataJson = session.World.Metadata().ToJsonString()
        });
        using var reopened = new WorkspaceSession();
        reopened.Load(VaultReader.Read(saved.Files.Values), ["Bairro"]);
        Assert.Equal(5, reopened.World.ProjectDocument(note.Path)?.X);
        Assert.Equal(3, reopened.World.ProjectDocument(note.Path)?.Y);
        Assert.NotNull(reopened.Documents.Get(note.Path));
        Assert.False(reopened.HasUnsavedChanges);
    }

    [Fact]
    public void CreateBairroAppearsWithoutSyntheticMarkdownAndSurvivesReload()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read(Array.Empty<VaultFile>()), ["Centro"]);
        var newBairro = session.CreateFolder("Novo bairro", "Centro");
        Assert.Equal("Centro/Novo bairro", newBairro);
        Assert.True(session.HasUnsavedChanges);
        Assert.Contains(session.ExplorerEntries("Centro"), entry =>
            entry.IsFolder && entry.Path == newBairro);

        // The physical SAF host persists the empty folder; it is not modeled
        // as a fake .md file in VaultWriter.
        session.Load(VaultReader.Read(Array.Empty<VaultFile>()),
            ["Centro", "Centro/Novo bairro"]);
        Assert.Contains(session.Folders, x => x == "Centro/Novo bairro");
        Assert.Empty(session.Documents.List());
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public void FutureMapRemainsReadOnlyAndOriginalBytesUntouched()
    {
        var files = new[]
        {
            File("Note.md", "original"),
            File(".urbe/mapa.json", """{"v":999,"notas":{"Note.md":{"x":3,"y":2}}}""")
        };
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read(files));

        Assert.True(session.World.IsReadOnly);
        Assert.False(session.PlaceHouse("Note.md", 1, 1));
        Assert.False(session.HasSpatialChanges);
        Assert.False(session.HasUnsavedChanges);
        Assert.Equal("original", Encoding.UTF8.GetString(files[0].Bytes.Span));
    }

    private static VaultFile File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));
}
