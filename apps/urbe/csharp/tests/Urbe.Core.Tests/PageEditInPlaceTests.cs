using System.Text.Json.Nodes;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>UC-62 slice 4: the preview lets one write the sections' plain texts in place; the export stays as it was.</summary>
public sealed class PageEditInPlaceTests
{
    private static JsonObject Page() => new()
    {
        ["meta"] = new JsonObject { ["title"] = "Portfólio da Lia" },
        ["sections"] = new JsonArray(
            new JsonObject { ["type"] = "hero", ["props"] = new JsonObject { ["title"] = "Olá, sou a Lia", ["eyebrow"] = "**Destaque**" } },
            new JsonObject { ["type"] = "features", ["props"] = new JsonObject { ["title"] = "O que faço", ["items"] = new JsonArray(
                new JsonObject { ["icon"] = "★", ["title"] = "Desenho", ["text"] = "Muito." }) } })
    };

    [Fact]
    public void ThePreviewMakesThePlainTextsEditableAndTheExportDoesNot()
    {
        var engine = new PageEngine(2026) { UrbeMarks = true };
        var preview = engine.Render(engine.Normalize(Page()).Spec, preview: true);
        Assert.Contains("<span class=\"urbe-ed\" contenteditable=\"true\" spellcheck=\"true\" data-path=\"sections.0.props.title\">Olá, sou a Lia</span>", preview);
        Assert.Contains("data-path=\"sections.1.props.title\">O que faço</span>", preview);
        Assert.Contains("data-path=\"sections.1.props.items.0.title\">Desenho</span>", preview);
        // a text with Markdown is left to the Editar panel; the icon is not text
        Assert.DoesNotContain("props.eyebrow", preview);
        Assert.DoesNotContain("items.0.icon", preview);
        Assert.Contains("<strong>Destaque</strong>", preview);
        // no mark is left anywhere (attributes, the title of the document)
        Assert.DoesNotContain(preview, c => c is >= '' and <= '');
        Assert.Contains("urbePage:'field'", preview);

        var export = engine.Render(engine.Normalize(Page()).Spec);
        Assert.DoesNotContain("urbe-ed", export);
        Assert.Contains("Olá, sou a Lia", export);
        // the engine of the oracles (no Urbe marks) never marks anything
        Assert.DoesNotContain("urbe-ed", new PageEngine(2026).Render(Page(), preview: true));
    }

    [Theory]
    [InlineData("Olá, sou a Lia", true)]
    [InlineData("Com **negrito**", false)]
    [InlineData("Duas\nlinhas", false)]
    [InlineData("Com comentário{\"x\"}", false)]
    [InlineData("", false)]
    public void PlainIsOneLineWithoutMarks(string text, bool plain) => Assert.Equal(plain, PageEngine.PlainText(text));

    [Fact]
    public void AnImageOfTheSectionsCarriesItsFieldForTheImageChoice()
    {
        var page = new JsonObject
        {
            ["meta"] = new JsonObject { ["title"] = "Fotos" },
            ["sections"] = new JsonArray(
                new JsonObject { ["type"] = "hero", ["props"] = new JsonObject { ["title"] = "Capa", ["image"] = "https://exemplo.com/capa.jpg?a=1&b=2" } },
                new JsonObject { ["type"] = "gallery", ["props"] = new JsonObject { ["items"] = new JsonArray(
                    new JsonObject { ["image"] = "https://exemplo.com/um.jpg", ["caption"] = "Um" },
                    new JsonObject { ["image"] = "https://exemplo.com/um.jpg", ["caption"] = "De novo" }) } })
        };
        var engine = new PageEngine(2026) { UrbeMarks = true };
        var preview = engine.Render(engine.Normalize(page).Spec, preview: true);
        Assert.Contains("<img data-urbe-img=\"sections.0.props.image\"", preview);
        // the same address twice: each picture its own field, in the page's order
        Assert.Contains("<img data-urbe-img=\"sections.1.props.items.0.image\"", preview);
        Assert.Contains("<img data-urbe-img=\"sections.1.props.items.1.image\"", preview);
        Assert.Contains("urbePage:'image'", preview);
        Assert.DoesNotContain("data-urbe-img", engine.Render(engine.Normalize(page).Spec));
    }
}
