using System.IO.Compression;
using System.Security.Cryptography;

namespace Lunet.Core;

/// <summary>A revision of the Projects tree. Private app settings and secrets live outside this tree.</summary>
public static class ProjectFolderSnapshot
{
    public static string Capture(string source, string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var entries = Inventory(source).ToArray();
        foreach (var (path, relative, directory) in entries)
        {
            var entry = archive.CreateEntry(directory ? relative + "/" : relative, CompressionLevel.NoCompression);
            entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            if (directory) continue;
            // Tooling depends on Git metadata. Embedded URL credentials must be removed by the user
            // before copying that repository beyond Android's app-private storage.
            if (relative.EndsWith("/.git/config", StringComparison.Ordinal))
            {
                var config = File.ReadAllBytes(path);
                foreach (var line in System.Text.Encoding.UTF8.GetString(config).Split('\n'))
                {
                    var separator = line.IndexOf('=');
                    if (separator < 0) continue;
                    var value = line[(separator + 1)..].Trim().Trim('"');
                    if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
                        (uri.UserInfo.Length != 0 || uri.Query.Length != 0))
                        throw new IOException("Remova credenciais embutidas no endereço remoto do Git antes de copiar os projetos.");
                }
                using var configOutput = entry.Open();
                configOutput.Write(config);
                continue;
            }
            using var input = File.OpenRead(path);
            using var output = entry.Open();
            input.CopyTo(output);
        }

        archive.Dispose();
        using var captured = ZipFile.OpenRead(archivePath);
        if (!Inventory(source).Select(item => item.Directory ? item.Relative + "/" : item.Relative)
                .SequenceEqual(captured.Entries.Select(entry => entry.FullName), StringComparer.Ordinal))
            throw new IOException("Os projetos mudaram durante a cópia. Tente de novo após concluir a edição.");

        foreach (var (path, relative, directory) in entries)
        {
            if (directory) continue;
            using var input = File.OpenRead(path);
            using var output = captured.GetEntry(relative)!.Open();
            if (!SHA256.HashData(input).AsSpan().SequenceEqual(SHA256.HashData(output)))
                throw new IOException("Um arquivo mudou durante a cópia. Tente de novo após concluir a edição.");
        }
        return Digest(archivePath);
    }

    public static void Restore(string archivePath, string expectedDigest, string destination)
    {
        if (Directory.Exists(destination))
            throw new IOException("A área de recuperação já existe.");
        if (!string.Equals(Digest(archivePath), expectedDigest, StringComparison.Ordinal))
            throw new InvalidDataException("A revisão está incompleta ou foi alterada fora do Lunet.");
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
            ValidateRelative(entry.FullName.TrimEnd('/'));
        Directory.CreateDirectory(destination);
        archive.ExtractToDirectory(destination);
    }

    public static string Digest(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static IEnumerable<(string Path, string Relative, bool Directory)> Inventory(string root, string prefix = "")
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal))
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Há um link simbólico na pasta de projetos.");
            var relative = prefix + Path.GetFileName(path);
            ValidateRelative(relative);
            bool directory = attributes.HasFlag(FileAttributes.Directory);
            yield return (path, relative, directory);
            if (directory)
                foreach (var item in Inventory(path, relative + "/"))
                    yield return item;
        }
    }

    private static void ValidateRelative(string path)
    {
        if (path.Contains('\\') || path.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("A revisão contém um caminho inválido.");
    }
}
