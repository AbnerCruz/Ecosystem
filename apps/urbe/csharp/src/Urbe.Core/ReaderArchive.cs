using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>Read-only container boundary; no member is ever extracted into a filesystem directory.</summary>
public sealed class ReaderArchive : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> Paths => _entries.Keys;
    public ReaderArchive(byte[] bytes)
    {
        _zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        try
        {
            long total = 0;
            foreach (var entry in _zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                string name = entry.FullName;
                if (name.StartsWith('/') || name.Contains('\\') || name.Contains(':') || name.Split('/').Any(p => p is ".." or "." or "") || !_entries.TryAdd(name, entry))
                    throw new InvalidDataException("O pacote contém caminhos inválidos ou duplicados.");
                total = checked(total + entry.Length);
                if (total > 512L * 1024 * 1024 || entry.Length > 128L * 1024 * 1024 ||
                    entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 10000)
                    throw new InvalidDataException("O pacote excede o orçamento seguro de descompressão deste leitor.");
            }
        }
        catch { _zip.Dispose(); throw; }
    }

    public byte[] Bytes(string path)
    {
        if (!_entries.TryGetValue(path, out var entry)) throw new InvalidDataException("Recurso ausente no pacote: " + path);
        using var stream = entry.Open();
        // never trust the declared size: read exactly it, and a member that inflates past it is refused
        var output = new byte[checked((int)entry.Length)];
        try { stream.ReadExactly(output); }
        catch (EndOfStreamException) { throw new InvalidDataException("Recurso incompleto no pacote."); }
        if (stream.ReadByte() != -1) throw new InvalidDataException("O pacote declara um tamanho menor do que o conteúdo descompactado.");
        return output;
    }
    public string Text(string path) => new UTF8Encoding(false, true).GetString(Bytes(path)).TrimStart('\ufeff');
    public XDocument Xml(string path)
    {
        using var input = new MemoryStream(Bytes(path), writable: false);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128L * 1024 * 1024 });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    public static string? Resolve(string from, string relative)
    {
        var target = relative.Split('#', 2)[0];
        if (target.Length == 0) return from;
        if (target.StartsWith('/') || target.Contains('\\') || target.Contains(':') || target.Contains('?')) return null;
        target = Uri.UnescapeDataString(target);
        if (target.StartsWith('/') || target.Contains('\\') || target.Contains(':') || target.Contains('?')) return null;
        var parts = from.Split('/').SkipLast(1).ToList();
        foreach (var part in target.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        return string.Join('/', parts);
    }
    public bool Contains(string path) => _entries.ContainsKey(path);
    public void Dispose() => _zip.Dispose();
}
