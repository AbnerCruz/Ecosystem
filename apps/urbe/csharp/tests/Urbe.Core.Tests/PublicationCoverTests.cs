using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core.Tests;

/// <summary>CST-159: a real note as the publication's cover — first, on a page of its own, outside the contents, in every format.</summary>
public sealed class PublicationCoverTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==";

    private static (DocumentStore Store, PublicationProject Book, UrbeDocument Cover) Book()
    {
        var store = new DocumentStore();
        var one = store.Upsert(new DocumentInput { Path = "Um.md", Content = "# A chuva\n\nCai devagar." });
        var two = store.Upsert(new DocumentInput { Path = "Dois.md", Content = "# O rio\n\nCorre." });
        var cover = store.Upsert(new DocumentInput { Path = "Capa.md", Content = "# Águas\n\nContos de Lia Vale\n\n![Um rio entre montanhas](rio.png)" });
        var book = PublicationProject.Create("Águas", one.Id).AddSource(two.Id).AddSource(cover.Id) with { Design = new PublicationDesign { Layout = "book" } };
        return (store, book.WithCover(book.Sections[2].Id), cover);
    }

    private static string Entry(byte[] zip, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        using var r = new StreamReader(archive.GetEntry(path)!.Open(), Encoding.UTF8);
        return r.ReadToEnd();
    }

    [Fact]
    public void TheCoverIsOnlyOneAndAlwaysFirst()
    {
        var (_, book, cover) = Book();
        Assert.Equal(cover.Id, book.Sections[0].Source.DocumentId);
        Assert.Equal(book.Sections[0], book.Cover);
        // moving a piece before it, or moving the cover itself, keeps it at the start
        Assert.Equal(cover.Id, book.MovePlacement(book.Sections[2].Id, 0).Sections[0].Source.DocumentId);
        Assert.Equal(cover.Id, book.MovePlacement(book.Cover!.Id, 2).Sections[0].Source.DocumentId);
        // another cover replaces it; the old one goes back to an ordinary section
        var other = book.WithCover(book.Sections[2].Id);
        Assert.Single(other.Sections, s => s.IsCover);
        Assert.Equal(book.Sections[2].Id, other.Cover!.Id);
        Assert.Null(other.WithCover(null).Cover);
        // the file keeps it, and a damaged file with two covers is refused
        Assert.Equal(book.Cover!.Id, PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(book)).Cover!.Id);
        var two = book with { Sections = book.Sections.Select(s => s with { Role = PublicationPlacement.CoverRole }).ToList() };
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(two));
    }

    [Fact]
    public void HtmlShowsTheCoverOnItsOwnPageOutsideTheContents()
    {
        var (store, book, _) = Book();
        using var catalog = new PublicationSourceCatalog(store);
        var html = PublicationHtml.Export(book, catalog).Html;
        Assert.Contains("class=\"cover\" aria-label=\"Capa\"><h1", html);
        Assert.DoesNotContain("<header>", html);   // the title is the cover's, not repeated above it
        var nav = html[html.IndexOf("<nav", StringComparison.Ordinal)..html.IndexOf("</nav>", StringComparison.Ordinal)];
        Assert.DoesNotContain("Capa", nav);
        Assert.Contains("Um", nav);
        Assert.True(html.IndexOf("Contos de Lia Vale", StringComparison.Ordinal) < html.IndexOf("Cai devagar", StringComparison.Ordinal));
        Assert.Contains("break-after:page", html);
        // without a cover, the title line comes back
        Assert.Contains("<header><h1>Águas</h1></header>", PublicationHtml.Export(book.WithCover(null), catalog).Html);
    }

    [Fact]
    public async Task EpubHasACoverPageAndTheImageReadersShow()
    {
        var (store, book, _) = Book();
        using var catalog = new PublicationSourceCatalog(store);
        var epub = await PublicationEpub.ExportWithReportAsync(book, catalog, _ => Task.FromResult<byte[]?>(Convert.FromBase64String(Png)), TestContext.Current.CancellationToken);
        XNamespace opf = "http://www.idpf.org/2007/opf";
        var package = XDocument.Parse(Entry(epub.Bytes, "EPUB/package.opf"));
        Assert.Equal("cover", (string?)package.Descendants(opf + "itemref").First().Attribute("idref"));
        var image = Assert.Single(package.Descendants(opf + "item"), i => (string?)i.Attribute("properties") == "cover-image");
        Assert.Equal("image/png", (string?)image.Attribute("media-type"));
        var cover = Entry(epub.Bytes, "EPUB/cover.xhtml");
        Assert.Contains("epub:type=\"cover\"", cover);
        Assert.Contains("role=\"doc-cover\"", cover);
        Assert.Contains("alt=\"Um rio entre montanhas\"", cover);
        var nav = Entry(epub.Bytes, "EPUB/nav.xhtml");
        Assert.Contains("epub:type=\"cover\" href=\"cover.xhtml\"", nav);
        Assert.Contains("epub:type=\"bodymatter\" href=\"chapter-1.xhtml\"", nav);
        var toc = nav[..nav.IndexOf("landmarks", StringComparison.Ordinal)];
        Assert.DoesNotContain("cover.xhtml", toc);
        Assert.Contains("A chuva", Entry(epub.Bytes, "EPUB/chapter-1.xhtml"));
    }

    [Fact]
    public async Task WordAndOdtStartWithTheCoverThenThePageBreakThenTheContents()
    {
        var (store, book, _) = Book();
        using var catalog = new PublicationSourceCatalog(store);
        Task<byte[]?> Asset(string _) => Task.FromResult<byte[]?>(Convert.FromBase64String(Png));
        var docx = Entry((await PublicationDocx.ExportAsync(book, catalog, Asset, TestContext.Current.CancellationToken)).Bytes, "word/document.xml");
        Assert.True(docx.IndexOf("<w:drawing>", StringComparison.Ordinal) < docx.IndexOf("w:type=\"page\"", StringComparison.Ordinal));   // the cover's picture is on the cover
        int coverAt = docx.IndexOf("Contos de Lia Vale", StringComparison.Ordinal), breakAt = docx.IndexOf("w:type=\"page\"", StringComparison.Ordinal),
            tocAt = docx.IndexOf("Sumário", StringComparison.Ordinal), firstAt = docx.IndexOf("Cai devagar", StringComparison.Ordinal);
        Assert.True(coverAt >= 0 && coverAt < breakAt && breakAt < tocAt && tocAt < firstAt, docx);
        Assert.DoesNotContain("w:val=\"Title\"", docx);
        var odt = Entry((await PublicationOdt.ExportAsync(book, catalog, Asset, TestContext.Current.CancellationToken)).Bytes, "content.xml");
        coverAt = odt.IndexOf("Contos de Lia Vale", StringComparison.Ordinal);
        tocAt = odt.IndexOf("table-of-content", StringComparison.Ordinal);
        Assert.True(coverAt >= 0 && coverAt < tocAt && tocAt < odt.IndexOf("Cai devagar", StringComparison.Ordinal));
        Assert.Contains("text:style-name=\"PBody\" />", odt.Replace("text:style-name=\"PBody\"/>", "text:style-name=\"PBody\" />"));
    }
}
