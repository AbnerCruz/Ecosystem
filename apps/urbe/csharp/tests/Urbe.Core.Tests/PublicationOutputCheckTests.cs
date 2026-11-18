using System.IO.Compression;
using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PublicationOutputCheckTests
{
    private static Task<byte[]?> NoAsset(string _) => Task.FromResult<byte[]?>(null);

    [Fact]
    public async Task EveryRealExportPassesItsOwnCheck()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "A.md", Content = "# A\n\nTexto com acento: ação & <b>.\n\n| x | y |\n|---|---|\n| 1 | 2 |\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Teste", note.Id);
        Assert.Null(PublicationOutputCheck.Problem("docx", (await PublicationDocx.ExportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes));
        Assert.Null(PublicationOutputCheck.Problem("odt", (await PublicationOdt.ExportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes));
        Assert.Null(PublicationOutputCheck.Problem("pptx", (await PublicationPptx.ExportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes));
        Assert.Null(PublicationOutputCheck.Problem("epub", (await PublicationEpub.ExportWithReportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes));
        // CST-213: the receipt says what was confirmed and what was not
        var pptx = PublicationOutputCheck.Receipt("pptx", (await PublicationPptx.ExportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes);
        Assert.Equal("parcial", pptx.Status);
        Assert.Contains(pptx.Checked, c => c.EndsWith("slide") || c.EndsWith("slides"));
        Assert.Contains(pptx.Checked, c => c.Contains("ligações internas íntegras"));
        Assert.Contains("aparência no programa de destino", pptx.NotChecked);
        Assert.Contains("ordem de leitura", PublicationOutputCheck.Receipt("epub", (await PublicationEpub.ExportWithReportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes).Summary);
        Assert.Contains("manifesto completo", PublicationOutputCheck.Receipt("odt", (await PublicationOdt.ExportAsync(project, catalog, NoAsset, TestContext.Current.CancellationToken)).Bytes).Summary);
    }

    /// <summary>CST-213: the corruptions a format checker exists for — the ones that make Word or a reader refuse the file.</summary>
    [Fact]
    public void ADanglingRelationshipAPartWithoutTypeAndALostChapterAreCaught()
    {
        const string Types = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"rels\" ContentType=\"r\"/></Types>";
        const string Rels = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Type=\"t\" Target=\"media/image1.png\"/><Relationship Id=\"r2\" Type=\"t\" Target=\"https://x.org\" TargetMode=\"External\"/></Relationships>";
        var dangling = PublicationOutputCheck.Problem("docx", Zip(("[Content_Types].xml", Types), ("word/document.xml", "<d/>"), ("word/_rels/document.xml.rels", Rels)));
        Assert.Contains("word/media/image1.png", dangling);
        var untyped = PublicationOutputCheck.Problem("docx", Zip(("[Content_Types].xml", Types), ("word/document.xml", "<d/>"), ("word/_rels/document.xml.rels", Rels), ("word/media/image1.png", "png")));
        Assert.Contains("tipo de conteúdo", untyped);
        const string Container = "<container xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/book.opf\"/></rootfiles></container>";
        const string Opf = "<package xmlns=\"http://www.idpf.org/2007/opf\"><manifest><item id=\"c1\" href=\"c1.xhtml\"/></manifest><spine><itemref idref=\"c1\"/><itemref idref=\"c2\"/></spine></package>";
        Assert.Contains("“c2”", PublicationOutputCheck.Problem("epub", Zip(("mimetype", "application/epub+zip"), ("META-INF/container.xml", Container), ("OEBPS/book.opf", Opf), ("OEBPS/c1.xhtml", "<html/>"))));
        Assert.Contains("c1.xhtml", PublicationOutputCheck.Problem("epub", Zip(("mimetype", "application/epub+zip"), ("META-INF/container.xml", Container), ("OEBPS/book.opf", Opf))));
        // a PDF whose font is named but not embedded; the standard 14 need no embedding
        var bare = "1 0 obj << /Type /Font /Subtype /TrueType /BaseFont /ABCDEF+Inter >> endobj\n";
        Assert.Contains("Inter", PublicationOutputCheck.FontProblem(bare));
        Assert.Null(PublicationOutputCheck.FontProblem(bare + "3 0 obj << /Type /FontDescriptor /FontName /ABCDEF+Inter /FontFile2 4 0 R >> endobj\n"));
        Assert.Null(PublicationOutputCheck.FontProblem("2 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj"));
    }

    [Fact]
    public void BrokenFilesAreCaught()
    {
        Assert.NotNull(PublicationOutputCheck.Problem("pdf", []));
        Assert.NotNull(PublicationOutputCheck.Problem("pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj")));
        Assert.Contains("não abre", PublicationOutputCheck.Problem("pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n...\n%%EOF\n")));
        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var helvetica = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("Olá", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), helvetica);
        var valid = PublicationOutputCheck.Receipt("pdf", builder.Build());
        Assert.Null(valid.Problem);
        Assert.Contains("1 página, estrutura válida", valid.Checked);
        Assert.NotNull(PublicationOutputCheck.Problem("docx", Encoding.ASCII.GetBytes("not a zip")));
        Assert.Contains("word/document.xml", PublicationOutputCheck.Problem("docx", Zip(("[Content_Types].xml", "<a/>"))));
        Assert.Contains("malformado", PublicationOutputCheck.Problem("docx", Zip(("[Content_Types].xml", "<a/>"), ("word/document.xml", "<a><b></a>"))));
        Assert.Contains("primeiro", PublicationOutputCheck.Problem("epub", Zip(("META-INF/container.xml", "<a/>"), ("mimetype", "application/epub+zip"))));
    }

    private static byte[] Zip(params (string Name, string Text)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, text) in entries)
                using (var w = new StreamWriter(zip.CreateEntry(name).Open())) w.Write(text);
        return stream.ToArray();
    }
}
