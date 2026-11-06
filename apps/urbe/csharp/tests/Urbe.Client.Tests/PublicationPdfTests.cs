using UglyToad.PdfPig;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-164: a real paginated PDF file (not the print dialog), read back with PdfPig.</summary>
public sealed class PublicationPdfTests
{
    [Fact]
    public async Task Book_HasCoverContentsChaptersOnNewPagesNumbersAndLinks()
    {
        var store = new DocumentStore();
        var longText = string.Join(" ", Enumerable.Repeat("Texto de estudo que ocupa várias linhas para testar a paginação do PDF editorial.", 60));
        var a = store.Upsert(new DocumentInput { Path = "Livro/Um.md", Content = "# Capítulo um\n\nVeja [o site](https://exemplo.org) e $x^2$.\n\n%%PRIVADO%%\n\n- [x] feita\n- [ ] por fazer\n\n| Nome | Valor |\n|---|---:|\n| A | 1 |\n\n$$\\frac{1}{3}$$\n\n" + longText + "\n" });
        var b = store.Upsert(new DocumentInput { Path = "Livro/Dois.md", Content = "# Capítulo dois\n\nFim.\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Livro de teste", a.Id).AddSource(b.Id) with { Design = new PublicationDesign { Layout = "book" } };
        var result = await PublicationPdf.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        if (Environment.GetEnvironmentVariable("URBE_SNAPSHOT_DIR") is { Length: > 0 } dir) { Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, "publication-book.pdf"), result.Bytes); }
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(result.Bytes, 0, 5));
        using var pdf = PdfDocument.Open(result.Bytes);
        Assert.True(pdf.NumberOfPages >= 5, "pages: " + pdf.NumberOfPages); // cover, contents, chapter one over pages, chapter two
        Assert.All(pdf.GetPages(), p => { Assert.Equal(595, p.Width, 0); Assert.Equal(842, p.Height, 0); });
        string Text(int n) => string.Join(" ", pdf.GetPage(n).GetWords().Select(w => w.Text));
        Assert.Contains("Livro de teste", Text(1));
        Assert.Contains("Sumário", Text(2));
        Assert.Contains("Capítulo dois", Text(2));
        Assert.Contains("Capítulo um", Text(3));
        var last = pdf.NumberOfPages;
        Assert.Contains("Capítulo dois", Text(last));
        Assert.DoesNotContain("Texto de estudo", Text(last)); // chapter two starts on its own page
        Assert.Contains(last.ToString(), Text(last));         // numbered footer
        var all = string.Join(" ", Enumerable.Range(1, last).Select(Text));
        Assert.DoesNotContain("PRIVADO", all);
        // the contents give chapter two's real page
        Assert.Contains("Capítulo dois", Text(2));
        Assert.Contains(last.ToString(), Text(2));
        // the link is clickable
        Assert.Contains(pdf.GetPage(3).GetHyperlinks(), l => l.Uri == "https://exemplo.org/");
    }

    [Fact]
    public async Task Article_StartsWithItsTitle_AndLongTablesRepeatTheirHeader()
    {
        var store = new DocumentStore();
        var rows = string.Concat(Enumerable.Range(1, 90).Select(i => "| item " + i + " | " + i + " |\n"));
        var a = store.Upsert(new DocumentInput { Path = "T.md", Content = "# Tabela\n\n| Produto | Qtd |\n|---|---:|\n" + rows });
        using var catalog = new PublicationSourceCatalog(store);
        var result = await PublicationPdf.ExportAsync(PublicationProject.Create("Inventário", a.Id), catalog, cancellationToken: TestContext.Current.CancellationToken);
        using var pdf = PdfDocument.Open(result.Bytes);
        Assert.True(pdf.NumberOfPages >= 2);
        Assert.StartsWith("Inventário", string.Join(" ", pdf.GetPage(1).GetWords().Select(w => w.Text)));
        var second = string.Join(" ", pdf.GetPage(2).GetWords().Select(w => w.Text));
        Assert.Contains("Produto", second); // header repeated
        Assert.Contains("item 90", string.Join(" ", Enumerable.Range(1, pdf.NumberOfPages).SelectMany(n => pdf.GetPage(n).GetWords().Select(w => w.Text))).Replace("item 90", "item 90"));
    }
}
