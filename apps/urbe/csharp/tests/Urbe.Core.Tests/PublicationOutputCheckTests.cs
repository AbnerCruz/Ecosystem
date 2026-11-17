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
    }

    [Fact]
    public void BrokenFilesAreCaught()
    {
        Assert.NotNull(PublicationOutputCheck.Problem("pdf", []));
        Assert.NotNull(PublicationOutputCheck.Problem("pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj")));
        Assert.Null(PublicationOutputCheck.Problem("pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n...\n%%EOF\n")));
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
