using System.IO.Compression;

namespace Lunet.Git;

/// <summary>Leitura de objetos em pacotes e aplicação de deltas.</summary>
internal static class PackReader
{
    /// <summary>Lê o objeto que começa em <paramref name="offset"/>, resolvendo deltas (por deslocamento ou por id).</summary>
    public static GitObject ReadObject(byte[] pack, int offset, ObjectStore store, Func<int, ObjectStore, GitObject>? readBase)
    {
        var (type, size, dataStart, baseOffset, baseId) = ReadHeader(pack, offset);
        var data = Decompress(pack, dataStart, size);
        switch (type)
        {
            case 1: return new GitObject(ObjectType.Commit, data);
            case 2: return new GitObject(ObjectType.Tree, data);
            case 3: return new GitObject(ObjectType.Blob, data);
            case 4: return new GitObject(ObjectType.Tag, data);
            case 6:
                if (readBase is null) throw new GitException("Delta por deslocamento fora de um pacote.");
                var offsetBase = readBase((int)baseOffset, store);
                return new GitObject(offsetBase.Type, ApplyDelta(offsetBase.Data, data));
            case 7:
                var idBase = store.Read(baseId);
                return new GitObject(idBase.Type, ApplyDelta(idBase.Data, data));
            default:
                throw new GitException($"Tipo de objeto de pacote desconhecido: {type}");
        }
    }

    /// <summary>Lê o cabeçalho de uma entrada: tipo, tamanho descompactado, início dos dados e a base (se for delta).</summary>
    public static (int Type, int Size, int DataStart, long BaseOffset, ObjectId BaseId) ReadHeader(byte[] pack, int offset)
    {
        var position = offset;
        var b = pack[position++];
        var type = b >> 4 & 7;
        long size = b & 0x0F;
        var shift = 4;
        while ((b & 0x80) != 0)
        {
            b = pack[position++];
            size |= (long)(b & 0x7F) << shift;
            shift += 7;
        }
        long baseOffset = 0;
        var baseId = ObjectId.Zero;
        if (type == 6)
        {
            b = pack[position++];
            var relative = (long)(b & 0x7F);
            while ((b & 0x80) != 0)
            {
                b = pack[position++];
                relative = (relative + 1 << 7) | (long)(b & 0x7F);
            }
            baseOffset = offset - relative;
        }
        else if (type == 7)
        {
            baseId = new ObjectId(pack.AsSpan(position, 20));
            position += 20;
        }
        return (type, (int)size, position, baseOffset, baseId);
    }

    private static byte[] Decompress(byte[] pack, int start, int size)
    {
        using var input = new MemoryStream(pack, start, pack.Length - start, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var result = new byte[size];
        var read = 0;
        while (read < size)
        {
            var n = zlib.Read(result, read, size - read);
            if (n <= 0) throw new GitException("Objeto de pacote truncado.");
            read += n;
        }
        return result;
    }

    /// <summary>Aplica um delta (instruções copiar/inserir) a uma base.</summary>
    public static byte[] ApplyDelta(byte[] source, byte[] delta)
    {
        var position = 0;
        var sourceSize = ReadVarint(delta, ref position);
        if (sourceSize != source.Length) throw new GitException("Delta não corresponde à base.");
        var targetSize = ReadVarint(delta, ref position);
        var result = new byte[targetSize];
        var written = 0;
        while (position < delta.Length)
        {
            var command = delta[position++];
            if ((command & 0x80) != 0)
            {
                var copyOffset = 0;
                var copySize = 0;
                if ((command & 0x01) != 0) copyOffset = delta[position++];
                if ((command & 0x02) != 0) copyOffset |= delta[position++] << 8;
                if ((command & 0x04) != 0) copyOffset |= delta[position++] << 16;
                if ((command & 0x08) != 0) copyOffset |= delta[position++] << 24;
                if ((command & 0x10) != 0) copySize = delta[position++];
                if ((command & 0x20) != 0) copySize |= delta[position++] << 8;
                if ((command & 0x40) != 0) copySize |= delta[position++] << 16;
                if (copySize == 0) copySize = 0x10000;
                if (copyOffset + copySize > source.Length || written + copySize > result.Length) throw new GitException("Delta inválido.");
                Buffer.BlockCopy(source, copyOffset, result, written, copySize);
                written += copySize;
            }
            else if (command != 0)
            {
                if (position + command > delta.Length || written + command > result.Length) throw new GitException("Delta inválido.");
                Buffer.BlockCopy(delta, position, result, written, command);
                position += command;
                written += command;
            }
            else throw new GitException("Delta inválido.");
        }
        if (written != targetSize) throw new GitException("Delta produziu tamanho inesperado.");
        return result;
    }

    private static int ReadVarint(byte[] data, ref int position)
    {
        var value = 0;
        var shift = 0;
        byte b;
        do
        {
            b = data[position++];
            value |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return value;
    }
}
