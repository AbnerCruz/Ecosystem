namespace Urbe.Core.Tests;

public sealed class ImageProvenanceCrcTests
{
    /// <summary>The PNG chunk CRC is the standard CRC-32 (check value of "123456789").</summary>
    [Fact]
    public void TheCrcIsTheStandardOne() =>
        Assert.Equal(0xCBF43926u, ImageProvenance.Crc32("123456789"u8));
}
