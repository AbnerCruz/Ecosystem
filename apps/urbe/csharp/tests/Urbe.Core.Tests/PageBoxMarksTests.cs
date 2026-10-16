using System.Text.Json.Nodes;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>UC-44 in pages: `:::tipo título` … `:::` as a box in preview and export with UrbeMarks; untouched when off.</summary>
public sealed class PageBoxMarksTests
{
    private sealed class Notes(params PageNote[] all) : IPageNotes
    {
        public PageNote? Get(string path) => all.FirstOrDefault(n => n.Path == path);
        public IReadOnlyList<PageNote> List() => all;
    }

    private static JsonObject Page(string markdown) => new()
    {
        ["meta"] = new JsonObject { ["title"] = "Fichas" },
        ["sections"] = new JsonArray(
            new JsonObject { ["id"] = "s_a", ["type"] = "text", ["props"] = new JsonObject { ["markdown"] = markdown } },
            new JsonObject { ["id"] = "s_b", ["type"] = "note", ["props"] = new JsonObject { ["source"] = "note", ["path"] = "Nota.md" } },
            new JsonObject { ["id"] = "s_c", ["type"] = "code", ["props"] = new JsonObject { ["code"] = ":::x\ncódigo\n:::" } })
    };

    private static readonly Notes Vault = new(new PageNote("Nota.md", "Nota", "# Nota\n\n:::aviso\nCuidado com **o dragão**.\n:::\n"));

    private const string Text = "Antes\n:::ficha Gandalf <o \"Cinzento\">\nRaça: **Maia**\n\n:::atributos\nSabedoria: 20\n:::\n:::\nDepois";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void On_TheContainersAreBoxesInPreviewAndExport(bool preview)
    {
        var html = new PageEngine(2026) { UrbeMarks = true }.Render(Page(Text), Vault, preview);
        Assert.Contains("<div class=\"urbe-box urbe-box-ficha\" data-type=\"ficha\"><div class=\"urbe-box-head\"><span class=\"urbe-box-type\">ficha</span>" +
                        "<span class=\"urbe-box-title\">Gandalf &lt;o &quot;Cinzento&quot;&gt;</span></div>", html);
        Assert.Contains("<p>Raça: <strong>Maia</strong></p>", html);
        Assert.Contains("<div class=\"urbe-box urbe-box-atributos\"", html);
        // nested: the inner box closes before the outer one
        var inner = html.IndexOf("urbe-box-atributos", StringComparison.Ordinal);
        Assert.True(inner > html.IndexOf("urbe-box-ficha", StringComparison.Ordinal));
        Assert.Contains("<p>Sabedoria: 20</p></div></div>", html.Replace("\n", "", StringComparison.Ordinal));
        Assert.Contains("<p>Antes</p>", html);
        Assert.Contains("<p>Depois</p>", html);
        // a note shown in the page has its boxes too; code keeps its text
        Assert.Contains("urbe-box-aviso", html);
        Assert.Contains("<strong>o dragão</strong>", html);
        Assert.Contains(":::x\ncódigo\n:::", html);
        Assert.Contains(".urbe-box{", html);
        Assert.DoesNotContain(":::ficha", html);
        Assert.DoesNotContain('', html);
        Assert.DoesNotContain('', html);
        // the studio does not edit a text with a box in place (its ::: lines would be lost)
        Assert.Equal(preview, html.Contains("removeAttribute('data-edit')", StringComparison.Ordinal));
    }

    [Fact]
    public void Off_RendersTheTextsAsThe184Does()
    {
        var html = new PageEngine(2026).Render(Page(Text), Vault, preview: true);
        Assert.DoesNotContain("urbe-box", html);
        Assert.Contains(":::ficha Gandalf &lt;o &quot;Cinzento&quot;&gt;", html);
        // without any container, on and off write the same page
        var none = Page("Nada a marcar: ::: sozinho\n:::aberto e nunca fechado");
        var clean = new Notes(new PageNote("Nota.md", "Nota", "# Nota\n\nCasa amarela."));
        Assert.Equal(new PageEngine(2026).Render(none, clean, preview: true), new PageEngine(2026) { UrbeMarks = true }.Render(none, clean, preview: true));
    }
}
