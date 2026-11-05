using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-167: the HTML presentation cut from live notes, with speaker notes and a print profile.</summary>
public sealed class PublicationSlidesTests
{
    [Fact]
    public void Split_ByHeadingAndRule_NotInsideCodeOrContainers_NotesApart()
    {
        var body = "Abertura\n\n## Tema\n\nTexto\n\n---\n\nSem título\n\n```md\n# não é slide\n---\n```\n\n:::aviso\n## dentro da caixa\n:::\n\n# Outro\n\nCorpo\n\n:::notas\nFalar devagar.\n:::\n\nTítulo setext\n---\n";
        var slides = PublicationSlides.Split(body);
        Assert.Equal(4, slides.Count);
        Assert.StartsWith("Abertura", slides[0].Markdown);
        Assert.StartsWith("## Tema", slides[1].Markdown);
        Assert.Contains("Sem título", slides[2].Markdown);
        Assert.Contains("# não é slide", slides[2].Markdown);
        Assert.Contains("## dentro da caixa", slides[2].Markdown);
        Assert.StartsWith("# Outro", slides[3].Markdown);
        Assert.Contains("Falar devagar.", slides[3].Notes);
        Assert.DoesNotContain("Falar devagar", slides[3].Markdown);
        Assert.Contains("Título setext\n---", slides[3].Markdown);
    }

    [Fact]
    public void Deck_HasOneSectionPerSlide_NotesHiddenFromSlides_AndPrintsOnePerPage()
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "Aula.md", Content = "# Um\n\nTexto $x^2$\n\n:::notas\nNOTA\n:::\n\n## Dois\n\n- a\n- b\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Aula <1>", a.Id) with { Design = new PublicationDesign { Layout = "slides" } };
        var html = PublicationHtml.Export(project, catalog).Html;
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, "<section class=\"slide\"").Count); // cover + 2
        Assert.Contains("Aula &lt;1&gt;", html);
        Assert.Contains("<aside class=\"notes\" data-for=\"s2\">", html);
        var slide2 = html[html.IndexOf("id=\"s2\"")..html.IndexOf("<aside class=\"notes\"")];
        Assert.DoesNotContain("NOTA", slide2);
        Assert.Contains("<svg", slide2);
        Assert.Contains("size:320mm 180mm", html);
        Assert.Contains("break-after:page", html);
        Assert.Contains("script-src 'unsafe-inline'", html);
        Assert.DoesNotContain("default-src *", html);
    }
}
