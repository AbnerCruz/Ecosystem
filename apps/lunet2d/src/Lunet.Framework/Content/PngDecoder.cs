using System.IO.Compression;

namespace Lunet.Content;

/// <summary>Imagem decodificada: RGBA de 8 bits, linhas de cima para baixo.</summary>
/// <param name="Width">Largura da imagem, em pixels.</param>
/// <param name="Height">Altura da imagem, em pixels.</param>
/// <param name="Rgba">Pixels em RGBA de 8 bits por canal, linha a linha de cima para baixo.</param>
public sealed record DecodedImage(int Width, int Height, byte[] Rgba);

/// <summary>Decodificador PNG mínimo e sem dependências: 8/16 bits, tons de cinza, RGB, paleta e alfa; sem entrelaçamento.</summary>
public static class PngDecoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Decodifica um PNG (8 ou 16 bits, sem entrelaçamento).</summary>
    /// <param name="data">Bytes do arquivo.</param>
    /// <returns>A imagem em RGBA.</returns>
    public static DecodedImage Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || !data[..8].SequenceEqual(Signature)) throw new InvalidDataException("Não é um arquivo PNG.");

        int width = 0, height = 0, bitDepth = 0, colorType = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();
        var sawHeader = false;
        var pos = 8;

        while (pos + 8 <= data.Length)
        {
            var length = (int)ReadUInt32(data, pos);
            var type = data.Slice(pos + 4, 4);
            pos += 8;
            if (length < 0 || pos + length + 4 > data.Length) throw new InvalidDataException("PNG truncado.");
            var body = data.Slice(pos, length);
            pos += length + 4; // dados + CRC (não verificado)

            if (type.SequenceEqual("IHDR"u8))
            {
                if (length < 13) throw new InvalidDataException("IHDR inválido.");
                width = (int)ReadUInt32(body, 0);
                height = (int)ReadUInt32(body, 4);
                bitDepth = body[8];
                colorType = body[9];
                if (body[12] != 0) throw new NotSupportedException("PNG entrelaçado não é suportado. Exporte sem entrelaçamento.");
                sawHeader = true;
            }
            else if (type.SequenceEqual("PLTE"u8)) palette = body.ToArray();
            else if (type.SequenceEqual("tRNS"u8)) transparency = body.ToArray();
            else if (type.SequenceEqual("IDAT"u8)) idat.Write(body);
            else if (type.SequenceEqual("IEND"u8)) break;
        }

        if (!sawHeader) throw new InvalidDataException("PNG sem IHDR.");
        if (width < 1 || height < 1 || (long)width * height > 64_000_000) throw new InvalidDataException("Dimensões de PNG inválidas.");
        if (bitDepth is not (8 or 16)) throw new NotSupportedException($"PNG com {bitDepth} bits por canal não é suportado (use 8 ou 16).");

        var channels = colorType switch
        {
            0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4,
            _ => throw new InvalidDataException("Tipo de cor PNG desconhecido."),
        };
        if (colorType == 3 && (bitDepth != 8 || palette is null)) throw new NotSupportedException("PNG com paleta precisa de 8 bits e chunk PLTE.");

        var bytesPerPixel = channels * (bitDepth / 8);
        var stride = width * bytesPerPixel;
        var raw = new byte[(stride + 1) * height];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        {
            try { z.ReadExactly(raw); }
            catch (EndOfStreamException) { throw new InvalidDataException("Dados de imagem PNG incompletos."); }
        }

        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var src = raw.AsSpan(y * (stride + 1) + 1, stride);
            var dst = pixels.AsSpan(y * stride, stride);
            var prev = y == 0 ? ReadOnlySpan<byte>.Empty : pixels.AsSpan((y - 1) * stride, stride);
            Unfilter(filter, src, dst, prev, bytesPerPixel);
        }

        var rgba = new byte[width * height * 4];
        var sample = bitDepth / 8; // bytes por canal; em 16 bits usa o byte mais significativo
        for (var i = 0; i < width * height; i++)
        {
            var p = i * bytesPerPixel;
            var o = i * 4;
            switch (colorType)
            {
                case 0:
                    rgba[o] = rgba[o + 1] = rgba[o + 2] = pixels[p];
                    rgba[o + 3] = 255;
                    break;
                case 2:
                    rgba[o] = pixels[p]; rgba[o + 1] = pixels[p + sample]; rgba[o + 2] = pixels[p + 2 * sample];
                    rgba[o + 3] = 255;
                    break;
                case 3:
                    var index = pixels[p];
                    if (index * 3 + 2 >= palette!.Length) throw new InvalidDataException("Índice de paleta inválido.");
                    rgba[o] = palette[index * 3]; rgba[o + 1] = palette[index * 3 + 1]; rgba[o + 2] = palette[index * 3 + 2];
                    rgba[o + 3] = transparency is not null && index < transparency.Length ? transparency[index] : (byte)255;
                    break;
                case 4:
                    rgba[o] = rgba[o + 1] = rgba[o + 2] = pixels[p];
                    rgba[o + 3] = pixels[p + sample];
                    break;
                default:
                    rgba[o] = pixels[p]; rgba[o + 1] = pixels[p + sample]; rgba[o + 2] = pixels[p + 2 * sample];
                    rgba[o + 3] = pixels[p + 3 * sample];
                    break;
            }
        }
        return new DecodedImage(width, height, rgba);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> s, int offset) =>
        (uint)(s[offset] << 24 | s[offset + 1] << 16 | s[offset + 2] << 8 | s[offset + 3]);

    private static void Unfilter(byte filter, ReadOnlySpan<byte> src, Span<byte> dst, ReadOnlySpan<byte> prev, int bpp)
    {
        for (var i = 0; i < src.Length; i++)
        {
            int a = i >= bpp ? dst[i - bpp] : 0;
            int b = prev.IsEmpty ? 0 : prev[i];
            int c = i >= bpp && !prev.IsEmpty ? prev[i - bpp] : 0;
            dst[i] = filter switch
            {
                0 => src[i],
                1 => (byte)(src[i] + a),
                2 => (byte)(src[i] + b),
                3 => (byte)(src[i] + ((a + b) >> 1)),
                4 => (byte)(src[i] + Paeth(a, b, c)),
                _ => throw new InvalidDataException("Filtro PNG desconhecido."),
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
