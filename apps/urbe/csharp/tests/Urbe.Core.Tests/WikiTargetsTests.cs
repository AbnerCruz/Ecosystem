using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-040/041: [[…]] and ![[…]] find every file of the vault, not only notes, and a file link opens the file.</summary>
public sealed class WikiTargetsTests
{
    private static readonly string[] Files =
    [
        "Imagens/retrato.png", "Personagens/retrato.png", "Livros/Calculo volume 1.pdf", "Audio/aula 03.m4a",
        "Projetos/site/index.html", "Projetos/site/estilo.css", "Grafos/Mapa.grafo.json", "Paginas/Apostila.page.json"
    ];

    [Fact]
    public void FilesMatchByNameStartFirstThenAnywhereInThePath()
    {
        Assert.Equal(["Livros/Calculo volume 1.pdf"], WikiTargets.Files(Files, "calc", 10));
        Assert.Equal(["Imagens/retrato.png", "Personagens/retrato.png"], WikiTargets.Files(Files, "retr", 10));
        // a folder name finds its files too, after the names that start with the query
        Assert.Equal(["Projetos/site/estilo.css", "Projetos/site/index.html"], WikiTargets.Files(Files, "site", 10));
        Assert.Equal(["Audio/aula 03.m4a"], WikiTargets.Files(Files, "AULA", 10));
        Assert.Empty(WikiTargets.Files(Files, "nada disso", 10));
        Assert.Equal(3, WikiTargets.Files(Files, "", 3).Count);
    }

    [Fact]
    public void TheLinkUsesTheFileNameUnlessItIsAmbiguous()
    {
        Assert.Equal("Calculo volume 1.pdf", WikiTargets.LinkName("Livros/Calculo volume 1.pdf", Files));
        Assert.Equal("Imagens/retrato.png", WikiTargets.LinkName("Imagens/retrato.png", Files));
    }

    [Theory]
    [InlineData("Calculo volume 1.pdf", "Livros/Calculo volume 1.pdf")]
    [InlineData("calculo VOLUME 1.pdf", "Livros/Calculo volume 1.pdf")]
    [InlineData("Imagens/retrato.png", "Imagens/retrato.png")]
    [InlineData("retrato.png", null)]
    [InlineData("Apostila", null)]
    [InlineData("Projetos/site/index.html#topo", "Projetos/site/index.html")]
    public void AFileLinkResolvesToOneFileOrNone(string name, string? path) =>
        Assert.Equal(path, WikiTargets.ResolveFile(name, Files));
}
