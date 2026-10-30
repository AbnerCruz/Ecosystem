namespace Urbe.Core.Tests;

public sealed class LinkRenameTests
{
    private static string Rewrite(string content, (string, string)[] moves, params string[] files) =>
        DocumentModel.RewriteLinks(content, LinkRename.Retarget(moves, files));

    [Fact]
    public void ALinkByNameGetsTheNewNameKeepingSectionAliasAndEmbed()
    {
        var files = new[] { "Comece.md", "Final.md" };
        Assert.Equal("Veja [[Final]], [[Final#Parte|a parte]], [[Final|apelido]] e ![[Final]].",
            Rewrite("Veja [[Rascunho]], [[Rascunho#Parte|a parte]], [[Rascunho|apelido]] e ![[Rascunho]].", [("Rascunho.md", "Final.md")], files));
        // the letter case of the link does not matter; the extension written is kept
        Assert.Equal("[[Final]] e [[Final.md]]", Rewrite("[[rascunho]] e [[Rascunho.md]]", [("Rascunho.md", "Final.md")], files));
    }

    [Fact]
    public void ALinkByPathFollowsAMoveAndANameLinkStaysWhenTheNameIsTheSame()
    {
        var files = new[] { "Comece.md", "Arquivo/Rascunho.md" };
        Assert.Equal("[[Arquivo/Rascunho]] [[Arquivo/Rascunho.md#x]] [[Rascunho]]",
            Rewrite("[[Pasta/Rascunho]] [[Pasta/Rascunho.md#x]] [[Rascunho]]", [("Pasta/Rascunho.md", "Arquivo/Rascunho.md")], files));
    }

    [Fact]
    public void AttachmentsFollowToo()
    {
        var files = new[] { "Comece.md", "Imagens/capa.png" };
        Assert.Equal("![[capa.png]] e ![[Imagens/capa.png]]",
            Rewrite("![[foto.png]] e ![[Anexos/foto.png]]", [("Anexos/foto.png", "Imagens/capa.png")], files));
    }

    [Fact]
    public void CodeOtherNotesAndAmbiguousNamesAreLeftAlone()
    {
        // another "Rascunho" in the vault: [[Rascunho]] may mean that one
        var files = new[] { "Comece.md", "Final.md", "Outra pasta/Rascunho.md" };
        var text = "[[Rascunho]] `[[Rascunho]]`\n```\n[[Rascunho]]\n```\n[[Pasta/Rascunho]] [[Outra]]";
        Assert.Equal("[[Rascunho]] `[[Rascunho]]`\n```\n[[Rascunho]]\n```\n[[Final]] [[Outra]]",
            Rewrite(text, [("Pasta/Rascunho.md", "Final.md")], files));
        Assert.Same(text, DocumentModel.RewriteLinks(text, _ => null));
    }
}
