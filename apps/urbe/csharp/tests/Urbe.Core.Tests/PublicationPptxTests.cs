using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-168: an editable deck — native text, lists, tables and pictures; a slide is never a picture of HTML.</summary>
public sealed class PublicationPptxTests
{
    private static string Entry(ZipArchive zip, string path)
    { using var r = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }

    [Fact]
    public async Task Pptx_IsAValidPackageOfNativeEditableSlides()
    {
        var (_, catalog, project) = PublicationOfficeTests.Sample("slides");
        using var _c = catalog;
        var result = await PublicationPptx.ExportAsync(project, catalog, PublicationOfficeTests.Asset, TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(new MemoryStream(result.Bytes), ZipArchiveMode.Read);
        foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))) XDocument.Parse(Entry(zip, e.FullName));
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main", a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var presentation = XDocument.Parse(Entry(zip, "ppt/presentation.xml"));
        var count = presentation.Descendants(p + "sldId").Count();
        var types = Entry(zip, "[Content_Types].xml");
        var slides = Enumerable.Range(1, count).Select(i => XDocument.Parse(Entry(zip, $"ppt/slides/slide{i}.xml"))).ToList();
        for (int i = 1; i <= count; i++) Assert.Contains($"/ppt/slides/slide{i}.xml", types);
        var all = string.Concat(slides.SelectMany(s => s.Descendants(a + "t")).Select(t => t.Value));
        Assert.Contains("Capítulo um", all);
        Assert.Contains("Capítulo dois", all);
        Assert.Contains("faltam 3", all);
        Assert.DoesNotContain("PRIVADO", all);
        // native objects: bullets and numbering, a table, a picture with its media
        Assert.Contains(slides, s => s.Descendants(a + "buChar").Any());
        Assert.Contains(slides, s => s.Descendants(a + "buAutoNum").Any());
        Assert.Single(slides.SelectMany(s => s.Descendants(a + "tbl")));
        Assert.Single(slides.SelectMany(s => s.Descendants(p + "pic")));
        Assert.NotNull(zip.GetEntry("ppt/media/image1.png"));
        Assert.Contains(slides, s => s.Descendants(a + "hlinkClick").Any());
        // every slide has its title as a real title placeholder (outline, accessibility)
        Assert.All(slides.Where(s => s.Descendants(a + "t").Any(t => t.Value.StartsWith("Capítulo"))), s => Assert.Contains(s.Descendants(p + "ph"), ph => (string?)ph.Attribute("type") == "title"));
        Assert.Contains(result.Limitations, l => l.Contains("TeX"));
    }

    [Fact]
    public void Slides_SplitByNoteHeadingAndRule_AndContinueTextThatDoesNotFit()
    {
        var runs = (string t) => (IReadOnlyList<OfficeRun>)[new OfficeRun(t)];
        var many = Enumerable.Range(1, 40).Select(i => (OfficeBlock)new OfficeListItem(1, 0, false, null, runs("ponto " + i)));
        var doc = new OfficeDocument("Deck", "slides", [
            new OfficeChapter("Nota A", "a", [new OfficeParagraph(runs("abertura")), new OfficeHeading(2, runs("Tema")), new OfficeParagraph(runs("x")), new OfficeRule(), new OfficeParagraph(runs("sem título"))]),
            new OfficeChapter("Nota B", "b", [new OfficeHeading(1, runs("Lista longa")), .. many]),
        ], []);
        var limits = new List<string>();
        var slides = PublicationPptx.Slides(doc, limits);
        Assert.Equal("Nota A", slides[0].Title);
        Assert.Equal("Tema", slides[1].Title);
        Assert.Equal("", slides[2].Title);
        Assert.Equal("Lista longa", slides[3].Title);
        Assert.True(slides.Count > 4);
        Assert.All(slides.Skip(4), s => { Assert.True(s.Continued); Assert.Equal("Lista longa", s.Title); });
        // nothing is lost in the split
        Assert.Equal(40, slides.Skip(3).Sum(s => s.Text.Count));
        Assert.Contains(limits, l => l.Contains("(cont.)"));
    }
}

/// <summary>CST-026/167/168: containers kept in every output; :::notas becomes speaker notes in slides.</summary>
public sealed class PublicationContainersTests
{
    private const string Note = "# Tema\n\nTexto\n\n:::aviso Cuidado\nNão **esqueça**\n:::\n\n:::notas\nFalar do exemplo.\n:::\n";

    private static (PublicationSourceCatalog Catalog, PublicationProject Project) One(string layout)
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Aula.md", Content = Note });
        return (new PublicationSourceCatalog(store), PublicationProject.Create("Aula", note.Id) with { Design = new PublicationDesign { Layout = layout } });
    }

    private static string Entry(ZipArchive zip, string path)
    { using var r = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }

    [Fact]
    public async Task Containers_AreBoxesInHtmlEpubAndOffice_NeverRawColons()
    {
        var (catalog, project) = One("article");
        using var _c = catalog;
        var html = PublicationHtml.Export(project, catalog).Html;
        Assert.Contains("<aside class=\"ubox\" data-type=\"aviso\">", html);
        Assert.DoesNotContain(":::", html);
        var epub = await PublicationEpub.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using (var zip = new ZipArchive(new MemoryStream(epub))) { var c = Entry(zip, "EPUB/chapter-1.xhtml"); Assert.Contains("class=\"ubox\"", c); Assert.DoesNotContain(":::", c); }
        var docx = await PublicationDocx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using (var zip = new ZipArchive(new MemoryStream(docx.Bytes))) { var d = Entry(zip, "word/document.xml"); Assert.Contains("w:val=\"BoxTitle\"", d); Assert.Contains("Cuidado", d); Assert.DoesNotContain(":::", d); }
        var odt = await PublicationOdt.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using (var zip = new ZipArchive(new MemoryStream(odt.Bytes))) Assert.Contains("text:style-name=\"Box\"", Entry(zip, "content.xml"));
    }

    [Fact]
    public async Task Pptx_SpeakerNotesGoToTheNotesPane_NotTheSlide()
    {
        var (catalog, project) = One("slides");
        using var _c = catalog;
        var deck = await PublicationPptx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(new MemoryStream(deck.Bytes));
        foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))) System.Xml.Linq.XDocument.Parse(Entry(zip, e.FullName));
        var slide = Entry(zip, "ppt/slides/slide1.xml");
        Assert.Contains("Cuidado", slide);
        Assert.DoesNotContain("Falar do exemplo", slide);
        Assert.Contains("Falar do exemplo", Entry(zip, "ppt/notesSlides/notesSlide1.xml"));
        Assert.Contains("notesSlide1.xml", Entry(zip, "ppt/slides/_rels/slide1.xml.rels"));
        Assert.Contains("notesMasterIdLst", Entry(zip, "ppt/presentation.xml"));
    }
}
