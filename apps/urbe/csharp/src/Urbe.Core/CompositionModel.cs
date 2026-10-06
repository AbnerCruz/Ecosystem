using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public enum CompositionFileState
{
    Current,
    Future,
    Corrupt
}

public sealed class CompositionFile
{
    public const int CurrentVersion = 1;

    private readonly JsonObject _raw;

    private CompositionFile(
        CompositionFileState state,
        int? version,
        JsonObject raw)
    {
        State = state;
        Version = version;
        _raw = PageDocument.Clone(raw);
    }

    public CompositionFileState State { get; }
    public int? Version { get; }
    public bool IsReadOnly => State is CompositionFileState.Future or CompositionFileState.Corrupt;
    public JsonObject Raw => PageDocument.Clone(_raw);

    public IReadOnlyList<CompositionItem> Items =>
        Array.AsReadOnly(
            (_raw["items"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(item => new CompositionItem(item))
                .ToArray());

    public static CompositionFile Parse(string? json)
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

    public static CompositionFile Parse(ReadOnlyMemory<byte> bytes) =>
        Parse(bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes.Span));

    public static CompositionFile Parse(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var version = PageDocument.IntValue(root["version"]);
        var state = version switch
        {
            > CurrentVersion => CompositionFileState.Future,
            <= 0 => CompositionFileState.Corrupt,
            _ => CompositionFileState.Current
        };

        if (root["version"] is null)
            state = CompositionFileState.Current;

        return new CompositionFile(state, version, root);
    }

    private static CompositionFile Corrupt() =>
        new(CompositionFileState.Corrupt, null, new JsonObject());
}

public sealed class CompositionItem
{
    private readonly JsonObject _raw;

    internal CompositionItem(JsonObject raw)
    {
        _raw = PageDocument.Clone(raw);
    }

    public string Id => PageDocument.StringValue(_raw["id"]) ?? string.Empty;
    public string Name => PageDocument.StringValue(_raw["name"]) ?? "Composição";
    public string Type => PageDocument.StringValue(_raw["type"]) ?? "document";
    public string Theme => PageDocument.StringValue(_raw["theme"]) ?? "clean";
    public string CustomCss => PageDocument.StringValue(_raw["customCSS"]) ?? string.Empty;
    public string HtmlSource => PageDocument.StringValue(_raw["htmlSource"]) ?? string.Empty;
    public JsonObject Raw => PageDocument.Clone(_raw);
    public JsonObject Styles => _raw["styles"] is JsonObject styles
        ? PageDocument.Clone(styles)
        : new JsonObject();
    public JsonObject Overrides => _raw["overrides"] is JsonObject overrides
        ? PageDocument.Clone(overrides)
        : new JsonObject();

    public IReadOnlyList<string> Sources => Strings(_raw["sources"]);
    public IReadOnlyList<string> Order => Strings(_raw["order"]);

    public IReadOnlyList<string> OrderedSources =>
        Order.Count > 0 ? Order : Sources;

    private static IReadOnlyList<string> Strings(JsonNode? node) =>
        Array.AsReadOnly(
            (node as JsonArray ?? [])
                .Select(PageDocument.StringValue)
                .Where(value => !string.IsNullOrEmpty(value))
                .Cast<string>()
                .ToArray());
}

public sealed record CompositionPageConversion(
    string CompositionId,
    string PagePath,
    PageDocument Page,
    IReadOnlyList<string> MissingDocumentIds);

/// <summary>
/// Pure, deterministic conversion. It never writes the vault and never removes
/// the legacy composition source. Persistence/backup is a separate critical
/// user-data step.
/// </summary>
public static class CompositionPageConverter
{
    public static CompositionPageConversion Convert(
        CompositionItem composition,
        DocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(documents);

        var sections = new JsonArray();
        var missing = new List<string>();

        var index = 0;
        foreach (var documentId in composition.OrderedSources)
        {
            index++;
            var document = documents.Get(documentId);
            if (document is null)
                missing.Add(documentId);

            sections.Add(
                new JsonObject
                {
                    ["id"] = SectionId(composition.Id, index),
                    ["type"] = "note",
                    ["props"] = new JsonObject
                    {
                        ["path"] = document?.Path ?? string.Empty,
                        ["showTitle"] = true,
                        ["showMeta"] = false
                    },
                    ["style"] = new JsonObject()
                });
        }

        var legacy = composition.Raw;
        var meta = new JsonObject
        {
            ["title"] = composition.Name,
            ["lang"] = "pt-BR",
            ["legacyComposition"] = legacy
        };

        var theme = new JsonObject();
        if (composition.CustomCss.Length > 0)
            theme["css"] = composition.CustomCss;

        // Keep original theme/style data losslessly. Current page fields do not
        // express every composition override, so claiming a lossy mapping would
        // violate ADR-0009.
        theme["legacyCompositionTheme"] = composition.Theme;
        theme["legacyCompositionStyles"] = composition.Styles;
        theme["legacyCompositionOverrides"] = composition.Overrides;
        if (composition.HtmlSource.Length > 0)
            theme["legacyCompositionHtmlSource"] = composition.HtmlSource;

        var root = new JsonObject
        {
            ["version"] = PageDocument.CurrentVersion,
            ["kind"] = "urbe-page",
            ["meta"] = meta,
            ["theme"] = theme,
            ["layout"] = new JsonObject { ["format"] = "web" },
            ["sections"] = sections
        };

        return new CompositionPageConversion(
            composition.Id,
            PagePath(composition.Name),
            PageDocument.Parse(root),
            missing.AsReadOnly());
    }

    private static string PagePath(string name)
    {
        var compact = new string(
            (name.Length == 0 ? "Composição" : name)
                .Select(ch => ch is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'
                    ? '-'
                    : ch)
                .ToArray()).Trim().Trim('.');

        if (compact.Length == 0)
            compact = "Composição";

        return "Páginas/Composições/" + compact + ".page.json";
    }

    private static string SectionId(string compositionId, int index)
    {
        var source = string.IsNullOrWhiteSpace(compositionId)
            ? "composition"
            : compositionId;
        var safe = new string(
            source.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-'
                ? ch
                : '_').ToArray());

        if (safe.Length > 28)
            safe = safe[..28];

        return safe + "_note_" + index.ToString(CultureInfo.InvariantCulture);
    }
}
