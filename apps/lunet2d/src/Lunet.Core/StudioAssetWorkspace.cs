using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lunet.Core;

/// <summary>
/// Local editable drafts for the standalone Studios. No game/project identity is created.
/// Final assets are published by ReusableAssetLibrary.PublishWorkspace, then copied to
/// chosen projects with existing verified library import; the draft is never a live link.
/// </summary>
public sealed class StudioAssetWorkspace
{
    private static readonly object BundleGate = new();
    private sealed record BundleFile(string Relative, string? Before, string After);
    public const string RecoveryFolder = ".lunet/studio-recovery";
    private string RecoveryDirectory => Path.Combine(RootDirectory, RecoveryFolder);
    private const string StagePrefix = "stage-";
    public StudioAssetWorkspace(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);
        lock (BundleGate)
        {
            RejectLinkedRecoveryRoot();
            if (!Directory.Exists(RecoveryDirectory)) return;
            foreach (var stage in Directory.EnumerateDirectories(RecoveryDirectory, StagePrefix + "*"))
            {
                if (!Guid.TryParseExact(Path.GetFileName(stage)[StagePrefix.Length..], "N", out _) ||
                    File.GetAttributes(stage).HasFlag(FileAttributes.ReparsePoint)) continue;
                Recover(stage);
            }
        }
    }

    public string RootDirectory { get; }

    public IReadOnlyList<string> ListFiles() =>
        new[] { "Assets", "Content" }.Select(directory => Path.Combine(RootDirectory, directory))
            .Where(Directory.Exists).SelectMany(folder => Directory.EnumerateFiles(folder, "*", new EnumerationOptions
            { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            .Select(p => Path.GetRelativePath(RootDirectory, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    public string ReadText(string relativePath) => File.ReadAllText(Resolve(relativePath));

    public byte[] ReadBytes(string relativePath) => File.ReadAllBytes(Resolve(relativePath));

    public byte[] ReadBytes(string relativePath, int maxBytes)
    {
        using var file = File.OpenRead(Resolve(relativePath));
        if (file.Length > maxBytes)
            throw new InvalidDataException($"O arquivo ultrapassa o tamanho máximo de {maxBytes / (1024 * 1024)} MiB.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        return bytes;
    }

    public void WriteText(string relativePath, string value) =>
        WriteBytes(relativePath, System.Text.Encoding.UTF8.GetBytes(value));

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> bytes)
    {
        string target = Resolve(relativePath);
        string parent = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes.ToArray());
            File.Move(temp, target, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

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
            var staging = Path.Combine(RecoveryDirectory, StagePrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            string journal = Path.Combine(staging, "journal.json");
            try
            {
                var prepared = new List<BundleFile>();
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    File.WriteAllBytes(Path.Combine(staging, i + ".next"), entry.Bytes);
                    string? before = null;
                    if (File.Exists(entry.Target))
                    {
                        var backup = Path.Combine(staging, i + ".before");
                        File.Copy(entry.Target, backup);
                        before = HashFile(backup);
                    }
                    prepared.Add(new(entry.Path, before, Convert.ToHexString(SHA256.HashData(entry.Bytes))));
                }

                AtomicFile.WriteAllText(journal, JsonSerializer.Serialize(prepared));
                for (int i = 0; i < entries.Length; i++)
                {
                    var target = Resolve(entries[i].Path);
                    if (CurrentHash(target) != prepared[i].Before)
                        throw new IOException("Outro editor alterou " + entries[i].Path + " durante a gravação.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(Path.Combine(staging, i + ".next"), target, overwrite: true);
                }
                File.WriteAllText(Path.Combine(staging, "committed"), "");
            }
            finally { Recover(staging); }
        }
    }

    private void RejectLinkedRecoveryRoot()
    {
        foreach (var directory in new[] { Path.Combine(RootDirectory, ".lunet"), RecoveryDirectory })
            if (Directory.Exists(directory) && File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("A recuperação do Studio não usa links.");
    }

    private void Recover(string staging)
    {
        var journal = Path.Combine(staging, "journal.json");
        if (!File.Exists(Path.Combine(staging, "committed")) && File.Exists(journal))
        {
            var entries = JsonSerializer.Deserialize<BundleFile[]>(File.ReadAllText(journal))
                ?? throw new InvalidDataException("Registro de recuperação do Studio inválido.");
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var item = entries[i];
                // A staged output that still exists was never committed.
                if (File.Exists(Path.Combine(staging, i + ".next"))) continue;
                var target = Resolve(item.Relative);
                if (CurrentHash(target) != item.After) continue;
                var backup = Path.Combine(staging, i + ".before");
                if (item.Before is null) File.Delete(target);
                else
                {
                    if (CurrentHash(backup) != item.Before) throw new InvalidDataException("Backup do Studio inconsistente.");
                    File.Move(backup, target, overwrite: true);
                }
            }
        }
        Directory.Delete(staging, recursive: true);
    }

    private static string? CurrentHash(string path) => File.Exists(path) ? HashFile(path) : null;

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

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
        var cursor = RootDirectory;
        foreach (var part in parts)
        {
            cursor = Path.Combine(cursor, part);
            if ((File.Exists(cursor) || Directory.Exists(cursor)) &&
                File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("O Studio não grava através de links.");
        }
        string target = Path.GetFullPath(cursor);
        if (!target.StartsWith(RootDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            throw new ArgumentException("Destino fora do Studio.", nameof(relative));
        return target;
    }
}
