using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Urbe.Core;

public enum VaultFormatState
{
    Absent,
    Current,
    Future,
    Corrupt
}

public sealed class VaultFile
{
    private readonly byte[] _bytes;

    public VaultFile(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(bytes);
        Path = path;
        _bytes = (byte[])bytes.Clone();
    }

    public string Path { get; }
    public ReadOnlyMemory<byte> Bytes => _bytes;
}

public sealed class VaultDocument
{
    private readonly byte[] _bytes;

    internal VaultDocument(string path, string id, bool idGenerated, byte[] bytes, string? text, double? x, double? y)
    {
        Path = path;
        Id = id;
        IdGenerated = idGenerated;
        _bytes = bytes;
        Text = text;
        X = x;
        Y = y;
    }

    public string Path { get; }
    public string Id { get; }
    public bool IdGenerated { get; }
    public ReadOnlyMemory<byte> Bytes => _bytes;
    public string? Text { get; }
    public double? X { get; }
    public double? Y { get; }
}

public sealed class VaultSnapshot
{
    internal VaultSnapshot(
        IReadOnlyDictionary<string, VaultFile> files,
        IReadOnlyList<VaultDocument> documents,
        IReadOnlyCollection<string> futureFiles,
        VaultFormatState formatState,
        int? vaultFormatVersion,
        bool isReadOnly,
        bool isMapReadOnly,
        bool recoveredFromJournal,
        string? recoveryJournalPath,
        string? mundo,
        int historyCount,
        int trashCount,
        int compositionCount)
    {
        Files = files;
        Documents = documents;
        FutureFiles = futureFiles;
        FormatState = formatState;
        VaultFormatVersion = vaultFormatVersion;
        IsReadOnly = isReadOnly;
        IsMapReadOnly = isMapReadOnly;
        RecoveredFromJournal = recoveredFromJournal;
        RecoveryJournalPath = recoveryJournalPath;
        Mundo = mundo;
        HistoryCount = historyCount;
        TrashCount = trashCount;
        CompositionCount = compositionCount;
    }

    public IReadOnlyDictionary<string, VaultFile> Files { get; }
    public IReadOnlyList<VaultDocument> Documents { get; }
    public IReadOnlyCollection<string> FutureFiles { get; }
    public VaultFormatState FormatState { get; }
    public int? VaultFormatVersion { get; }
    public bool IsReadOnly { get; }
    public bool IsMapReadOnly { get; }
    public bool RecoveredFromJournal { get; }
    public string? RecoveryJournalPath { get; }
    public string? Mundo { get; }
    public int HistoryCount { get; }
    public int TrashCount { get; }
    public int CompositionCount { get; }
}

