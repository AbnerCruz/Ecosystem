using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class WorldStableIdsTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalLegacyFixtureGetsExactJavascriptCompatibleIds()
    {
        var path = Path.Combine(
            Root,
            "..",
            "tests",
            "fixtures",
            "vaults",
            "v1-mapa-v4",
            ".urbe",
            "mapa.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        var assignment = WorldStableIds.Assign(root);

        var region = root["regioes"]!.AsArray()[0]!.AsObject();
        var asset = root["construcoes"]!.AsArray()[0]!.AsObject();

        Assert.Equal("reg_qfce2k", region["id"]!.GetValue<string>());
        Assert.Equal("ast_1n5z61x", asset["id"]!.GetValue<string>());
        Assert.Equal("reg_qfce2k", assignment.Regions["Pasta"]);
        Assert.Equal("ast_1n5z61x", Assert.Single(assignment.Assets));

        Assert.Equal("Pasta", region["caminho"]!.GetValue<string>());
        Assert.Equal("Alfa", asset["parentNoteName"]!.GetValue<string>());
        Assert.Equal("Pasta/logo.png",
            asset["files"]!.AsArray()[0]!["relPath"]!.GetValue<string>());
    }

    [Fact]
    public void AssignmentIsIdempotentAndResolvesDuplicatePathsAndIdsLikeJavascript()
    {
        var map = JsonNode.Parse(
            """
            {
              "regioes":[
                {"caminho":"X"},
                {"caminho":"X"},
                {"caminho":"Y","id":"reg_dup"},
                {"caminho":"Z","id":"reg_dup"},
                null,
                3
              ],
              "construcoes":[
                {"name":"f","caminho":""},
                {"name":"f","caminho":""},
                {"name":"bad","id":"reg_wrong"}
              ]
            }
            """)!.AsObject();

        WorldStableIds.Assign(map);
        var allIds = map["regioes"]!.AsArray()
            .OfType<JsonObject>()
            .Concat(map["construcoes"]!.AsArray().OfType<JsonObject>())
            .Select(item => item["id"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(allIds.Length, allIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("reg_dup",
            map["regioes"]!.AsArray()[2]!["id"]!.GetValue<string>());
        Assert.StartsWith("reg_",
            map["regioes"]!.AsArray()[3]!["id"]!.GetValue<string>());
        Assert.StartsWith("ast_",
            map["construcoes"]!.AsArray()[2]!["id"]!.GetValue<string>());

        var once = allIds.ToArray();
        WorldStableIds.Assign(map);
        var twice = map["regioes"]!.AsArray()
            .OfType<JsonObject>()
            .Concat(map["construcoes"]!.AsArray().OfType<JsonObject>())
            .Select(item => item["id"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(once, twice);
    }

    [Fact]
    public void RegionForUsesDeterministicIdsAndOptionalCollisionSet()
    {
        Assert.Equal("reg_qfce2k", WorldStableIds.RegionFor("Pasta"));
        Assert.Equal("reg_qfce2k", WorldStableIds.RegionFor("Pasta"));

        var used = new HashSet<string>(StringComparer.Ordinal)
        {
            "reg_qfce2k"
        };

        Assert.Equal("reg_qfce2k_2", WorldStableIds.RegionFor("Pasta", used));
        Assert.Equal("reg_qfce2k_3", WorldStableIds.RegionFor("Pasta", used));
    }

    [Fact]
    public void EnsureUsesFreshUidOnlyWhenMissingOrWrongPrefix()
    {
        var region = new JsonObject();
        var first = WorldStableIds.Ensure(region, "reg");
        var second = WorldStableIds.Ensure(region, "reg");

        Assert.Equal(first, second);
        Assert.StartsWith("reg_", first);
        Assert.Equal(first, region["uid"]!.GetValue<string>());

        var other = new JsonObject { ["uid"] = "ast_wrong" };
        var fixedId = WorldStableIds.Ensure(other, "reg");
        Assert.StartsWith("reg_", fixedId);
        Assert.NotEqual("ast_wrong", fixedId);

        Assert.NotEqual(
            WorldStableIds.Ensure(new JsonObject(), "ast"),
            WorldStableIds.Ensure(new JsonObject(), "ast"));
    }

    [Theory]
    [InlineData("reg", "reg_x", true)]
    [InlineData("reg", "reg_", false)]
    [InlineData("reg", "ast_x", false)]
    [InlineData("ast", null, false)]
    public void ValidMatchesLegacyPrefixContract(
        string prefix,
        string? value,
        bool expected)
    {
        Assert.Equal(expected, WorldStableIds.Valid(prefix, value));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Urbe.Portable.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Execute os testes dentro do checkout do Urbe.");
    }
}

public sealed class WorldProjectionTests
{
    [Fact]
    public void ProjectsDocumentsRegionsAssetsAndPreservesUnknownMetadata()
    {
        var store = new DocumentStore();
        store.ReplaceAll(
        [
            new DocumentInput
            {
                Id = "doc_a",
                Path = "A.md",
                Content = "#tag",
                Created = "doc-created"
            },
            new DocumentInput
            {
                Id = "doc_b",
                Path = "Pasta/B.md",
                Content = "b"
            }
        ]);

        var metadata = WorldMapMetadata.Parse(
            JsonNode.Parse(
                """
                {
                  "v":4,
                  "mundo":"placas-1",
                  "customRoot":{"keep":true},
                  "notas":{
                    "A.md":{"x":4,"y":5,"sprite":"house2","criado":"map-created","custom":"note-extra"},
                    "Pasta/B.md":{"x":8,"y":9,"modificado":"map-modified"}
                  },
                  "regioes":[
                    {"caminho":"Pasta","nome":"Pasta","cor":"#4aa3ff","x":2,"y":3,"w":9,"h":8,"custom":"region-extra"}
                  ],
                  "construcoes":[
                    {"caminho":"Pasta","name":"logo.png","files":[{"relPath":"Pasta/logo.png"}],"custom":"asset-extra"}
                  ]
                }
                """)!.AsObject());

        using var projection = new WorldProjection(store);
        var snapshot = projection.Load(metadata);

        Assert.Equal("placas-1", snapshot.Mundo);
        Assert.False(snapshot.IsReadOnly);
        Assert.Equal(2, snapshot.Documents.Count);

        var a = projection.ProjectDocument("doc_a")!;
        Assert.Equal(4d, a.X);
        Assert.Equal(5d, a.Y);
        Assert.Equal("house2", a.Sprite);
        Assert.Equal("doc-created", a.Created);
        Assert.Equal(string.Empty, a.Folder);
        Assert.Contains("tag", a.Tags);

        var b = projection.ProjectDocument("pasta/b.md")!;
        Assert.Equal("Pasta", b.Folder);
        Assert.Equal("map-modified", b.Modified);

        var region = projection.Region("Pasta")!;
        Assert.Equal("reg_qfce2k", region.Id);
        Assert.Equal("region-extra", region.Raw["custom"]!.GetValue<string>());
        Assert.Null(projection.Region("pasta"));

        var asset = Assert.Single(snapshot.Assets);
        Assert.Equal("ast_1n5z61x", asset.Id);
        Assert.Equal("asset-extra", asset.Raw["custom"]!.GetValue<string>());

        var output = projection.Metadata();
        Assert.True(output["customRoot"]!["keep"]!.GetValue<bool>());
        Assert.Equal("note-extra",
            output["notas"]!["A.md"]!["custom"]!.GetValue<string>());
        Assert.Equal("region-extra",
            output["regioes"]!.AsArray()[0]!["custom"]!.GetValue<string>());
        Assert.Equal("asset-extra",
            output["construcoes"]!.AsArray()[0]!["custom"]!.GetValue<string>());
        Assert.Equal("doc_a",
            output["notas"]!["A.md"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void SpatialMetadataSurvivesRenameByStableDocumentIdentity()
    {
        var store = new DocumentStore();
        var original = store.Upsert(
            new DocumentInput
            {
                Id = "doc_stable",
                Path = "A.md",
                Content = "a"
            });

        using var projection = new WorldProjection(store);
        projection.Load(
            JsonNode.Parse(
                """{"v":4,"notas":{"A.md":{"x":20,"y":21,"sprite":"house3"}}}""")!
                .AsObject());

        var events = new List<WorldProjectionChangedEventArgs>();
        projection.Changed += (_, args) => events.Add(args);

        store.Upsert(
            new DocumentInput
            {
                Id = original.Id,
                Path = "Renomeada.md",
                Content = "a"
            });

        var moved = projection.ProjectDocument(original.Id)!;
        Assert.Equal(20d, moved.X);
        Assert.Equal(21d, moved.Y);
        Assert.Equal("house3", moved.Sprite);
        Assert.Null(projection.ProjectDocument("A.md"));

        var metadata = projection.Metadata();
        Assert.Null(metadata["notas"]!["A.md"]);
        Assert.Equal(20d,
            metadata["notas"]!["Renomeada.md"]!["x"]!.GetValue<double>());
        Assert.Equal("doc_stable",
            metadata["notas"]!["Renomeada.md"]!["id"]!.GetValue<string>());

        var pathEvent = Assert.Single(
            events,
            item => item.Kind == WorldProjectionChangeKind.PathMigrated);
        Assert.Equal("A.md", pathEvent.From);
        Assert.Equal("Renomeada.md", pathEvent.To);
        Assert.Equal("doc_stable", pathEvent.DocumentId);
    }

    [Fact]
    public void CaseOnlyRenameMigratesSpatialBecauseJavascriptPathComparisonIsCaseSensitive()
    {
        var store = new DocumentStore();
        var original = store.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "A.md",
                Content = "a"
            });

        using var projection = new WorldProjection(store);
        projection.Load(
            JsonNode.Parse(
                """{"notas":{"A.md":{"x":1,"y":2}}}""")!.AsObject());

        store.Upsert(
            new DocumentInput
            {
                Id = original.Id,
                Path = "a.md",
                Content = "a"
            });

        Assert.Equal(1d, projection.ProjectDocument("doc_a")!.X);
        var output = projection.Metadata();
        Assert.Null(output["notas"]!["A.md"]);
        Assert.NotNull(output["notas"]!["a.md"]);
    }

    [Fact]
    public void SpatialLookupRemainsCaseSensitiveLikeJavascriptMap()
    {
        var store = new DocumentStore();
        store.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "a.md",
                Content = "a"
            });

        using var projection = new WorldProjection(store);
        projection.Load(
            JsonNode.Parse(
                """{"notas":{"A.md":{"x":4,"y":5}}}""")!.AsObject());

        var document = projection.ProjectDocument("A.MD")!;
        Assert.Null(document.X);
        Assert.Null(document.Y);
    }

    [Fact]
    public void SetSpatialPreservesUnknownFieldsAndRevisionSemantics()
    {
        var store = new DocumentStore();
        store.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "A.md",
                Content = "a"
            });

        using var projection = new WorldProjection(store);
        projection.Load(
            JsonNode.Parse(
                """{"notas":{"A.md":{"x":1,"y":2,"custom":"keep"}}}""")!
                .AsObject());
        Assert.Equal(1, projection.Revision);

        var moved = projection.SetSpatial(
            "doc_a",
            new JsonObject
            {
                ["x"] = 7,
                ["sprite"] = "house4"
            })!;

        Assert.Equal(2, projection.Revision);
        Assert.Equal(7d, moved.X);
        Assert.Equal(2d, moved.Y);
        Assert.Equal("house4", moved.Sprite);

        var output = projection.Metadata();
        Assert.Equal("keep",
            output["notas"]!["A.md"]!["custom"]!.GetValue<string>());

        projection.SetEnabled(false);
        Assert.False(projection.Enabled);
        Assert.Equal(2, projection.Revision);
    }

    [Fact]
    public void FutureAndCorruptMapsAreReadOnlyAndNeverReceiveGeneratedIds()
    {
        foreach (var json in new[]
                 {
                     """{"v":9,"regioes":[{"caminho":"Pasta"}],"construcoes":[{"name":"x"}]}""",
                     """{"v":1.5,"regioes":[{"caminho":"Pasta"}],"construcoes":[{"name":"x"}]}"""
                 })
        {
            var metadata = WorldMapMetadata.Parse(
                JsonNode.Parse(json)!.AsObject());

            Assert.True(metadata.IsReadOnly);
            Assert.DoesNotContain(
                metadata.Regions,
                region => !string.IsNullOrEmpty(region.Id));
            Assert.DoesNotContain(
                metadata.Assets,
                asset => !string.IsNullOrEmpty(asset.Id));

            var store = new DocumentStore();
            using var projection = new WorldProjection(store);
            projection.Load(metadata);
            Assert.Throws<InvalidOperationException>(
                () => projection.Metadata());
        }
    }

    [Fact]
    public void VaultHelperLoadsPhysicalMapButRefusesRecoveredJournalAmbiguity()
    {
        var normal = VaultReader.Read(
        [
            new VaultFile("A.md", Encoding.UTF8.GetBytes("a")),
            new VaultFile(
                ".urbe/mapa.json",
                Encoding.UTF8.GetBytes(
                    """{"v":4,"mundo":"placas-1","notas":{"A.md":{"x":3,"y":4}}}"""))
        ]);

        var metadata = WorldMapMetadata.FromVault(normal);
        Assert.Equal(WorldMapState.Current, metadata.State);
        Assert.Equal("placas-1", metadata.Mundo);

        var recovered = VaultReader.Read(
        [
            new VaultFile("A.md", Encoding.UTF8.GetBytes("disk")),
            new VaultFile(
                ".urbe/journal.json",
                Encoding.UTF8.GetBytes(
                    """
                    {
                      "version":1,
                      "documents":[{"id":"doc_a","path":"A.md","content":"journal"}],
                      "metadata":{"v":4,"regioes":[{"caminho":"Journal"}]}
                    }
                    """))
        ]);

        Assert.True(recovered.RecoveredFromJournal);
        Assert.Throws<InvalidOperationException>(
            () => WorldMapMetadata.FromVault(recovered));
    }

    [Fact]
    public void RegionDuplicatePathKeepsLastValueButStableIdsRemainUnique()
    {
        var metadata = WorldMapMetadata.Parse(
            JsonNode.Parse(
                """
                {
                  "regioes":[
                    {"caminho":"X","nome":"Primeiro"},
                    {"caminho":"X","nome":"Segundo"}
                  ]
                }
                """)!.AsObject());

        Assert.Equal(2, metadata.Regions.Count);
        Assert.NotEqual(metadata.Regions[0].Id, metadata.Regions[1].Id);

        using var projection = new WorldProjection(new DocumentStore());
        projection.Load(metadata);

        Assert.Equal("Segundo", projection.Region("X")!.Name);
        Assert.Single(projection.Snapshot().Regions);
        Assert.Single(projection.Metadata()["regioes"]!.AsArray());
    }
}
