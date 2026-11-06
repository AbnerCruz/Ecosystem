using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-242: a book's raster figures exposed to the reader with what the book says about them.</summary>
public sealed class ReaderImagesTests
{
    [Fact]
    public async Task ChapterImages_ListsRealPicturesWithTheirDescriptionOnce()
    {
        using var book = new EpubReadingDocument(await Urbe.Tests.Fixtures.BookFixture.WithFigures());
        var first = book.ChapterImages(0);
        // three pictures in the package (the exporter packages each use); those without alt have no description
        Assert.Equal(3, first.Count);
        Assert.Equal([1, 2, 3], first.Select(p => p.Number));
        Assert.Equal((1, "Diagrama do ciclo da água"), (first[0].Number, first[0].Description));
        Assert.Equal("", first[1].Description);
        Assert.True(FileSignatures.IsImage(FileSignatures.Detect(first[0].Bytes)));
        Assert.Empty(book.ChapterImages(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => book.ChapterImages(5));
    }

    [Fact]
    public void FigureCitation_NamesTheChapterAndTheRegion()
    {
        var reference = new PartSourceReference("chapter", 3, new string('a', 64)) with { Region = ReaderRegion.Parse("0.1,0.2,0.3,0.4") };
        var parsed = PartSourceReference.Parse(reference.Fragment)!;
        Assert.Equal(("chapter", 3), (parsed.Unit, parsed.Index));
        Assert.NotNull(parsed.Region);
    }
}
