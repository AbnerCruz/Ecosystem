using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-166: Word and OpenDocument files built from the publication's live notes, by semantics.</summary>
public sealed class PublicationOfficeTests
{
    internal const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==";

    internal const string Rich = "---\ntags: [estudo]\n---\n# Capítulo um\n\nTexto com **negrito**, *itálico*, ~~riscado~~, `código`, [site](https://exemplo.org) e [[Outra nota]].\n\n%%PRIVADO%%\n\n" +
        "- item a\n  - sub b\n- item c\n\n1. um\n2. dois\n\n- [ ] tarefa\n- [x] feita\n\n> Uma citação\n\n```python\nprint(1)\n    recuo\n```\n\n$$x^2$$\n\n" +
        "| A | B |\n|---|---|\n| 1 | 2 |\n\n![Foto](img/foto.png)\n\n---\n\n```progress\nvalue: 1\ntotal: 4\n```\n";

    internal static (DocumentStore Store, PublicationSourceCatalog Catalog, PublicationProject Project) Sample(string layout = "article")
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "Livro/Um.md", Content = Rich });
        var b = store.Upsert(new DocumentInput { Path = "Livro/Dois.md", Content = "# Capítulo dois\n\n1. outra\n2. lista\n" });
        var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Meu livro", a.Id).AddSource(b.Id) with { Design = new PublicationDesign { Layout = layout } };
        return (store, catalog, project);
    }

    internal static Task<byte[]?> Asset(string path) =>
        Task.FromResult<byte[]?>(path == "Livro/img/foto.png" ? Convert.FromBase64String(Png1x1) : null);

    private static string Entry(ZipArchive zip, string path)
    { using var r = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }

    [Fact]
    public async Task Docx_HasRealStylesListsTablesImagesAndLinks()
    {
        var (_, catalog, project) = Sample("book");
        using var _c = catalog;
        var result = await PublicationDocx.ExportAsync(project, catalog, Asset, TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(new MemoryStream(result.Bytes), ZipArchiveMode.Read);
        foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))) XDocument.Parse(Entry(zip, e.FullName));
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var doc = XDocument.Parse(Entry(zip, "word/document.xml"));
        string[] Styles() => doc.Descendants(w + "pStyle").Select(s => (string)s.Attribute(w + "val")!).ToArray();
        Assert.Contains("Title", Styles());
        Assert.Contains("Heading1", Styles());
        Assert.Contains("Quote", Styles());
        Assert.Contains("Code", Styles());
        Assert.Contains("Formula", Styles());
        Assert.Equal(9, Styles().Count(s => s == "ListParagraph"));
        Assert.Contains(doc.Descendants(w + "ilvl"), l => (string)l.Attribute(w + "val")! == "1");
        Assert.Single(doc.Descendants(w + "tbl"));
        Assert.Single(doc.Descendants(w + "drawing"));
        Assert.Single(doc.Descendants(w + "hyperlink"));
        Assert.Contains(doc.Descendants(w + "b"), _ => true);
        Assert.NotNull(zip.GetEntry("word/media/image1.png"));
        var text = string.Concat(doc.Descendants(w + "t").Select(t => t.Value));
        Assert.Contains("Capítulo dois", text);
        Assert.Contains("faltam 3", text);
        Assert.Contains("☑ feita", text);
        Assert.DoesNotContain("☐ ☐", text);
        Assert.Contains("Meu livro", text);
        Assert.Contains("    recuo", text);
        Assert.DoesNotContain("PRIVADO", text);
        Assert.DoesNotContain("tags:", text);
        Assert.Contains(doc.Descendants(w + "br"), b => (string?)b.Attribute(w + "type") == "page");
        Assert.Contains(doc.Descendants(w + "instrText"), i => i.Value.Contains("TOC"));
        // two ordered lists in two notes: each counts from 1
        var numbering = XDocument.Parse(Entry(zip, "word/numbering.xml"));
        Assert.True(numbering.Descendants(w + "startOverride").Count() >= 2);
        Assert.Contains(result.Limitations, l => l.Contains("TeX"));
        Assert.Contains(result.Limitations, l => l.Contains("[[entre notas]]"));
    }

    [Fact]
    public async Task Odt_HasOutlineNestedListsTablesAndPictures()
    {
        var (_, catalog, project) = Sample("book");
        using var _c = catalog;
        var result = await PublicationOdt.ExportAsync(project, catalog, Asset, TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(new MemoryStream(result.Bytes), ZipArchiveMode.Read);
        Assert.Equal("mimetype", zip.Entries[0].FullName);
        Assert.Equal(zip.Entries[0].Length, zip.Entries[0].CompressedLength);
        Assert.Equal("application/vnd.oasis.opendocument.text", Entry(zip, "mimetype"));
        var content = XDocument.Parse(Entry(zip, "content.xml"));
        XDocument.Parse(Entry(zip, "styles.xml"));
        var manifest = Entry(zip, "META-INF/manifest.xml");
        Assert.Contains("Pictures/image1.png", manifest);
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0", table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        Assert.Equal(2, content.Descendants(text + "h").Count(h => (string?)h.Attribute(text + "outline-level") == "1"));
        Assert.Contains(content.Descendants(text + "list"), l => l.Parent?.Name == text + "list-item");
        Assert.Single(content.Descendants(table + "table"));
        Assert.Contains(content.Descendants(text + "p"), p => (string?)p.Attribute(text + "style-name") == "Quotations");
        Assert.Contains(content.Descendants(text + "s"), _ => true);
        var all = content.Root!.Value;
        Assert.Contains("faltam 3", all);
        Assert.DoesNotContain("PRIVADO", all);
        Assert.Contains(result.Limitations, l => l.Contains("TeX"));
    }

    [Fact]
    public async Task Office_FailsForMissingImagesAndChangesDuringExport()
    {
        var (store, catalog, project) = Sample();
        using var _c = catalog;
        await Assert.ThrowsAsync<InvalidDataException>(() => PublicationDocx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken));
        var first = project.Sources[0].DocumentId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PublicationOdt.ExportAsync(project, catalog, path =>
        {
            store.Upsert(new DocumentInput { Id = first, Path = "Livro/Um.md", Content = Rich + "\nmudou" });
            return Asset(path);
        }, TestContext.Current.CancellationToken));
    }
}
