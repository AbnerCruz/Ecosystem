namespace Lunet.Git;

/// <summary>
/// Descompactador zlib/DEFLATE que informa quantos bytes de entrada consumiu. Necessário para ler pacotes (packfiles) recebidos,
/// onde os objetos vêm um atrás do outro sem tamanho compactado.
/// </summary>
public static class Inflate
{
    private static readonly int[] LengthBase = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];
    private static readonly int[] LengthExtra = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];
    private static readonly int[] DistBase = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577];
    private static readonly int[] DistExtra = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];
    private static readonly int[] CodeLengthOrder = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    /// <summary>Descompacta um fluxo zlib que começa em <paramref name="offset"/>. Devolve os dados e quantos bytes da entrada foram usados.</summary>
    public static (byte[] Data, int Consumed) Zlib(byte[] input, int offset, int expectedSize = -1)
    {
        var reader = new BitReader(input, offset);
        var cmf = reader.ReadByteAligned();
        var flg = reader.ReadByteAligned();
        if ((cmf & 0x0F) != 8 || (cmf << 8 | flg) % 31 != 0) throw new GitException("Fluxo zlib inválido.");
        if ((flg & 0x20) != 0) throw new GitException("Dicionário zlib não é suportado.");

        var output = new MemoryStream(expectedSize > 0 ? expectedSize : 256);
        bool last;
        do
        {
            last = reader.ReadBits(1) == 1;
            switch (reader.ReadBits(2))
            {
                case 0:
                    reader.AlignToByte();
                    var length = reader.ReadByteAligned() | reader.ReadByteAligned() << 8;
                    reader.ReadByteAligned();
                    reader.ReadByteAligned();
                    for (var i = 0; i < length; i++) output.WriteByte((byte)reader.ReadByteAligned());
                    break;
                case 1:
                    InflateBlock(reader, output, FixedLiteral.Value, FixedDistance.Value);
                    break;
                case 2:
                    var (literal, distance) = ReadDynamicTables(reader);
                    InflateBlock(reader, output, literal, distance);
                    break;
                default:
                    throw new GitException("Bloco DEFLATE inválido.");
            }
        } while (!last);

        reader.AlignToByte();
        for (var i = 0; i < 4; i++) reader.ReadByteAligned(); // Adler-32
        return (output.ToArray(), reader.Position - offset);
    }

    private static (Huffman Literal, Huffman Distance) ReadDynamicTables(BitReader reader)
    {
        var literalCount = reader.ReadBits(5) + 257;
        var distanceCount = reader.ReadBits(5) + 1;
        var codeLengthCount = reader.ReadBits(4) + 4;
        var codeLengths = new int[19];
        for (var i = 0; i < codeLengthCount; i++) codeLengths[CodeLengthOrder[i]] = reader.ReadBits(3);
        var codeTable = new Huffman(codeLengths);

        var lengths = new int[literalCount + distanceCount];
        for (var i = 0; i < lengths.Length;)
        {
            var symbol = codeTable.Decode(reader);
            if (symbol < 16) { lengths[i++] = symbol; continue; }
            int repeat, value = 0;
            if (symbol == 16)
            {
                if (i == 0) throw new GitException("DEFLATE inválido.");
                value = lengths[i - 1];
                repeat = reader.ReadBits(2) + 3;
            }
            else if (symbol == 17) repeat = reader.ReadBits(3) + 3;
            else repeat = reader.ReadBits(7) + 11;
            if (i + repeat > lengths.Length) throw new GitException("DEFLATE inválido.");
            while (repeat-- > 0) lengths[i++] = value;
        }
        return (new Huffman(lengths[..literalCount]), new Huffman(lengths[literalCount..]));
    }

    private static void InflateBlock(BitReader reader, MemoryStream output, Huffman literal, Huffman distance)
    {
        while (true)
        {
            var symbol = literal.Decode(reader);
            if (symbol < 256) { output.WriteByte((byte)symbol); continue; }
            if (symbol == 256) return;
            symbol -= 257;
            if (symbol >= LengthBase.Length) throw new GitException("DEFLATE inválido.");
            var length = LengthBase[symbol] + reader.ReadBits(LengthExtra[symbol]);
            var distanceSymbol = distance.Decode(reader);
            if (distanceSymbol >= DistBase.Length) throw new GitException("DEFLATE inválido.");
            var back = DistBase[distanceSymbol] + reader.ReadBits(DistExtra[distanceSymbol]);
            if (back > output.Length) throw new GitException("DEFLATE inválido.");
            var buffer = output.GetBuffer();
            var start = (int)output.Length - back;
            for (var i = 0; i < length; i++)
            {
                buffer = output.GetBuffer();
                output.WriteByte(buffer[start + i]);
            }
        }
    }

    private sealed class BitReader(byte[] data, int position)
    {
        private int _bitBuffer;
        private int _bitCount;

        public int Position { get; private set; } = position;

        public int ReadBits(int count)
        {
            while (_bitCount < count)
            {
                if (Position >= data.Length) throw new GitException("Fim inesperado dos dados compactados.");
                _bitBuffer |= data[Position++] << _bitCount;
                _bitCount += 8;
            }
            var value = _bitBuffer & (1 << count) - 1;
            _bitBuffer >>= count;
            _bitCount -= count;
            return value;
        }

        public void AlignToByte()
        {
            _bitBuffer = 0;
            _bitCount = 0;
        }

        public int ReadByteAligned()
        {
            if (Position >= data.Length) throw new GitException("Fim inesperado dos dados compactados.");
            return data[Position++];
        }
    }

    private sealed class Huffman
    {
        private readonly int[] _counts = new int[16];
        private readonly int[] _symbols;

        public Huffman(int[] lengths)
        {
            foreach (var length in lengths) _counts[length]++;
            _counts[0] = 0;
            var offsets = new int[16];
            for (var i = 1; i < 16; i++) offsets[i] = offsets[i - 1] + _counts[i - 1];
            _symbols = new int[lengths.Length];
            for (var symbol = 0; symbol < lengths.Length; symbol++)
                if (lengths[symbol] != 0) _symbols[offsets[lengths[symbol]]++] = symbol;
        }

        public int Decode(BitReader reader)
        {
            var code = 0;
            var first = 0;
            var index = 0;
            for (var length = 1; length < 16; length++)
            {
                code |= reader.ReadBits(1);
                var count = _counts[length];
                if (code - count < first) return _symbols[index + (code - first)];
                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
            }
            throw new GitException("Código Huffman inválido.");
        }
    }

    private static class FixedLiteral
    {
        public static readonly Huffman Value = Build();

        private static Huffman Build()
        {
            var lengths = new int[288];
            for (var i = 0; i < 144; i++) lengths[i] = 8;
            for (var i = 144; i < 256; i++) lengths[i] = 9;
            for (var i = 256; i < 280; i++) lengths[i] = 7;
            for (var i = 280; i < 288; i++) lengths[i] = 8;
            return new Huffman(lengths);
        }
    }

    private static class FixedDistance
    {
        public static readonly Huffman Value = new(Enumerable.Repeat(5, 30).ToArray());
    }
}
