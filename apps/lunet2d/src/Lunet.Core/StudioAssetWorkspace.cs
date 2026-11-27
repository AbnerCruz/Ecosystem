using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lunet.Core;

/// <summary>
/// Where the Studios read and write: a game's folder (its <see cref="LunetProject.Files"/>, edited directly) or the
/// standalone Studios' drafts. Every multi-file save is journaled (<see cref="WriteBundle"/>): prepared first, then
/// replaced, and rolled back on the next opening if the app stopped in the middle. Disk-only protections (no writing
/// through links) apply where there is a disk folder.
/// </summary>
public sealed class StudioAssetWorkspace
{
    private static readonly object BundleGate = new();
    private sealed record BundleFile(string Relative, string? Before, string After);
    public const string RecoveryFolder = ".lunet/studio-recovery";
    private const string StagePrefix = "stage-";
    private readonly IProjectFiles _files;

    public StudioAssetWorkspace(string rootDirectory) : this(CreateDisk(rootDirectory)) { }

    private static IProjectFiles CreateDisk(string rootDirectory)
    {
        var full = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(full);
        return new DiskProjectFiles(full);
    }

    /// <summary>The Studios over a game's own files (no copy): what they save is in the game's folder.</summary>
    public StudioAssetWorkspace(IProjectFiles files)
    {
        _files = files;
        lock (BundleGate)
        {
            RejectLinkedRecoveryRoot();
            foreach (var stage in _files.List(RecoveryFolder).Where(e => e.IsDirectory && e.Name.StartsWith(StagePrefix, StringComparison.Ordinal)))
            {
                if (!Guid.TryParseExact(stage.Name[StagePrefix.Length..], "N", out _) || IsLink(RecoveryFolder + "/" + stage.Name)) continue;
                Recover(RecoveryFolder + "/" + stage.Name);
            }
        }
    }

    /// <summary>The folder on this device's disk (desktop, tests); empty when the folder is reached only through SAF.</summary>
    public string RootDirectory => _files.LocalDirectory ?? "";

    /// <summary>Where the files are (for messages).</summary>
    public string Location => _files.Location;

