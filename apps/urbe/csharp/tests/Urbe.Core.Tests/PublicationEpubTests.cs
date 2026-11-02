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
}
