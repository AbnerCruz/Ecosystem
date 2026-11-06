using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PublicationEpubTests
{
    [Fact]
    public async Task EpubHasRealPackageNavigationEditableTextAndLiveSources()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Capítulo.md", Content = "# Olá\n\n**Texto** & ação\n\n%%PRIVADO%%\n\n- [x] Feito\n\n```progress\nvalue: 1\ntotal: 4\n```" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Livro português", note.Id);
        var bytes = await PublicationEpub.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal("mimetype", zip.Entries[0].FullName);
        Assert.Equal(20, zip.Entries[0].Length);
        Assert.Equal(zip.Entries[0].Length, zip.Entries[0].CompressedLength);
        string Read(string path) { using var r = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }
        Assert.Equal("application/epub+zip", Read("mimetype"));
        XDocument.Parse(Read("META-INF/container.xml"));
        var package = XDocument.Parse(Read("EPUB/package.opf"));
        XNamespace opf = "http://www.idpf.org/2007/opf";
        Assert.Single(package.Descendants(opf + "itemref"));
        Assert.Contains("properties=\"nav\"", Read("EPUB/package.opf"));
        var chapter = Read("EPUB/chapter-1.xhtml");
        XDocument.Parse(chapter);
        Assert.Contains("<strong>Texto</strong>", chapter);
        Assert.Contains("faltam 3", chapter);
        Assert.DoesNotContain("PRIVADO", chapter);
        Assert.Contains("chapter-1.xhtml", Read("EPUB/nav.xhtml"));
        Assert.DoesNotContain("Texto", PublicationProjectCodec.Serialize(project));
    }

    [Fact]
    public async Task EpubFailsForMissingSourcesAssetsAndConcurrentChanges()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "A.md", Content = "![Imagem](foto.png)" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Imagem", note.Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => PublicationEpub.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PublicationEpub.ExportAsync(project, catalog, path =>
        {
            store.Upsert(new DocumentInput { Id = note.Id, Path = "A.md", Content = "texto alterado" });
            return Task.FromResult<byte[]?>(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg=="));
        }, TestContext.Current.CancellationToken));
        store.Remove(note.Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => PublicationEpub.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>CST-165/183: accessibility said in the package, landmarks, chapters named by their heading; a picture without a description is reported and never claimed.</summary>
    [Fact]
    public async Task Epub_DeclaresItsAccessibilityHonestlyAndReportsUndescribedImages()
    {
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==";
        var store = new DocumentStore();
        var described = store.Upsert(new DocumentInput { Path = "Um.md", Content = "# A chuva\n\n![Nuvem carregada](nuvem.png)\n\n$$x^2$$" });
        var bare = store.Upsert(new DocumentInput { Path = "Dois.md", Content = "Sem título.\n\n![](rio.png)" });
        using var catalog = new PublicationSourceCatalog(store);
        Task<byte[]?> Asset(string _) => Task.FromResult<byte[]?>(Convert.FromBase64String(png));
        XNamespace opf = "http://www.idpf.org/2007/opf";
        static string Read(ZipArchive zip, string path) { using var r = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }
        static IReadOnlyList<string> Meta(XDocument package, string property) =>
            package.Descendants().Where(e => e.Name.LocalName == "meta" && (string?)e.Attribute("property") == property).Select(e => e.Value).ToList();

        var good = await PublicationEpub.ExportWithReportAsync(PublicationProject.Create("Livro", described.Id), catalog, Asset, TestContext.Current.CancellationToken);
        Assert.Empty(good.Warnings);
        using (var zip = new ZipArchive(new MemoryStream(good.Bytes), ZipArchiveMode.Read))
        {
            var package = XDocument.Parse(Read(zip, "EPUB/package.opf"));
            Assert.Equal(["textual", "visual"], Meta(package, "schema:accessMode"));
            Assert.Equal(["textual"], Meta(package, "schema:accessModeSufficient"));
            Assert.Contains("alternativeText", Meta(package, "schema:accessibilityFeature"));
            Assert.Contains("tableOfContents", Meta(package, "schema:accessibilityFeature"));
            Assert.Equal(["none"], Meta(package, "schema:accessibilityHazard"));
            Assert.Contains("todas as imagens", Assert.Single(Meta(package, "schema:accessibilitySummary")));
            var nav = Read(zip, "EPUB/nav.xhtml");
            Assert.Contains("epub:type=\"landmarks\"", nav);
            Assert.Contains("epub:type=\"bodymatter\"", nav);
            Assert.Contains(">A chuva</a>", nav);
            var chapter = XDocument.Parse(Read(zip, "EPUB/chapter-1.xhtml"));
            var section = chapter.Descendants().Single(e => e.Name.LocalName == "section");
            Assert.Equal("doc-chapter", (string?)section.Attribute("role"));
            Assert.Equal("A chuva", chapter.Descendants().Single(e => (string?)e.Attribute("id") == (string?)section.Attribute("aria-labelledby")).Value);
        }

        var poor = await PublicationEpub.ExportWithReportAsync(PublicationProject.Create("Livro", described.Id).AddSource(bare.Id), catalog, Asset, TestContext.Current.CancellationToken);
        var warning = Assert.Single(poor.Warnings);
        Assert.Contains("rio.png", warning);
        using (var zip = new ZipArchive(new MemoryStream(poor.Bytes), ZipArchiveMode.Read))
        {
            var package = XDocument.Parse(Read(zip, "EPUB/package.opf"));
            Assert.DoesNotContain("alternativeText", Meta(package, "schema:accessibilityFeature"));
            Assert.Equal(["textual,visual"], Meta(package, "schema:accessModeSufficient"));
            Assert.Contains("1 de 3 imagens", Assert.Single(Meta(package, "schema:accessibilitySummary")));
            Assert.Contains(">Dois</a>", Read(zip, "EPUB/nav.xhtml"));
        }
    }
}
