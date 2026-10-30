using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-101: Markdown links ([texto](Nota.md)) follow what was renamed or moved, as the wiki links already did.</summary>
public sealed class MarkdownLinkRenameTests
{
    private static string Rewrite(string content, string note, (string From, string To)[] moves, string[] files, string? noteFrom = null) =>
        LinkRename.RewriteMarkdownLinks(content, noteFrom ?? note, note, moves, files);

    [Fact]
    public void ALinkFromTheRootOrRelativeToTheNoteKeepsItsForm()
    {
        var files = new[] { "Projetos/Plano final.md", "Projetos/Índice.md", "Diário.md", "Imagens/foto.png" };
        var moves = new[] { ("Projetos/Plano.md", "Projetos/Plano final.md") };

        // relative to the note's folder: stays relative; the space is encoded so the link still works
        Assert.Equal("Veja [o plano](Plano%20final.md#Metas).",
            Rewrite("Veja [o plano](Plano.md#Metas).", "Projetos/Índice.md", moves, files));
        // from the root of the vault, from a note at the root
        Assert.Equal("[p](Projetos/Plano%20final.md \"título\")",
            Rewrite("[p](Projetos/Plano.md \"título\")", "Diário.md", moves, files));
        // with <…> the space stays as it is
        Assert.Equal("[p](<Projetos/Plano final.md>)", Rewrite("[p](<Projetos/Plano.md>)", "Diário.md", moves, files));
        // a leading slash stays
        Assert.Equal("[p](/Projetos/Plano%20final.md)", Rewrite("[p](/Projetos/Plano.md)", "Diário.md", moves, files));
    }

    [Fact]
    public void AnImageInAFolderThatMovedAndARelativePathWithDots()
    {
        var files = new[] { "Arquivo/Imagens/foto.png", "Projetos/Sub/Nota.md" };
        var moves = new[] { ("Imagens/foto.png", "Arquivo/Imagens/foto.png") };
        Assert.Equal("![retrato](../../Arquivo/Imagens/foto.png)", Rewrite("![retrato](../../Imagens/foto.png)", "Projetos/Sub/Nota.md", moves, files));
        Assert.Equal("![retrato](Arquivo/Imagens/foto.png)", Rewrite("![retrato](Imagens/foto.png)", "Projetos/Sub/Nota.md", moves, files));
    }

    [Fact]
    public void ANoteThatMovedKeepsItsRelativeLinksToFilesThatStayed()
    {
        var files = new[] { "Arquivo/Nota.md", "Projetos/Plano.md", "Projetos/foto.png" };
        var moves = new[] { ("Projetos/Nota.md", "Arquivo/Nota.md") };
        Assert.Equal("[plano](../Projetos/Plano.md) ![](../Projetos/foto.png) [raiz](Projetos/Plano.md)",
            Rewrite("[plano](Plano.md) ![](foto.png) [raiz](Projetos/Plano.md)", "Arquivo/Nota.md", moves, files, noteFrom: "Projetos/Nota.md"));
    }

    [Fact]
    public void AddressesCodeAndLinksToOtherFilesAreLeftAlone()
    {
        var files = new[] { "Plano 2.md", "Outra.md", "Nota.md" };
        var moves = new[] { ("Plano.md", "Plano 2.md") };
        const string text = "[site](https://exemplo.com/Plano.md) [mail](mailto:a@b.c) [aqui](#Plano) [outra](Outra.md) `[c](Plano.md)`\n```\n[c](Plano.md)\n```\n[[Plano]]";
        Assert.Equal(text, Rewrite(text, "Nota.md", moves, files));
        // nothing moved: the same text, the same instance
        const string plain = "[outra](Outra.md)";
        Assert.Same(plain, Rewrite(plain, "Nota.md", [], files));
    }

    [Fact]
    public void AnEncodedLinkStaysEncoded()
    {
        var files = new[] { "Minhas notas/Plano novo.md", "Nota.md" };
        var moves = new[] { ("Minhas notas/Plano.md", "Minhas notas/Plano novo.md") };
        Assert.Equal("[p](Minhas%20notas/Plano%20novo.md)", Rewrite("[p](Minhas%20notas/Plano.md)", "Nota.md", moves, files));
    }
}
