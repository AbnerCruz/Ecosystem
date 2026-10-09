using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public sealed record VaultWriteDocument(string Id, string Path, string Content);

public enum VaultOperationKind
{
    Write,
    Remove
}

public sealed class VaultOperation
{
    private readonly byte[]? _bytes;

    internal VaultOperation(VaultOperationKind kind, string path, byte[]? bytes)
    {
        Kind = kind;
        Path = path;
        _bytes = bytes is null ? null : (byte[])bytes.Clone();
    }

    public VaultOperationKind Kind { get; }
    public string Path { get; }
    public ReadOnlyMemory<byte>? Bytes => _bytes;
}

public sealed class VaultWriteRequest
{
    public required IEnumerable<VaultFile> Files { get; init; }
    public required IEnumerable<VaultWriteDocument> Documents { get; init; }
    public string? MetadataJson { get; init; }
    public string? HistoryJson { get; init; }
    public string? TrashJson { get; init; }
    public string? CompositionsJson { get; init; }
    public string AppVersion { get; init; } = "?";
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class VaultWriteResult
{
    internal VaultWriteResult(
        bool writable,
        IReadOnlyList<VaultOperation> operations,
        IReadOnlyDictionary<string, VaultFile> files,
        string? backupDirectory,
        bool migrated,
        bool journalUsed)
    {
        Writable = writable;
        Operations = operations;
        Files = files;
        BackupDirectory = backupDirectory;
        Migrated = migrated;
        JournalUsed = journalUsed;
    }

    public bool Writable { get; }
    public IReadOnlyList<VaultOperation> Operations { get; }
    public IReadOnlyDictionary<string, VaultFile> Files { get; }
    public string? BackupDirectory { get; }
    public bool Migrated { get; }
    public bool JournalUsed { get; }
}

public static class VaultWriter
{
    private const string VaultPath = ".urbe/vault.json";
    private const string MapPath = ".urbe/mapa.json";
    private const string JournalV1 = ".urbe/journal.json";
    private const string JournalV2 = ".urbe/journal.v2.json";
    private const string BackupRoot = ".urbe/backup";

    private static readonly string[] LegacyOwned =
    [
        MapPath,
        JournalV1,
        ".urbe/trash.json",
        ".urbe/history.json",
        ".urbe/compositions.json",
        ".urbe/tutorial.json",
        ".urbe/merged-v1.json"
    ];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".js", ".mjs", ".css",
        ".json", ".yaml", ".yml", ".csv"
    };

    public static VaultWriteResult Plan(VaultWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Files);
        ArgumentNullException.ThrowIfNull(request.Documents);

        var current = CloneFiles(request.Files);
        var snapshot = VaultReader.Read(current.Values);
        if (snapshot.IsReadOnly)
            return new VaultWriteResult(
                false,
                Array.Empty<VaultOperation>(),
                new ReadOnlyDictionary<string, VaultFile>(current),
                null,
                false,
                false);

        var documents = NormalizeDocuments(request.Documents);
        var operations = new List<VaultOperation>();
        var working = current.ToDictionary(
            pair => pair.Key,
            pair => new VaultFile(pair.Key, pair.Value.Bytes.ToArray()),
            StringComparer.OrdinalIgnoreCase);

        var future = new HashSet<string>(snapshot.FutureFiles, StringComparer.OrdinalIgnoreCase);
        var now = request.Now;
        var migrated = snapshot.FormatState is VaultFormatState.Absent or VaultFormatState.Corrupt;
        string? backupDirectory = null;

