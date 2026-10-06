using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageModelTests
{
    [Fact]
    public void CurrentPagePreservesUnknownFieldsAndOriginalVersion()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "kind":"urbe-page",
              "meta":{"title":"Início","lang":"pt-BR","futureMeta":{"keep":true}},
              "theme":{"preset":"aurora","futureTheme":7},
              "layout":{"format":"web"},
              "sections":[
                {"id":"s1","type":"hero","props":{"title":"Olá","futureProp":"x"},"style":{},"futureSection":3}
              ],
              "futureRoot":{"nested":true}
            }
            """);

        Assert.Equal(PageDocumentState.Current, page.State);
        Assert.False(page.IsReadOnly);
        Assert.Equal(1, page.Version);
        Assert.Equal("Início", page.Title);
        Assert.Single(page.Sections);

        var roundTrip = JsonNode.Parse(page.SerializeForWrite())!.AsObject();
        Assert.Equal(1, roundTrip["version"]!.GetValue<int>());
        Assert.True(roundTrip["futureRoot"]!["nested"]!.GetValue<bool>());
        Assert.True(roundTrip["meta"]!["futureMeta"]!["keep"]!.GetValue<bool>());
        Assert.Equal("x",
            roundTrip["sections"]!.AsArray()[0]!["props"]!["futureProp"]!.GetValue<string>());
        Assert.Equal(3,
            roundTrip["sections"]!.AsArray()[0]!["futureSection"]!.GetValue<int>());

        var edited = page.WithMeta("description", "Nova descrição").ForWrite();
        Assert.Equal("Nova descrição", edited["meta"]!["description"]!.GetValue<string>());
        Assert.True(edited["futureRoot"]!["nested"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("""{"version":99,"kind":"urbe-page","future":{"keep":1}}""", PageDocumentState.Future)]
    [InlineData("""{"version":0,"kind":"urbe-page"}""", PageDocumentState.Corrupt)]
    [InlineData("""{"version":"1","kind":"urbe-page"}""", PageDocumentState.Corrupt)]
    [InlineData("""[]""", PageDocumentState.Corrupt)]
    [InlineData("""{oops""", PageDocumentState.Corrupt)]
    public void FutureAndCorruptPagesAreReadOnly(string json, PageDocumentState expected)
    {
        var page = PageDocument.Parse(json);

        Assert.Equal(expected, page.State);
        Assert.True(page.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => page.ForWrite());
    }

    [Fact]
    public void MissingVersionRemainsMissingInsteadOfBeingSilentlyRewritten()
    {
        var page = PageDocument.Parse(
            """{"kind":"urbe-page","meta":{"title":"Legada"},"sections":[]}""");

        Assert.Equal(PageDocumentState.Current, page.State);
        Assert.Null(page.Version);

        var output = page.SerializeForWrite();
        Assert.DoesNotContain("\"version\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticRendererUsesMarkdownAndDocumentsWithoutDomOrJavascript()
    {
        var documents = new DocumentStore();
        documents.ReplaceAll(
        [
            new DocumentInput
            {
                Id = "doc_a",
                Path = "Guia/Alfa.md",
                Content = "# Alfa\n\nTexto **forte**.",
                Tags = ["guia"],
                Modified = "2026-10-06"
            },
            new DocumentInput
            {
                Id = "doc_b",
                Path = "Guia/Beta.md",
                Content = "# Beta\n\nOutro texto.",
                Tags = ["guia"],
                Modified = "2026-10-05"
            }
        ]);

        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "kind":"urbe-page",
              "meta":{"title":"Manual","lang":"pt-BR"},
              "sections":[
                {"id":"h","type":"hero","props":{"title":"Olá","subtitle":"**Urbe**"}},
                {"id":"t","type":"text","props":{"title":"Introdução","markdown":"Veja **isto**."}},
                {"id":"n","type":"note","props":{"path":"Guia/Alfa.md","showTitle":true}},
                {"id":"c","type":"notes","props":{"source":"tag","tag":"guia","sort":"title","expand":true}}
              ]
            }
            """);

        var html = PageRenderer.Render(page, documents);

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Manual</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Olá</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Urbe</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<h2>Introdução</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>isto</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class='note-title'>Alfa</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Texto <strong>forte</strong>.", html, StringComparison.Ordinal);
        Assert.Contains("<h3>Beta</h3>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HiddenSectionsAndUnsafeImageUrlsDoNotLeakActiveContent()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[
                {"type":"text","props":{"markdown":"segredo"},"style":{"hidden":true}},
                {"type":"image","props":{"src":"javascript:alert(1)","alt":"x"}}
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        Assert.DoesNotContain("segredo", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Escolha uma imagem", html, StringComparison.Ordinal);
    }
}



