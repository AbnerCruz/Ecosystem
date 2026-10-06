using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageBookRendererTests
{
    [Fact]
    public void TocBuildsStableUniqueAnchorsAndSkipsHiddenSections()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[
                {"id":"toc","type":"toc","props":{"title":"Nesta página"},"style":{}},
                {"id":"a","type":"text","props":{"title":"Visão geral","markdown":"A"},"style":{"menu":true}},
                {"id":"b","type":"text","props":{"title":"Visão geral","markdown":"B"},"style":{}},
                {"id":"c","type":"text","props":{"title":"Oculta","markdown":"C"},"style":{"hidden":true}},
                {"id":"d","type":"text","props":{"title":"Âncora customizada","markdown":"D"},"style":{"anchor":"Seção Especial"}}
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        Assert.Contains("href='#visao-geral'", html, StringComparison.Ordinal);
        Assert.Contains("href='#visao-geral-2'", html, StringComparison.Ordinal);
        Assert.Contains("href='#secao-especial'", html, StringComparison.Ordinal);
        Assert.Contains("id='visao-geral'", html, StringComparison.Ordinal);
        Assert.Contains("id='visao-geral-2'", html, StringComparison.Ordinal);
        Assert.Contains("id='secao-especial'", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Oculta", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BookContextNumbersPartsSingleChaptersAndFolderChaptersTogether()
    {
        var documents = new DocumentStore();
        documents.ReplaceAll(
        [
            new DocumentInput
            {
                Id = "doc_intro",
                Path = "Livro/Introdução.md",
                Content = "# Introdução\n\n## Contexto\n\nTexto."
            },
            new DocumentInput
            {
                Id = "doc_a",
                Path = "Livro/02 - Beta.md",
                Content = "# Beta\n\n## Cena\n\nB."
            },
            new DocumentInput
            {
                Id = "doc_b",
                Path = "Livro/03 - Gama.md",
                Content = "# Gama\n\nG."
            }
        ]);

        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "layout":{"format":"book","chapterStyle":"roman"},
              "sections":[
                {"id":"sum","type":"booktoc","props":{"title":"Sumário"},"style":{}},
                {"id":"p1","type":"part","props":{"title":"Começo"},"style":{}},
                {"id":"c1","type":"chapter","props":{"title":"","source":"note","path":"Livro/Introdução.md","numbered":true,"dropCap":true},"style":{}},
                {"id":"cs","type":"chapters","props":{"folder":"Livro","sort":"path","dropCap":false},"style":{}},
                {"id":"c0","type":"chapter","props":{"title":"Interlúdio","source":"text","markdown":"# Interno\n\nTexto","numbered":false},"style":{}}
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page, documents);

        Assert.Contains("Parte I", html, StringComparison.Ordinal);
        Assert.Contains("href='#comeco'", html, StringComparison.Ordinal);
        Assert.Contains("href='#introducao'", html, StringComparison.Ordinal);
        Assert.Contains("<p class='bk-chnum'>I</p>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class='bk-chtitle'>Introdução</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h4>Contexto</h4>", html, StringComparison.Ordinal);
        Assert.Contains("id='nota-introducao'", html, StringComparison.Ordinal);
        Assert.Contains("id='nota-beta'", html, StringComparison.Ordinal);
        Assert.Contains("id='nota-gama'", html, StringComparison.Ordinal);
        Assert.Contains("<p class='bk-chnum'>II</p>", html, StringComparison.Ordinal);
        Assert.Contains("<p class='bk-chnum'>III</p>", html, StringComparison.Ordinal);
        Assert.Contains("<p class='bk-chnum'>IV</p>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class='bk-chtitle'>Interlúdio</h2>", html, StringComparison.Ordinal);

        var interludeStart = html.IndexOf(
            "<h2 class='bk-chtitle'>Interlúdio</h2>",
            StringComparison.Ordinal);
        Assert.True(interludeStart >= 0);
        var interludeChunk = html[interludeStart..];
        Assert.DoesNotContain(
            "<p class='bk-chnum'>",
            interludeChunk,
            StringComparison.Ordinal);
        Assert.Contains("<h3>Interno</h3>", interludeChunk, StringComparison.Ordinal);
    }

    [Fact]
    public void BookCoverAndTitlePageEscapeDataAndRejectUnsafeImage()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "layout":{"format":"book"},
              "sections":[
                {
                  "type":"bookcover",
                  "props":{
                    "title":"**Livro**",
                    "subtitle":"Sub",
                    "author":"A < B",
                    "publisher":"Editora",
                    "image":"javascript:alert(1)",
                    "style":"modern"
                  },
                  "style":{}
                },
                {
                  "type":"titlepage",
                  "props":{
                    "title":"Livro",
                    "author":"Autor",
                    "publisher":"Casa",
                    "place":"Joinville",
                    "year":"2026"
                  },
                  "style":{}
                }
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        Assert.Contains("<strong>Livro</strong>", html, StringComparison.Ordinal);
        Assert.Contains("A &lt; B", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class='bk-bar'></span>", html, StringComparison.Ordinal);
        Assert.Contains("Joinville · 2026", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizerAppliesBookDefaultsWithoutOverwritingUnknownLayoutFields()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "layout":{"format":"book","futureBook":{"keep":true}},
              "sections":[
                {"type":"chapter","props":{},"style":{}},
                {"type":"bookcover","props":{},"style":{}}
              ]
            }
            """);

        var result = PageNormalizer.Normalize(page);
        var root = result.Page.Raw;

        Assert.True(result.IsValid);
        Assert.Equal("word", root["layout"]!["chapterStyle"]!.GetValue<string>());
        Assert.Equal("a5", root["layout"]!["pageSize"]!.GetValue<string>());
        Assert.True(root["layout"]!["futureBook"]!["keep"]!.GetValue<bool>());
        Assert.True(root["sections"]!.AsArray()[0]!["props"]!["numbered"]!.GetValue<bool>());
        Assert.Equal(
            "Título do capítulo",
            root["sections"]!.AsArray()[0]!["props"]!["title"]!.GetValue<string>());
        Assert.Equal(
            "O título do livro",
            root["sections"]!.AsArray()[1]!["props"]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void UnnumberedChapterDoesNotConsumeNextNumber()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "layout":{"format":"book","chapterStyle":"number"},
              "sections":[
                {"type":"chapter","props":{"title":"Prólogo","numbered":false,"markdown":"P"},"style":{}},
                {"type":"chapter","props":{"title":"Primeiro","numbered":true,"markdown":"A"},"style":{}}
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        var first = html.IndexOf(
            "<h2 class='bk-chtitle'>Primeiro</h2>",
            StringComparison.Ordinal);
        Assert.True(first >= 0);
        var aroundFirst = html[..first];
        Assert.Contains("<p class='bk-chnum'>1</p>", aroundFirst, StringComparison.Ordinal);
        Assert.DoesNotContain("<p class='bk-chnum'>2</p>", html, StringComparison.Ordinal);
    }
}