/// <summary>
/// Pure Urbe vault reader. It receives an in-memory file set and never writes,
/// deletes, migrates or accesses the host filesystem.
/// </summary>
public static class VaultReader
{
    private const string VaultPath = ".urbe/vault.json";
    private const string MapPath = ".urbe/mapa.json";
    private const string IdentityPath = ".urbe/identity.json";
    private const int CurrentVaultVersion = 2;
    private const int CurrentMapVersion = 4;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".js", ".mjs", ".css",
        ".json", ".yaml", ".yml", ".csv"
    };

    public static VaultSnapshot Read(IEnumerable<VaultFile> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var files = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            ArgumentNullException.ThrowIfNull(item);
            var path = NormalizePath(item.Path);
            if (!files.TryAdd(path, new VaultFile(path, item.Bytes.ToArray())))
                throw new InvalidDataException($"Caminho duplicado no vault: {path}");
        }

        var future = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var (formatState, vaultVersion) = ReadVaultFormat(files, future);
        var readOnly = formatState == VaultFormatState.Future;

        if (readOnly)
        {
            foreach (var path in files.Keys.Where(IsSystemPath))
                future.Add(path);
        }

        var physicalMap = ReadMap(files, future);
        var mapReadOnly = physicalMap.IsReadOnly;

        var history = ReadSidecar(files, "history", future);
        var trash = ReadSidecar(files, "trash", future);
        var compositions = ReadSidecar(files, "compositions", future);
        var journal = ReadJournal(files, future);
        var identity = ReadIdentity(files, future);

        MarkForwardTextArtifacts(files, future);

        var effectiveMap = journal.IsRecovered && journal.Metadata is not null
            ? ReadMap(journal.Metadata.Value)
            : physicalMap;

        var documents = journal.IsRecovered
            ? BuildJournalDocuments(journal.Documents!, effectiveMap, identity)
            : BuildDiskDocuments(files, effectiveMap, identity);

        var exposedFiles = new ReadOnlyDictionary<string, VaultFile>(files);
        return new VaultSnapshot(
            exposedFiles,
            documents.AsReadOnly(),
            Array.AsReadOnly(future.Order(StringComparer.Ordinal).ToArray()),
            formatState,
            vaultVersion,
            readOnly,
            mapReadOnly,
            journal.IsRecovered,
            journal.Path,
            effectiveMap.Mundo,
            history.Count,
            trash.Count,
            compositions.Count);
    }

    private static (VaultFormatState State, int? Version) ReadVaultFormat(
        IReadOnlyDictionary<string, VaultFile> files,
        ISet<string> future)
    {
        if (!files.TryGetValue(VaultPath, out var file))
            return (VaultFormatState.Absent, null);

        if (!TryParseObject(file, out var json))
        {
            future.Add(VaultPath);
            return (VaultFormatState.Corrupt, null);
        }

        using (json)
        {
            if (!TryReadInt(json.RootElement, "formatVersion", out var version))
            {
                future.Add(VaultPath);
                return (VaultFormatState.Corrupt, null);
            }

            if (version > CurrentVaultVersion)
            {
                future.Add(VaultPath);
                return (VaultFormatState.Future, version);
            }

            return (VaultFormatState.Current, version);
        }
    }

    private static MapProjection ReadMap(
        IReadOnlyDictionary<string, VaultFile> files,
        ISet<string> future)
    {
        if (!files.TryGetValue(MapPath, out var file))
            return MapProjection.Empty;

        if (!TryParseObject(file, out var json))
            return new MapProjection(true, null, new Dictionary<string, NoteMetadata>(StringComparer.OrdinalIgnoreCase));

        using (json)
            return ReadMap(json.RootElement);
    }

    private static MapProjection ReadMap(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return new MapProjection(true, null, new Dictionary<string, NoteMetadata>(StringComparer.OrdinalIgnoreCase));

        var mapReadOnly = false;
        if (root.TryGetProperty("v", out var version))
        {
            mapReadOnly = !version.TryGetInt32(out var number) || number < 1 || number > CurrentMapVersion;
        }

        string? mundo = null;
        if (root.TryGetProperty("mundo", out var world) && world.ValueKind == JsonValueKind.String)
            mundo = world.GetString();

        var notes = new Dictionary<string, NoteMetadata>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("notas", out var notas) && notas.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in notas.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                string? id = null;
                if (property.Value.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String)
                    id = NullIfBlank(idElement.GetString());

                notes[property.Name] = new NoteMetadata(
                    id,
                    ReadNumber(property.Value, "x"),
                    ReadNumber(property.Value, "y"));
            }
        }

        return new MapProjection(mapReadOnly, mundo, notes);
    }

    private static SidecarProjection ReadSidecar(
        IReadOnlyDictionary<string, VaultFile> files,
        string key,
        ISet<string> future)
    {
        var v2 = $".urbe/{key}.v2.json";
        var v1 = $".urbe/{key}.json";

        if (files.TryGetValue(v2, out var v2File))
            return ParseSidecar(v2File, v2, 2, key, future);

        if (files.TryGetValue(v1, out var v1File))
            return ParseSidecar(v1File, v1, 1, key, future);

        return new SidecarProjection(null, 0, false);
    }

    private static SidecarProjection ParseSidecar(
        VaultFile file,
        string path,
        int expectedVersion,
        string key,
        ISet<string> future)
    {
        if (!TryParseObject(file, out var json))
        {
            future.Add(path);
            return new SidecarProjection(path, 0, true);
        }

        using (json)
        {
            var root = json.RootElement;
            var versionAccepted = TryReadInt(root, "version", out var version) && version == expectedVersion;

            // trash v1 historically accepted an omitted version.
            if (expectedVersion == 1 && key == "trash" && !root.TryGetProperty("version", out _))
                versionAccepted = true;

            if (!versionAccepted)
            {
                future.Add(path);
                return new SidecarProjection(path, 0, true);
            }

            return new SidecarProjection(path, CountSidecar(root, key), false);
        }
    }

    private static int CountSidecar(JsonElement root, string key)
    {
        if (key == "history")
        {
            if (!root.TryGetProperty("documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Object)
                return 0;
            return documents.EnumerateObject().Count();
        }

        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return 0;
        return items.GetArrayLength();
    }

    private static JournalProjection ReadJournal(
        IReadOnlyDictionary<string, VaultFile> files,
        ISet<string> future)
    {
        var candidates = new[]
        {
            (Path: ".urbe/journal.v2.json", Version: 2),
            (Path: ".urbe/journal.json", Version: 1)
        };

        foreach (var candidate in candidates)
        {
            if (!files.TryGetValue(candidate.Path, out var file))
                continue;

            if (!TryParseObject(file, out var json))
            {
                future.Add(candidate.Path);
                return JournalProjection.Foreign(candidate.Path);
            }

            using (json)
            {
                var root = json.RootElement;
                if (!TryReadInt(root, "version", out var version) ||
                    version != candidate.Version ||
                    !root.TryGetProperty("documents", out var docs) ||
                    docs.ValueKind != JsonValueKind.Array)
                {
                    future.Add(candidate.Path);
                    return JournalProjection.Foreign(candidate.Path);
                }

                var projected = new List<JournalDocument>();
                try
                {
                    foreach (var item in docs.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object ||
                            !item.TryGetProperty("path", out var pathElement) ||
                            pathElement.ValueKind != JsonValueKind.String)
                            throw new InvalidDataException("Journal contém documento sem path.");

                        var path = NormalizePath(pathElement.GetString()!);
                        if (IsSystemPath(path))
                            throw new InvalidDataException("Journal contém documento em caminho de sistema.");

                        if (!item.TryGetProperty("content", out var contentElement) ||
                            contentElement.ValueKind != JsonValueKind.String)
                            throw new InvalidDataException("Journal contém documento sem conteúdo textual.");

                        string? id = null;
                        if (item.TryGetProperty("id", out var idElement) &&
                            idElement.ValueKind == JsonValueKind.String)
                            id = NullIfBlank(idElement.GetString());

                        projected.Add(new JournalDocument(path, id, contentElement.GetString() ?? string.Empty));
                    }
                }
                catch (InvalidDataException)
                {
                    future.Add(candidate.Path);
                    return JournalProjection.Foreign(candidate.Path);
                }

                JsonElement? metadata = null;
                if (root.TryGetProperty("metadata", out var metadataElement) &&
                    metadataElement.ValueKind == JsonValueKind.Object)
                    metadata = metadataElement.Clone();

                return new JournalProjection(candidate.Path, true, projected, metadata);
            }
        }

        return JournalProjection.None;
    }

    private static Dictionary<string, string> ReadIdentity(
        IReadOnlyDictionary<string, VaultFile> files,
        ISet<string> future)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!files.TryGetValue(IdentityPath, out var file))
            return result;

        if (!TryParseObject(file, out var json))
            return result; // identity is derived; corrupt v1 is ignored, not promoted to authority.

        using (json)
        {
            var root = json.RootElement;
            if (!TryReadInt(root, "version", out var version) || version != 1)
            {
                future.Add(IdentityPath);
                return result;
            }

            if (!root.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in docs.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object ||
                    !property.Value.TryGetProperty("path", out var pathElement) ||
                    pathElement.ValueKind != JsonValueKind.String)
                    continue;

                string path;
                try { path = NormalizePath(pathElement.GetString()!); }
                catch (InvalidDataException) { continue; }

                var id = NullIfBlank(property.Name);
                if (id is not null)
                    result[path] = id;
            }
        }

        return result;
    }

    private static void MarkForwardTextArtifacts(
        IReadOnlyDictionary<string, VaultFile> files,
        ISet<string> future)
    {
        const string themePath = "Personalização/tema.json";
        if (files.TryGetValue(themePath, out var theme) &&
            TryParseObject(theme, out var themeJson))
        {
            using (themeJson)
            {
                if (TryReadInt(themeJson.RootElement, "versao", out var version) && version > 1)
                    future.Add(themePath);
            }
        }

        foreach (var pair in files)
        {
            if (!pair.Key.StartsWith("Páginas/", StringComparison.OrdinalIgnoreCase) ||
                !pair.Key.EndsWith(".page.json", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!TryParseObject(pair.Value, out var pageJson))
                continue;

            using (pageJson)
            {
                if (TryReadInt(pageJson.RootElement, "version", out var version) && version > 1)
                    future.Add(pair.Key);
            }
        }
    }

    private static List<VaultDocument> BuildDiskDocuments(
        IReadOnlyDictionary<string, VaultFile> files,
        MapProjection map,
        IReadOnlyDictionary<string, string> identity)
    {
        var documents = new List<VaultDocument>();
        foreach (var pair in files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsEditableText(pair.Key))
                continue;

            var bytes = pair.Value.Bytes.ToArray();
            var text = TryDecode(bytes);
            map.Notes.TryGetValue(pair.Key, out var metadata);
            var id = metadata?.Id;
            if (id is null)
                identity.TryGetValue(pair.Key, out id);

            var generated = id is null;
            id ??= GenerateDocumentId(pair.Key);

            documents.Add(new VaultDocument(
                pair.Key,
                id,
                generated,
                bytes,
                text,
                metadata?.X,
                metadata?.Y));
        }

        return documents;
    }

    private static List<VaultDocument> BuildJournalDocuments(
        IReadOnlyList<JournalDocument> entries,
        MapProjection map,
        IReadOnlyDictionary<string, string> identity)
    {
        var documents = new List<VaultDocument>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!seen.Add(entry.Path))
                throw new InvalidDataException($"Journal contém caminho duplicado: {entry.Path}");

            map.Notes.TryGetValue(entry.Path, out var metadata);
            var id = entry.Id ?? metadata?.Id;
            if (id is null)
                identity.TryGetValue(entry.Path, out id);

            var generated = id is null;
            id ??= GenerateDocumentId(entry.Path);
            var bytes = Encoding.UTF8.GetBytes(entry.Content);

            documents.Add(new VaultDocument(
                entry.Path,
                id,
                generated,
                bytes,
                entry.Content,
                metadata?.X,
                metadata?.Y));
        }

        documents.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        return documents;
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Length == 0 || normalized[0] == '/')
            throw new InvalidDataException($"Caminho inválido no vault: {path}");

        var segments = normalized.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException($"Caminho inválido no vault: {path}");

        return string.Join('/', segments);
    }

    private static bool IsSystemPath(string path) =>
        path.Split('/').Any(segment => segment.Length > 0 && segment[0] == '.');

    private static bool IsEditableText(string path)
    {
        if (IsSystemPath(path))
            return false;

        var dot = path.LastIndexOf('.');
        return dot >= 0 && TextExtensions.Contains(path[dot..]);
    }

    private static string TryDecode(byte[] bytes) =>
        Encoding.UTF8.GetString(bytes);

    private static bool TryParseObject(VaultFile file, out JsonDocument json)
    {
        try
        {
            json = JsonDocument.Parse(file.Bytes);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
                return true;
            json.Dispose();
        }
        catch (JsonException) { }

        json = null!;
        return false;
    }

    private static bool TryReadInt(JsonElement root, string property, out int value)
    {
        value = default;
        return root.TryGetProperty(property, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value);
    }

    private static double? ReadNumber(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Number)
            return null;
        return element.TryGetDouble(out var number) ? number : null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string GenerateDocumentId(string path)
    {
        _ = path;
        return "doc_" + Guid.NewGuid().ToString("D");
    }

    private sealed record NoteMetadata(string? Id, double? X, double? Y);

    private sealed record MapProjection(
        bool IsReadOnly,
        string? Mundo,
        IReadOnlyDictionary<string, NoteMetadata> Notes)
    {
        public static MapProjection Empty { get; } =
            new(false, null, new Dictionary<string, NoteMetadata>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record SidecarProjection(string? Path, int Count, bool IsFuture);

    private sealed record JournalDocument(string Path, string? Id, string Content);

    private sealed record JournalProjection(
        string? Path,
        bool IsRecovered,
        IReadOnlyList<JournalDocument>? Documents,
        JsonElement? Metadata)
    {
        public static JournalProjection None { get; } = new(null, false, null, null);
        public static JournalProjection Foreign(string path) => new(path, false, null, null);
    }
}