        if (migrated)
        {
            var originals = LegacyOwned
                .Concat(working.Keys.Where(path => path.StartsWith(".urbe/origens/", StringComparison.OrdinalIgnoreCase)))
                .Concat(snapshot.FormatState == VaultFormatState.Corrupt && working.ContainsKey(VaultPath)
                    ? [VaultPath]
                    : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(working.ContainsKey)
                .ToArray();

            if (originals.Length > 0)
            {
                backupDirectory = CreateBackup(working, originals, now, operations);
            }
        }

        var desired = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var recovery = ReadRecoveryJournal(working);
        var metadataJson = request.MetadataJson
                           ?? RecoveryObjectJson(recovery, "metadata")
                           ?? CurrentObjectJson(working, MapPath);
        var historyJson = request.HistoryJson
                          ?? RecoveryObjectJson(recovery, "history")
                          ?? CurrentLogicalSidecarJson(working, "history");
        var trashJson = request.TrashJson
                        ?? RecoveryObjectJson(recovery, "trash")
                        ?? CurrentLogicalSidecarJson(working, "trash");
        var compositionsJson = request.CompositionsJson
                               ?? RecoveryObjectJson(recovery, "compositions")
                               ?? CurrentLogicalSidecarJson(working, "compositions");

        foreach (var document in documents)
        {
            if (future.Contains(document.Path))
                continue;

            if (working.TryGetValue(document.Path, out var original))
            {
                var originalBytes = original.Bytes.ToArray();
                // Do not re-encode untouched user files. In particular, an
                // ANSI/Latin-1 Markdown file would otherwise have invalid
                // bytes converted to UTF-8 replacement characters merely
                // because a different note was edited.
                if (string.Equals(Encoding.UTF8.GetString(originalBytes),
                    document.Content, StringComparison.Ordinal))
                {
                    desired[document.Path] = originalBytes;
                    continue;
                }

                if (!IsValidUtf8(originalBytes))
                    throw new InvalidDataException(
                        "O arquivo foi alterado, mas sua codificação original " +
                        "não é UTF-8 válida. Converta-o explicitamente antes de editar: " +
                        document.Path);
            }
            else
            {
                // A move/rename keeps the document identity but changes its
                // path. The new target cannot be reconstructed faithfully
                // from replacement characters if the source was non-UTF-8.
                var movedFrom = snapshot.Documents.FirstOrDefault(existing =>
                    string.Equals(existing.Id, document.Id, StringComparison.Ordinal));
                if (movedFrom is not null && !IsValidUtf8(movedFrom.Bytes.Span))
                    throw new InvalidDataException(
                        "Não é seguro mover uma nota com codificação não UTF-8. " +
                        "Converta o arquivo explicitamente: " + movedFrom.Path);
            }

            desired[document.Path] = Encoding.UTF8.GetBytes(document.Content);
        }

        if (!snapshot.IsMapReadOnly && metadataJson is not null)
            desired[MapPath] = ValidateJsonObject(metadataJson, MapPath);

        WriteSidecarDesired(working, desired, future, "history", historyJson);
        WriteSidecarDesired(working, desired, future, "trash", trashJson);
        WriteSidecarDesired(working, desired, future, "compositions", compositionsJson);

        if (!future.Contains(VaultIdentity.Path))
        {
            VaultIdentityState? previous = null;
            if (working.TryGetValue(VaultIdentity.Path, out var identityFile))
                previous = VaultIdentity.Parse(identityFile.Bytes, out _);
            desired[VaultIdentity.Path] = VaultIdentity.Serialize(VaultIdentity.Build(documents, previous, now));
        }

        desired[VaultPath] = BuildVaultMetadata(
            working.TryGetValue(VaultPath, out var vaultFile) ? vaultFile.Bytes : null,
            request.AppVersion,
            now,
            migrated,
            backupDirectory);

        var changedUserPaths = desired
            .Where(pair => IsEditableText(pair.Key) &&
                           (!working.TryGetValue(pair.Key, out var before) ||
                            !before.Bytes.Span.SequenceEqual(pair.Value)))
            .Select(pair => pair.Key)
            .ToList();

        var removedUserPaths = working.Keys
            .Where(IsEditableText)
            .Where(path => !documents.Any(document =>
                string.Equals(document.Path, path, StringComparison.OrdinalIgnoreCase)))
            .Where(path => !future.Contains(path))
            .ToList();

        var journalIsFuture = future.Contains(JournalV2);
        // A journal serializes document contents as Unicode strings. Do not
        // create a lossy recovery journal when any untouched source file
        // contains non-UTF-8 bytes that the reader can only approximate.
        var hasNonUtf8Documents = documents.Any(document =>
            working.TryGetValue(document.Path, out var original) &&
            !IsValidUtf8(original.Bytes.Span));
        var journalUsed = !journalIsFuture && !hasNonUtf8Documents &&
            changedUserPaths.Count + removedUserPaths.Count > 1;
        if (journalUsed)
        {
            var journalBytes = BuildJournal(
                documents,
                now,
                metadataJson,
                historyJson,
                trashJson,
                compositionsJson);
            ApplyWrite(working, operations, JournalV2, journalBytes);
        }

        // Documents first, then metadata/sidecars. Legacy v1 sidecars are never rewritten.
        foreach (var document in documents.OrderBy(document => document.Path, StringComparer.Ordinal))
        {
            if (!desired.TryGetValue(document.Path, out var bytes))
                continue;
            if (!working.TryGetValue(document.Path, out var before) || !before.Bytes.Span.SequenceEqual(bytes))
                ApplyWrite(working, operations, document.Path, bytes);
        }

        foreach (var path in removedUserPaths.Order(StringComparer.Ordinal))
            ApplyRemove(working, operations, path);

        foreach (var pair in desired
                     .Where(pair => IsSystemOrMap(pair.Key))
                     .OrderBy(pair => SystemWriteOrder(pair.Key))
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!working.TryGetValue(pair.Key, out var before) || !before.Bytes.Span.SequenceEqual(pair.Value))
                ApplyWrite(working, operations, pair.Key, pair.Value);
        }

