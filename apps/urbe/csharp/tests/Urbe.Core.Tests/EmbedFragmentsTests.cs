using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-064: <c>![[Nota#Título]]</c>, <c>![[Nota#^id]]</c> and <c>![[Nota#L3-L5]]</c> show that part of the note
/// (Obsidian's convention for headings and blocks; GitHub's for lines), not the whole note.
/// </summary>
public sealed class EmbedFragmentsTests
{
    private const string Note = """
        # Reino
        Abertura.

        ## História
        Fundado no ano 3.

        ### Guerras
        A guerra do norte.

        ## Economia
        Trigo e sal. ^renda

        - ferro
        - ouro ^metais

        ```
        ## não é título
        ```
        """;

    [Fact]
    public void AHeadingBringsItsSectionUpToTheNextHeadingOfTheSameLevel()
    {
        var part = EmbedFragments.Extract(Note, "História");
        Assert.Null(part.Error);
        Assert.StartsWith("## História\nFundado no ano 3.", part.Text);
        Assert.Contains("### Guerras\nA guerra do norte.", part.Text);
        Assert.DoesNotContain("Economia", part.Text);
    }

    [Fact]
    public void HeadingsMatchWithoutCaseOrAccentsAndCanBeNested()
    {
        Assert.StartsWith("### Guerras", EmbedFragments.Extract(Note, "historia#guerras").Text);
        Assert.StartsWith("### Guerras", EmbedFragments.Extract(Note, "Guerras").Text);
        Assert.Contains("não existe", EmbedFragments.Extract(Note, "não é título").Error);
    }

    [Fact]
    public void ABlockIdBringsItsParagraphOrListItemWithoutTheMarker()
    {
        Assert.Equal("Trigo e sal.", EmbedFragments.Extract(Note, "^renda").Text);
        Assert.Equal("- ouro", EmbedFragments.Extract(Note, "^metais").Text);
        Assert.Contains("^nada", EmbedFragments.Extract(Note, "^nada").Error);
        // a seal the Urbe keeps at the end of the line does not hide the marker
        Assert.Equal("Texto. {ia:\"m\"}", EmbedFragments.Extract("Texto. ^x {ia:\"m\"}\n", "^x").Text);
    }

    [Fact]
    public void TheMarkersAreHiddenWhenShownButNotInCode() =>
        Assert.Equal("Trigo e sal.\n- ouro {ia:\"m\"}\n```\nx ^code\n```",
            EmbedFragments.HideBlockIds("Trigo e sal. ^renda\n- ouro ^metais {ia:\"m\"}\n```\nx ^code\n```"));

    [Fact]
    public void LinesAreOneBased()
    {
        Assert.Equal("## História\nFundado no ano 3.", EmbedFragments.Extract(Note, "L4-L5").Text);
        Assert.Equal("Abertura.", EmbedFragments.Extract(Note, "L2").Text);
        Assert.Equal("Abertura.", EmbedFragments.Extract(Note, "l2-2").Text);
        Assert.Contains("linhas", EmbedFragments.Extract(Note, "L90-L95").Error);
    }

    [Fact]
    public void ATargetSplitsIntoNameFragmentAndLabel()
    {
        Assert.Equal(("Reino", "História"), EmbedFragments.Split("Reino#História|apelido"));
        Assert.Equal(("Reino", "^renda"), EmbedFragments.Split("Reino#^renda"));
        Assert.Equal(("Reino", null), EmbedFragments.Split("Reino|apelido"));
        Assert.Equal(("Pasta/Mapa.grafo.json", null), EmbedFragments.Split("Pasta/Mapa.grafo.json"));
    }
}
