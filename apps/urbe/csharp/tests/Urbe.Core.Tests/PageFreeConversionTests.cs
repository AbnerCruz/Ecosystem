using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageFreeConversionTests
{
    [Fact]
    public void ChapterFromNoteMatchesLegacyParagraphSemantics()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "Cap/01 A.md",
                Title = "A",
                Content = "# A\n\nPrimeiro parágrafo.\n\n## Subtítulo\n\nSegundo.\n\n---\n\n> Citação"
            });

        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[{
                "id":"ch",
                "type":"chapter",
                "props":{"source":"note","path":"Cap/01 A.md","title":""},
                "style":{}
              }]
            }
            """);

        var tree = PageFreeConversion.FromBlock(page.Sections[0], documents);

        Assert.NotNull(tree);
        var children = tree!["children"]!.AsArray();
        Assert.Equal(
            new[] { "heading", "text", "heading", "text", "divider", "quote" },
            children.Select(node => node!["type"]!.GetValue<string>()).ToArray());
        Assert.Equal("A", children[0]!["content"]!["text"]!.GetValue<string>());
        Assert.Equal(3, children[2]!["content"]!["level"]!.GetValue<int>());
    }

    [Fact]
    public void AllDeclaredConvertibleBlocksProduceValidFreeTrees()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Path = "Nota.md",
                Title = "Nota",
                Content = "# Nota\n\nTexto."
            });

        var samples = new Dictionary<string, string>
        {
            ["text"] = """{"markdown":"# T\n\nTexto"}""",
            ["hero"] = """{"title":"T","subtitle":"Sub","buttons":[{"label":"Ir","url":"#"}]}""",
            ["quote"] = """{"text":"Q","author":"A"}""",
            ["image"] = """{"src":"https://example.com/a.png","alt":"A"}""",
            ["bookcover"] = """{"title":"L","author":"A"}""",
            ["titlepage"] = """{"title":"L","author":"A"}""",
            ["dedication"] = """{"title":"D","markdown":"Texto"}""",
            ["copyright"] = """{"title":"C","markdown":"Texto"}""",
            ["colophon"] = """{"title":"C","markdown":"Texto"}""",
            ["about"] = """{"title":"A","markdown":"Texto"}""",
            ["part"] = """{"title":"P","markdown":"Texto"}""",
            ["chapter"] = """{"title":"C","source":"text","markdown":"Texto"}""",
            ["note"] = """{"path":"Nota.md","showTitle":true}"""
        };

        foreach (var type in PageFreeConversion.ConvertibleTypes)
        {
            var props = samples[type];
            var page = PageDocument.Parse(
                $$"""
                {
                  "version":1,
                  "sections":[{"id":"s","type":"{{type}}","props":{{props}},"style":{}}]
                }
                """);
            var tree = PageFreeConversion.FromBlock(page.Sections[0], documents);
            Assert.NotNull(tree);

            var wrapper = PageDocument.Parse(
                new JsonObject
                {
                    ["version"] = 1,
                    ["sections"] = new JsonArray(
                        new JsonObject
                        {
                            ["id"] = "free",
                            ["type"] = "free",
                            ["props"] = new JsonObject
                            {
                                ["root"] = tree!.DeepClone()
                            },
                            ["style"] = new JsonObject()
                        })
                });
            var normalized = PageNormalizer.Normalize(wrapper);
            Assert.True(
                normalized.IsValid,
                type + ": " + string.Join(
                    "; ",
                    normalized.Errors.Select(error => error.Path + "=" + error.Message)));
        }
    }

    [Fact]
    public void CompactRemovesKnownDefaultsButPreservesUnknownDataAndRoundTrips()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "kind":"urbe-page",
              "futureRoot":{"keep":true},
              "meta":{"title":"Página sem título","lang":"pt-BR","futureMeta":"keep"},
              "theme":{"preset":"aurora","mode":"","spacing":"comfortable","scale":1,"width":1120,"shadow":"soft","animations":true},
              "layout":{"nav":true,"sticky":true,"themeToggle":true,"backToTop":true,"progress":false,"format":"web","pageSize":"a5","margins":"normal","pageNumbers":true,"runningHead":"","chapterStyle":"word","recto":false,"justify":true,"indent":true},
              "sections":[{
                "id":"s",
                "type":"text",
                "props":{"markdown":"oi","columns":1,"futureProp":"keep"},
                "style":{"hidden":false,"menu":false,"futureStyle":"keep"}
              }]
            }
            """);

        var normalized = PageNormalizer.Normalize(page);
        Assert.True(normalized.IsValid);

        var compact = PageCompactor.Compact(page);
        var renormalized = PageNormalizer.Normalize(compact);
        Assert.True(renormalized.IsValid);
        Assert.True(JsonNode.DeepEquals(normalized.Page.Raw, renormalized.Page.Raw));

        var raw = compact.Raw;
        Assert.Null(raw["theme"]);
        Assert.Null(raw["layout"]);
        Assert.Equal("keep", raw["futureRoot"]!["keep"]!.GetValue<bool>() ? "keep" : "bad");
        Assert.Equal("keep", raw["meta"]!["futureMeta"]!.GetValue<string>());
        var section = raw["sections"]!.AsArray()[0]!.AsObject();
        Assert.Equal("keep", section["props"]!["futureProp"]!.GetValue<string>());
        Assert.Equal("keep", section["style"]!["futureStyle"]!.GetValue<string>());

        Assert.True(
            compact.SerializeForWrite(false).Length <
            normalized.Page.SerializeForWrite(false).Length);
    }

    [Fact]
    public void CompactRefusesFutureDocuments()
    {
        var future = PageDocument.Parse("""{"version":99,"sections":[]}""");
        Assert.Throws<InvalidOperationException>(() => PageCompactor.Compact(future));
    }
}