        // A supported interrupted journal is consumed after the recovered state is committed.
        foreach (var path in new[] { JournalV2, JournalV1 })
        {
            if (!working.ContainsKey(path))
                continue;
            if (future.Contains(path))
                continue;
            if (journalUsed && string.Equals(path, JournalV2, StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsSupportedJournal(working[path]))
                ApplyRemove(working, operations, path);
        }

        if (journalUsed && working.ContainsKey(JournalV2))
            ApplyRemove(working, operations, JournalV2);

        return new VaultWriteResult(
            true,
            operations.AsReadOnly(),
            new ReadOnlyDictionary<string, VaultFile>(working),
            backupDirectory,
            migrated,
            journalUsed);
    }

    public static VaultWriteResult RestoreBackup(
        IEnumerable<VaultFile> source,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Diretório de backup obrigatório.", nameof(directory));

        var working = CloneFiles(source);
        var operations = new List<VaultOperation>();
        var manifestPath = directory.TrimEnd('/') + "/manifest.json";
        if (!working.TryGetValue(manifestPath, out var manifestFile))
            throw new InvalidDataException("backup incompleto: manifest.json");

        JsonObject manifest;
        try
        {
            manifest = JsonNode.Parse(manifestFile.Bytes.Span)?.AsObject()
                ?? throw new InvalidDataException("manifesto de backup inválido");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("manifesto de backup inválido", exception);
        }

        if (manifest["files"] is not JsonArray files)
            throw new InvalidDataException("manifesto de backup sem files");

        foreach (var item in files)
        {
            var obj = item?.AsObject() ?? throw new InvalidDataException("entrada de backup inválida");
            var path = obj["path"]?.GetValue<string>()
                ?? throw new InvalidDataException("entrada de backup sem path");
            var stored = directory.TrimEnd('/') + "/files/" + MapBackupPath(path);

            if (!working.TryGetValue(stored, out var backed))
                throw new InvalidDataException("backup incompleto: " + path);

            var expectedHash = obj["sha256"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(expectedHash) &&
                !string.Equals(expectedHash, Sha256(backed.Bytes.Span), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("backup corrompido: " + path);

            ApplyWrite(working, operations, path, backed.Bytes.ToArray());
        }

        if (manifest["absent"] is JsonArray absent)
        {
            foreach (var item in absent)
            {
                var path = item?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(path) && working.ContainsKey(path))
                    ApplyRemove(working, operations, path);
            }
        }

        return new VaultWriteResult(
            true,
            operations.AsReadOnly(),
            new ReadOnlyDictionary<string, VaultFile>(working),
            directory,
            false,
            false);
    }

    internal static Dictionary<string, VaultFile> CloneFiles(IEnumerable<VaultFile> files)
    {
        var result = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (!result.TryAdd(file.Path, new VaultFile(file.Path, file.Bytes.ToArray())))
                throw new InvalidDataException("Caminho duplicado no vault: " + file.Path);
        }

        return result;
    }

