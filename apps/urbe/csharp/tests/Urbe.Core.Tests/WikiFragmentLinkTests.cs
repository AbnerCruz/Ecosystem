using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-120: <c>[[Nota#Título]]</c> and <c>[[Nota#^id]]</c> are links (they were plain text): they show
/// "Nota › Título", like Obsidian's "Nota > Título", and lead to that part of the note. <c>[[#Título]]</c> is a part of
/// the note itself.
/// </summary>
public sealed class WikiFragmentLinkTests
{
    [Theory]
    [InlineData("Veja [[Plano#Metas]].", "data-note-name=\"Plano\" data-fragment=\"#Metas\">Plano › Metas<")]
    [InlineData("Veja [[Plano#Metas|as metas]].", "data-note-name=\"Plano\" data-fragment=\"#Metas\">as metas<")]
    [InlineData("Veja [[Plano#^meta]].", "data-note-name=\"Plano\" data-fragment=\"#^meta\">Plano › ^meta<")]
    [InlineData("Veja [[#Metas]].", "data-note-name=\"\" data-fragment=\"#Metas\">Metas<")]
    [InlineData("Veja [[Plano]].", "data-note-name=\"Plano\">Plano<")]   // as before (the 1.8.4's form)
    public void FragmentLinksAreLinks(string markdown, string expected) =>
        Assert.Contains(expected, MarkdownEngine.Render(markdown));

    [Fact]
    public void TheLineOfAHeadingOrABlock()
    {
        const string note = "# Plano\n\nIntro.\n\n## Metas\n\n- ler\n- correr ^meta\n\n```\n## Metas\n```\n";
        Assert.Equal(5, EmbedFragments.LineOf(note, "Metas"));
        Assert.Equal(5, EmbedFragments.LineOf(note, "metas"));            // without case
        Assert.Equal(8, EmbedFragments.LineOf(note, "^meta"));
        Assert.Equal(5, EmbedFragments.LineOf(note, "Plano#Metas"));      // a path of headings
        Assert.Null(EmbedFragments.LineOf(note, "Outra"));
    }

    [Theory]
    [InlineData("Veja [[Plano#Metas]] aqui.")]
    [InlineData("Veja [[Plano#Metas|as metas]] aqui.")]
    [InlineData("Veja [[#Metas]] aqui.")]
    [InlineData("Veja [[Plano]] e [[Plano|o plano]] aqui.")]
    public void AFragmentLinkSurvivesTheVisualRoundTrip(string markdown) =>
        Assert.Equal(markdown, VisualMarkdown.FromHtml(MarkdownEngine.Render(markdown)).Trim());

    [Fact]
    public void TheHeadingsAndBlocksALinkCanNameOutsideCode()
    {
        const string note = "# Plano\n\n## Metas\n\n- correr ^meta\n\n```\n## Falso\n```\n\n### Orçamento anual\n";
        Assert.Equal(["Plano", "Metas", "Orçamento anual"], EmbedFragments.Headings(note));
        Assert.Equal(["meta"], EmbedFragments.BlockIds(note));
    }
}
