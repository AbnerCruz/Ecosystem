using System.Security.Cryptography;

namespace Lunet.Core;

/// <summary>
/// Local editable drafts for the standalone Studios. No game/project identity is created.
/// Final assets are published by ReusableAssetLibrary.PublishWorkspace, then copied to
/// chosen projects with existing verified library import; the draft is never a live link.
/// </summary>
public sealed class StudioAssetWorkspace
{
    public StudioAssetWorkspace(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public IReadOnlyList<string> ListFiles() =>
        Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(RootDirectory, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    public string ReadText(string relativePath) => File.ReadAllText(Resolve(relativePath));

    public byte[] ReadBytes(string relativePath) => File.ReadAllBytes(Resolve(relativePath));

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

    /// <summary>A unique generated name; assets never overwrite an older draft implicitly.</summary>
    public string NewPath(string directory, string prefix, string extension)
    {
        if (directory is not ("Assets" or "Content/Audio" or "Content/Textures" or "Content/Data"))
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