public sealed class PageNormalizerTests
{
    [Fact]
    public void DefaultsAreAppliedWithoutDroppingUnknownFieldsOrRewritingVersion()
    {
        var source = PageDocument.Parse(
            """
            {
              "version":1,
              "kind":"urbe-page",
              "futureRoot":{"keep":true},
              "meta":{"futureMeta":7},
              "theme":{"futureTheme":"x"},
              "layout":{"futureLayout":3},
              "sections":[
                {
                  "id":"s1",
                  "type":"hero",
                  "futureSection":{"keep":true},
                  "props":{"futureProp":"ok"},
                  "style":{"futureStyle":9}
                }
              ]
            }
            """);

        var result = PageNormalizer.Normalize(source);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        var root = result.Page.Raw;
        Assert.Equal(1, root["version"]!.GetValue<int>());
        Assert.True(root["futureRoot"]!["keep"]!.GetValue<bool>());
        Assert.Equal(7, root["meta"]!["futureMeta"]!.GetValue<int>());
        Assert.Equal("Página sem título", root["meta"]!["title"]!.GetValue<string>());
        Assert.Equal("pt-BR", root["meta"]!["lang"]!.GetValue<string>());
        Assert.Equal("x", root["theme"]!["futureTheme"]!.GetValue<string>());
        Assert.Equal("aurora", root["theme"]!["preset"]!.GetValue<string>());
        Assert.Equal(3, root["layout"]!["futureLayout"]!.GetValue<int>());
        Assert.Equal("web", root["layout"]!["format"]!.GetValue<string>());

        var section = root["sections"]!.AsArray()[0]!.AsObject();
        Assert.True(section["futureSection"]!["keep"]!.GetValue<bool>());
        Assert.Equal("ok", section["props"]!["futureProp"]!.GetValue<string>());
        Assert.Equal("Um título que diz tudo",
            section["props"]!["title"]!.GetValue<string>());
        Assert.Equal(9, section["style"]!["futureStyle"]!.GetValue<int>());
        Assert.False(section["style"]!["hidden"]!.GetValue<bool>());
    }

    [Fact]
    public void MissingVersionStaysMissingAfterNormalization()
    {
        var source = PageDocument.Parse(
            """{"kind":"urbe-page","sections":[]}""");

        var result = PageNormalizer.Normalize(source);

        Assert.True(result.IsValid);
        Assert.Null(result.Page.Raw["version"]);
        Assert.Equal(
            "Página sem título",
            result.Page.Raw["meta"]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void UnknownBlocksAndMalformedKnownContainersArePreservedWithDiagnostics()
    {
        var source = PageDocument.Parse(
            """
            {
              "version":1,
              "meta":"não normalizar",
              "sections":[
                {"type":"future-block","props":{"x":1},"style":{}},
                42,
                {"type":"text","props":"texto cru","style":{}}
              ]
            }
            """);

        var result = PageNormalizer.Normalize(source);

        Assert.False(result.IsValid);
        Assert.Contains(result.Warnings,
            issue => issue.Path == "sections[0].type");
        Assert.Contains(result.Errors,
            issue => issue.Path == "meta");
        Assert.Contains(result.Errors,
            issue => issue.Path == "sections[1]");
        Assert.Contains(result.Errors,
            issue => issue.Path == "sections[2].props");

        var root = result.Page.Raw;
        Assert.Equal("não normalizar", root["meta"]!.GetValue<string>());
        Assert.Equal("future-block",
            root["sections"]!.AsArray()[0]!["type"]!.GetValue<string>());
        Assert.Equal(42, root["sections"]!.AsArray()[1]!.GetValue<int>());
        Assert.Equal("texto cru",
            root["sections"]!.AsArray()[2]!["props"]!.GetValue<string>());
    }

    [Fact]
    public void FuturePageNormalizationIsRefusedWithoutMutatingInput()
    {
        var source = PageDocument.Parse(
            """{"version":99,"kind":"urbe-page","future":{"keep":true}}""");
        var before = source.Raw.ToJsonString();

        var result = PageNormalizer.Normalize(source);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(before, result.Page.Raw.ToJsonString());
        Assert.Equal(PageDocumentState.Future, result.Page.State);
    }
}

public sealed class CompositionMigrationPlannerTests
{
    [Fact]
    public void PlanIsIdempotentWhenGeneratedPageAlreadyExists()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "A.md",
                Content = "# A"
            });