    internal static byte[] SetVaultMaintenance(
        ReadOnlyMemory<byte>? current,
        string appVersion,
        DateTimeOffset now,
        JsonObject maintenance)
    {
        var bytes = BuildVaultMetadata(current, appVersion, now, current is null, null);
        var root = JsonNode.Parse(bytes)!.AsObject();
        var log = root["maintenance"] as JsonArray ?? new JsonArray();
        log.Add(maintenance);
        while (log.Count > 20)
            log.RemoveAt(0);
        root["maintenance"] = log;
        return Serialize(root, indented: true);
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static IReadOnlyList<VaultWriteDocument> NormalizeDocuments(IEnumerable<VaultWriteDocument> documents)
    {
        var result = new List<VaultWriteDocument>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            if (document is null)
                throw new InvalidDataException("Documento nulo.");
            var path = NormalizePath(document.Path);
            if (IsSystemPath(path))
                throw new InvalidDataException("Documento em caminho de sistema: " + path);
            if (string.IsNullOrWhiteSpace(document.Id))
                throw new InvalidDataException("Documento sem ID: " + path);
            if (!paths.Add(path))
                throw new InvalidDataException("Caminho duplicado no estado desejado: " + path);
            if (!ids.Add(document.Id))
                throw new InvalidDataException("ID duplicado no estado desejado: " + document.Id);

            result.Add(document with { Path = path });
        }

        return result;
    }

    private static void WriteSidecarDesired(
        IReadOnlyDictionary<string, VaultFile> current,
        IDictionary<string, byte[]> desired,
        ISet<string> future,
        string key,
        string? overrideJson)
    {
        var v2 = $".urbe/{key}.v2.json";
        var v1 = $".urbe/{key}.json";

        if (future.Contains(v2))
            return;

        JsonObject logical;
        if (overrideJson is not null)
        {
            logical = ParseObject(overrideJson, v2);
        }
        else if (current.TryGetValue(v2, out var currentV2) &&
                 TryParseObject(currentV2.Bytes, out var parsedV2) &&
                 parsedV2["version"]?.GetValue<int>() == 2)
        {
            logical = parsedV2;
        }
        else if (current.TryGetValue(v1, out var currentV1) &&
                 TryParseObject(currentV1.Bytes, out var parsedV1) &&
                 IsAcceptedLegacySidecar(parsedV1, key))
        {
            logical = parsedV1;
        }
        else
        {
            logical = key == "history"
                ? new JsonObject { ["version"] = 1, ["documents"] = new JsonObject() }
                : new JsonObject { ["version"] = 1, ["items"] = new JsonArray() };
        }

        logical["version"] = 2;
        desired[v2] = Serialize(logical, indented: key == "trash");
    }

    private static bool IsAcceptedLegacySidecar(JsonObject obj, string key)
    {
        if (obj["version"] is null)
            return key == "trash";
        return obj["version"]!.GetValue<int>() == 1;
    }

