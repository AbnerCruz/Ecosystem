using System.Text;

namespace Lunet.Git;

/// <summary>Entrada de uma árvore (diretório): modo, nome e id.</summary>
public sealed record TreeEntry(string Mode, string Name, ObjectId Id)
{
    public bool IsTree => Mode == "40000";
}

/// <summary>Leitura e escrita do formato binário de árvores do Git.</summary>
public static class TreeFormat
{
    public static List<TreeEntry> Parse(byte[] data)
    {
        var entries = new List<TreeEntry>();
        var position = 0;
        while (position < data.Length)
        {
            var space = Array.IndexOf(data, (byte)' ', position);
            var nul = Array.IndexOf(data, (byte)0, space);
            if (space < 0 || nul < 0 || nul + 21 > data.Length) throw new GitException("Árvore corrompida.");
            var mode = Encoding.ASCII.GetString(data, position, space - position);
            var name = Encoding.UTF8.GetString(data, space + 1, nul - space - 1);
            entries.Add(new TreeEntry(mode, name, new ObjectId(data.AsSpan(nul + 1, 20))));
            position = nul + 21;
        }
        return entries;
    }

    public static byte[] Serialize(IEnumerable<TreeEntry> entries)
    {
        // O Git ordena diretórios como se o nome terminasse em '/'.
        var ordered = entries.OrderBy(e => e.IsTree ? e.Name + "/" : e.Name, StringComparer.Ordinal);
        using var stream = new MemoryStream();
        foreach (var entry in ordered)
        {
            stream.Write(Encoding.ASCII.GetBytes(entry.Mode + " "));
            stream.Write(Encoding.UTF8.GetBytes(entry.Name));
            stream.WriteByte(0);
            stream.Write(entry.Id.Bytes);
        }
        return stream.ToArray();
    }
}

/// <summary>Um commit lido do repositório.</summary>
public sealed record CommitInfo(ObjectId Id, ObjectId Tree, IReadOnlyList<ObjectId> Parents, GitSignature Author, GitSignature Committer, string Message)
{
    public string Summary => Message.Split('\n', 2)[0].Trim();

    public static CommitInfo Parse(ObjectId id, byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        var split = text.IndexOf("\n\n", StringComparison.Ordinal);
        var header = split < 0 ? text : text[..split];
        var message = split < 0 ? "" : text[(split + 2)..];
        ObjectId tree = default;
        var parents = new List<ObjectId>();
        GitSignature author = new("?", "?", DateTimeOffset.UnixEpoch), committer = author;
        foreach (var line in header.Split('\n'))
        {
            if (line.StartsWith("tree ", StringComparison.Ordinal)) tree = ObjectId.Parse(line[5..]);
            else if (line.StartsWith("parent ", StringComparison.Ordinal)) parents.Add(ObjectId.Parse(line[7..]));
            else if (line.StartsWith("author ", StringComparison.Ordinal)) author = GitSignature.Parse(line[7..]);
            else if (line.StartsWith("committer ", StringComparison.Ordinal)) committer = GitSignature.Parse(line[10..]);
        }
        return new CommitInfo(id, tree, parents, author, committer, message.TrimEnd('\n'));
    }

    public static byte[] Serialize(ObjectId tree, IEnumerable<ObjectId> parents, GitSignature author, GitSignature committer, string message)
    {
        var builder = new StringBuilder();
        builder.Append("tree ").Append(tree.Hex).Append('\n');
        foreach (var parent in parents) builder.Append("parent ").Append(parent.Hex).Append('\n');
        builder.Append("author ").Append(author.Format()).Append('\n');
        builder.Append("committer ").Append(committer.Format()).Append("\n\n");
        builder.Append(message.TrimEnd('\n')).Append('\n');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}

/// <summary>Entrada do índice (área de preparação).</summary>
public sealed record IndexEntry(string Path, string Mode, ObjectId Id, long Size, long ModifiedSeconds, int ModifiedNanos);

/// <summary>Índice do Git (versão 2): lista ordenada de arquivos preparados para o próximo commit.</summary>
public sealed class GitIndex
{
    private readonly SortedDictionary<string, IndexEntry> _entries = new(StringComparer.Ordinal);