        var file = CompositionFile.Parse(
            """{"version":1,"items":[{"id":"cmp_a","name":"Livro","sources":["doc_a"]}]}""");

        var first = CompositionMigrationPlanner.Plan(file, documents);

        Assert.False(first.Blocked);
        var created = Assert.Single(first.Creates);
        Assert.Empty(first.AlreadyMigratedCompositionIds);
        Assert.Empty(first.Conflicts);

        var second = CompositionMigrationPlanner.Plan(
            file,
            documents,
            [new PersistedPage(created.PagePath, created.Page)]);

        Assert.False(second.Blocked);
        Assert.Empty(second.Creates);
        Assert.Equal(new[] { "cmp_a" }, second.AlreadyMigratedCompositionIds);
        Assert.Empty(second.Conflicts);
    }

    [Fact]
    public void PlanNeverOverwritesAnUnrelatedExistingPage()
    {
        var documents = new DocumentStore();
        var file = CompositionFile.Parse(
            """{"version":1,"items":[{"id":"cmp_a","name":"Livro","sources":[]}]}""");
        var occupied = PageDocument.Parse(
            """{"version":1,"kind":"urbe-page","meta":{"title":"Outra"},"sections":[]}""");

        var plan = CompositionMigrationPlanner.Plan(
            file,
            documents,
            [new PersistedPage("Páginas/Composições/Livro.page.json", occupied)]);

        Assert.False(plan.Blocked);
        Assert.Empty(plan.Creates);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal("cmp_a", conflict.CompositionId);
        Assert.Equal("Páginas/Composições/Livro.page.json", conflict.TargetPath);
    }

    [Fact]
    public void FutureAndCorruptCompositionFilesBlockMigrationPlanning()
    {
        foreach (var file in new[]
                 {
                     CompositionFile.Parse("""{"version":99,"items":[]}"""),
                     CompositionFile.Parse("""{oops""")
                 })
        {
            var plan = CompositionMigrationPlanner.Plan(
                file,
                new DocumentStore());

            Assert.True(plan.Blocked);
            Assert.False(plan.HasChanges);
            Assert.NotNull(plan.BlockReason);
            Assert.Empty(plan.Creates);
        }
    }
}