    private static byte[] BuildVaultMetadata(
        ReadOnlyMemory<byte>? current,
        string appVersion,
        DateTimeOffset now,
        bool migrated,
        string? backupDirectory)
    {
        JsonObject root;
        var hasCurrent = false;

        if (current is not null && TryParseObject(current.Value, out var parsed))
        {
            var version = GetOptionalInt(parsed["formatVersion"]);
            if (version is not null && version <= 2)
            {
                root = parsed;
                hasCurrent = true;
            }
            else
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        var writer = "urbe@" + (string.IsNullOrWhiteSpace(appVersion) ? "?" : appVersion);
        if (!hasCurrent)
        {
            root["formatVersion"] = 2;
            root["createdBy"] = writer;
            root["lastWriter"] = writer;
            root["created"] = UtcIso(now);
            var migrations = new JsonArray();
            if (migrated && backupDirectory is not null)
            {
                migrations.Add(new JsonObject
                {
                    ["id"] = "1-to-2",
                    ["from"] = 1,
                    ["to"] = 2,
                    ["at"] = UtcIso(now),
                    ["backup"] = backupDirectory
                });
            }
            root["migrations"] = migrations;
        }
        else
        {
            root["lastWriter"] = writer;
        }

        return Serialize(root, indented: true);
    }

    private static string? CreateBackup(
        IDictionary<string, VaultFile> working,
        IReadOnlyList<string> paths,
        DateTimeOffset now,
        ICollection<VaultOperation> operations)
    {
        var baseDirectory = BackupRoot + "/" + now.UtcDateTime.ToString("yyyyMMdd-HHmmss") + "-1-2";
        var directory = baseDirectory;
        var n = 2;
        while (working.ContainsKey(directory + "/manifest.json"))
            directory = baseDirectory + "-" + n++;

        var files = new JsonArray();
        foreach (var path in paths)
        {
            var source = working[path];
            var bytes = source.Bytes.ToArray();
            var text = Encoding.UTF8.GetString(bytes);
            var storedPath = directory + "/files/" + MapBackupPath(path);
            ApplyWrite(working, operations, storedPath, bytes);
            files.Add(new JsonObject
            {
                ["path"] = path,
                ["size"] = text.Length,
                ["sha256"] = Sha256(bytes)
            });
        }

        var manifest = new JsonObject
        {
            ["version"] = 1,
            ["from"] = 1,
            ["to"] = 2,
            ["reason"] = "migration",
            ["at"] = UtcIso(now),
            ["files"] = files,
            ["absent"] = new JsonArray()
        };
        ApplyWrite(working, operations, directory + "/manifest.json", Serialize(manifest, indented: true));
        return directory;
    }

    private static byte[] BuildJournal(
        IReadOnlyList<VaultWriteDocument> documents,
        DateTimeOffset now,
        string? metadataJson,
        string? historyJson,
        string? trashJson,
        string? compositionsJson)
    {
        var array = new JsonArray();
        foreach (var document in documents)
        {
            array.Add(new JsonObject
            {
                ["id"] = document.Id,
                ["path"] = document.Path,
                ["content"] = document.Content
            });
        }

        var root = new JsonObject
        {
            ["version"] = 2,
            ["timestamp"] = now.ToUnixTimeMilliseconds(),
            ["documents"] = array
        };

        if (metadataJson is not null)
            root["metadata"] = JsonNode.Parse(metadataJson);
        if (trashJson is not null)
            root["trash"] = JsonNode.Parse(trashJson);
        if (historyJson is not null)
            root["history"] = JsonNode.Parse(historyJson);
        if (compositionsJson is not null)
            root["compositions"] = JsonNode.Parse(compositionsJson);

        return Serialize(root, indented: false);
    }

    private sealed record RecoveryJournal(string Path, JsonObject Root);

    private static RecoveryJournal? ReadRecoveryJournal(
        IReadOnlyDictionary<string, VaultFile> files)
    {
        foreach (var candidate in new[]
                 {
                     (Path: JournalV2, Version: 2),
                     (Path: JournalV1, Version: 1)
                 })
        {
            if (!files.TryGetValue(candidate.Path, out var file) ||
                !TryParseObject(file.Bytes, out var root))
                continue;

            if (GetOptionalInt(root["version"]) != candidate.Version ||
                root["documents"] is not JsonArray)
                continue;

            return new RecoveryJournal(candidate.Path, root);
        }

        return null;
    }

    private static string? RecoveryObjectJson(RecoveryJournal? recovery, string property)
    {
        if (recovery?.Root[property] is not JsonObject obj)
            return null;
        return obj.ToJsonString();
    }

    private static string? CurrentObjectJson(
        IReadOnlyDictionary<string, VaultFile> files,
        string path)
    {
        if (!files.TryGetValue(path, out var file) ||
            !TryParseObject(file.Bytes, out var obj))
            return null;
        return obj.ToJsonString();
    }

    private static string? CurrentLogicalSidecarJson(
        IReadOnlyDictionary<string, VaultFile> files,
        string key)
    {
        var v2 = $".urbe/{key}.v2.json";
        var v1 = $".urbe/{key}.json";

        if (files.TryGetValue(v2, out var currentV2) &&
            TryParseObject(currentV2.Bytes, out var parsedV2) &&
            GetOptionalInt(parsedV2["version"]) == 2)
        {
            parsedV2["version"] = 1;
            return parsedV2.ToJsonString();
        }

        if (files.TryGetValue(v1, out var currentV1) &&
            TryParseObject(currentV1.Bytes, out var parsedV1) &&
            IsAcceptedLegacySidecar(parsedV1, key))
            return parsedV1.ToJsonString();

        return null;
    }

    private static bool IsSupportedJournal(VaultFile file)
    {
        if (!TryParseObject(file.Bytes, out var obj))
            return false;
        var version = GetOptionalInt(obj["version"]);
        if (version is not 1 and not 2)
            return false;
        return obj["documents"] is JsonArray;
    }

    private static int SystemWriteOrder(string path) => path switch
    {
        MapPath => 10,
        ".urbe/history.v2.json" => 20,
        ".urbe/trash.v2.json" => 21,
        ".urbe/compositions.v2.json" => 22,
        VaultIdentity.Path => 30,
        VaultPath => 40,
        _ => 35
    };

    private static void ApplyWrite(
        IDictionary<string, VaultFile> working,
        ICollection<VaultOperation> operations,
        string path,
        byte[] bytes)
    {
        working[path] = new VaultFile(path, bytes);
        operations.Add(new VaultOperation(VaultOperationKind.Write, path, bytes));
    }

    private static void ApplyRemove(
        IDictionary<string, VaultFile> working,
        ICollection<VaultOperation> operations,
        string path)
    {
        working.Remove(path);
        operations.Add(new VaultOperation(VaultOperationKind.Remove, path, null));
    }

    private static bool IsSystemOrMap(string path) =>
        string.Equals(path, MapPath, StringComparison.OrdinalIgnoreCase) || IsSystemPath(path);

    private static bool IsEditableText(string path)
    {
        if (IsSystemPath(path))
            return false;
        var dot = path.LastIndexOf('.');
        return dot >= 0 && TextExtensions.Contains(path[dot..]);
    }

    private static bool IsSystemPath(string path) =>
        path.Split('/').Any(segment => segment.Length > 0 && segment[0] == '.');

    private static string NormalizePath(string path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        if (normalized.Length == 0 || normalized[0] == '/')
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        var segments = normalized.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        return string.Join('/', segments);
    }

    private static byte[] ValidateJsonObject(string json, string path) =>
        Serialize(ParseObject(json, path), indented: true);

    private static JsonObject ParseObject(string json, string path)
    {
        try
        {
            return JsonNode.Parse(json)?.AsObject()
                   ?? throw new InvalidDataException("JSON inválido: " + path);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("JSON inválido: " + path, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("JSON não é objeto: " + path, exception);
        }
    }

    private static bool TryParseObject(ReadOnlyMemory<byte> bytes, out JsonObject obj)
    {
        try
        {
            obj = JsonNode.Parse(bytes.Span)?.AsObject()!;
            return obj is not null;
        }
        catch (JsonException)
        {
            obj = null!;
            return false;
        }
        catch (InvalidOperationException)
        {
            obj = null!;
            return false;
        }
    }

    private static string UtcIso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static int? GetOptionalInt(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        return value.TryGetValue<int>(out var number) ? number : null;
    }

    private static byte[] Serialize(JsonNode node, bool indented) =>
        Encoding.UTF8.GetBytes(node.ToJsonString(new JsonSerializerOptions { WriteIndented = indented }));

    private static string MapBackupPath(string path) =>
        path.StartsWith(".urbe/", StringComparison.Ordinal)
            ? "urbe/" + path[6..]
            : path;

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