    public IReadOnlyList<string> ListFiles()
    {
        var result = new List<string>();
        foreach (var root in new[] { "Assets", "Content" }) Walk(root, result);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private void Walk(string folder, List<string> result)
    {
        if (IsLink(folder)) return;
        foreach (var entry in _files.List(folder))
        {
            var path = folder + "/" + entry.Name;
            if (entry.IsDirectory) Walk(path, result);
            else if (!IsLink(path)) result.Add(path);
        }
    }

    public bool Exists(string relativePath) => _files.FileExists(Resolve(relativePath));

    public string ReadText(string relativePath)
    {
        using var reader = new StreamReader(new MemoryStream(ReadBytes(relativePath)), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public byte[] ReadBytes(string relativePath) => _files.Read(Resolve(relativePath));

    public byte[] ReadBytes(string relativePath, int maxBytes)
    {
        var bytes = ReadBytes(relativePath);
        if (bytes.Length > maxBytes)
            throw new InvalidDataException($"O arquivo ultrapassa o tamanho máximo de {maxBytes / (1024 * 1024)} MiB.");
        return bytes;
    }

    public void WriteText(string relativePath, string value) =>
        WriteBytes(relativePath, Encoding.UTF8.GetBytes(value));

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> bytes) => _files.Write(Resolve(relativePath), bytes.ToArray());

    /// <summary>Prepare every output before replacing files. If commit fails, restore the previous
    /// files that still contain this operation's bytes. Publication commits its own immutable directory.</summary>
    public void WriteBundle(params (string Path, byte[] Bytes)[] files)
    {
        lock (BundleGate)
        {
            var entries = files.Select(file => (file.Path, Target: Resolve(file.Path), file.Bytes)).ToArray();
            if (entries.Select(entry => entry.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
                throw new ArgumentException("Arquivos duplicados no pacote.");

            RejectLinkedRecoveryRoot();
            var staging = RecoveryFolder + "/" + StagePrefix + Guid.NewGuid().ToString("N");
            _files.CreateDirectory(staging);
            var journal = staging + "/journal.json";
            try
            {
                var prepared = new List<BundleFile>();
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    _files.Write(staging + "/" + i + ".next", entry.Bytes);
                    string? before = null;
                    if (_files.FileExists(entry.Target))
                    {
                        var previous = _files.Read(entry.Target);
                        _files.Write(staging + "/" + i + ".before", previous);
                        before = Hash(previous);
                    }
                    prepared.Add(new(entry.Path, before, Hash(entry.Bytes)));
                }

                _files.Write(journal, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(prepared)));
                for (int i = 0; i < entries.Length; i++)
                {
                    var target = entries[i].Target;
                    if (CurrentHash(target) != prepared[i].Before)
                        throw new IOException("Outro editor alterou " + entries[i].Path + " durante a gravação.");
                    _files.Write(target, entries[i].Bytes);
                    // Gone .next = this output was written (the recovery's "committed" mark per file).
                    _files.Delete(staging + "/" + i + ".next");
                }
                _files.Write(staging + "/committed", []);
            }
            finally { Recover(staging); }
        }
    }

    private void RejectLinkedRecoveryRoot()
    {
        if (IsLink(".lunet") || IsLink(RecoveryFolder)) throw new IOException("A recuperação do Studio não usa links.");
    }

    /// <summary>A symbolic link or junction on the disk (never followed by the Studios); SAF has none.</summary>
    private bool IsLink(string relative)
    {
        if (_files.LocalDirectory is not { } root) return false;
        var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return (File.Exists(full) || Directory.Exists(full)) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint);
    }

    private void Recover(string staging)
    {
        var journal = staging + "/journal.json";
        if (!_files.FileExists(staging + "/committed") && _files.FileExists(journal))
        {
            var entries = JsonSerializer.Deserialize<BundleFile[]>(Encoding.UTF8.GetString(_files.Read(journal)))
                ?? throw new InvalidDataException("Registro de recuperação do Studio inválido.");
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var item = entries[i];
                var target = Resolve(item.Relative);
                // Only an output that reached its file is rolled back: its content is this operation's (a staged .next
                // that still exists may still have been written, if the app stopped between writing and marking it).
                if (CurrentHash(target) != item.After) continue;
                var backup = staging + "/" + i + ".before";
                if (item.Before is null) _files.Delete(target);
                else
                {
                    var previous = _files.FileExists(backup) ? _files.Read(backup) : null;
                    if (previous is null || Hash(previous) != item.Before) throw new InvalidDataException("Backup do Studio inconsistente.");
                    _files.Write(target, previous);
                }
            }
        }
        _files.Delete(staging);
    }

    private string? CurrentHash(string path) => _files.FileExists(path) ? Hash(_files.Read(path)) : null;

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public void WriteTextPair(string first, string firstText, string second, string secondText) =>
        WriteBundle((first, Encoding.UTF8.GetBytes(firstText)), (second, Encoding.UTF8.GetBytes(secondText)));

    /// <summary>A unique generated name; assets never overwrite an older draft implicitly.</summary>
    public string NewPath(string directory, string prefix, string extension)
    {
        if (directory is not ("Assets" or "Content/Audio" or "Content/Textures" or "Content/Data" or "Content/Effects"))
            throw new ArgumentException("Área de trabalho inválida.", nameof(directory));
        if (prefix.Length == 0 || prefix.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Prefixo inválido.", nameof(prefix));
        if (!extension.StartsWith('.') || extension.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.'))
            throw new ArgumentException("Extensão inválida.", nameof(extension));
        return directory + "/" + prefix + "-" + Guid.NewGuid().ToString("N")[..8] + extension;
    }

    /// <summary>A path inside Assets/ or Content/ (checked part by part; never through a link on the disk).</summary>
    private string Resolve(string relative)
    {
        if (string.IsNullOrEmpty(relative) || relative.StartsWith('/') || relative.Contains('\\') ||
            relative.Contains(':') || relative.Any(char.IsControl))
            throw new ArgumentException("Caminho inválido.", nameof(relative));
        var parts = relative.Split('/');
        if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.')))
            throw new ArgumentException("Caminho inválido.", nameof(relative));
        if (parts[0] != "Assets" && parts[0] != "Content")
            throw new ArgumentException("Os Studios salvam em Assets/ ou Content/.", nameof(relative));
        var cursor = "";
        foreach (var part in parts)
        {
            cursor = cursor.Length == 0 ? part : cursor + "/" + part;
            if (IsLink(cursor)) throw new IOException("O Studio não grava através de links.");
        }
        return relative;
    }
}
