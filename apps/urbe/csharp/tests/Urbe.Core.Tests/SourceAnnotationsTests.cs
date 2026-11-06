using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class SourceAnnotationsTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Other = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    // the exact shape the readers write (MainView.InsertStudyCitationAsync)
    private static readonly string Note = "# Estudo\n\nTexto meu.\n\n" +
        "**Destaque**\n\n> A célula é a unidade\n> básica da vida.\n\nFonte: [[livros/bio.pdf#page=12&sha256=" + Sha + "&text=40&length=37|bio.pdf, p. 12]]\n\n" +
        "> Uma citação simples.\n\nFonte: [[livros/bio.pdf#page=3&sha256=" + Sha + "|bio.pdf, p. 3]]\n\n" +
        "**Anotação**\n\n> Mitocôndria\n\nComentário: rever para a prova\n\nFonte: [[livros/quimica.epub#chapter=2&sha256=" + Other + "&text=5&length=11|quimica.epub, 2]]\n\n" +
        "**Marcador**\n\n> Marcador da página 7\n\nFonte: [[livros/bio.pdf#page=7&sha256=" + Sha + "|bio.pdf, p. 7]]\n\n" +
        "> Da web.\n\nFonte: [Fonte](<https://exemplo.org/artigo#:~:text=Da%20web.>) · captura em 2026-11-05 10:00 -03:00; conteúdo web pode mudar\n";

    [Fact]
    public void Parse_ReadsEveryKindQuoteCommentAndAnchorWhereTheReadersWroteThem()
    {
        var all = SourceAnnotations.Parse("Estudo.md", Note);
        Assert.Equal(5, all.Count);
        var highlight = all[0];
        Assert.Equal(SourceAnnotationKind.Highlight, highlight.Kind);
        Assert.Equal("A célula é a unidade\nbásica da vida.", highlight.Quote);
        Assert.Equal("livros/bio.pdf", highlight.Source);
        Assert.Equal(("page", 12, 40, 37), (highlight.Unit, highlight.Index, highlight.TextOffset, highlight.TextLength));
        Assert.Equal(Sha, highlight.Sha256);
        Assert.Equal("p. 12", highlight.Place);
        Assert.Equal("**Destaque**", Note.Split('\n')[highlight.Line - 1]);
        Assert.Equal(SourceAnnotationKind.Citation, all[1].Kind);
        Assert.Equal(SourceAnnotationKind.Comment, all[2].Kind);
        Assert.Equal("rever para a prova", all[2].Comment);
        Assert.Equal("capítulo 2", all[2].Place);
        Assert.Equal(SourceAnnotationKind.Bookmark, all[3].Kind);
        Assert.True(all[4].IsWeb);
        Assert.Equal("https://exemplo.org/artigo", all[4].Source);
    }

    [Fact]
    public void ForSource_GathersOneSourceAcrossNotes()
    {
        var notes = new[] { ("b.md", Note), ("a.md", "> Outro\n\nFonte: [[livros/bio.pdf#page=1&sha256=" + Sha + "|bio]]\n"), ("c.md", "sem nada") };
        var bio = SourceAnnotations.ForSource(notes, "livros/bio.pdf");
        Assert.Equal(["a.md", "b.md", "b.md", "b.md"], bio.Select(a => a.NotePath));
        Assert.Single(SourceAnnotations.ForSource(notes, "livros/quimica.epub"));
    }

    [Fact]
    public void Status_IsExactOnlyForTheSameRevisionAndNeverReanchors()
    {
        var a = SourceAnnotations.Parse("n.md", Note)[0];
        Assert.Equal(SourceAnnotationStatus.Current, SourceAnnotations.Status(a, Sha));
        Assert.Equal(SourceAnnotationStatus.Outdated, SourceAnnotations.Status(a, Other));
        Assert.Equal(SourceAnnotationStatus.Unknown, SourceAnnotations.Status(a, null));
        Assert.Equal(SourceAnnotationStatus.Missing, SourceAnnotations.Status(a, Sha, exists: false));
        Assert.Equal(SourceAnnotationStatus.Unknown, SourceAnnotations.Status(SourceAnnotations.Parse("n.md", Note)[4], Sha));
    }

    [Fact]
    public void WebAnnotations_AreW3cWithQuoteContextOnlyWhenTheTextMatches()
    {
        var all = SourceAnnotations.Parse("Estudo.md", Note);
        string page = new string('x', 40) + "A célula é a unidade\nbásica da vida." + " E segue o texto.";
        var json = SourceAnnotations.ToWebAnnotations(all, a => a.Index == 12 ? page : "nada a ver", "Teste");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("AnnotationCollection", root.GetProperty("type").GetString());
        Assert.Equal(5, root.GetProperty("total").GetInt32());
        var items = root.GetProperty("first").GetProperty("items");
        var first = items[0];
        Assert.Equal("highlighting", first.GetProperty("motivation").GetString());
        var selectors = first.GetProperty("target").GetProperty("selector");
        var quote = selectors[0];
        Assert.Equal("TextQuoteSelector", quote.GetProperty("type").GetString());
        Assert.Equal("A célula é a unidade\nbásica da vida.", quote.GetProperty("exact").GetString());
        Assert.Equal(new string('x', 32), quote.GetProperty("prefix").GetString());
        Assert.Equal(" E segue o texto.", quote.GetProperty("suffix").GetString());
        Assert.Equal(40, selectors[1].GetProperty("start").GetInt32());
        Assert.Equal("page=12", selectors[2].GetProperty("value").GetString());
        // a text that does not hold the quote at its offset gives no invented context
        Assert.False(items[2].GetProperty("target").GetProperty("selector")[0].TryGetProperty("prefix", out _));
        Assert.Equal("commenting", items[2].GetProperty("motivation").GetString());
        Assert.Equal("rever para a prova", items[2].GetProperty("body").GetProperty("value").GetString());
        Assert.Equal("bookmarking", items[3].GetProperty("motivation").GetString());
        Assert.Equal("FragmentSelector", items[3].GetProperty("target").GetProperty("selector").GetProperty("type").GetString());
        Assert.Equal("https://exemplo.org/artigo", items[4].GetProperty("target").GetProperty("source").GetString());
        // ids are stable across exports
        Assert.Equal(first.GetProperty("id").GetString(), JsonDocument.Parse(SourceAnnotations.ToWebAnnotations(all)).RootElement.GetProperty("first").GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void Parse_IgnoresFonteLinesThatAreNotAnnotations()
    {
        Assert.Empty(SourceAnnotations.Parse("n.md", "Fonte: [[x.pdf#page=1|x]]\n"));
        Assert.Empty(SourceAnnotations.Parse("n.md", "> quote\n\nFonte: livro antigo sem link\n"));
    }
}
