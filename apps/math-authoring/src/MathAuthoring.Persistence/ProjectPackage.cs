using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathAuthoring.Core;

namespace MathAuthoring.Persistence;

public sealed record ProjectAsset(string Id, string MediaType, byte[] Bytes);

public sealed record PortableProject(
    ProjectDocument Document,
    long Revision,
    IReadOnlyList<ProjectAsset> Assets);

/// <summary>
/// Portable, untrusted-data-only package. Entry names are derived from SHA-256,
/// never from user input. Caller owns the destination stream.
/// </summary>
public static class ProjectPackage
{
    public const int FormatVersion = 1;
    public const int MaxAssets = 128;
    public const int MaxAssetBytes = 8 * 1024 * 1024;
    public const int MaxUncompressedBytes = 64 * 1024 * 1024;
    public const int MaxArchiveBytes = 72 * 1024 * 1024;
    public const int MaxManifestBytes = 1024 * 1024;
    private static readonly DateTimeOffset ZipEpoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> MediaTypes = new(StringComparer.Ordinal)
    {
        "image/png", "image/jpeg", "audio/wav", "audio/mpeg", "font/woff2"
    };

    public static void Write(Stream destination, PortableProject project)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(project);
        if (!destination.CanWrite) throw new ArgumentException("Destino não gravável.", nameof(destination));
        if (project.Revision < 0 || project.Assets is null || project.Assets.Count > MaxAssets)
            throw new InvalidDataException("Revisão/quantidade de assets inválida.");

        var json = Encoding.UTF8.GetBytes(ProjectJson.Format(project.Document));
        var entries = new List<AssetIndex>();
        var dataByHash = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long expanded = json.Length;

        foreach (var asset in project.Assets)
        {
            if (asset is null || !ValidId(asset.Id) || !MediaTypes.Contains(asset.MediaType) ||
                asset.Bytes is null || asset.Bytes.Length > MaxAssetBytes || !ids.Add(asset.Id))
                throw new InvalidDataException("Asset inválido, duplicado, não confiável ou grande demais.");
            var hash = Sha(asset.Bytes);
            entries.Add(new AssetIndex(asset.Id, asset.MediaType, hash, asset.Bytes.Length));
            dataByHash.TryAdd(hash, asset.Bytes);
        }
        foreach (var bytes in dataByHash.Values) expanded += bytes.Length;
        if (expanded > MaxUncompressedBytes)
            throw new InvalidDataException("Assets excedem o limite total.");

