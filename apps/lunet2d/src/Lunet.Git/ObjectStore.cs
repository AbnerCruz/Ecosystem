using System.IO.Compression;
using System.Text;

namespace Lunet.Git;

/// <summary>Um objeto do Git já lido: tipo e conteúdo.</summary>
public sealed record GitObject(ObjectType Type, byte[] Data);

/// <summary>Leitura e gravação de objetos do repositório (soltos e em pacotes), no formato do Git.</summary>
public sealed class ObjectStore
{
    private readonly string _objects;
    private readonly List<PackFile> _packs = [];
    private bool _packsLoaded;

    public ObjectStore(string objectsDirectory) => _objects = objectsDirectory;

    public bool Contains(ObjectId id)
    {
        if (File.Exists(LoosePath(id))) return true;
        LoadPacks();
        return _packs.Any(p => p.Contains(id));
    }

    public GitObject Read(ObjectId id) => TryRead(id) ?? throw new GitException($"Objeto {id.Short} não encontrado no repositório.");

    public GitObject? TryRead(ObjectId id)
    {
        var path = LoosePath(id);
        if (File.Exists(path)) return ReadLoose(path);
        LoadPacks();
        foreach (var pack in _packs)
            if (pack.TryRead(id, this) is { } found) return found;
        return null;
    }

    /// <summary>Grava um objeto solto (se ainda não existir) e devolve seu id.</summary>
    public ObjectId Write(ObjectType type, ReadOnlySpan<byte> data)
    {
        var id = ObjectId.Compute(type, data);
        var path = LoosePath(id);
        if (File.Exists(path)) return id;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp" + Environment.ProcessId;
        using (var file = File.Create(temporary))
        using (var zlib = new ZLibStream(file, CompressionLevel.Fastest))
        {
            zlib.Write(Encoding.ASCII.GetBytes($"{ObjectId.TypeName(type)} {data.Length}\0"));
            zlib.Write(data);
        }
        File.Move(temporary, path, overwrite: true);
        return id;
    }

    /// <summary>Grava um objeto já lido de um pacote (mantém o id calculado).</summary>
    public ObjectId Write(GitObject obj) => Write(obj.Type, obj.Data);

    /// <summary>Todos os ids de objetos soltos e em pacotes (para alcançar o que enviar/receber).</summary>
    public IEnumerable<ObjectId> EnumerateAll()
    {
        if (Directory.Exists(_objects))
        {
            foreach (var directory in Directory.EnumerateDirectories(_objects))
            {
                var name = Path.GetFileName(directory);
                if (name.Length != 2) continue;
                foreach (var file in Directory.EnumerateFiles(directory))
                    if (ObjectId.TryParse(name + Path.GetFileName(file), out var id)) yield return id;
            }
        }
        LoadPacks();
        foreach (var pack in _packs)
            foreach (var id in pack.Ids) yield return id;
    }

    public void ReloadPacks()
    {
        _packsLoaded = false;
        _packs.Clear();
    }

    private void LoadPacks()
    {
        if (_packsLoaded) return;
        _packsLoaded = true;
        var packDirectory = Path.Combine(_objects, "pack");
        if (!Directory.Exists(packDirectory)) return;
        foreach (var index in Directory.EnumerateFiles(packDirectory, "*.idx"))
        {
            var pack = Path.ChangeExtension(index, ".pack");
            if (File.Exists(pack)) _packs.Add(new PackFile(index, pack));
        }
    }

    private string LoosePath(ObjectId id)
    {
        var hex = id.Hex;
        return Path.Combine(_objects, hex[..2], hex[2..]);
    }

    private static GitObject ReadLoose(string path)
    {
        using var file = File.OpenRead(path);
        using var zlib = new ZLibStream(file, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        zlib.CopyTo(buffer);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var nul = bytes.IndexOf((byte)0);
        if (nul < 0) throw new GitException("Objeto solto corrompido.");
        var header = Encoding.ASCII.GetString(bytes[..nul]);
        var space = header.IndexOf(' ');
        var type = ObjectId.ParseType(header[..space]);
        return new GitObject(type, bytes[(nul + 1)..].ToArray());
    }
}

/// <summary>Um pacote (.pack + .idx v2) de objetos.</summary>
internal sealed class PackFile
{
    private readonly string _packPath;
    private readonly byte[] _index;
    private readonly int _count;
    private byte[]? _pack;

    public PackFile(string indexPath, string packPath)
    {
        _packPath = packPath;
        _index = File.ReadAllBytes(indexPath);
        if (_index.Length < 8 + 256 * 4 || _index[0] != 0xFF || _index[1] != 't' || _index[2] != 'O' || _index[3] != 'c' || ReadInt(_index, 4) != 2)
            throw new GitException("Índice de pacote em formato não suportado (esperado v2).");
        _count = ReadInt(_index, 8 + 255 * 4);
    }

    private int ShaStart => 8 + 256 * 4;

    public IEnumerable<ObjectId> Ids
    {
        get
        {
            for (var i = 0; i < _count; i++) yield return new ObjectId(_index.AsSpan(ShaStart + i * 20, 20));
        }
    }

    public bool Contains(ObjectId id) => Find(id) >= 0;

    public GitObject? TryRead(ObjectId id, ObjectStore store)
    {
        var position = Find(id);
        return position < 0 ? null : ReadAt(OffsetOf(position), store);
    }

    private int Find(ObjectId id)
    {
        var first = id.Bytes[0];
        var low = first == 0 ? 0 : ReadInt(_index, 8 + (first - 1) * 4);
        var high = ReadInt(_index, 8 + first * 4) - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var compare = _index.AsSpan(ShaStart + mid * 20, 20).SequenceCompareTo(id.Bytes);
            if (compare == 0) return mid;
            if (compare < 0) low = mid + 1; else high = mid - 1;
        }
        return -1;
    }

    private long OffsetOf(int position)
    {
        var table = ShaStart + _count * 20 + _count * 4;
        var value = (uint)ReadInt(_index, table + position * 4);
        if ((value & 0x80000000) == 0) return value;
        var large = table + _count * 4 + (int)(value & 0x7FFFFFFF) * 8;
        return (long)ReadUInt64(_index, large);
    }

    private GitObject ReadAt(long offset, ObjectStore store)
    {
        _pack ??= File.ReadAllBytes(_packPath);
        return PackReader.ReadObject(_pack, (int)offset, store, ReadBaseByOffset);
    }

    private GitObject ReadBaseByOffset(int offset, ObjectStore store) => ReadAt(offset, store);

    private static int ReadInt(byte[] data, int position) => data[position] << 24 | data[position + 1] << 16 | data[position + 2] << 8 | data[position + 3];

    private static ulong ReadUInt64(byte[] data, int position) => (ulong)(uint)ReadInt(data, position) << 32 | (uint)ReadInt(data, position + 4);
}
