using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-44 (DEC-0045): note templates in Modelos/ and `:::tipo` … `:::` containers.</summary>
public sealed class NoteContainersTests
{
    [Fact]
    public void ContainerHasTypeTitleBodyAndRange()
    {
        const string text = "Antes\n\n:::ficha Gandalf\nRaça: Maia\n\nIdade: muita\n:::\n\nDepois\n";
        var box = Assert.Single(NoteContainers.Parse(text));
        Assert.Equal("ficha", box.Type);
        Assert.Equal("Gandalf", box.Title);
        Assert.Equal(text.IndexOf(":::ficha", StringComparison.Ordinal), box.Start);
        Assert.Equal(":::ficha Gandalf\nRaça: Maia\n\nIdade: muita\n:::", text[box.Start..box.End]);
        Assert.Equal("Raça: Maia\n\nIdade: muita\n", text[box.BodyStart..box.BodyEnd]);
        Assert.Empty(box.Children);
    }

    [Fact]
    public void ContainersNestAndTheClosingLineClosesTheInnermost()
    {
        const string text = ":::ficha Frodo\n:::atributos\nForça: 3\n:::\n:::nota\nx\n:::\n:::\n:::outro\n:::";
        var all = NoteContainers.Parse(text);
        Assert.Equal(2, all.Count);
        var ficha = all[0];
        Assert.Equal(new[] { "atributos", "nota" }, ficha.Children.Select(c => c.Type));
        Assert.Equal("", ficha.Children[0].Title);
        Assert.Equal("Força: 3\n", text[ficha.Children[0].BodyStart..ficha.Children[0].BodyEnd]);
        Assert.Equal("outro", all[1].Type);
        Assert.Equal(all[1].BodyStart, all[1].BodyEnd);   // empty body
        Assert.Equal(text.Length, all[1].End);
    }

    [Fact]
    public void UnclosedOpeningStrayCloseAndFencedCodeStayText()
    {
        Assert.Empty(NoteContainers.Parse(":::ficha\nsem fim\n"));
        Assert.Empty(NoteContainers.Parse("texto\n:::\n"));
        Assert.Empty(NoteContainers.Parse("```\n:::ficha\n:::\n```\n"));
        Assert.Empty(NoteContainers.Parse("::: ficha\n:::\n"));            // the type comes right after the colons
        // an unclosed outer one: the closed inner one is still a container
        var inner = Assert.Single(NoteContainers.Parse(":::a\n:::b\nx\n:::\n"));
        Assert.Equal("b", inner.Type);
    }

    [Fact]
    public void CrLfAndIndentedLinesAreRead()
    {
        var box = Assert.Single(NoteContainers.Parse("  :::ficha  Sam \r\ncorpo\r\n  :::\r\n"));
        Assert.Equal("Sam", box.Title);
    }

    [Fact]
    public void TheRendererKeepsContainersAsTextWithoutBreaking()
    {
        // the 1.8.4 renderer (and so pages and HTML export) has no containers: the lines stay text, nothing is lost
        var html = MarkdownEngine.Render(":::ficha Gandalf\nRaça: Maia\n:::\n");
        Assert.Contains(":::ficha Gandalf", html, StringComparison.Ordinal);
        Assert.Contains("Raça: Maia", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplatesAreTheNotesOfModelosByName()
    {
        var templates = NoteTemplateEngine.Templates(["Notas/a.md", "Modelos/Reunião.md", "Modelos/Ficha de personagem.md", "Modelos/img.png", "x/Modelos/b.md"]);
        Assert.Equal(new[] { "Modelos/Ficha de personagem.md", "Modelos/Reunião.md" }, templates);
        Assert.Equal("Ficha de personagem", NoteTemplateEngine.NameOf("Modelos/Ficha de personagem.md"));
        Assert.Equal("RPG/Ficha", NoteTemplateEngine.NameOf("Modelos/RPG/Ficha.md"));
        Assert.Empty(NoteTemplateEngine.Templates(null));
    }

    [Fact]
    public void FillReplacesTitleAndDateAndKeepsTheRest()
    {
        var text = NoteTemplateEngine.Fill("# {{titulo}}\r\nCriada em {{data}} · {{título}}\n{{outro}} {{}}\n", "Gandalf {{data}}", new DateTime(2026, 10, 16, 23, 59, 0));
        Assert.Equal("# Gandalf {{data}}\r\nCriada em 2026-10-16 · Gandalf {{data}}\n{{outro}} {{}}\n", text);
        Assert.Equal("", NoteTemplateEngine.Fill(null, "x", DateTime.Now));
    }
}
