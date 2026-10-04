using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public enum WorldMapState
{
    Absent,
    Current,
    Future,
    Corrupt
}

public sealed class WorldRegion
{
    private readonly JsonObject _raw;

    internal WorldRegion(JsonObject raw)
    {
        _raw = CloneObject(raw);
        Id = StringValue(_raw["id"]);
        Path = StringValue(_raw["caminho"]) ?? string.Empty;
        Name = StringValue(_raw["nome"]) ?? string.Empty;
        ParentId = StringValue(_raw["parentId"]);
        X = FiniteNumber(_raw["x"]);
        Y = FiniteNumber(_raw["y"]);
        Width = FiniteNumber(_raw["w"]);
        Height = FiniteNumber(_raw["h"]);

        if (_raw["cells"] is JsonArray cells)
        {
            Cells = Array.AsReadOnly(
                cells.Select(StringValue)
                    .Where(value => value is not null)
                    .Cast<string>()
                    .ToArray());
        }
        else
        {
            Cells = Array.Empty<string>();
        }
    }

    public string? Id { get; }
    public string Path { get; }
    public string Name { get; }
    public string? ParentId { get; }
    public double? X { get; }
    public double? Y { get; }
    public double? Width { get; }
    public double? Height { get; }
    public IReadOnlyList<string> Cells { get; }
    public JsonObject Raw => CloneObject(_raw);

    internal static JsonObject CloneObject(JsonObject value) =>
        (JsonObject)value.DeepClone();

    internal static string? StringValue(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal static double? FiniteNumber(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;

        if (value.TryGetValue<double>(out var number) && double.IsFinite(number))
            return number;
        if (value.TryGetValue<int>(out var integer))
            return integer;
        if (value.TryGetValue<long>(out var longInteger))
            return longInteger;
        if (value.TryGetValue<float>(out var single) && float.IsFinite(single))
            return single;
        if (value.TryGetValue<decimal>(out var decimalNumber))
            return (double)decimalNumber;

        return null;
    }
}

public sealed class WorldAsset
{
    private readonly JsonObject _raw;

    internal WorldAsset(JsonObject raw)
    {
        _raw = WorldRegion.CloneObject(raw);
        Id = WorldRegion.StringValue(_raw["id"]);
        Folder = WorldRegion.StringValue(_raw["caminho"]) ?? string.Empty;
        var name = WorldRegion.StringValue(_raw["fileName"]);
        if (string.IsNullOrEmpty(name))
            name = WorldRegion.StringValue(_raw["name"]);
        Name = name ?? string.Empty;
        ParentId = WorldRegion.StringValue(_raw["parentId"]);
        ParentNoteName = WorldRegion.StringValue(_raw["parentNoteName"]);
        X = WorldRegion.FiniteNumber(_raw["x"]);
        Y = WorldRegion.FiniteNumber(_raw["y"]);
        Width = WorldRegion.FiniteNumber(_raw["w"]);
        Height = WorldRegion.FiniteNumber(_raw["h"]);
    }

    public string? Id { get; }
    public string Folder { get; }
    public string Name { get; }
    public string? ParentId { get; }
    public string? ParentNoteName { get; }
    public double? X { get; }
    public double? Y { get; }
    public double? Width { get; }
    public double? Height { get; }
    public JsonObject Raw => WorldRegion.CloneObject(_raw);
}

public sealed class WorldMapMetadata
{
    public const int CurrentVersion = 4;
    public const string MapPath = ".urbe/mapa.json";

    private readonly JsonObject _raw;
    private readonly IReadOnlyDictionary<string, JsonObject> _notes;

