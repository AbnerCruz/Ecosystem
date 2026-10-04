using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class ArtifactModelTests
{
    [Theory]
    [InlineData("Alfa.md", ArtifactType.Note)]
    [InlineData("Pasta/Beta.markdown", ArtifactType.Note)]
    [InlineData("Diário.txt", ArtifactType.Note)]
    [InlineData("Web/index.html", ArtifactType.Text)]
    [InlineData("src/x.js", ArtifactType.Text)]
    [InlineData("dados.csv", ArtifactType.Text)]
    [InlineData("cfg.yaml", ArtifactType.Text)]
    [InlineData("cfg.yml", ArtifactType.Text)]
    [InlineData("a.json", ArtifactType.Text)]
    [InlineData("a.css", ArtifactType.Text)]
    [InlineData("a.mjs", ArtifactType.Text)]
    [InlineData("a.htm", ArtifactType.Text)]
    [InlineData("Personalização/tema.json", ArtifactType.Theme)]
    [InlineData("Personalização/temas/noite.json", ArtifactType.Theme)]
    [InlineData("Personalização/estilos/meu.css", ArtifactType.Style)]
    [InlineData("Personalização/texturas/g.json", ArtifactType.Texture)]
    [InlineData("Personalização/plugins/ola.js", ArtifactType.Plugin)]
    [InlineData("Personalização/outro.md", ArtifactType.Personalization)]
    [InlineData("Páginas/Inicio.page.json", ArtifactType.Page)]
    [InlineData("Páginas/Modelos/Blog.template.json", ArtifactType.PageTemplate)]
    [InlineData("Páginas/Blocos/Rodape.block.json", ArtifactType.PageBlock)]
    [InlineData("x/Y.page.json", ArtifactType.Page)]
    [InlineData(".urbe/mapa.json", ArtifactType.System)]
    [InlineData(".urbe/backup/x/manifest.json", ArtifactType.System)]
    [InlineData("Pasta/.pasta", ArtifactType.System)]
    [InlineData(".git/config", ArtifactType.System)]
    [InlineData("imagem.png", ArtifactType.Asset)]
    [InlineData("doc.pdf", ArtifactType.Asset)]
    [InlineData("canvas/quadro.canvas", ArtifactType.Asset)]
    [InlineData("Anexos/foto.JPG", ArtifactType.Asset)]
    public void ClassifyMatchesCanonicalJavascriptCases(string path, ArtifactType expected)
    {
        Assert.Equal(expected, ArtifactModel.Classify(path).Type);
    }

    [Fact]
    public void ClassificationFlagsAndExtensionsHaveOneAuthority()
    {
        Assert.True(ArtifactModel.Classify("Personalização/plugins/ola.js").Editable);
        Assert.False(ArtifactModel.Classify(".urbe/mapa.json").Editable);
        Assert.False(ArtifactModel.Classify("imagem.png").Editable);
        Assert.True(ArtifactModel.IsNote("a.md"));
        Assert.False(ArtifactModel.IsNote("a.js"));
        Assert.True(ArtifactModel.Classify("A.MD").Note);
        Assert.True(ArtifactModel.Classify(@"\Pasta\a.md").Note);
        Assert.True(ArtifactModel.IsText("x.yml"));
        Assert.False(ArtifactModel.IsText("x.canvas"));
        Assert.Equal(".jpg", ArtifactModel.Extension("Anexos/FOTO.JPG"));
    }

    [Fact]
    public void OnlyUrbeInternalArtifactTypesAreNotLinkable()
    {
        foreach (var path in new[]
                 {
                     "Personalização/plugins/x.js",
                     "Personalização/tema.json",
                     ".urbe/mapa.json",
                     "Personalização/estilos/a.css"
                 })
            Assert.False(ArtifactModel.Linkable(path), path);

        foreach (var path in new[]
                 {
                     "Alfa.md",
                     "Páginas/Inicio.page.json",
                     "web/index.html"
                 })
            Assert.True(ArtifactModel.Linkable(path), path);
    }

    [Fact]
    public void SafeNameMatchesCrossPlatformRules()
    {
        Assert.Equal("foo-bar-", ArtifactModel.SafeName("..foo/bar*"));
        Assert.Equal("sem-nome", ArtifactModel.SafeName("..."));
        Assert.Equal("a - b", ArtifactModel.SafeName("  a \t  b. "));
        Assert.Equal(90, ArtifactModel.SafeName(new string('x', 100)).Length);
    }

    [Fact]
    public void TitleDerivationPreservesTxtLegacyBehavior()
    {
        Assert.Equal("Roma", ArtifactModel.TitleFromPath("História/Roma.md"));
        Assert.Equal("Roma", ArtifactModel.TitleFromPath("História/Roma.markdown"));
        Assert.Equal("Diário.txt", ArtifactModel.TitleFromPath("Diário.txt"));
    }

    [Fact]
    public void ArtifactRouterRunsBeforeHooksAndHonorsRaw()
    {
        var store = new DocumentStore();
        var page = store.Make(
            new DocumentInput
            {
                Path = "P/Inicio.page.json",
                Content = "{}"
            });
        var note = store.Make(
            new DocumentInput
            {
                Path = "a.md",
                Content = "a"
            });
        var calls = new List<string>();
        var router = new ArtifactRouter();

        router.RegisterOpener(
            ArtifactType.Page,
            (document, _) =>
            {
                calls.Add("open:" + document.Path);
                return "estudio";
            });
        router.OnBeforeOpen(
            (document, context) =>
                calls.Add("before:" + document.Path + (context.Raw ? ":raw" : string.Empty)));

        var opened = router.Route(page);
        var raw = router.Route(page, new ArtifactOpenContext(Raw: true));
        var normal = router.Route(note);

        Assert.True(opened.Handled);
        Assert.Equal("estudio", opened.Result);
        Assert.False(raw.Handled);
        Assert.False(normal.Handled);
        Assert.Equal(
            new[]
            {
                "before:P/Inicio.page.json",
                "open:P/Inicio.page.json",
                "before:P/Inicio.page.json:raw",
                "before:a.md"
            },
            calls);
    }
}

