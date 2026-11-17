using System.IO.Compression;
using System.Text;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-159: what the old pages offered, as parts of a note that every product shows — title, text, image, table, code,
/// formula, quote and link, the same note in HTML, EPUB, Word, ODT and slides, with no schema of the old pages.
/// </summary>
public sealed class PublicationComponentsTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==";
    private const string Note = "# Marés\n\nA Lua **puxa** a água.\n\n![Praia na maré baixa](praia.png)\n\n| Fase | Maré |\n|---|---|\n| Cheia | Viva |\n\n```python\naltura = 2 * lua\n```\n\n$$F = G\\frac{m_1 m_2}{r^2}$$\n\n> O mar respira duas vezes por dia.\n\n[Tábua das marés](https://exemplo.org/mares)\n";

    private static string AllText(byte[] zip, string prefix)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var sb = new StringBuilder();
        foreach (var e in archive.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal) || e.FullName.EndsWith(".xhtml", StringComparison.Ordinal) || e.FullName.EndsWith(".rels", StringComparison.Ordinal)))
        {
            using var r = new StreamReader(e.Open(), Encoding.UTF8);
            sb.Append(r.ReadToEnd());
        }
        return sb.ToString();
    }

    private static bool HasPicture(byte[] zip) { using var a = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read); return a.Entries.Any(e => e.FullName.EndsWith(".png", StringComparison.Ordinal)); }

    [Fact]
    public async Task EveryPartOfTheNoteReachesEveryProduct()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Marés.md", Content = Note });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Marés", note.Id);
        Task<byte[]?> Asset(string _) => Task.FromResult<byte[]?>(Convert.FromBase64String(Png));
        var ct = TestContext.Current.CancellationToken;

        var html = PublicationHtml.Export(project, catalog).Html;
        foreach (var part in new[] { "<h1", "<strong>puxa</strong>", "<img", "<table", "<pre", "role=\"math\"", "<blockquote", "href=\"https://exemplo.org/mares\"" })
            Assert.Contains(part, html);

        var epub = (await PublicationEpub.ExportWithReportAsync(project, catalog, Asset, ct)).Bytes;
        var epubText = AllText(epub, "EPUB/");
        foreach (var part in new[] { "<h1", "puxa", "<table", "<pre", "<blockquote", "https://exemplo.org/mares", "Praia na maré baixa" })
            Assert.Contains(part, epubText);
        Assert.True(HasPicture(epub));

        var word = await PublicationDocx.ExportAsync(project, catalog, Asset, ct);
        var docxText = AllText(word.Bytes, "word/");
        // the formula goes as editable TeX, and the export says so (never a silent loss)
        foreach (var part in new[] { "Marés", "puxa", "<w:tbl>", "altura = 2 * lua", "\\frac{m_1 m_2}{r^2}", "O mar respira", "https://exemplo.org/mares", "<w:drawing>" })
            Assert.Contains(part, docxText);
        Assert.Contains(word.Limitations, l => l.Contains("TeX", StringComparison.Ordinal));

        var odt = (await PublicationOdt.ExportAsync(project, catalog, Asset, ct)).Bytes;
        var odtText = AllText(odt, "");
        foreach (var part in new[] { "Marés", "puxa", "<table:table", "altura = 2 * lua", "O mar respira", "https://exemplo.org/mares", "draw:image" })
            Assert.Contains(part, odtText);

        var pptx = (await PublicationPptx.ExportAsync(project with { Design = new PublicationDesign { Layout = "slides" } }, catalog, Asset, ct)).Bytes;
        var pptxText = AllText(pptx, "ppt/");
        foreach (var part in new[] { "Marés", "puxa", "<a:tbl>", "altura = 2 * lua", "O mar respira", "https://exemplo.org/mares" })
            Assert.Contains(part, pptxText);
        Assert.True(HasPicture(pptx));
    }
}
