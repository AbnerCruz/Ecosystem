using Urbe.Core;
namespace Urbe.Core.Tests;
public sealed class ReaderSourceReferenceTests
{
    [Fact]
    public void SourceLocationsRoundTripOnlyWithValidRevisionCoordinatesAndTextPositions()
    {
        string hash=new('a',64);var region=new ReaderRegion(.1,.2,.4,.3);
        var pdf=new PdfSourceReference(18,hash,region,900,40);Assert.Equal(pdf,PdfSourceReference.Parse(pdf.Fragment));
        var book=new PartSourceReference("chapter",3,hash,120,25);Assert.Equal(book,PartSourceReference.Parse(book.Fragment));
        var image=new PartSourceReference("image",1,hash,Region:region);Assert.Equal(image,PartSourceReference.Parse(image.Fragment));
        Assert.Null(PdfSourceReference.Parse("page=1&text=4"));Assert.Null(PartSourceReference.Parse("chapter=1&text=-1&length=2"));
        Assert.Null(ReaderRegion.Parse("0,0,Infinity,1"));Assert.Null(ReaderRegion.Parse("0.8,0,0.5,1"));Assert.Null(ReaderRegion.Parse("0,0,0,0"));
    }
}
