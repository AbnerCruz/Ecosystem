using Urbe.Core;
namespace Urbe.Core.Tests;
public sealed class ReaderByteRangeTests
{
    [Theory]
    [InlineData(null,0,100,false)]
    [InlineData("bytes=20-39",20,20,true)]
    [InlineData("bytes=80-",80,20,true)]
    [InlineData("bytes=-10",90,10,true)]
    [InlineData("bytes=90-200",90,10,true)]
    [InlineData("bytes=-500",0,100,true)]
    public void BoundsMediaResponses(string? header, long offset, long count, bool partial) => Assert.Equal(new ReaderByteRange(offset,count,partial),ReaderByteRange.Parse(header,100));
    [Theory]
    [InlineData("bytes=100-")][InlineData("bytes=5-2")][InlineData("bytes=0-1,4-5")][InlineData("bytes=-0")][InlineData("bytes=-")][InlineData("file=0-")][InlineData("bytes=999999999999999999999999999-")]
    public void RejectsInvalidOrMultipleRanges(string header) => Assert.Null(ReaderByteRange.Parse(header,100));
}
