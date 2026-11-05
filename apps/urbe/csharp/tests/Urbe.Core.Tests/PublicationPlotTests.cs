using System.IO.Compression;
using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-069/169: a ```grafico block is a drawing in every publication output, vector where the format holds it.</summary>
public sealed class PublicationPlotTests
{
    private const string Note = "# Funções\n\n```grafico\ntítulo: Seno\ny = sin(x)\nx: -6..6\n```\n";

    private static (PublicationSourceCatalog, PublicationProject) One(string layout = "article")
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "F.md", Content = Note });
        return (new PublicationSourceCatalog(store), PublicationProject.Create("Funções", note.Id) with { Design = new PublicationDesign { Layout = layout } });
    }

    private static string Entry(ZipArchive zip, string path)
    { using var r = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }

    [Fact]
    public async Task Graph_IsSvgInHtmlSlidesAndEpub_NotCode()
    {
        var (catalog, project) = One();
        using var _c = catalog;
        var html = PublicationHtml.Export(project, catalog).Html;
        Assert.Contains("<figure class=\"urbe-plot\"><svg", html);
        Assert.DoesNotContain("data-lang=\"grafico\"", html);
        Assert.Contains("<figure class=\"urbe-plot\"><svg", PublicationHtml.Export(project with { Design = new PublicationDesign { Layout = "slides" } }, catalog).Html);
        using var epub = new ZipArchive(new MemoryStream(await PublicationEpub.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Contains(epub.Entries, e => e.FullName.StartsWith("EPUB/assets/plot-") && e.FullName.EndsWith(".svg"));
        Assert.Contains("image/svg+xml", Entry(epub, "EPUB/package.opf"));
    }

    [Fact]
    public async Task Graph_IsVectorInOdt_AndPngWithSvgOriginalInDocxAndPptx()
    {
        var (catalog, project) = One();
        using var _c = catalog;
        var png = Convert.FromBase64String(PublicationOfficeTests.Png1x1);
        int calls = 0;
        OfficeRaster Raster(string svg, int w, int h) { calls++; Assert.Contains("<path", svg); Assert.DoesNotContain("currentColor", svg); return new OfficeRaster(png, w, h); }
        var odt = await PublicationOdt.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken, rasterize: Raster);
        using (var zip = new ZipArchive(new MemoryStream(odt.Bytes))) Assert.Contains("Pictures/image1.svg", Entry(zip, "META-INF/manifest.xml"));
        var docx = await PublicationDocx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken, rasterize: Raster);
        using (var zip = new ZipArchive(new MemoryStream(docx.Bytes)))
        {
            Assert.NotNull(zip.GetEntry("word/media/image1.png"));
            Assert.NotNull(zip.GetEntry("word/media/image1.svg"));
            Assert.Contains("svgBlip", Entry(zip, "word/document.xml"));
        }
        var pptx = await PublicationPptx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken, rasterize: Raster);
        using (var zip = new ZipArchive(new MemoryStream(pptx.Bytes))) Assert.NotNull(zip.GetEntry("ppt/media/image1.svg"));
        Assert.Equal(3, calls);
        // without a platform to draw pixels the graph still goes, as SVG, and the export says so
        var bare = await PublicationDocx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(bare.Limitations, l => l.Contains("SVG"));
    }
}
