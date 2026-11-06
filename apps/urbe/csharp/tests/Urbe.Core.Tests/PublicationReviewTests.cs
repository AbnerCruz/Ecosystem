using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-179/180: the review names what to fix before exporting, with the note and line.</summary>
public sealed class PublicationReviewTests
{
    [Fact]
    public void Review_FindsMissingAndEmptySourcesHeadingsImagesLinksAndBlocks()
    {
        var store = new DocumentStore();
        var good = store.Upsert(new DocumentInput { Path = "Boa.md", Content = "# Título\n\n## Parte\n\nTexto com [[Boa]] e ![Foto](foto.png)." });
        var bad = store.Upsert(new DocumentInput { Path = "Pasta/Ruim.md", Content =
            "---\ntags: [x]\n---\n# Título\n\n#### Pulou\n\n![](falta.png)\n\n![Web](https://site/x.png)\n\nVeja [[Inexistente]].\n\n" +
            "```quiz\n? Só uma alternativa\n- [x] a\n```\n\n```calculo\n1/0 +\n```\n\n```dados\nfonte: nada.csv\n```\n\n```python\nsem fim" });
        var empty = store.Upsert(new DocumentInput { Path = "Vazia.md", Content = "%%só comentário%%\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Livro", good.Id).AddSource(bad.Id).AddSource(empty.Id).AddSource("doc_sumiu");
        var issues = PublicationReview.Run(project, catalog,
            fileExists: path => path is "foto.png",
            noteExists: name => name is "Boa",
            readData: _ => null);
        string Of(string text) => Assert.Single(issues, i => i.Message.Contains(text, StringComparison.Ordinal)).Place;
        Assert.DoesNotContain(issues, i => i.NotePath == "Boa.md");
        Assert.Equal("Pasta/Ruim.md, linha 6", Of("pula do nível 1 para o 4"));
        Assert.Contains(issues, i => i.Message.StartsWith("Imagem sem descrição", StringComparison.Ordinal) && i.Line == 8);
        Assert.Equal("Pasta/Ruim.md, linha 8", Of("Imagem não encontrada no vault: falta.png"));
        Assert.Equal("Pasta/Ruim.md, linha 10", Of("Imagem da web"));
        Assert.Equal("Pasta/Ruim.md, linha 12", Of("[[Inexistente]]"));
        Assert.Contains(issues, i => i.Message.StartsWith("Questionário:", StringComparison.Ordinal) && i.Line == 14);
        Assert.Contains(issues, i => i.Message.StartsWith("Cálculo com erro", StringComparison.Ordinal) && i.Line == 19);
        Assert.Contains(issues, i => i.Message.StartsWith("Dados indisponíveis", StringComparison.Ordinal) && i.Line == 23);
        Assert.Contains(issues, i => i.Message.StartsWith("Bloco de código aberto", StringComparison.Ordinal) && i.Line == 27);
        Assert.Equal("Vazia.md", Of("A nota está vazia"));
        Assert.Contains(issues, i => i.Message.StartsWith("A nota desta seção não existe", StringComparison.Ordinal) && i.Section == 3);
        // errors first
        Assert.True(issues.TakeWhile(i => i.Severity == ReviewSeverity.Error).Count() == issues.Count(i => i.Severity == ReviewSeverity.Error));
        Assert.All(issues, i => Assert.False(string.IsNullOrWhiteSpace(i.Fix)));
    }

    [Fact]
    public void Review_SlidesWithTooMuchTextAndFastSubtitles()
    {
        var store = new DocumentStore();
        string many = string.Join(' ', Enumerable.Repeat("palavra", PublicationReview.SlideWords + 10));
        var deck = store.Upsert(new DocumentInput { Path = "Aula.md", Content = "# Um\n\ncurto\n\n## Dois\n\n" + many + "\n\n---\n\ncurto" });
        using var catalog = new PublicationSourceCatalog(store);
        var slides = PublicationProject.Create("Aula", deck.Id) with { Design = new PublicationDesign { Layout = "slides" } };
        var issue = Assert.Single(PublicationReview.Run(slides, catalog));
        Assert.Equal(5, issue.Line);
        Assert.Contains((PublicationReview.SlideWords + 10) + " palavras", issue.Message);
        // the same note as an article has nothing to fix
        Assert.Empty(PublicationReview.Run(PublicationProject.Create("Aula", deck.Id), catalog));

        var video = VideoComposition.Synced(PublicationProject.Create("Vídeo", deck.Id) with { Design = new PublicationDesign { Layout = "video" } });
        video = video with { Video = video.Video!.WithScene(video.Video.Scenes[0] with { Duration = 5 }) };
        var fast = Assert.Single(PublicationReview.Run(video, catalog));
        Assert.Contains("legenda passa rápido demais", fast.Message);
        Assert.Contains("pelo menos", fast.Fix);
    }

    [Fact]
    public void Review_AnEmptyPublicationSaysSo() =>
        Assert.Equal("A publicação não tem nenhuma nota.", Assert.Single(PublicationReview.Run(
            PublicationProject.Create("Nada", "x") with { Sections = [], Sources = [] }, new PublicationSourceCatalog(new DocumentStore()))).Message);
}
