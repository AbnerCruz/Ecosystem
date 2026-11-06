using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-242: a figure of an EPUB chapter opens in the recognition view, cited by its chapter, and ‹ goes back to the text.</summary>
    [Fact]
    public async Task BookFigures_OpenForRecognitionCitedByTheirChapter() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            File.WriteAllBytes(Path.Combine(vault, "livro.epub"), await Urbe.Tests.Fixtures.BookFixture.WithFigures());
            await view.OpenVaultAsync();
            await view.FilePreview.OpenAsync("livro.epub");
            await Settle(window, view);
            var book = view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single();
            var pictures = await book.LoadChapterPicturesAsync();
            Assert.Equal(3, pictures.Count);
            book.OpenFigure(pictures[0]);
            await Settle(window, view);
            var figure = Assert.IsType<OcrStudyView>(book.FigureView);
            Assert.Equal(("chapter", 1), (figure.Part!.Unit, figure.Part.Index));
            Assert.Contains(book.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>(), t => t.Text?.Contains("Diagrama do ciclo da água") == true);
            Assert.Same(book, view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single());
            // a damaged picture is said in the reader, never a crash
            book.OpenFigure(new ReaderImage(9, "x.png", "", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]));
            Assert.Same(figure, book.FigureView);
            Assert.Contains(book.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>(), t => t.Text?.StartsWith("A imagem 9 não pôde ser aberta", StringComparison.Ordinal) == true);
            return true;
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }, CancellationToken.None));
}
