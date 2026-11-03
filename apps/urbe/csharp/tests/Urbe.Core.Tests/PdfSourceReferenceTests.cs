using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PdfSourceReferenceTests
{
    [Fact]
    public void PdfLocationsRoundTripWithSourceRevision()
    {
        var reference = new PdfSourceReference(300, new string('a', 64));
        Assert.Equal(reference, PdfSourceReference.Parse(reference.Fragment));
        Assert.Equal(new PdfSourceReference(1, null), PdfSourceReference.Parse("page=1"));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=999999999999999999")]
    [InlineData("page=2&sha256=wrong")]
    [InlineData("page=2&unknown=1")]
    [InlineData("page=2&sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa&more=x")]
    public void InvalidLocationsNeverBecomeExactReferences(string value) => Assert.Null(PdfSourceReference.Parse(value));
}
