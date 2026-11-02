using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantDocumentToolsTests
{
    private static byte[] Document()
    {
        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        var first = pdf.AddPage(PageSize.A4);
        first.AddText("Capitulo primeiro. Texto conferivel.", 12, new PdfPoint(30, 700), font);
        first.DrawRectangle(new PdfPoint(30, 500), 100, 30);
        pdf.AddPage(PageSize.A4).AddText("Capitulo dois. Referencia singular.", 12, new PdfPoint(30, 700), font);
        pdf.AddPage(PageSize.A4);
        return pdf.Build();
    }

    [Fact]
    public void PdfIsAddressableByPageHashAndTextOffsets()
    {
        var pdf = Document();
        var info = AssistantDocumentTools.Inspect(pdf);
        Assert.Equal(3, info.Pages);
        Assert.Equal(64, info.Sha256.Length);
        var page = AssistantDocumentTools.Page(pdf, 1);
        Assert.Equal(info.Sha256, page.Sha256);
        Assert.Contains("conferivel", page.Text);
        Assert.True(page.VisualReviewRequired);
        Assert.True(page.VectorPaths > 0);
        Assert.False(page.NeedsOcr);
        var a = AssistantDocumentTools.Page(pdf, 2, 0, 8);
        var b = AssistantDocumentTools.Page(pdf, 2, a.Next, 16000);
        Assert.True(a.More);
        Assert.False(b.More);
        Assert.Contains("Referencia singular.", a.Text + b.Text);
    }

    [Fact]
    public void SearchIsBatchedAndDoesNotInventImageOrMathContent()
    {
        var pdf = Document();
        var first = AssistantDocumentTools.Search(pdf, "Referencia", count: 1);
        Assert.True(first.Partial);
        Assert.Empty(first.Matches);
        Assert.Equal(2, first.NextPage);
        var remainder = AssistantDocumentTools.Search(pdf, "Referencia", start: 2);
        Assert.Equal(2, Assert.Single(remainder.Matches).Page);
        Assert.Equal(1, remainder.OcrPages);
        Assert.False(remainder.Partial);
        var empty = AssistantDocumentTools.Page(pdf, 3);
        Assert.True(empty.NeedsOcr);
        Assert.True(empty.VisualReviewRequired);
        Assert.Empty(empty.Text);
    }

    [Fact]
    public void BrokenInputAndOutOfBoundsRangesAreExplicitErrors()
    {
        var pdf = Document();
        Assert.Throws<AiToolException>(() => AssistantDocumentTools.Inspect("not a pdf"u8.ToArray()));
        Assert.Throws<AiToolException>(() => AssistantDocumentTools.Page(pdf, 4));
        Assert.Throws<AiToolException>(() => AssistantDocumentTools.Page(pdf, 1, -1));
        Assert.Throws<AiToolException>(() => AssistantDocumentTools.Search(pdf, "something", count: 501));
    }
}
