namespace Urbe.Core.Tests;

public sealed class PublicationAssetsTests
{
    [Fact]
    public void VaultImagesAreEmbeddedAndAMissingOneIsSaid()
    {
        var html = MarkdownEngine.Render("![[foto.png]]\n\n![Capa](Pasta/capa.jpg)\n\n![[sumiu.png]]\n\n![web](https://x.org/a.png)\n\n![[Outra nota]]", (_, _) => "");
        Assert.Equal(["foto.png", "sumiu.png", "Pasta/capa.jpg"], PublicationAssets.References(html));
        var out_ = PublicationAssets.Embed(html, p => p == "sumiu.png" ? null : [1, 2, 3]);
        Assert.Contains("<img src=\"data:image/png;base64,AQID\" alt=\"foto\">", out_);
        Assert.Contains("<img src=\"data:image/jpeg;base64,AQID\" alt=\"Capa\">", out_);
        Assert.Contains("[imagem não encontrada: sumiu.png]", out_);
        Assert.Contains("https://x.org/a.png", out_);
        Assert.Contains("data-note-name=\"Outra nota\"", out_);
        Assert.DoesNotContain("src=\"foto.png\"", out_);
    }

    [Fact]
    public void WikiImageEmbedsBecomeMarkdownImagesOutsideCode() =>
        Assert.Equal("![mapa velho](Pasta/mapa%20velho.png) e ![[Nota]]\n```\n![[x.png]]\n```\n![b](b.jpg)",
            PublicationAssets.WikiImagesToMarkdown("![[Pasta/mapa velho.png|300]] e ![[Nota]]\n```\n![[x.png]]\n```\n![[b.jpg]]"));
}
