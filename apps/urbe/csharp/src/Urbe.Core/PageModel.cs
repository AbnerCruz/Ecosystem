using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public enum PageDocumentState
{
    Current,
    Future,
    Corrupt
}

/// <summary>
/// Lossless representation of a persisted Urbe page. Reading is permissive;
/// writing is refused for future/corrupt formats so unknown data is never
/// silently normalized away (REQ-035 / REQ-049).
/// </summary>
public sealed class PageDocument
{
    public const int CurrentVersion = 1;

    private readonly JsonObject _raw;

    private PageDocument(
        PageDocumentState state,
        int? version,
        JsonObject raw)
    {
        State = state;
        Version = version;
        _raw = Clone(raw);
    }

    public PageDocumentState State { get; }
    public int? Version { get; }
    public bool IsReadOnly => State is PageDocumentState.Future or PageDocumentState.Corrupt;
    public string? Kind => StringValue(_raw["kind"]);
    public string Title => StringValue(_raw["meta"]?["title"]) ?? "Página sem título";
    public string Language => StringValue(_raw["meta"]?["lang"]) ?? "pt-BR";
    public JsonObject Raw => Clone(_raw);

    public IReadOnlyList<PageSection> Sections =>
        Array.AsReadOnly(
            (_raw["sections"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(section => new PageSection(section))
                .ToArray());

    public static PageDocument Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Corrupt();

        try
        {
            return JsonNode.Parse(json) is JsonObject root
                ? Parse(root)
                : Corrupt();
        }
        catch (JsonException)
        {
            return Corrupt();
        }
    }

    public static PageDocument Parse(ReadOnlyMemory<byte> bytes) =>
        Parse(bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes.Span));

    public static PageDocument Parse(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var version = IntValue(root["version"]);
        var state = version switch
        {
            > CurrentVersion => PageDocumentState.Future,
            <= 0 => PageDocumentState.Corrupt,
            _ => PageDocumentState.Current
        };

        // Pages written before the explicit version existed remain readable.
        if (root["version"] is null)
            state = PageDocumentState.Current;

        return new PageDocument(state, version, root);
    }

    public JsonObject ForWrite()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException(
                State == PageDocumentState.Future
                    ? "Página de versão futura é somente leitura."
                    : "Página inválida é somente leitura.");
        }

        return Clone(_raw);
    }

    public string SerializeForWrite(bool indented = true) =>
        ForWrite().ToJsonString(new JsonSerializerOptions { WriteIndented = indented });

    public PageDocument WithMeta(string key, JsonNode? value)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Página somente leitura não pode ser alterada.");

        var next = Clone(_raw);
        var meta = next["meta"] as JsonObject ?? new JsonObject();
        meta[key] = value?.DeepClone();
        next["meta"] = meta;
        return Parse(next);
    }

    private static PageDocument Corrupt() =>
        new(PageDocumentState.Corrupt, null, new JsonObject());

    internal static JsonObject Clone(JsonObject value) =>
        (JsonObject)value.DeepClone();

    internal static string? StringValue(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        return value.TryGetValue<string>(out var text) ? text : null;
    }

    internal static int? IntValue(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<int>(out var number))
            return number;
        if (value.TryGetValue<long>(out var longNumber) &&
            longNumber is >= int.MinValue and <= int.MaxValue)
            return (int)longNumber;
        return null;
    }
}

public sealed class PageSection
{
    private readonly JsonObject _raw;

    internal PageSection(JsonObject raw)
    {
        _raw = PageDocument.Clone(raw);
    }

    public string? Id => PageDocument.StringValue(_raw["id"]);
    public string Type => PageDocument.StringValue(_raw["type"]) ?? string.Empty;
    public JsonObject Props => _raw["props"] is JsonObject props
        ? PageDocument.Clone(props)
        : new JsonObject();
    public JsonObject Style => _raw["style"] is JsonObject style
        ? PageDocument.Clone(style)
        : new JsonObject();
    public JsonObject Raw => PageDocument.Clone(_raw);
}
