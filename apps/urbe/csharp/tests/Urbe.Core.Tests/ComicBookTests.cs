using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-238: CBZ pages in natural order, read lazily, and an archive that tries anything is refused.</summary>
public sealed class ComicBookTests
{
    /// <summary>The smallest real PNG header (8×4), enough for the signature and size checks.</summary>
    public static byte[] Png(int width = 8, int height = 4)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    public static byte[] Zip(params (string Name, byte[] Bytes)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, bytes) in entries)
            {
                using var output = zip.CreateEntry(name).Open();
                output.Write(bytes);
            }
        return stream.ToArray();
    }

    [Fact]
    public void PagesAreOnlyPicturesInNaturalOrder()
    {
        var bytes = Zip(("page10.png", Png()), ("page2.png", Png()), ("page1.png", Png()), ("ComicInfo.xml", "<ComicInfo/>"u8.ToArray()),
            ("__MACOSX/._page1.png", Png()), (".thumb.jpg", Png()), ("Cap 10/01.jpg", Png()), ("Cap 2/01.jpg", Png()));
        using var book = ComicBook.Open(bytes);
        Assert.Equal(["Cap 2/01.jpg", "Cap 10/01.jpg", "page1.png", "page2.png", "page10.png"], book.Pages);
        Assert.Equal(3, book.IgnoredEntries);
        Assert.Equal(Png(), book.PageBytes(4));
    }

    [Fact]
    public void NaturalOrderComparesNumbersOfAnyLength()
    {
        string[] names = ["p100000000000000000000002", "p9", "p01", "p1", "P2", "p100000000000000000000001"];
        Assert.Equal(["p1", "p01", "P2", "p9", "p100000000000000000000001", "p100000000000000000000002"], names.Order(NaturalOrder.Instance));
    }

    [Theory]
    [InlineData("../fora.png")]
    [InlineData("/etc/fora.png")]
    [InlineData("pasta/../../fora.png")]
    [InlineData("C:/fora.png")]
    [InlineData("pasta\\..\\fora.png")]
    public void PathsOutsideTheArchiveAreRefused(string name) =>
        Assert.Throws<InvalidDataException>(() => ComicBook.Open(Zip(("page1.png", Png()), (name, Png()))));

    [Fact]
    public void DuplicateEntriesAreRefused() =>
        Assert.Throws<InvalidDataException>(() => ComicBook.Open(Zip(("page1.png", Png()), ("page1.png", Png()))));

    [Fact]
    public void AZipBombIsRefusedBeforeAnythingIsInflated()
    {
        // 130 MB of zeros, a few hundred KB compressed: over the per-member budget
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var page = zip.CreateEntry("page1.png").Open()) page.Write(Png());
            using var bomb = zip.CreateEntry("page2.png", CompressionLevel.SmallestSize).Open();
            var zeros = new byte[1024 * 1024];
            for (int i = 0; i < 130; i++) bomb.Write(zeros);
        }
        Assert.True(stream.Length < 2 * 1024 * 1024);
        var error = Assert.Throws<InvalidDataException>(() => ComicBook.Open(stream.ToArray()));
        Assert.Contains("orçamento", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMemberThatInflatesPastItsDeclaredSizeIsNeverReadPastIt()
    {
        var content = Encoding.ASCII.GetBytes(new string('A', 4000));
        var page = Png().Concat(content).ToArray();
        var bytes = Zip(("page1.png", page));
        // the central directory and the local header now lie: 40 bytes
        Patch(bytes, [0x50, 0x4B, 0x01, 0x02], 24);
        Patch(bytes, [0x50, 0x4B, 0x03, 0x04], 22);
        using var book = ComicBook.Open(bytes);
        // never more than the declared size is inflated: the member is cut there, or refused
        try { Assert.Equal(40, book.PageBytes(0).Length); }
        catch (InvalidDataException) { }

        static void Patch(byte[] zip, byte[] signature, int offset)
        {
            int at = zip.AsSpan().IndexOf(signature);
            Assert.True(at >= 0);
            BinaryPrimitives.WriteInt32LittleEndian(zip.AsSpan(at + offset), 40);
        }
    }

    [Fact]
    public void AnEntryNamedLikeAPictureThatIsNotOneNeverReachesTheDecoder()
    {
        using var book = ComicBook.Open(Zip(("page1.png", Png()), ("page2.jpg", "MZ executable"u8.ToArray())));
        Assert.Equal(2, book.PageCount);
        var error = Assert.Throws<InvalidDataException>(() => book.PageBytes(1));
        Assert.Contains("página 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RarAndSevenZipAreNotSupportedAndOtherBytesAreNotACbz()
    {
        var rar = "Rar!\u001a\u0007\u0001\u0000restante"u8.ToArray();
        var error = Assert.Throws<NotSupportedException>(() => ComicBook.Open(rar));
        Assert.Contains("CBR", error.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => ComicBook.Open([(byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C, 0, 4]));
        Assert.Throws<InvalidDataException>(() => ComicBook.Open("%PDF-1.7 renomeado"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => ComicBook.Open(Zip(("leia.txt", "sem imagens"u8.ToArray()))));
        Assert.Throws<InvalidDataException>(() => ComicBook.Open([0x50, 0x4B, 0x03, 0x04, 1, 2, 3]));
    }

    [Fact]
    public void ImageHeaderReadsWidthAndHeight()
    {
        Assert.Equal((8, 4), ImageHeader.Size(Png()));
        Assert.Equal(8, ImageHeader.Width(Png()));
        Assert.Equal(4, ImageHeader.Height(Png()));
        Assert.Null(ImageHeader.Size("not a picture"u8));
    }
}
