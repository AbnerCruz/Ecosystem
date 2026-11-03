using Urbe.Client;
namespace Urbe.Client.Tests;
public sealed class ReaderRangeStreamTests
{
    [Fact]
    public void CanOnlyReadAndSeekWithinAuthorizedRegion()
    {
        using var source = new MemoryStream(Enumerable.Range(0,100).Select(i=>(byte)i).ToArray());
        using var range = new ReaderRangeStream(source,20,10);
        byte[] bytes = new byte[100]; Assert.Equal(10,range.Read(bytes,0,100)); Assert.Equal(Enumerable.Range(20,10).Select(i=>(byte)i),bytes[..10]);
        Assert.Equal(0,range.Read(bytes)); Assert.Equal(5,range.Seek(-5,SeekOrigin.End)); Assert.Equal(25,range.ReadByte());
        Assert.Throws<IOException>(()=>range.Seek(11,SeekOrigin.Begin)); Assert.Throws<NotSupportedException>(()=>range.WriteByte(1));
    }
}
