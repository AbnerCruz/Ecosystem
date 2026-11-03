using System.IO.Compression;
using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class EpubReadingTests
{
    public static byte[] Sample(bool drm = false, bool escape = false)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Put(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8); writer.Write(text); }
            Put("mimetype", "application/epub+zip");
            Put("META-INF/container.xml", "<container xmlns='urn:oasis:names:tc:opendocument:xmlns:container' version='1.0'><rootfiles><rootfile full-path='EPUB/book.opf' media-type='application/oebps-package+xml'/></rootfiles></container>");
            Put("EPUB/book.opf", "<package xmlns='http://www.idpf.org/2007/opf' version='3.0' unique-identifier='id'><metadata xmlns:dc='http://purl.org/dc/elements/1.1/'><dc:identifier id='id'>test-book</dc:identifier><dc:title>Livro de estudo</dc:title><dc:language>pt-BR</dc:language><meta property='dcterms:modified'>2026-01-01T00:00:00Z</meta></metadata><manifest><item id='nav' href='nav.xhtml' media-type='application/xhtml+xml' properties='nav'/><item id='one' href='one.xhtml' media-type='application/xhtml+xml'/><item id='two' href='two.xhtml' media-type='application/xhtml+xml'/><item id='style' href='style.css' media-type='text/css'/><item id='image' href='image.png' media-type='image/png'/></manifest><spine><itemref idref='one'/><itemref idref='two'/></spine></package>");
            Put("EPUB/nav.xhtml", "<html xmlns='http://www.w3.org/1999/xhtml' xmlns:epub='http://www.idpf.org/2007/ops'><head><title>Sumário</title></head><body><nav epub:type='toc'><ol><li><a href='one.xhtml'>Primeiro</a></li><li><a href='two.xhtml'>Segundo</a></li></ol></nav></body></html>");
            Put("EPUB/one.xhtml", "<html xmlns='http://www.w3.org/1999/xhtml'><head><title>Primeiro</title><link rel='stylesheet' href='style.css'/></head><body><h1>Primeiro capítulo</h1><p>Trecho de estudo exato.</p><img src='image.png'/><img src='https://unsafe.invalid/track'/><a href='two.xhtml'>Próximo</a><script>alert('unsafe')</script></body></html>");
            Put("EPUB/two.xhtml", "<html xmlns='http://www.w3.org/1999/xhtml'><head><title>Segundo</title></head><body><h1>Segundo capítulo</h1><p>Outro trecho.</p></body></html>");
            Put("EPUB/style.css", "@import 'https://unsafe.invalid/css';p{color:#123456}body{background:url('image.png')}");
            Put("EPUB/image.png", "PNG fixture");
            if (drm) Put("META-INF/encryption.xml", "<encryption xmlns='urn:oasis:names:tc:opendocument:xmlns:container'><EncryptionMethod Algorithm='http://www.w3.org/2001/04/xmlenc#aes128-cbc'/></encryption>");
            if (escape) Put("../escape.txt", "never extracted");
        }
        return stream.ToArray();
    }
    [Fact]
    public void ReadsSpineLazilyWithOfflineImagesAndNoExecutableOrRemoteContent()
    {
        using var book = new EpubReadingDocument(Sample());
        Assert.Equal(2, book.Chapters.Count);
        Assert.Equal("Livro de estudo", book.Title);
        var first = book.ReadChapter(0);
        Assert.Contains("Trecho de estudo exato.", first.Text);
        Assert.Contains("data:image/png;base64,", first.Html);
        Assert.Contains("color:#123456", first.Html);
        Assert.Contains("https://urbe.reader.invalid/chapter/1", first.Html);
        Assert.DoesNotContain("<script", first.Html);
        Assert.DoesNotContain("unsafe.invalid", first.Html);
        Assert.Contains("Outro trecho.", book.ReadChapter(1).Text);
        Assert.Equal(64, book.Sha256.Length);
    }
    [Fact]
    public void SearchOffsetsUseTheSameBodyTextAsVisualSelections()
    {
        using var book = new EpubReadingDocument(Sample());
        var chapter = book.ReadChapter(0);
        var body = System.Xml.Linq.XDocument.Parse(chapter.Html).Descendants().Single(e => e.Name.LocalName == "body");
        var visible = string.Concat(body.DescendantNodes().OfType<System.Xml.Linq.XText>().Select(n => n.Value));
        Assert.Equal(visible, book.ReadText(0));
        Assert.Equal(visible, chapter.Text);
        Assert.DoesNotContain("unsafe", chapter.Text);
    }

    [Fact]
    public void RejectsArchiveEscapesAndProtectedBooks()
    {
        Assert.Throws<InvalidDataException>(() => new EpubReadingDocument(Sample(escape: true)));
        Assert.Throws<NotSupportedException>(() => new EpubReadingDocument(Sample(drm: true)));
        Assert.Null(ReaderArchive.Resolve("EPUB/one.xhtml", "../../escape"));
        Assert.Null(ReaderArchive.Resolve("EPUB/one.xhtml", "%2fetc/passwd"));
        Assert.Null(ReaderArchive.Resolve("EPUB/one.xhtml", "file:///secret"));
    }
}
