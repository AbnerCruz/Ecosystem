using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class EditorLinksTests
{
    [Theory]
    [InlineData("Veja [[Outra nota|apelido]] aqui", 8, true, "Outra nota")]
    [InlineData("Veja [[Outra nota#Seção]] aqui", 10, true, "Outra nota#Seção")]
    [InlineData("![[imagem.png]]", 5, true, "imagem.png")]
    [InlineData("Um [site](https://exemplo.com/a) bom", 6, false, "https://exemplo.com/a")]
    [InlineData("Um [texto](Pasta/Nota%20boa.md) bom", 6, true, "Pasta/Nota boa")]
    [InlineData("Endereço https://exemplo.com/x. Fim", 14, false, "https://exemplo.com/x")]
    public void FindsTheLinkUnderTheCaret(string text, int caret, bool note, string target)
    {
        var link = EditorLinks.At(text, caret);
        Assert.NotNull(link);
        Assert.Equal(note, link!.Note);
        Assert.Equal(target, link.Target);
    }

    [Theory]
    [InlineData("Veja [[Outra nota]] aqui", 2)]
    [InlineData("Veja [[Outra nota]] aqui", 4)]
    [InlineData("Veja [[Outra nota]] aqui", 19)]
    [InlineData("[[a\nb]]", 2)]
    [InlineData("", 0)]
    public void NothingOutsideALink(string text, int caret) => Assert.Null(EditorLinks.At(text, caret));
}
