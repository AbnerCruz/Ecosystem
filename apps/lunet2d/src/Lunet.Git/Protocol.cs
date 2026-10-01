using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Lunet.Git;

/// <summary>Formato "pkt-line" do protocolo Git: linhas prefixadas por 4 dígitos hexadecimais de tamanho.</summary>
public static class PktLine
{
    public static byte[] Encode(string text) => Encode(Encoding.UTF8.GetBytes(text));

    public static byte[] Encode(ReadOnlySpan<byte> payload)
    {
        var result = new byte[payload.Length + 4];
        Encoding.ASCII.GetBytes((payload.Length + 4).ToString("x4")).CopyTo(result, 0);
        payload.CopyTo(result.AsSpan(4));
        return result;
    }

    public static readonly byte[] Flush = "0000"u8.ToArray();

    /// <summary>Lê um pkt-line. Devolve falso no fim dos dados; <paramref name="payload"/> nulo indica um flush (0000).</summary>
    public static bool TryRead(byte[] data, ref int position, out byte[]? payload)
    {
        payload = null;
        if (position + 4 > data.Length) return false;
        if (!int.TryParse(Encoding.ASCII.GetString(data, position, 4), System.Globalization.NumberStyles.HexNumber, null, out var length))
            throw new GitException("Resposta do servidor Git em formato inesperado.");
        if (length == 0)
        {
            position += 4;
            return true;
        }
        if (length < 4 || position + length > data.Length) throw new GitException("Resposta do servidor Git truncada.");
        payload = data.AsSpan(position + 4, length - 4).ToArray();
        position += length;
        return true;
    }
}

/// <summary>Referências anunciadas por um servidor e suas capacidades.</summary>
public sealed record Advertisement(IReadOnlyDictionary<string, ObjectId> Refs, IReadOnlySet<string> Capabilities, string? HeadTarget)
{
    public static Advertisement Parse(byte[] data)
    {
        var refs = new Dictionary<string, ObjectId>(StringComparer.Ordinal);
        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        string? headTarget = null;
        var position = 0;
        var first = true;
        while (PktLine.TryRead(data, ref position, out var payload))
        {
            if (payload is null) continue;
            var line = Encoding.UTF8.GetString(payload).TrimEnd('\n');
            if (line.StartsWith("# service=", StringComparison.Ordinal)) continue;
            if (line.StartsWith("ERR ", StringComparison.Ordinal)) throw new GitException("O servidor recusou: " + line[4..]);
            var nul = line.IndexOf('\0');
            if (nul >= 0)
            {
                foreach (var capability in line[(nul + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    capabilities.Add(capability);
                    if (capability.StartsWith("symref=HEAD:", StringComparison.Ordinal)) headTarget = capability["symref=HEAD:".Length..];
                }
                line = line[..nul];
            }
            var space = line.IndexOf(' ');
            if (space != 40 || !ObjectId.TryParse(line.AsSpan(0, 40), out var id)) continue;
            var name = line[(space + 1)..];
            if (first && name == "capabilities^{}") { first = false; continue; }
            first = false;
            if (!name.EndsWith("^{}", StringComparison.Ordinal)) refs[name] = id;
        }
        return new Advertisement(refs, capabilities, headTarget);
    }
}

/// <summary>Montagem e leitura de pacotes (packfiles).</summary>
public static class PackFormat
{
    /// <summary>Monta um pacote sem deltas com os objetos dados.</summary>
    public static byte[] Build(IReadOnlyList<(ObjectType Type, byte[] Data)> objects)
    {
        using var stream = new MemoryStream();
        stream.Write("PACK"u8);
        WriteInt(stream, 2);
        WriteInt(stream, objects.Count);
        foreach (var (type, data) in objects)
        {
            var size = data.Length;
            var first = (byte)((int)type << 4 | size & 0x0F);
            size >>= 4;
            if (size > 0) first |= 0x80;
            stream.WriteByte(first);
            while (size > 0)
            {
                var next = (byte)(size & 0x7F);
                size >>= 7;
                if (size > 0) next |= 0x80;
                stream.WriteByte(next);
            }
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) zlib.Write(data);
            compressed.WriteTo(stream);
        }
        var body = stream.ToArray();
        return [.. body, .. SHA1.HashData(body)];
    }

    private static void WriteInt(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    /// <summary>Lê todos os objetos de um pacote recebido (resolvendo deltas) e os grava no repositório. Devolve quantos.</summary>
    public static int Unpack(byte[] pack, ObjectStore store)
    {
        if (pack.Length < 32 || pack[0] != 'P' || pack[1] != 'A' || pack[2] != 'C' || pack[3] != 'K') throw new GitException("Pacote recebido inválido.");
        var version = pack[4] << 24 | pack[5] << 16 | pack[6] << 8 | pack[7];
        if (version is not (2 or 3)) throw new GitException($"Versão de pacote {version} não suportada.");
        var count = pack[8] << 24 | pack[9] << 16 | pack[10] << 8 | pack[11];
        var trailer = SHA1.HashData(pack.AsSpan(0, pack.Length - 20));
        if (!trailer.AsSpan().SequenceEqual(pack.AsSpan(pack.Length - 20))) throw new GitException("Pacote recebido corrompido (checksum).");

        var entries = new List<(int Offset, int Type, byte[] Data, long BaseOffset, ObjectId BaseId)>();
        var position = 12;
        for (var i = 0; i < count; i++)
        {
            var (type, size, dataStart, baseOffset, baseId) = PackReader.ReadHeader(pack, position);
            var (data, consumed) = Inflate.Zlib(pack, dataStart, size);
            if (data.Length != size) throw new GitException("Objeto de pacote com tamanho inesperado.");
            entries.Add((position, type, data, baseOffset, baseId));
            position = dataStart + consumed;
        }

        var indexByOffset = new Dictionary<int, int>();
        for (var i = 0; i < entries.Count; i++) indexByOffset[entries[i].Offset] = i;
        var resolved = new Dictionary<int, GitObject>();
        var written = 0;
        GitObject Resolve(int index, HashSet<int>? visiting = null)
        {
            var entry = entries[index];
            if (resolved.TryGetValue(entry.Offset, out var known)) return known;
            GitObject result;
            switch (entry.Type)
            {
                case 1 or 2 or 3 or 4:
                    result = new GitObject((ObjectType)entry.Type, entry.Data);
                    break;
                case 6:
                    if (!indexByOffset.TryGetValue((int)entry.BaseOffset, out var baseIndex)) throw new GitException("Delta aponta para objeto inexistente no pacote.");
                    var baseObject = Resolve(baseIndex);
                    result = new GitObject(baseObject.Type, PackReader.ApplyDelta(baseObject.Data, entry.Data));
                    break;
                case 7:
                    GitObject baseByIdObject;
                    var byId = entries.FindIndex(e => e.Type is >= 1 and <= 4 && ObjectId.Compute((ObjectType)e.Type, e.Data) == entry.BaseId);
                    baseByIdObject = byId >= 0 ? Resolve(byId) : store.Read(entry.BaseId);
                    result = new GitObject(baseByIdObject.Type, PackReader.ApplyDelta(baseByIdObject.Data, entry.Data));
                    break;
                default:
                    throw new GitException("Tipo de objeto de pacote desconhecido.");
            }
            resolved[entry.Offset] = result;
            return result;
        }
        for (var i = 0; i < entries.Count; i++)
        {
            store.Write(Resolve(i));
            written++;
        }
        return written;
    }
}