public sealed class DocumentStoreTests
{
    [Fact]
    public void CanonicalDocumentsLinksTagsAndKnowledgeGraphMatchJavascript()
    {
        var documents = new DocumentStore();
        using var knowledge = new KnowledgeIndex(documents);

        documents.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "História/Roma.md",
                    Content = "---\ntags: [historia, roma]\n---\n# Roma\nVeja [[César]]."
                },
                new DocumentInput
                {
                    Path = "História/César.md",
                    Content = "# César\nLigado a [[Roma]]. #biografia"
                },
                new DocumentInput
                {
                    Path = "Inbox.md",
                    Content = "Texto sem links"
                }
            ],
            "test");

        Assert.Equal(3, documents.List().Count);
        Assert.Equal("Roma", documents.Get("História/Roma.md")!.Title);
        Assert.Contains("César", documents.Get("História/Roma.md")!.Links);
        Assert.Equal("César", Assert.Single(knowledge.Links("História/Roma.md")).Title);
        Assert.Equal("César", Assert.Single(knowledge.Backlinks("História/Roma.md")).Title);
        Assert.Contains(knowledge.Tagged("biografia"), document => document.Title == "César");
        Assert.Contains(knowledge.Search("Roma"), document => document.Title == "Roma");

        documents.Upsert(
            new DocumentInput
            {
                Path = "Inbox.md",
                Content = "Agora [[Roma]]"
            });

        Assert.Contains(
            knowledge.Links("Inbox.md"),
            document => document.Title == "Roma");
    }

    [Fact]
    public void ParsingIgnoresWikiLinksAndTagsInsideCode()
    {
        var content =
            """
            #real
            Veja [[Real|rótulo#seção]] e [[Real]].

            @@BT@@[[Inline]] #inline@@BT@@

            @@FENCE@@md
            [[Fence]]
            #fence
            @@FENCE@@
            """;

        content = content
            .Replace("@@BT@@", new string((char)96, 1), StringComparison.Ordinal)
            .Replace("@@FENCE@@", new string((char)96, 3), StringComparison.Ordinal);

        var links = DocumentModel.ParseLinks(content);
        var tags = DocumentModel.ParseTags(content);

        Assert.Equal(new[] { "Real" }, links);
        Assert.Contains("real", tags);
        Assert.DoesNotContain("inline", tags);
        Assert.DoesNotContain("fence", tags);
    }

    [Fact]
    public void FrontmatterAndPropertyTagsFollowLegacyParser()
    {
        var content =
            """
            ---
            tags: [historia, #roma]
            owner: Abner:extra
            ---
            Texto #corpo
            """;

        var properties = DocumentModel.ParseFrontmatter(content);
        var tags = DocumentModel.ParseTags(content, properties);

        Assert.Equal("[historia, #roma]", properties["tags"]);
        Assert.Equal("Abner:extra", properties["owner"]);
        Assert.Equal(new[] { "roma", "corpo", "historia" }, tags);
    }

    [Fact]
    public void UpsertReusesPathIdentityAndRecomputesDerivedDataWhenContentChanges()
    {
        var store = new DocumentStore();
        var first = store.Upsert(
            new DocumentInput
            {
                Path = "Inbox.md",
                Content = "#old [[Antiga]]"
            });

        var second = store.Upsert(
            new DocumentInput
            {
                Path = "inbox.MD",
                Content = "#new [[Nova]]",
                Tags = new[] { "stale" },
                Links = new[] { "Stale" },
                Properties = new Dictionary<string, string> { ["tags"] = "stale" }
            });

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.Revision);
        Assert.Contains("new", second.Tags);
        Assert.DoesNotContain("stale", second.Tags);
        Assert.Equal(new[] { "Nova" }, second.Links);
        Assert.Equal(second, store.Get("INBOX.md"));
    }

    [Fact]
    public void RenameWithExplicitIdPreservesIdentityAndRemovesOldPathIndex()
    {
        var store = new DocumentStore();
        var first = store.Upsert(
            new DocumentInput
            {
                Path = "A.md",
                Content = "a"
            });
        var moved = store.Upsert(
            new DocumentInput
            {
                Id = first.Id,
                Path = "Pasta/B.md",
                Content = "a"
            });

        Assert.Equal(first.Id, moved.Id);
        Assert.Null(store.Get("A.md"));
        Assert.Equal(moved, store.Get("pasta/b.md"));
    }

    [Fact]
    public void StoreEmitsLifecycleWithMonotonicStoreRevision()
    {
        var store = new DocumentStore();
        var events = new List<DocumentStoreChangedEventArgs>();
        store.Changed += (_, args) => events.Add(args);

        var created = store.Upsert(
            new DocumentInput
            {
                Path = "A.md",
                Content = "a"
            },
            "create");
        store.Upsert(
            new DocumentInput
            {
                Id = created.Id,
                Path = "A.md",
                Content = "b"
            },
            "update");
        Assert.True(store.Remove(created.Id, "remove"));
        store.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "B.md",
                    Content = "b"
                }
            ],
            "replace");
        store.Clear("clear");

        Assert.Equal(
            new[]
            {
                DocumentStoreChangeKind.Created,
                DocumentStoreChangeKind.Updated,
                DocumentStoreChangeKind.Removed,
                DocumentStoreChangeKind.Reset,
                DocumentStoreChangeKind.Reset
            },
            events.Select(item => item.Kind).ToArray());
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, events.Select(item => item.StoreRevision).ToArray());
        Assert.Equal("create", events[0].Metadata);
        Assert.Equal("update", events[1].Metadata);
        Assert.Equal("remove", events[2].Metadata);
        Assert.Equal("replace", events[3].Metadata);
        Assert.Equal("clear", events[4].Metadata);
        Assert.Single(events[3].Documents!);
        Assert.Single(events[4].PreviousDocuments!);
    }

    [Fact]
    public void ReplaceAllForcesDocumentRevisionOne()
    {
        var store = new DocumentStore();
        var documents = store.ReplaceAll(
            [
                new DocumentInput
                {
                    Id = "doc_fixed",
                    Path = "A.md",
                    Content = "a",
                    Revision = 99
                }
            ]);

        Assert.Equal(1, Assert.Single(documents).Revision);
        Assert.Equal(1, store.Revision);
    }

    [Fact]
    public void DocumentIdsUseStablePrefix()
    {
        var store = new DocumentStore();
        var document = store.Upsert(
            new DocumentInput
            {
                Path = "A.md",
                Content = "a"
            });

        Assert.StartsWith("doc_", document.Id);
        Assert.NotEqual(document.Id, DocumentModel.CreateDocumentId());
    }

    [Fact]
    public void VaultSnapshotCanPopulateDocumentStoreWithoutChangingIdentity()
    {
        var snapshot = VaultReader.Read(
            [
                new VaultFile("Alfa.md", Encoding.UTF8.GetBytes("# Alfa")),
                new VaultFile("Páginas/Inicio.page.json", Encoding.UTF8.GetBytes("{}")),
                new VaultFile("imagem.png", new byte[] { 1, 2, 3 })
            ]);
        var store = new DocumentStore();

        var documents = store.ReplaceFromVault(snapshot);

        Assert.Equal(snapshot.Documents.Count, documents.Count);
        foreach (var vaultDocument in snapshot.Documents)
        {
            var document = store.Get(vaultDocument.Path);
            Assert.NotNull(document);
            Assert.Equal(vaultDocument.Id, document!.Id);
            Assert.Equal(vaultDocument.Text, document.Content);
        }
        Assert.Null(store.Get("imagem.png"));
    }
}

