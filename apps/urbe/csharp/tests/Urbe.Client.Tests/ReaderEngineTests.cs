using Avalonia;
using SkiaSharp;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed class ReaderEngineTests
{
    private static string Corpus(string file) => Path.Combine(AppContext.BaseDirectory, "fixtures", file);
    [Fact]
    public void OcrCropKeepsExactRegionAndOfflineEngineCanBeExportedForBrowserVerification()
    {
        byte[] image = File.ReadAllBytes(Corpus("scan.png"));
        var png = OcrStudyView.Crop(image, new Rect(0, 0, 1, .5));
        using var cropped = SKBitmap.Decode(png);
        Assert.Equal(1400, cropped.Width); Assert.Equal(350, cropped.Height);
        string html = OcrReaderHtml.Build(png);
        Assert.Contains("worker-src blob:", html);
        Assert.Contains("OCR network denied", html);
        Assert.DoesNotContain("cdn.jsdelivr.net/npm/tesseract.js@", html[..html.IndexOf("<script", StringComparison.Ordinal)]);
        if (Environment.GetEnvironmentVariable("URBE_READER_ENGINE_TEST_DIR") is { } output)
        { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "ocr.html"), html); }
    }
    [Fact]
    public void PresentationUsesPackagedRendererAndExportsActualFixture()
    {
        byte[] bytes = File.ReadAllBytes(Corpus("slides.pptx"));
        using var doc = new OfficeReadingDocument("slides.pptx", bytes);
        Assert.Equal(2, doc.Chapters.Count);
        Assert.Contains("Forma e texto preservados", doc.ReadText(0));
        Assert.Contains("Segundo slide: trecho exato.", doc.ReadText(1));
        string html = PresentationReaderHtml.Build(bytes, 0);
        Assert.Contains("UrbePresentation.parse", html);
        Assert.Contains("connect-src blob:", html);
        if (Environment.GetEnvironmentVariable("URBE_READER_ENGINE_TEST_DIR") is { } output)
        { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "slides.html"), html); }
    }
    [Theory]
    [InlineData("reflow.epub", false)]
    [InlineData("epub2.epub", false)]
    [InlineData("fixed.epub", true)]
    public void BooksPreserveNavigationResourcesAndFixedLayout(string path, bool fixedLayout)
    {
        using var doc = new EpubReadingDocument(File.ReadAllBytes(Corpus(path)));
        Assert.Equal(2, doc.Chapters.Count); Assert.Equal("Primeira seção", doc.Chapters[0].Title);
        Assert.Equal(fixedLayout, doc.FixedLayout);
        var chapter = doc.ReadChapter(0); Assert.Contains("Parágrafo 20.", chapter.Text); Assert.Contains("data:image/png",chapter.Html);
        Assert.Contains("MathML",chapter.Html); Assert.Contains("https://urbe.reader.invalid/chapter/1#note",chapter.Html);
        if (Environment.GetEnvironmentVariable("URBE_READER_ENGINE_TEST_DIR") is { } output && path == "reflow.epub")
        {
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output,"book.html"),chapter.Html);
            File.WriteAllText(Path.Combine(output,"locator.json"),System.Text.Json.JsonSerializer.Serialize(new { read = ReaderTextLocator.ReadScript(true), restore = ReaderTextLocator.RestoreScript(0,0,0) }));
        }
    }
    [Theory]
    [InlineData("document.docx", "Trecho original de estudo.")]
    [InlineData("document.odt", "Trecho ODF exato.")]
    [InlineData("document.odp", "Trecho ODF exato.")]
    public void SemanticReadersPreserveTextAndStateFidelity(string path, string text)
    {
        using var doc = new OfficeReadingDocument(path, File.ReadAllBytes(Corpus(path)));
        Assert.Contains(text, doc.ReadText(0));
        Assert.NotNull(doc.FidelityWarning);
        Assert.Contains(text, doc.ReadChapter(0).Html);
        if (path.EndsWith("docx", StringComparison.Ordinal))
        { Assert.Contains("<table>", doc.ReadChapter(0).Html); Assert.Contains("data:image/png;base64", doc.ReadChapter(0).Html); }
    }
    [Fact]
    public void ComicsUseNaturalPageOrderAndEmbedOriginalImages()
    {
        using var doc = new OfficeReadingDocument("comic.cbz", File.ReadAllBytes(Corpus("comic.cbz")));
        Assert.Equal(new[] { "page1.png", "page2.png", "page10.png" }, doc.Chapters.Select(c => c.Key));
        Assert.Contains("data:image/png;base64", doc.ReadChapter(1).Html);
    }
}