public sealed class CompositionPageConverterTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void HistoricalCompositionFixtureLoadsWithLegacyOrder()
    {
        var json = File.ReadAllText(
            Path.Combine(
                Root,
                "..",
                "tests",
                "fixtures",
                "vaults",
                "v1-mapa-v4",
                ".urbe",
                "compositions.json"));

        var file = CompositionFile.Parse(json);
        var item = Assert.Single(file.Items);

        Assert.Equal(CompositionFileState.Current, file.State);
        Assert.Equal(1, file.Version);
        Assert.Equal("cmp_00000001", item.Id);
        Assert.Equal("Junção", item.Name);
        Assert.Equal(2, item.Sources.Count);
        Assert.Equal(item.Sources, item.Order);
        Assert.Equal(item.Order, item.OrderedSources);
    }

    [Fact]
    public void ConversionUsesStableDocumentIdsForOrderedNoteSectionsAndPreservesLegacyData()
    {
        var documents = new DocumentStore();
        documents.ReplaceAll(
        [
            new DocumentInput
            {
                Id = "doc_b",
                Path = "Livro/02.md",
                Content = "# Dois"
            },
            new DocumentInput
            {
                Id = "doc_a",
                Path = "Livro/01.md",
                Content = "# Um"
            }
        ]);

        var file = CompositionFile.Parse(
            """
            {
              "version":1,
              "items":[{
                "id":"cmp_livro",
                "name":"Livro: Edição/Final",
                "type":"document",
                "sources":["doc_a","doc_b"],
                "order":["doc_b","doc_a"],
                "theme":"clean",
                "styles":{"fontFamily":"Georgia, serif","textAlign":"center"},
                "overrides":{"doc_a_0":{"color":"#ff0000"}},
                "customCSS":"h1{letter-spacing:1px}",
                "htmlSource":"<main>snapshot legado</main>",
                "futureField":{"keep":true}
              }]
            }
            """);

        var conversion = CompositionPageConverter.Convert(
            Assert.Single(file.Items),
            documents);

        Assert.Empty(conversion.MissingDocumentIds);
        Assert.Equal(
            "Páginas/Composições/Livro- Edição-Final.page.json",
            conversion.PagePath);

        var page = conversion.Page.Raw;
        var sections = page["sections"]!.AsArray();
        Assert.Equal(2, sections.Count);
        Assert.Equal("Livro/02.md",
            sections[0]!["props"]!["path"]!.GetValue<string>());
        Assert.Equal("Livro/01.md",
            sections[1]!["props"]!["path"]!.GetValue<string>());

        var legacy = page["meta"]!["legacyComposition"]!.AsObject();
        Assert.True(legacy["futureField"]!["keep"]!.GetValue<bool>());
        Assert.Equal("<main>snapshot legado</main>",
            legacy["htmlSource"]!.GetValue<string>());
        Assert.Equal("h1{letter-spacing:1px}",
            page["theme"]!["css"]!.GetValue<string>());
        Assert.Equal("Georgia, serif",
            page["theme"]!["legacyCompositionStyles"]!["fontFamily"]!.GetValue<string>());
    }

    [Fact]
    public void MissingSourceIsReportedWithoutDroppingItsLegacyIdentity()
    {
        var documents = new DocumentStore();
        var file = CompositionFile.Parse(
            """{"version":1,"items":[{"id":"cmp_x","name":"X","sources":["doc_missing"]}]}""");

        var conversion = CompositionPageConverter.Convert(
            Assert.Single(file.Items),
            documents);

        Assert.Equal(new[] { "doc_missing" }, conversion.MissingDocumentIds);
        Assert.Equal(string.Empty,
            conversion.Page.Raw["sections"]!.AsArray()[0]!["props"]!["path"]!.GetValue<string>());
        Assert.Equal(
            "doc_missing",
            conversion.Page.Raw["meta"]!["legacyComposition"]!["sources"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public void ConversionIsDeterministicAndDoesNotMutateComposition()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "A.md",
                Content = "A"
            });

        var file = CompositionFile.Parse(
            """{"version":1,"items":[{"id":"cmp_a","name":"A","sources":["doc_a"],"custom":"keep"}]}""");
        var item = Assert.Single(file.Items);
        var before = item.Raw.ToJsonString();

        var a = CompositionPageConverter.Convert(item, documents);
        var b = CompositionPageConverter.Convert(item, documents);

        Assert.Equal(a.PagePath, b.PagePath);
        Assert.Equal(a.Page.Raw.ToJsonString(), b.Page.Raw.ToJsonString());
        Assert.Equal(before, item.Raw.ToJsonString());
    }

    [Theory]
    [InlineData("""{"version":3,"items":[{"id":"cmp_futuro"}]}""", CompositionFileState.Future)]
    [InlineData("""{"version":"1","items":[]}""", CompositionFileState.Corrupt)]
    [InlineData("""[]""", CompositionFileState.Corrupt)]
    public void FutureAndCorruptCompositionFilesAreReadOnly(
        string json,
        CompositionFileState expected)
    {
        var file = CompositionFile.Parse(json);

        Assert.Equal(expected, file.State);
        Assert.True(file.IsReadOnly);
    }

    [Fact]
    public void FutureCompositionFixtureIsPreserved()
    {
        var json = File.ReadAllText(
            Path.Combine(
                Root,
                "..",
                "tests",
                "fixtures",
                "vaults",
                "futuro-v2",
                ".urbe",
                "compositions.v2.json"));

        var file = CompositionFile.Parse(json);

        Assert.Equal(CompositionFileState.Future, file.State);
        Assert.True(file.IsReadOnly);
        Assert.Equal(3, file.Version);
        Assert.Equal(
            "cmp_futuro",
            Assert.Single(file.Items).Id);
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
