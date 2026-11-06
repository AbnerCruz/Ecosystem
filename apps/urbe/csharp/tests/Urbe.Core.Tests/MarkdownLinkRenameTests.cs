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

    // CST-196 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFolderRenamedCarriesEveryKindOfLinkToTheFilesInside()
    {
        // the client expands a folder into each file inside it
        var moves = new[] { ("Projetos/Plano.md", "Trabalho/Plano.md"), ("Projetos/img/a.png", "Trabalho/img/a.png"), ("Projetos/Sub/Nota b.md", "Trabalho/Sub/Nota b.md") };
        var files = new[] { "Trabalho/Plano.md", "Trabalho/img/a.png", "Trabalho/Sub/Nota b.md", "Diário.md", "Arquivo/Outra.md" };
        Assert.Equal("[p](Trabalho/Plano.md) ![](Trabalho/img/a.png) [b](<Trabalho/Sub/Nota b.md#Fim>) [c](Trabalho/Sub/Nota%20b.md)",
            Rewrite("[p](Projetos/Plano.md) ![](Projetos/img/a.png) [b](<Projetos/Sub/Nota b.md#Fim>) [c](Projetos/Sub/Nota%20b.md)", "Diário.md", moves, files));
        Assert.Equal("[p](../Trabalho/Plano.md \"o plano\") ![x](../Trabalho/img/a.png 'foto')",
            Rewrite("[p](../Projetos/Plano.md \"o plano\") ![x](../Projetos/img/a.png 'foto')", "Arquivo/Outra.md", moves, files));
        // the notes inside the folder that moved with it: relative links between them stay as they were
        Assert.Equal("[p](../Plano.md) ![](../img/a.png)",
            Rewrite("[p](../Plano.md) ![](../img/a.png)", "Trabalho/Sub/Nota b.md", moves, files, noteFrom: "Projetos/Sub/Nota b.md"));
    }

    [Fact]
    public void ADotSlashAQueryAndATitleInParenthesesStay()
    {
        var files = new[] { "Notas/Novo.md", "Notas/Índice.md" };
        var moves = new[] { ("Notas/Velho.md", "Notas/Novo.md") };
        Assert.Equal("[a](./Novo.md) [b](Novo.md?x=1#Topo) [c](Novo.md (título))",
            Rewrite("[a](./Velho.md) [b](Velho.md?x=1#Topo) [c](Velho.md (título))", "Notas/Índice.md", moves, files));
    }

    [Fact]
    public void ParenthesesInAFileNameAndANewNameThatNeedsEncoding()
    {
        var files = new[] { "Relatório (2).md", "Nota.md", "Plano (final).md" };
        Assert.Equal("[r](Relatório%20%282%29.md)", Rewrite("[r](Relatorio.md)", "Nota.md", [("Relatorio.md", "Relatório (2).md")], files));
        // a destination already written with balanced parentheses is found
        Assert.Equal("[p](Plano%20%28final%29.md)", Rewrite("[p](Plano(v1).md)", "Nota.md", [("Plano(v1).md", "Plano (final).md")], files));
    }

    [Fact]
    public void AccentsWrittenEncodedStayEncoded()
    {
        var files = new[] { "Índice novo.md", "Nota.md" };
        var moves = new[] { ("Índice.md", "Índice novo.md") };
        Assert.Equal("[i](%C3%8Dndice%20novo.md)", Rewrite("[i](%C3%8Dndice.md)", "Nota.md", moves, files));
        // written plainly, they stay plain
        Assert.Equal("[i](<Índice novo.md>)", Rewrite("[i](<Índice.md>)", "Nota.md", moves, files));
    }

    [Fact]
    public void AReferenceDefinitionFollowsButAFootnoteDoesNot()
    {
        var files = new[] { "Pasta/Plano novo.md", "Nota.md", "Plano.md" };
        var moves = new[] { ("Pasta/Plano.md", "Pasta/Plano novo.md") };
        const string text = "Veja [o plano][p] e [^1].\n\n[p]: Pasta/Plano.md \"Plano\"\n  [q]: <Pasta/Plano.md>\n[^1]: Pasta/Plano.md\n";
        Assert.Equal("Veja [o plano][p] e [^1].\n\n[p]: Pasta/Plano%20novo.md \"Plano\"\n  [q]: <Pasta/Plano novo.md>\n[^1]: Pasta/Plano.md\n",
            Rewrite(text, "Nota.md", moves, files));
    }

    [Fact]
    public void CodeOfEveryKindIsNeverTouched()
    {
        var files = new[] { "Novo.md", "Nota.md" };
        var moves = new[] { ("Velho.md", "Novo.md") };
        const string text = "````md\n```\n[a](Velho.md)\n```\n[b](Velho.md)\n````\n" +
                            "  ~~~\n[c](Velho.md)\n  ~~~\n" +
                            "``um ` [f](Velho.md)`` e \\[g](Velho.md)\n";
        Assert.Equal(text, Rewrite(text, "Nota.md", moves, files));
        // a fence never closed runs to the end of the note
        const string open = "```\n[a](Velho.md)\n\n[b](Velho.md)\n";
        Assert.Equal(open, Rewrite(open, "Nota.md", moves, files));
        // but a link after the code, one in an indented paragraph (the Urbe shows it as a link) and one in a list item follow
        Assert.Equal("```\nx\n```\n[a](Novo.md)\n- item\n\n    [b](Novo.md)\n\n    [c](Novo.md) `x`\n",
            Rewrite("```\nx\n```\n[a](Velho.md)\n- item\n\n    [b](Velho.md)\n\n    [c](Velho.md) `x`\n", "Nota.md", moves, files));
    }

    [Fact]
    public void LinksThatDidNotFindTheirFileBeforeAreLeftAlone()
    {
        var files = new[] { "Projetos/Novo.md", "Projetos/Índice.md", "Outra.md" };
        var moves = new[] { ("Projetos/Velho.md", "Projetos/Novo.md") };
        // Velho.md from the root never existed, and ../ out of the vault finds nothing
        const string text = "[a](Pasta/Velho.md) [b](../../Velho.md) [c](Nada.md) [d](file:///c:/Velho.md) [e](www.site.com/Velho.md) [f]()";
        Assert.Equal(text, Rewrite(text, "Projetos/Índice.md", moves, files));
    }

    [Fact]
    public void ANoteThatLeftTheRootWritesItsLinksFromWhereItIs()
    {
        var files = new[] { "Pasta/Nota.md", "Plano.md", "Pasta/Plano.md", "img/a.png" };
        var moves = new[] { ("Nota.md", "Pasta/Nota.md") };
        // from Pasta/, "Plano.md" would now be Pasta/Plano.md: the link keeps pointing to the one it meant
        Assert.Equal("[p](../Plano.md) ![](../img/a.png) [self](Nota.md#Topo)",
            Rewrite("[p](Plano.md) ![](img/a.png) [self](Nota.md#Topo)", "Pasta/Nota.md", moves, files, noteFrom: "Nota.md"));
    }

    [Fact]
    public void BackslashesAndWindowsLineEndings()
    {
        var files = new[] { "Pasta/Novo.md", "Nota.md" };
        var moves = new[] { ("Pasta/Velho.md", "Pasta/Novo.md") };
        Assert.Equal("[a](Pasta/Novo.md)\r\n```\r\n[b](Pasta/Velho.md)\r\n```\r\n",
            Rewrite("[a](Pasta\\Velho.md)\r\n```\r\n[b](Pasta/Velho.md)\r\n```\r\n", "Nota.md", moves, files));
    }
}
