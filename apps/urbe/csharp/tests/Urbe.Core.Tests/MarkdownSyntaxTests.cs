using Urbe.Core;
using S = Urbe.Core.MarkdownSyntax.Style;

namespace Urbe.Core.Tests;

public sealed class MarkdownSyntaxTests
{
    private static S At(string text, int index) =>
        MarkdownSyntax.Spans(text).FirstOrDefault(s => index >= s.Start && index < s.Start + s.Length).Style;

    private static S At(string text, string piece, int offset = 0) => At(text, text.IndexOf(piece, StringComparison.Ordinal) + offset);

    [Fact]
    public void BoldTextIsBoldAndItsStarsAreMarks()
    {
        const string t = "Um **forte** fim";
        Assert.Equal(S.Mark | S.Bold, At(t, "**"));
        Assert.Equal(S.Bold, At(t, "forte"));
        Assert.Equal(S.None, At(t, "fim"));
    }

    [Fact]
    public void ItalicIsNotReadInsideTheStarsOfBold()
    {
        const string t = "**forte** e *leve* e _outra_";
        Assert.Equal(S.Bold, At(t, "forte"));
        Assert.Equal(S.Italic, At(t, "leve"));
        Assert.Equal(S.Mark | S.Italic, At(t, "*leve"));
        Assert.Equal(S.Italic, At(t, "outra"));
        Assert.Equal(S.None, At("snake_case_name", "case"));
    }

    [Fact]
    public void LinksShowTheirTextAsLinkAndTheRestAsMarks()
    {
        const string t = "Veja [[Nota|apelido]] e [site](https://x.com) e https://y.com";
        Assert.Equal(S.Mark, At(t, "[["));
        Assert.Equal(S.Mark, At(t, "Nota"));
        Assert.Equal(S.Link, At(t, "apelido"));
        Assert.Equal(S.Link, At(t, "site"));
        Assert.Equal(S.Mark, At(t, "https://x"));
        Assert.Equal(S.Link, At(t, "https://y"));
        Assert.Equal(S.Link, At("[[Só nota]]", "Só"));
    }

    [Fact]
    public void NothingIsMarkdownInsideCodeOrComments()
    {
        const string t = "Use `**não**` e %%*nota*%%";
        Assert.Equal(S.Code, At(t, "**não"));
        Assert.Equal(S.Mark | S.Code, At(t, "`"));
        Assert.Equal(S.Comment, At(t, "*nota"));
        Assert.Empty(MarkdownSyntax.Spans("```\n**x**\n```"));
    }

    [Fact]
    public void LineMarksTagsAndFormulas()
    {
        Assert.Equal(S.Mark, At("## Título", 0));
        Assert.Equal(S.None, At("## Título", "Título"));
        Assert.Equal(S.Bullet, At("- [ ] tarefa", 0));
        Assert.Equal(S.Bullet, At("- [ ] tarefa", 3));
        Assert.Equal(S.None, At("- [ ] tarefa", "tarefa"));
        Assert.Equal(S.Bullet, At("12. item", 0));
        Assert.Equal(S.Mark, At("> citação", 0));
        Assert.Equal(S.Tag, At("Um #projeto/a fim", "#projeto"));
        Assert.Equal(S.Math, At("Seja $x^2$ aqui", "x^2"));
        Assert.Equal(S.Mark, At("Seja $x^2$ aqui", "$"));
        Assert.Equal(S.Strike, At("~~fora~~", "fora"));
        Assert.Equal(S.Highlight, At("==luz==", "luz"));
    }

    [Fact]
    public void SpansNeverOverlapAndStayInsideTheText()
    {
        const string t = "# T **a *b* c** `d` [[e|f]] [g](h) ~~i~~ ==j== %%k%% $l$ #m https://n.o\n- [x] p";
        var spans = MarkdownSyntax.Spans(t);
        int end = 0;
        foreach (var s in spans)
        {
            Assert.True(s.Start >= end && s.Length > 0 && s.Start + s.Length <= t.Length);
            end = s.Start + s.Length;
        }
    }
}
