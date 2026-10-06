using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;

namespace Urbe.Core;

public sealed class VaultArchiveExportOptions
{
    public string? AppVersion { get; init; }
    public string? VaultName { get; init; }
    public int? VaultFormatVersion { get; init; }
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
    public VaultExportState? State { get; init; }
}

public sealed class VaultArchiveExportResult
{
    private readonly byte[] _bytes;

    internal VaultArchiveExportResult(
        byte[] bytes,
        VaultExportManifest manifest,
        IReadOnlyList<VaultFile> files)
    {
        _bytes = (byte[])bytes.Clone();
        Manifest = manifest;
        Files = files;
    }

    public ReadOnlyMemory<byte> Bytes => _bytes;
    public VaultExportManifest Manifest { get; }
    public IReadOnlyList<VaultFile> Files { get; }
}

public sealed class VaultArchiveImportResult
{
    internal VaultArchiveImportResult(
        IReadOnlyDictionary<string, VaultFile> files,
        VaultExportManifestState manifestState,
        VaultExportManifest? manifest,
        VaultExportVerification? verification,
        string? rootPrefix)
    {
        Files = files;
        ManifestState = manifestState;
        Manifest = manifest;
        Verification = verification;
        RootPrefix = rootPrefix;
    }

    public IReadOnlyDictionary<string, VaultFile> Files { get; }
    public VaultExportManifestState ManifestState { get; }
    public VaultExportManifest? Manifest { get; }
    public VaultExportVerification? Verification { get; }
    public string? RootPrefix { get; }
    public bool IsUrbeExport => ManifestState == VaultExportManifestState.Current;
}

public static class VaultArchive
{
    public const string LegacyJournalPath = ".urbe/journal.json";
    public const string CurrentJournalPath = ".urbe/journal.v2.json";

    public static VaultArchiveExportResult Export(
        IEnumerable<VaultFile> source,
        VaultArchiveExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new VaultArchiveExportOptions();

        var normalized = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in source)
        {
            ArgumentNullException.ThrowIfNull(file);
            var path = NormalizeArchivePath(file.Path);

            if (string.Equals(path, VaultExportManifest.FileName, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "O nome " + VaultExportManifest.FileName +
                    " é reservado para o manifesto do export.");

            if (IsJournal(path))
                continue;

            if (!normalized.TryAdd(path, new VaultFile(path, file.Bytes.ToArray())))
                throw new InvalidDataException("Caminho duplicado no vault: " + path);
        }

        var exportedFiles = normalized.Values
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        var manifest = VaultExportManifestCodec.Build(
            exportedFiles,
            options.AppVersion,
            options.Now,
            options.VaultName,
            options.VaultFormatVersion,
            options.State);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in exportedFiles)
            {
                var entry = zip.CreateEntry(file.Path, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(file.Bytes.Span);
            }

            var manifestEntry = zip.CreateEntry(
                VaultExportManifest.FileName,
                CompressionLevel.Optimal);
            using var manifestStream = manifestEntry.Open();
            var manifestBytes = VaultExportManifestCodec.Serialize(manifest);
            manifestStream.Write(manifestBytes);
        }