    public IReadOnlyCollection<IndexEntry> Entries => _entries.Values;

    public IndexEntry? Get(string path) => _entries.GetValueOrDefault(path);

    public void Set(IndexEntry entry) => _entries[entry.Path] = entry;

    public bool Remove(string path) => _entries.Remove(path);

    public void Clear() => _entries.Clear();

    public static GitIndex Load(string path)
    {
        var index = new GitIndex();
        if (!File.Exists(path)) return index;
        var data = File.ReadAllBytes(path);
        if (data.Length < 32 || data[0] != 'D' || data[1] != 'I' || data[2] != 'R' || data[3] != 'C') throw new GitException("Índice do Git inválido.");
        var version = ReadInt(data, 4);
        if (version != 2) throw new GitException($"Índice do Git versão {version} não é suportado (use a versão 2).");
        var count = ReadInt(data, 8);
        var position = 12;
        for (var i = 0; i < count; i++)
        {
            var start = position;
            var seconds = (uint)ReadInt(data, position);
            var nanos = ReadInt(data, position + 4);
            var mode = ReadInt(data, position + 24);
            var size = (uint)ReadInt(data, position + 36);
            var id = new ObjectId(data.AsSpan(position + 40, 20));
            var flags = data[position + 60] << 8 | data[position + 61];
            var nameLength = flags & 0xFFF;
            position += 62;
            if ((flags & 0x4000) != 0) position += 2; // flags estendidas (v3)
            int end;
            if (nameLength < 0xFFF) end = position + nameLength;
            else end = Array.IndexOf(data, (byte)0, position);
            var name = Encoding.UTF8.GetString(data, position, end - position);
            position = end + 1;
            while ((position - start) % 8 != 0) position++;
            index._entries[name] = new IndexEntry(name, ModeString(mode), id, size, seconds, nanos);
        }
        return index;
    }

    public void Save(string path)
    {
        using var stream = new MemoryStream();
        stream.Write("DIRC"u8);
        WriteInt(stream, 2);
        WriteInt(stream, _entries.Count);
        foreach (var entry in _entries.Values)
        {
            var start = stream.Position;
            WriteInt(stream, (int)entry.ModifiedSeconds);
            WriteInt(stream, entry.ModifiedNanos);
            WriteInt(stream, (int)entry.ModifiedSeconds);
            WriteInt(stream, entry.ModifiedNanos);
            WriteInt(stream, 0); // dev
            WriteInt(stream, 0); // ino
            WriteInt(stream, ModeNumber(entry.Mode));
            WriteInt(stream, 0); // uid
            WriteInt(stream, 0); // gid
            WriteInt(stream, (int)entry.Size);
            stream.Write(entry.Id.Bytes);
            var name = Encoding.UTF8.GetBytes(entry.Path);
            var flags = Math.Min(name.Length, 0xFFF);
            stream.WriteByte((byte)(flags >> 8));
            stream.WriteByte((byte)flags);
            stream.Write(name);
            stream.WriteByte(0);
            while ((stream.Position - start) % 8 != 0) stream.WriteByte(0);
        }
        var body = stream.ToArray();
        var checksum = System.Security.Cryptography.SHA1.HashData(body);
        var temporary = path + ".lock";
        using (var file = File.Create(temporary))
        {
            file.Write(body);
            file.Write(checksum);
        }
        File.Move(temporary, path, overwrite: true);
    }

    private static int ReadInt(byte[] data, int position) => data[position] << 24 | data[position + 1] << 16 | data[position + 2] << 8 | data[position + 3];

    private static void WriteInt(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static string ModeString(int mode) => (mode & 0xF000) switch
    {
        0xA000 => "120000",
        0xE000 => "160000",
        _ => (mode & 0x49) != 0 ? "100755" : "100644",
    };

    private static int ModeNumber(string mode) => mode switch
    {
        "100755" => 0x81ED,
        "120000" => 0xA000,
        "160000" => 0xE000,
        _ => 0x81A4,
    };
}