    private WorldMapMetadata(
        WorldMapState state,
        int? version,
        string? world,
        bool isReadOnly,
        JsonObject raw)
    {
        State = state;
        Version = version;
        Mundo = world;
        IsReadOnly = isReadOnly;
        _raw = WorldRegion.CloneObject(raw);

        var notes = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (_raw["notas"] is JsonObject noteObject)
        {
            foreach (var pair in noteObject)
            {
                if (pair.Value is JsonObject spatial)
                    notes[pair.Key] = WorldRegion.CloneObject(spatial);
            }
        }

        _notes = new ReadOnlyDictionary<string, JsonObject>(notes);

        Regions = Array.AsReadOnly(
            (_raw["regioes"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(region => new WorldRegion(region))
                .ToArray());

        Assets = Array.AsReadOnly(
            (_raw["construcoes"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(asset => new WorldAsset(asset))
                .ToArray());
    }

    public WorldMapState State { get; }
    public int? Version { get; }
    public string? Mundo { get; }
    public bool IsReadOnly { get; }
    public IReadOnlyList<WorldRegion> Regions { get; }
    public IReadOnlyList<WorldAsset> Assets { get; }
    public JsonObject Raw => WorldRegion.CloneObject(_raw);

    internal IReadOnlyDictionary<string, JsonObject> Notes => _notes;

    public static WorldMapMetadata Empty(bool readOnly = false) =>
        new(
            WorldMapState.Absent,
            null,
            null,
            readOnly,
            new JsonObject());

    public static WorldMapMetadata Parse(
        ReadOnlyMemory<byte> bytes,
        bool forceReadOnly = false)
    {
        if (bytes.Length == 0)
            return new WorldMapMetadata(
                WorldMapState.Corrupt,
                null,
                null,
                true,
                new JsonObject());

        try
        {
            var node = JsonNode.Parse(Encoding.UTF8.GetString(bytes.Span));
            return node is JsonObject root
                ? Parse(root, forceReadOnly)
                : new WorldMapMetadata(
                    WorldMapState.Corrupt,
                    null,
                    null,
                    true,
                    new JsonObject());
        }
        catch (JsonException)
        {
            return new WorldMapMetadata(
                WorldMapState.Corrupt,
                null,
                null,
                true,
                new JsonObject());
        }
    }

    public static WorldMapMetadata Parse(
        JsonObject root,
        bool forceReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(root);

        var clone = WorldRegion.CloneObject(root);
        var state = WorldMapState.Current;
        int? version = null;

        if (clone["v"] is JsonValue versionNode)
        {
            if (!versionNode.TryGetValue<int>(out var parsed) ||
                parsed < 1)
            {
                state = WorldMapState.Corrupt;
            }
            else
            {
                version = parsed;
                if (parsed > CurrentVersion)
                    state = WorldMapState.Future;
            }
        }

        var readOnly = forceReadOnly ||
                       state is WorldMapState.Future or WorldMapState.Corrupt;

        if (!readOnly)
            WorldStableIds.Assign(clone);

        return new WorldMapMetadata(
            state,
            version,
            WorldRegion.StringValue(clone["mundo"]),
            readOnly,
            clone);
    }

    public static WorldMapMetadata FromVault(VaultSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.RecoveredFromJournal)
        {
            throw new InvalidOperationException(
                "O snapshot foi recuperado por journal; o JSON efetivo completo " +
                "do mapa não é exposto pelo VaultSnapshot. Use metadata explícita.");
        }

        if (!snapshot.Files.TryGetValue(MapPath, out var map))
            return Empty(snapshot.IsReadOnly);

        return Parse(
            map.Bytes,
            snapshot.IsReadOnly || snapshot.IsMapReadOnly);
    }
}

public sealed record WorldProjectedDocument(
    string Id,
    string DocumentId,
    string Path,
    string Title,
    string Folder,
    double? X,
    double? Y,
    string? Sprite,
    IReadOnlyList<string> Tags,
    string? Created,
    string? Modified);

public enum WorldProjectionChangeKind
{
    Load,
    PathMigrated,
    SpatialChanged,
    Enabled
}

public sealed class WorldProjectionChangedEventArgs : EventArgs
{
    internal WorldProjectionChangedEventArgs(
        WorldProjectionChangeKind kind,
        long revision,
        string? documentId = null,
        string? from = null,
        string? to = null)
    {
        Kind = kind;
        Revision = revision;
        DocumentId = documentId;
        From = from;
        To = to;
    }

    public WorldProjectionChangeKind Kind { get; }
    public long Revision { get; }
    public string? DocumentId { get; }
    public string? From { get; }
    public string? To { get; }
}

public sealed record WorldProjectionSnapshot(
    bool Enabled,
    long Revision,
    bool IsReadOnly,
    string? Mundo,
    IReadOnlyList<WorldProjectedDocument> Documents,
    IReadOnlyList<WorldRegion> Regions,
    IReadOnlyList<WorldAsset> Assets);

/// <summary>
/// Pure path/spatial projection. It never accesses a filesystem or renderer.
/// </summary>
public sealed class WorldProjection : IDisposable
{
    private readonly DocumentStore _store;
    private readonly Dictionary<string, JsonObject> _spatial =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldRegion> _regions =
        new(StringComparer.Ordinal);

    private WorldMapMetadata _metadata = WorldMapMetadata.Empty();
    private bool _disposed;

    public WorldProjection(DocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _store.Changed += OnDocumentChanged;
    }

    public event EventHandler<WorldProjectionChangedEventArgs>? Changed;

    public bool Enabled { get; private set; } = true;
    public bool IsReadOnly => _metadata.IsReadOnly;
    public long Revision { get; private set; }
    public string? Mundo => _metadata.Mundo;

    public WorldProjectionSnapshot Load(WorldMapMetadata? metadata)
    {
        _metadata = metadata ?? WorldMapMetadata.Empty();
        _spatial.Clear();
        _regions.Clear();

        foreach (var pair in _metadata.Notes)
            _spatial[pair.Key] = WorldRegion.CloneObject(pair.Value);

        foreach (var region in _metadata.Regions)
        {
            if (region.Path.Length > 0)
                _regions[region.Path] = region;
        }

        Revision++;
        Raise(WorldProjectionChangeKind.Load);
        return Snapshot();
    }

    public WorldProjectionSnapshot Load(JsonObject? metadata) =>
        Load(
            metadata is null
                ? WorldMapMetadata.Empty()
                : WorldMapMetadata.Parse(metadata));

    public WorldProjectionSnapshot LoadFromVault(VaultSnapshot snapshot) =>
        Load(WorldMapMetadata.FromVault(snapshot));

    public WorldProjectedDocument? ProjectDocument(string? idOrPath)
    {
        var document = _store.Get(idOrPath);
        if (document is null)
            return null;

        _spatial.TryGetValue(document.Path, out var saved);

        return new WorldProjectedDocument(
            document.Id,
            document.Id,
            document.Path,
            document.Title,
            Directory(document.Path),
            WorldRegion.FiniteNumber(saved?["x"]),
            WorldRegion.FiniteNumber(saved?["y"]),
            FalsyString(saved?["sprite"]),
            document.Tags,
            FalsyString(document.Created) ?? FalsyString(saved?["criado"]),
            FalsyString(document.Modified) ?? FalsyString(saved?["modificado"]));
    }

    public IReadOnlyList<WorldProjectedDocument> Documents() =>
        Array.AsReadOnly(
            _store.List()
                .Select(document => ProjectDocument(document.Id))
                .Where(document => document is not null)
                .Cast<WorldProjectedDocument>()
                .ToArray());

    public WorldRegion? Region(string? path)
    {
        var key = path ?? string.Empty;
        return _regions.TryGetValue(key, out var region)
            ? region
            : null;
    }

    public WorldProjectedDocument? SetSpatial(
        string? idOrPath,
        JsonObject patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var document = _store.Get(idOrPath);
        if (document is null)
            return null;

        var next = _spatial.TryGetValue(document.Path, out var previous)
            ? WorldRegion.CloneObject(previous)
            : new JsonObject();

        foreach (var pair in patch)
            next[pair.Key] = pair.Value?.DeepClone();

        _spatial[document.Path] = next;
        Revision++;
        Raise(
            WorldProjectionChangeKind.SpatialChanged,
            document.Id);

        return ProjectDocument(document.Id);
    }

    public WorldProjectedDocument? SetSpatial(
        string? idOrPath,
        double? x,
        double? y)
    {
        var patch = new JsonObject();
        if (x is not null)
            patch["x"] = x.Value;
        if (y is not null)
            patch["y"] = y.Value;
        return SetSpatial(idOrPath, patch);
    }

    public JsonObject Metadata(JsonObject? baseMap = null)
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException(
                "O mapa está em modo somente leitura e não pode gerar metadata para persistência.");
        }

        var output = baseMap is null
            ? _metadata.Raw
            : WorldRegion.CloneObject(baseMap);

        var notes = new JsonObject();
        foreach (var document in _store.List())
        {
            var spatial = _spatial.TryGetValue(document.Path, out var saved)
                ? WorldRegion.CloneObject(saved)
                : new JsonObject();

            spatial["id"] = document.Id;

            var tags = new JsonArray();
            foreach (var tag in document.Tags)
                tags.Add(tag);
            spatial["tags"] = tags;

            spatial["criado"] =
                FalsyString(document.Created) ??
                FalsyString(spatial["criado"]);
            spatial["modificado"] =
                FalsyString(document.Modified) ??
                FalsyString(spatial["modificado"]);

            notes[document.Path] = spatial;
        }

        output["notas"] = notes;

        var regions = new JsonArray();
        foreach (var region in _regions.Values)
            regions.Add(region.Raw);
        output["regioes"] = regions;

        return output;
    }

    public bool SetEnabled(bool value)
    {
        Enabled = value;
        Raise(WorldProjectionChangeKind.Enabled);
        return Enabled;
    }

    public WorldProjectionSnapshot Snapshot() =>
        new(
            Enabled,
            Revision,
            IsReadOnly,
            Mundo,
            Documents(),
            Array.AsReadOnly(_regions.Values.ToArray()),
            _metadata.Assets);

    public void Dispose()
    {
        if (_disposed)
            return;

        _store.Changed -= OnDocumentChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnDocumentChanged(
        object? sender,
        DocumentStoreChangedEventArgs args)
    {
        if (args.Kind != DocumentStoreChangeKind.Updated ||
            args.Previous is null ||
            args.Document is null ||
            string.Equals(
                args.Previous.Path,
                args.Document.Path,
                StringComparison.Ordinal))
            return;

        if (!_spatial.TryGetValue(args.Previous.Path, out var spatial))
            return;

        _spatial.Remove(args.Previous.Path);
        _spatial[args.Document.Path] = spatial;
        Revision++;

        Raise(
            WorldProjectionChangeKind.PathMigrated,
            args.Document.Id,
            args.Previous.Path,
            args.Document.Path);
    }

    private void Raise(
        WorldProjectionChangeKind kind,
        string? documentId = null,
        string? from = null,
        string? to = null)
    {
        Changed?.Invoke(
            this,
            new WorldProjectionChangedEventArgs(
                kind,
                Revision,
                documentId,
                from,
                to));
    }

    private static string Directory(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    private static string? FalsyString(JsonNode? node) =>
        FalsyString(WorldRegion.StringValue(node));

    private static string? FalsyString(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