        return new VaultArchiveExportResult(
            output.ToArray(),
            manifest,
            Array.AsReadOnly(exportedFiles));
    }

    public static VaultArchiveImportResult Import(ReadOnlyMemory<byte> bytes, ImportLimits? limits = null)
    {
        limits ??= new ImportLimits();
        if (bytes.Length > limits.TotalBytes) throw new InvalidDataException("ZIP excede o limite seguro.");
        if (bytes.Length == 0)
            throw new InvalidDataException("ZIP vazio.");

        using var input = new MemoryStream(bytes.ToArray(), writable: false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);

        var entries = zip.Entries
            .Where(entry =>
                !string.IsNullOrEmpty(entry.Name) &&
                !entry.FullName.Contains("__MACOSX", StringComparison.Ordinal))
            .ToArray();

        if (entries.Length > limits.FileCount || entries.Sum(e => e.Length) > limits.TotalBytes || entries.Any(e => e.Length > limits.FileBytes))
            throw new InvalidDataException("ZIP excede o limite seguro de importação.");

        var rawNames = entries
            .Select(entry => NormalizeZipEntryName(entry.FullName))
            .ToArray();

        // Valide o caminho bruto antes de remover um possível diretório-raiz.
        // Caso contrário "../evil.md" poderia ser confundido com um ZIP
        // embrulhado na pasta ".." e escapar da proteção de traversal.
        foreach (var rawName in rawNames)
            _ = NormalizeArchivePath(rawName);

        var rootPrefix = DetectRootPrefix(rawNames);
        var files = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < entries.Length; i++)
        {
            var rawName = rawNames[i];
            var relative = rootPrefix is not null &&
                           rawName.StartsWith(rootPrefix, StringComparison.Ordinal)
                ? rawName[rootPrefix.Length..]
                : rawName;

            var path = NormalizeArchivePath(relative);
            using var stream = entries[i].Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(chunk)) != 0)
            {
                if (buffer.Length + read > limits.FileBytes || buffer.Length + read > entries[i].Length)
                    throw new InvalidDataException("ZIP expandido excede o tamanho declarado ou o limite seguro.");
                buffer.Write(chunk, 0, read);
            }

            if (!files.TryAdd(path, new VaultFile(path, buffer.ToArray())))
                throw new InvalidDataException(
                    "ZIP contém caminho duplicado após normalização: " + path);
        }

        if (!files.TryGetValue(VaultExportManifest.FileName, out var manifestFile))
        {
            return new VaultArchiveImportResult(
                new ReadOnlyDictionary<string, VaultFile>(files),
                VaultExportManifestState.Absent,
                null,
                null,
                rootPrefix);
        }

        var parse = VaultExportManifestCodec.Parse(manifestFile.Bytes);
        if (parse.State == VaultExportManifestState.Corrupt)
        {
            // Um arquivo com este nome que não é manifesto válido não ganha autoridade.
            return new VaultArchiveImportResult(
                new ReadOnlyDictionary<string, VaultFile>(files),
                VaultExportManifestState.Corrupt,
                null,
                null,
                rootPrefix);
        }

        if (parse.State == VaultExportManifestState.Future)
            throw new InvalidDataException(
                "Export de versão mais nova: formatVersion=" +
                (parse.DetectedFormatVersion?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ?? "?") +
                ".");

        var manifest = parse.Manifest!;
        foreach (var listed in manifest.Files)
        {
            var normalizedPath = NormalizeArchivePath(listed.Path);
            if (!string.Equals(normalizedPath, listed.Path, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Manifesto contém caminho não canônico: " + listed.Path);
        }

        files.Remove(VaultExportManifest.FileName);
        var verification = VaultExportManifestCodec.Verify(
            manifest,
            files.Values);

        return new VaultArchiveImportResult(
            new ReadOnlyDictionary<string, VaultFile>(files),
            VaultExportManifestState.Current,
            manifest,
            verification,
            rootPrefix);
    }

    private static string? DetectRootPrefix(IReadOnlyList<string> rawNames)
    {
        var candidates = rawNames
            .Where(name =>
            {
                var segments = name.Split('/');
                var baseName = segments[^1];
                return baseName.Length > 0 && baseName[0] != '.';
            })
            .Select(name => name.Split('/'))
            .ToArray();

        if (candidates.Length == 0 ||
            candidates.Any(parts => parts.Length <= 1))
            return null;

        var root = candidates[0][0];
        // Diretórios de sistema/ocultos pertencem ao vault; nunca são apenas
        // uma embalagem descartável do ZIP (ex.: ".urbe/").
        if (root.Length == 0 ||
            root[0] == '.' ||
            candidates.Any(parts =>
                !string.Equals(parts[0], root, StringComparison.Ordinal)))
            return null;

        return root + "/";
    }

    private static string NormalizeZipEntryName(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized;
    }

    internal static string NormalizeArchivePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var normalized = path.Replace('\\', '/');
        if (normalized.Length == 0 ||
            normalized[0] == '/' ||
            normalized.EndsWith("/", StringComparison.Ordinal))
            throw new InvalidDataException("Caminho inválido no ZIP: " + path);

        var segments = normalized.Split('/');
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".."))
            throw new InvalidDataException("Caminho inválido no ZIP: " + path);

        if (segments[0].Length == 2 &&
            char.IsLetter(segments[0][0]) &&
            segments[0][1] == ':')
            throw new InvalidDataException("Caminho absoluto inválido no ZIP: " + path);

        return string.Join('/', segments);
    }

    private static bool IsJournal(string path) =>
        string.Equals(path, LegacyJournalPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, CurrentJournalPath, StringComparison.OrdinalIgnoreCase);
}
