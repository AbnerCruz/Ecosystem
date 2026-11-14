using System.IO.Compression;
using System.Buffers.Binary;

namespace Lunet.Core;

/// <summary>Minimal PNG encoder (RGBA8, no filter) in pure .NET, for assets generated without the Android graphics stack.</summary>
public static class PngWriter
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width < 1 || height < 1 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Dimensões e pixels RGBA não conferem.");
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6; // 8 bits, RGBA
        Chunk(output, "IHDR", header);
        using (var raw = new MemoryStream())
        {
            using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(rgba.Slice(y * width * 4, width * 4));
                }
            }
            Chunk(output, "IDAT", raw.ToArray());
        }
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        uint crc = 0xffffffff;
        foreach (var b in typeBytes) crc = CrcTable[(crc ^ b) & 0xff] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xff] ^ (crc >> 8);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xffffffff);
        output.Write(crcBytes);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
