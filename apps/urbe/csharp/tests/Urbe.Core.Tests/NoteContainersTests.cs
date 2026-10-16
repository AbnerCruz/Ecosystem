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

    [Fact]
    public void InputFieldsAreTheOtherPlaceholdersInOrderOnce()
    {
        Assert.Equal(["classe", "raça", "nivel_1"],
            NoteTemplateEngine.InputFields("# {{titulo}} {{título}}\n{{data}} {{classe}} {{raça}}\n{{classe}} {{nivel_1}} {{}} {{1x}}"));
        Assert.Empty(NoteTemplateEngine.InputFields("# {{titulo}} {{data}}"));
        Assert.Empty(NoteTemplateEngine.InputFields(null));
    }

    [Fact]
    public void FillWithValuesInsertsThemLiterallyAndKeepsUnknownFields()
    {
        var values = new Dictionary<string, string> { ["classe"] = "Mago {{titulo}} $1 \\", ["vazio"] = "" };
        var text = NoteTemplateEngine.Fill("# {{titulo}}\n{{classe}}|{{vazio}}|{{outro}}|{{data}}", "Gandalf", new DateTime(2026, 10, 16), values);
        Assert.Equal("# Gandalf\nMago {{titulo}} $1 \\||{{outro}}|2026-10-16", text);
        // without values it is the first slice's Fill
        Assert.Equal(NoteTemplateEngine.Fill("{{titulo}} {{x}}", "a", new DateTime(2026, 1, 1)),
            NoteTemplateEngine.Fill("{{titulo}} {{x}}", "a", new DateTime(2026, 1, 1), null));
    }

    [Fact]
    public void WrapPutsTheSelectionInABoxOrAnEmptyOneWithTheCaretInside()
    {
        // empty: the caret on the empty line of the body
        var e = NoteContainers.Wrap("", 0, 0, "aviso", "")!;
        Assert.Equal(":::aviso\n\n:::", e.Text);
        Assert.Equal((9, 9), (e.SelectionStart, e.SelectionEnd));
        // a selection in the middle of a line: the box gets lines of its own and the text stays selected
        var s = NoteContainers.Wrap("Antes meio depois", 6, 10, "ficha", " Gandalf\nCinzento ")!;
        Assert.Equal("Antes \n:::ficha Gandalf Cinzento\nmeio\n:::\n depois", s.Text);
        Assert.Equal("meio", s.Text[s.SelectionStart..s.SelectionEnd]);
        var box = Assert.Single(NoteContainers.Parse(s.Text));
        Assert.Equal(("ficha", "Gandalf Cinzento"), (box.Type, box.Title));
        // whole lines: the line breaks of the selection stay after the box
        Assert.Equal("a\n:::dica\nb\nc\n:::\n\nd", NoteContainers.Wrap("a\nb\nc\n\nd", 2, 6, "dica", null)!.Text);
        Assert.Null(NoteContainers.Wrap("x", 0, 0, "dois tipos", ""));
        Assert.Null(NoteContainers.Wrap("x", 0, 0, " ", ""));
        Assert.True(NoteContainers.IsType("ficha-rpg_2"));
        Assert.False(NoteContainers.IsType("a:b"));
    }
}