        entries.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        var manifest = SerializeManifest(project.Revision, Sha(json), entries);
        if (manifest.Length > MaxManifestBytes)
            throw new InvalidDataException("Manifest excede o limite.");

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        Put(archive, "manifest.json", manifest);
        Put(archive, "project.json", json);
        foreach (var pair in dataByHash)
            Put(archive, $"assets/{pair.Key}.bin", pair.Value);
    }

    public static PortableProject Read(Stream source, params DocumentMigration[] migrations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(migrations);
        if (!source.CanRead) throw new ArgumentException("Fonte não legível.", nameof(source));

        // Bounded buffering works with both seekable local files and Android SAF streams.
        using var buffer = new MemoryStream();
        var temp = new byte[32768];
        int count;
        while ((count = source.Read(temp, 0, temp.Length)) != 0)
        {
            if (buffer.Length + count > MaxArchiveBytes)
                throw new InvalidDataException("ZIP excede o tamanho máximo.");
            buffer.Write(temp, 0, count);
        }
        buffer.Position = 0;

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count < 2 || archive.Entries.Count > MaxAssets + 2)
            throw new InvalidDataException("Número de entradas inesperado.");

        var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            // Directories, absolute paths, traversal, symlinks, and arbitrary
            // payloads are never extracted, executed, or silently ignored.
            var name = entry.FullName;
            if (!AllowedEntry(name) || !files.TryAdd(name, entry) || IsSymlink(entry))
                throw new InvalidDataException("Entrada ZIP desconhecida, duplicada ou insegura.");
            var limit = name == "manifest.json" ? MaxManifestBytes :
                name == "project.json" ? ProjectJson.MaxBytes : MaxAssetBytes;
            if (entry.Length < 0 || entry.Length > limit)
                throw new InvalidDataException("Entrada ZIP grande demais.");
            total += entry.Length;
            if (total > MaxUncompressedBytes + MaxManifestBytes)
                throw new InvalidDataException("ZIP excede limite de expansão.");
        }

        if (!files.TryGetValue("manifest.json", out var manifestEntry) ||
            !files.TryGetValue("project.json", out var documentEntry))
            throw new InvalidDataException("Package sem manifest ou documento.");

        var manifest = ParseManifest(ReadEntry(manifestEntry, MaxManifestBytes));
        var documentBytes = ReadEntry(documentEntry, ProjectJson.MaxBytes);
        if (!string.Equals(Sha(documentBytes), manifest.DocumentSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Hash do documento incompatível.");
        var loaded = ProjectMigrator.Open(Encoding.UTF8.GetString(documentBytes), migrations);
        if (!loaded.Success)
            throw new InvalidDataException("Documento inválido: " + loaded.Problems[0].Code);

        var assets = new List<ProjectAsset>();
        var required = new HashSet<string>(StringComparer.Ordinal) { "manifest.json", "project.json" };
        var expanded = (long)documentBytes.Length;
        foreach (var item in manifest.Assets)
        {
            var path = $"assets/{item.Sha256}.bin";
            required.Add(path);
            if (!files.TryGetValue(path, out var entry) || entry.Length != item.ByteLength)
                throw new InvalidDataException("Asset obrigatório ausente ou com comprimento errado.");
            var bytes = ReadEntry(entry, MaxAssetBytes);
            if (!string.Equals(Sha(bytes), item.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Hash do asset incompatível.");
            expanded += bytes.Length;
            if (expanded > MaxUncompressedBytes)
                throw new InvalidDataException("Package excede o limite expandido.");
            assets.Add(new ProjectAsset(item.Id, item.MediaType, bytes));
        }
        if (files.Keys.Any(name => !required.Contains(name)))
            throw new InvalidDataException("ZIP contém entrada não declarada no manifest.");
        return new PortableProject(loaded.Document!, manifest.Revision, assets.AsReadOnly());
    }

    private static void Put(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = ZipEpoch;
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int limit)
    {
        if (entry.Length > limit)
            throw new InvalidDataException("Entrada excede limite.");
        using var result = new MemoryStream();
        using var input = entry.Open();
        var chunk = new byte[32768];
        int length;
        while ((length = input.Read(chunk, 0, chunk.Length)) != 0)
        {
            if (result.Length + length > limit)
                throw new InvalidDataException("ZIP expandiu além do limite.");
            result.Write(chunk, 0, length);
        }
        if (result.Length != entry.Length)
            throw new InvalidDataException("Comprimento de entrada ZIP inconsistente.");
        return result.ToArray();
    }

    private static byte[] SerializeManifest(long revision, string documentSha, IReadOnlyList<AssetIndex> assets)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("bundleVersion", FormatVersion);
            writer.WriteNumber("revision", revision);
            writer.WriteString("documentSha256", documentSha);
            writer.WriteStartArray("assets");
            foreach (var asset in assets)
            {
                writer.WriteStartObject();
                writer.WriteString("id", asset.Id);
                writer.WriteString("mediaType", asset.MediaType);
                writer.WriteString("sha256", asset.Sha256);
                writer.WriteNumber("byteLength", asset.ByteLength);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static Manifest ParseManifest(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            MaxDepth = 16, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow
        });
        var root = document.RootElement;
        Strict(root, "bundleVersion", "revision", "documentSha256", "assets");
        if (root.GetProperty("bundleVersion").GetInt32() != FormatVersion)
            throw new InvalidDataException("Bundle de versão não suportada.");
        if (!root.GetProperty("revision").TryGetInt64(out var revision) || revision < 0)
            throw new InvalidDataException("Revisão inválida.");
        var documentSha = root.GetProperty("documentSha256").GetString();
        if (!ValidSha(documentSha)) throw new InvalidDataException("Hash de documento inválido.");
        var array = root.GetProperty("assets");
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > MaxAssets)
            throw new InvalidDataException("Quantidade de assets inválida.");
        var assets = new List<AssetIndex>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            Strict(element, "id", "mediaType", "sha256", "byteLength");
            var id = element.GetProperty("id").GetString();
            var mediaType = element.GetProperty("mediaType").GetString();
            var hash = element.GetProperty("sha256").GetString();
            if (!element.GetProperty("byteLength").TryGetInt32(out var size) ||
                size < 0 || size > MaxAssetBytes || !ValidId(id) || !ids.Add(id!) ||
                !MediaTypes.Contains(mediaType ?? string.Empty) || !ValidSha(hash))
                throw new InvalidDataException("Metadados de asset inválidos.");
            assets.Add(new AssetIndex(id!, mediaType!, hash!, size));
        }
        return new Manifest(revision, documentSha!, assets);
    }

    private static void Strict(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Esperado objeto JSON.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in element.EnumerateObject())
            if (!seen.Add(field.Name) || !names.Contains(field.Name, StringComparer.Ordinal))
                throw new InvalidDataException("Campo JSON duplicado ou desconhecido.");
        if (seen.Count != names.Length || names.Any(n => !seen.Contains(n)))
            throw new InvalidDataException("Manifest incompleto.");
    }

    private static bool AllowedEntry(string value) =>
        value is "manifest.json" or "project.json" ||
        (value.StartsWith("assets/", StringComparison.Ordinal) &&
         value.EndsWith(".bin", StringComparison.Ordinal) &&
         value.Length == "assets/".Length + 64 + ".bin".Length &&
         ValidSha(value["assets/".Length..^".bin".Length]));

    private static bool IsSymlink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    private static bool ValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 128 &&
        id.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');

    private static bool ValidSha(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record AssetIndex(string Id, string MediaType, string Sha256, int ByteLength);
    private sealed record Manifest(long Revision, string DocumentSha256, IReadOnlyList<AssetIndex> Assets);
}
