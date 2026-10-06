using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageThemeTemplateTests
{
    [Fact]
    public void AllThemePresetsResolveAndProduceCss()
    {
        foreach (var preset in PageThemeCatalog.Presets.Values)
        {
            var theme = new JsonObject { ["preset"] = preset.Id };
            var resolved = PageThemeCatalog.Resolve(theme);
            var css = PageThemeCatalog.BuildCss(theme, new JsonObject(), false);

            Assert.Equal(preset.Id, resolved.Preset.Id);
            Assert.Contains("--primary:", css, StringComparison.Ordinal);
            Assert.Contains("--surface:", css, StringComparison.Ordinal);
            Assert.Contains("--fh:", css, StringComparison.Ordinal);
            Assert.Contains("--fb:", css, StringComparison.Ordinal);
        }

        var autoCss = PageThemeCatalog.BuildCss(
            new JsonObject { ["preset"] = "aurora", ["mode"] = "auto" },
            new JsonObject(),
            false);
        Assert.Contains("prefers-color-scheme:dark", autoCss, StringComparison.Ordinal);

        var system = PageThemeCatalog.Resolve(
            new JsonObject
            {
                ["preset"] = "grafite",
                ["headingFont"] = "system",
                ["bodyFont"] = "system"
            });
        Assert.Equal(string.Empty, PageThemeCatalog.GoogleFontsQuery(system));
    }

    [Fact]
    public void ThemeAndSectionStyleValidationReportKnownInvalidValuesWithoutDroppingUnknowns()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "theme":{"preset":"aurora","primary":"vermelho!!","futureTheme":{"keep":true}},
              "sections":[{
                "type":"text",
                "props":{"markdown":"oi","futureProp":"keep"},
                "style":{"padding":"gigante","futureStyle":"keep"}
              }]
            }
            """);

        var result = PageNormalizer.Normalize(page);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Path == "theme.primary");
        Assert.Contains(result.Errors, issue => issue.Path == "sections[0].style.padding");
        Assert.True(result.Page.Raw["theme"]!["futureTheme"]!["keep"]!.GetValue<bool>());
        Assert.Equal(
            "keep",
            result.Page.Raw["sections"]!.AsArray()[0]!["props"]!["futureProp"]!.GetValue<string>());
        Assert.Equal(
            "keep",
            result.Page.Raw["sections"]!.AsArray()[0]!["style"]!["futureStyle"]!.GetValue<string>());
    }

    [Fact]
    public void AllBuiltInTemplatesNormalizeAndRender()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Path = "Guia/Uso.md",
                Title = "Uso",
                Content = "# Uso\n\nTexto."
            });

        foreach (var template in PageTemplateCatalog.List())
        {
            var page = PageTemplateCatalog.Build(
                template.Id,
                new PageTemplateContext
                {
                    Title = "X",
                    Folder = "Guia",
                    NoteTitle = "Uso",
                    NotePath = "Guia/Uso.md",
                    Year = 2026
                });

            var normalized = PageNormalizer.Normalize(page);
            Assert.True(
                normalized.IsValid,
                template.Id + ": " +
                string.Join("; ", normalized.Errors.Select(error => error.Path + "=" + error.Message)));

            var html = PageRenderer.Render(page, documents);
            Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
            Assert.Contains("<meta name='generator' content='Urbe'>", html, StringComparison.Ordinal);
            Assert.True(html.Length > 3000, template.Id + " HTML curto demais: " + html.Length);
            if (template.Id != "empty")
                Assert.NotEmpty(page.Sections);
        }
    }

    [Fact]
    public void EmptyTemplateIsMinimalAndVariantsPreserveStructure()
    {
        var empty = PageTemplateCatalog.Build(
            "empty",
            new PageTemplateContext { Title = "Nada", Year = 2026 });
        Assert.Empty(empty.Sections);
        Assert.False(empty.Raw["layout"]!["nav"]!.GetValue<bool>());
        Assert.False(empty.Raw["theme"]!["animations"]!.GetValue<bool>());

        foreach (var template in PageTemplateCatalog.List())
        {
            var full = PageTemplateCatalog.Build(
                template.Id,
                new PageTemplateContext
                {
                    Title = "X",
                    Folder = "Guia",
                    NoteTitle = "Uso",
                    NotePath = "Guia/Uso.md",
                    Year = 2026
                });
            var simple = PageTemplateCatalog.Variant(full, "simple");
            var skeleton = PageTemplateCatalog.Variant(full, "skeleton");

            Assert.True(PageNormalizer.Normalize(simple).IsValid, template.Id + " simple");
            Assert.True(PageNormalizer.Normalize(skeleton).IsValid, template.Id + " skeleton");
            Assert.True(simple.Sections.Count <= full.Sections.Count, template.Id + " simple menor");
            Assert.Equal(
                full.Sections.Select(section => section.Type),
                skeleton.Sections.Select(section => section.Type));
        }

        var links = PageTemplateCatalog.Build(
            "links",
            new PageTemplateContext { Title = "@x", Year = 2026 });
        Assert.Equal(640d, links.Raw["theme"]!["width"]!.GetValue<double>());

        var landing = PageTemplateCatalog.Build(
            "landing",
            new PageTemplateContext { Title = "P", Year = 2026 });
        var landingSkeleton = PageTemplateCatalog.Variant(landing, "skeleton");
        Assert.DoesNotContain(
            "O jeito mais simples",
            landingSkeleton.Raw.ToJsonString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void BookTemplateKeepsTwoPartsAndFiveChaptersWhenFolderChaptersAreAdded()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Path = "Guia/Instalação.md",
                Title = "Instalação",
                Content = "# Instalação\n\nVeja [[Uso]]."
            });
        documents.Upsert(
            new DocumentInput
            {
                Path = "Guia/Uso.md",
                Title = "Uso",
                Content = "# Uso\n\nUse com calma."
            });

        var book = PageTemplateCatalog.Build(
            "book",
            new PageTemplateContext { Title = "A Cidade", Year = 2026 });
        var raw = book.Raw;
        raw["sections"]!.AsArray().Add(
            new JsonObject
            {
                ["type"] = "chapters",
                ["props"] = new JsonObject
                {
                    ["folder"] = "Guia",
                    ["sort"] = "path",
                    ["dropCap"] = true
                },
                ["style"] = new JsonObject()
            });

        var html = PageRenderer.Render(PageDocument.Parse(raw), documents);

        Assert.Contains("Parte I", html, StringComparison.Ordinal);
        Assert.Contains("Parte II", html, StringComparison.Ordinal);
        for (var number = 1; number <= 5; number++)
            Assert.Contains("Capítulo " + number, html, StringComparison.Ordinal);
        Assert.Contains("href='#nota-instalacao'", html, StringComparison.Ordinal);
        Assert.Contains("href='#nota-uso'", html, StringComparison.Ordinal);
        Assert.Contains("id='nota-instalacao'", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FullRendererIncludesThemeNavigationFooterSectionStyleAndBookPrintCss()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "meta":{
                "title":"Produto",
                "description":"Descrição",
                "head":"<meta name='k' content='1'>"
              },
              "theme":{
                "preset":"grafite",
                "headingFont":"system",
                "bodyFont":"system",
                "headingCase":"upper",
                "lineHeight":1.8,
                "border":"#abcdef",
                "buttonStyle":"pill",
                "cardStyle":"glass",
                "css":"body{letter-spacing:.01em}</style><script>alert(9)</script>"
              },
              "layout":{
                "nav":true,
                "brand":"Marca",
                "footer":"Rodapé **forte**",
                "themeToggle":true,
                "backToTop":true
              },
              "sections":[{
                "id":"s_a",
                "type":"text",
                "props":{"title":"Recursos","markdown":"oi"},
                "style":{
                  "anchor":"Recursos",
                  "menu":true,
                  "bgColor":"#123456",
                  "textColor":"#ffffff",
                  "boxed":true,
                  "minHeight":"half",
                  "animation":"zoom",
                  "className":"minha \"><x",
                  "css":"& h2{color:red}"
                }
              },{
                "id":"s_b",
                "type":"text",
                "props":{"markdown":"b"},
                "style":{"css":"padding:0"}
              }]
            }
            """);

        var html = PageRenderer.Render(page);

        Assert.Contains("<meta name='description' content='Descrição'>", html, StringComparison.Ordinal);
        Assert.Contains("<meta name='k' content='1'>", html, StringComparison.Ordinal);
        Assert.Contains("class='nav", html, StringComparison.Ordinal);
        Assert.Contains("href='#recursos'>Recursos</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Marca</a>", html, StringComparison.Ordinal);
        Assert.Contains("Rodapé <strong>forte</strong>", html, StringComparison.Ordinal);
        Assert.Contains("background:#123456", html, StringComparison.Ordinal);
        Assert.Contains("color:#ffffff", html, StringComparison.Ordinal);
        Assert.Contains("boxed", html, StringComparison.Ordinal);
        Assert.Contains("mh-half", html, StringComparison.Ordinal);
        Assert.Contains("an-zoom", html, StringComparison.Ordinal);
        Assert.DoesNotContain("minha \"><x", html, StringComparison.Ordinal);
        Assert.Contains("minha x", html, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts.googleapis.com/css2?", html, StringComparison.Ordinal);
        Assert.Contains("text-transform:uppercase", html, StringComparison.Ordinal);
        Assert.Contains("line-height:1.8", html, StringComparison.Ordinal);
        Assert.Contains("--border:#abcdef", html, StringComparison.Ordinal);
        Assert.Contains("border-radius:999px", html, StringComparison.Ordinal);
        Assert.Contains("backdrop-filter:blur(16px)", html, StringComparison.Ordinal);
        Assert.Contains("--text:#ffffff", html, StringComparison.Ordinal);
        Assert.Contains("[data-s='s_a'] h2{color:red}", html, StringComparison.Ordinal);
        Assert.Contains("[data-s='s_b']{padding:0}", html, StringComparison.Ordinal);
        Assert.Contains("body{letter-spacing:.01em}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("</style><script>alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<script>(function(){var d=document", html, StringComparison.Ordinal);
        Assert.Contains("urbe-page-theme", html, StringComparison.Ordinal);
        Assert.Contains("scrollTo({top:0,behavior:\"smooth\"})", html, StringComparison.Ordinal);

        var book = PageDocument.Parse(
            """
            {
              "version":1,
              "meta":{"title":"Livro"},
              "theme":{"preset":"livro"},
              "layout":{
                "format":"book",
                "pageSize":"a5",
                "margins":"normal",
                "pageNumbers":true,
                "runningHead":"Livro"
              },
              "sections":[{"type":"chapter","props":{"title":"Um","markdown":"Texto"},"style":{}}]
            }
            """);
        var bookHtml = PageRenderer.Render(book);
        Assert.Contains("class='light-default book'", bookHtml, StringComparison.Ordinal);
        Assert.Contains("@page{size:148", bookHtml, StringComparison.Ordinal);
        Assert.Contains("@page :left", bookHtml, StringComparison.Ordinal);
        Assert.Contains("@bottom-center{content:counter(page)", bookHtml, StringComparison.Ordinal);
        Assert.Contains("@top-center{content:\"Livro\"", bookHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("class='nav", bookHtml, StringComparison.Ordinal);
        Assert.Contains("Imprimir ou salvar PDF", bookHtml, StringComparison.Ordinal);
    }
}
