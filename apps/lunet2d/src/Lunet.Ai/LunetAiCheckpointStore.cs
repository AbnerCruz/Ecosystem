using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lunet.Core;

namespace Lunet.Ai;

/// <summary>A recovery point retained locally, even after the chat or Android process is closed.</summary>
public sealed record LunetAiRecoveryPoint(string Id, DateTime CreatedUtc, IReadOnlyList<string> Paths);

/// <summary>
/// Write-ahead undo journal for AI runs. Original bytes are durable BEFORE a tool may write.
/// It is deliberately independent from Git and excluded from project exports by .lunet.
/// </summary>
internal sealed class LunetAiCheckpointStore
{
    private sealed record Entry(string Path, bool Existed, string? LastAiHash);
    private sealed record Index(string Id, DateTime CreatedUtc, List<Entry> Files);

    private readonly LunetProject _project;
    private readonly string _directory;
    private readonly object _gate = new();
    private Index _index;

    public string Id => _index.Id;
    public IReadOnlyList<string> Paths { get { lock (_gate) return _index.Files.Select(x => x.Path).ToArray(); } }
    public bool IsEmpty { get { lock (_gate) return _index.Files.Count == 0; } }

    public LunetAiCheckpointStore(LunetProject project, string? id = null, bool existing = false)
    {
        _project = project;
        id ??= Guid.NewGuid().ToString("N");
        if (id.Length != 32 || id.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("ID de checkpoint inválido.", nameof(id));
        _directory = Path.Combine(project.Directory, ".lunet", "ai-checkpoints", id);
        if (existing)
        {
            _index = ReadIndex(Path.Combine(_directory, "index.json"))
                ?? throw new FileNotFoundException("Checkpoint indisponível.");
            if (_index.Id != id) throw new InvalidDataException("Checkpoint inconsistente.");
        }
        else
        {
            if (File.Exists(Path.Combine(_directory, "index.json")))
                throw new IOException("O checkpoint já existe.");
            _index = new Index(id, DateTime.UtcNow, []);
        }
    }

    public static IReadOnlyList<LunetAiRecoveryPoint> List(LunetProject project)
    {
        var root = Path.Combine(project.Directory, ".lunet", "ai-checkpoints");
        if (!Directory.Exists(root)) return [];
        var points = new List<LunetAiRecoveryPoint>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                var index = ReadIndex(Path.Combine(directory, "index.json"));
                if (index is null || Path.GetFileName(directory) != index.Id || index.Files.Count == 0) continue;
                points.Add(new LunetAiRecoveryPoint(index.Id, index.CreatedUtc,
                    index.Files.Select(file => file.Path).ToArray()));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // One damaged checkpoint cannot hide the remaining recoverable versions.
            }
        }
        return points.OrderByDescending(p => p.CreatedUtc).ToArray();
    }

    public void Capture(string relativePath)
    {
        var path = ValidatePath(relativePath);
        lock (_gate)
        {
            if (_index.Files.Any(e => e.Path == path)) return;
            var absolute = Resolve(path);
            var existed = File.Exists(absolute);
            Directory.CreateDirectory(_directory);
            if (existed)
            {
                // A disk failure here must STOP the write, not silently discard undo history.
                WriteAtomic(Backup(path), File.ReadAllBytes(absolute));
            }
            var old = _index;
            _index = old with { Files = [.. old.Files, new Entry(path, existed, null)] };
            try { SaveIndex(); }
            catch { _index = old; throw; }
        }
    }

    /// <summary>Called immediately after each tool write, including partially failed multi-file tools.</summary>
    public void RecordAfter(string relativePath)
    {
        var path = ValidatePath(relativePath);
        lock (_gate)
        {
            var index = _index.Files.FindIndex(e => e.Path == path);
            if (index < 0) throw new InvalidOperationException("Edição sem checkpoint: " + path);
            var original = _index.Files[index];
            var absolute = Resolve(path);
            var hash = File.Exists(absolute) ? Hash(File.ReadAllBytes(absolute)) : null;
            var updated = _index.Files.ToList();
            updated[index] = original with { LastAiHash = hash };
            _index = _index with { Files = updated };
            SaveIndex();
        }
    }

    public IReadOnlyList<LunetAiChange> Changes()
    {
        lock (_gate)
        {
            var result = new List<LunetAiChange>();
            foreach (var entry in _index.Files)
            {
                var before = entry.Existed ? File.ReadAllBytes(Backup(entry.Path)) : null;
                var full = Resolve(entry.Path);
                var after = File.Exists(full) ? File.ReadAllBytes(full) : null;
                if (before is null && after is null) continue;
                if (before is not null && after is not null && before.AsSpan().SequenceEqual(after)) continue;
                bool text = new[] { ".cs", ".json", ".md", ".txt", ".xml", ".yml", ".yaml", ".csv", ".glsl", ".hlsl", ".shader", ".csproj", ".slnx" }
                    .Contains(Path.GetExtension(entry.Path), StringComparer.OrdinalIgnoreCase);
                string? Show(byte[]? bytes) => bytes is null ? null
                    : text ? Encoding.UTF8.GetString(bytes) : "[arquivo binário · " + bytes.Length + " bytes]";
                result.Add(new LunetAiChange(entry.Path, Show(before), Show(after) ?? ""));
            }
            return result;
        }
    }

    /// <summary>
    /// Restores only if the file still has the bytes last written by this AI run.
    /// A user's later edits are never overwritten. The backup is intentionally retained.
    /// </summary>
    public void Revert(string? onlyPath = null)
    {
        lock (_gate)
        {
            var targets = _index.Files.Where(e => onlyPath is null || e.Path == ValidatePath(onlyPath)).ToArray();
            var changed = new List<(Entry Entry, string Absolute, byte[]? Before)>();
            // Preflight ALL conflicts before mutating ANY file.
            foreach (var entry in targets)
            {
                var absolute = Resolve(entry.Path);
                var before = entry.Existed ? File.ReadAllBytes(Backup(entry.Path)) : null;
                var current = File.Exists(absolute) ? File.ReadAllBytes(absolute) : null;
                if ((before is null && current is null) ||
                    (before is not null && current is not null && before.AsSpan().SequenceEqual(current))) continue;
                var hash = current is null ? null : Hash(current);
                if (entry.LastAiHash is null || !string.Equals(hash, entry.LastAiHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("O arquivo " + entry.Path +
                        " mudou após a IA, ou a gravação foi interrompida. A versão anterior está preservada; não será sobrescrita automaticamente.");
                changed.Add((entry, absolute, before));
            }
            foreach (var item in changed)
            {
                if (item.Before is null) File.Delete(item.Absolute);
                else WriteAtomic(item.Absolute, item.Before);
            }
        }
    }

    /// <summary>Safe recovery fallback when a later user edit conflicts with the AI run.</summary>
    public string SaveOriginalAsCopy(string relativePath)
    {
        var path = ValidatePath(relativePath);
        lock (_gate)
        {
            var entry = _index.Files.FirstOrDefault(e => e.Path == path)
                ?? throw new FileNotFoundException("O arquivo não faz parte deste checkpoint.");
            if (!entry.Existed) throw new InvalidOperationException("Este arquivo foi criado pela IA; não existe versão anterior.");
            var name = Path.GetFileName(path).Replace(".", "_", StringComparison.Ordinal);
            var destination = "Notes/Recovery/" + name + "-" + Id[..8] + ".txt";
            _project.CreateFile(destination, Encoding.UTF8.GetString(File.ReadAllBytes(Backup(path))));
            return destination;
        }
    }

    private string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') ||
            path.Contains(':') || path.Split('/').Any(s => s is "" or "." or ".." || s.StartsWith('.')))
            throw new ArgumentException("Caminho de arquivo inválido.", nameof(path));
        _project.EnsureInsideProject(path);
        return path;
    }

    private string Resolve(string path)
    {
        var current = _project.Directory;
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Checkpoint não acompanha links simbólicos.");
        }
        return current;
    }

    private string Backup(string path)
        => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))) + ".bak");

    private void SaveIndex() => WriteAtomic(Path.Combine(_directory, "index.json"), JsonSerializer.SerializeToUtf8Bytes(_index));

    private static Index? ReadIndex(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<Index>(File.ReadAllBytes(path)) : null;

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}