public sealed class KnowledgeIndexTests
{
    [Fact]
    public void NonLinkableArtifactsNeverBecomeWikiLinkTargets()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        store.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "Origem.md",
                    Content = "[[Personalização/plugins/x.js]] [[Páginas/Inicio.page.json]]"
                },
                new DocumentInput
                {
                    Path = "Personalização/plugins/x.js",
                    Content = "plugin"
                },
                new DocumentInput
                {
                    Path = "Páginas/Inicio.page.json",
                    Content = "{}"
                }
            ]);

        var links = knowledge.Links("Origem.md");

        Assert.Single(links);
        Assert.Equal("Páginas/Inicio.page.json", links[0].Path);
    }

    [Fact]
    public void AliasesResolveTitlePathAndPathWithoutMarkdownExtension()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        store.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "História/Roma.md",
                    Content = "Roma"
                },
                new DocumentInput
                {
                    Path = "A.md",
                    Content = "[[Roma]]"
                },
                new DocumentInput
                {
                    Path = "B.md",
                    Content = "[[História/Roma]]"
                },
                new DocumentInput
                {
                    Path = "C.md",
                    Content = "[[História/Roma.md]]"
                }
            ]);

        foreach (var source in new[] { "A.md", "B.md", "C.md" })
            Assert.Equal("Roma", Assert.Single(knowledge.Links(source)).Title);
    }

    [Fact]
    public void SearchScoresTitlePathBeforeTokenOnlyMatches()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        store.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "Roma.md",
                    Content = "capital"
                },
                new DocumentInput
                {
                    Path = "Viagem.md",
                    Content = "roteiro roma"
                }
            ]);

        var results = knowledge.Search("roma");

        Assert.Equal("Roma", results[0].Title);
        Assert.Contains(results, document => document.Title == "Viagem");
    }

    [Fact]
    public void EmptySearchReturnsFirstDocumentsAndZeroLimitMeansDefaultFifty()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        store.ReplaceAll(
            Enumerable.Range(0, 60)
                .Select(
                    index =>
                        new DocumentInput
                        {
                            Path = $"N{index:D2}.md",
                            Content = "x"
                        }));

        Assert.Equal(50, knowledge.Search(string.Empty).Count);
        Assert.Equal(50, knowledge.Search(string.Empty, 0).Count);
        Assert.Equal(3, knowledge.Search(string.Empty, 3).Count);
        Assert.Equal(59, knowledge.Search(string.Empty, -1).Count);
    }

    [Fact]
    public void StatsAndIndexedEventTrackAutomaticRebuilds()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        var indexed = new List<KnowledgeIndexedEventArgs>();
        knowledge.Indexed += (_, args) => indexed.Add(args);

        store.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "A.md",
                    Content = "#tag [[B]] dois tokens"
                },
                new DocumentInput
                {
                    Path = "B.md",
                    Content = "destino"
                }
            ]);

        var stats = knowledge.Stats();

        Assert.Equal(2, stats.Documents);
        Assert.Equal(1, stats.Links);
        Assert.Equal(1, stats.Tags);
        Assert.True(stats.Tokens >= 4);
        Assert.Single(indexed);
        Assert.Equal(2, indexed[0].Documents);
        Assert.Equal(1, indexed[0].Links);
    }

    [Fact]
    public void RemovingDocumentReindexesBacklinksImmediately()
    {
        var store = new DocumentStore();
        using var knowledge = new KnowledgeIndex(store);
        store.ReplaceAll(
            [
                new DocumentInput
                {
                    Path = "A.md",
                    Content = "[[B]]"
                },
                new DocumentInput
                {
                    Path = "B.md",
                    Content = "b"
                }
            ]);

        Assert.Single(knowledge.Links("A.md"));
        Assert.True(store.Remove("B.md"));
        Assert.Empty(knowledge.Links("A.md"));
    }
}
