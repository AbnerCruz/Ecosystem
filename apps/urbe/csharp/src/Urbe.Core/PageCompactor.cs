using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// Removes only defaults whose restoration is defined by PageNormalizer.
/// Unknown keys are deliberately preserved (REQ-049), unlike the historical
/// JS compact representation which could discard fields it did not know.
/// </summary>
public static class PageCompactor
{
    private static readonly IReadOnlyDictionary<string, JsonNode?> MetaDefaults =
        new Dictionary<string, JsonNode?>
        {
            ["title"] = "Página sem título",
            ["lang"] = "pt-BR"
        };

    private static readonly IReadOnlyDictionary<string, JsonNode?> ThemeDefaults =
        new Dictionary<string, JsonNode?>
        {
            ["preset"] = "aurora",
            ["mode"] = "",
            ["spacing"] = "comfortable",
            ["scale"] = 1,
            ["width"] = 1120,
            ["shadow"] = "soft",
            ["animations"] = true
        };

    private static readonly IReadOnlyDictionary<string, JsonNode?> LayoutDefaults =
        new Dictionary<string, JsonNode?>
        {
            ["nav"] = true,
            ["sticky"] = true,
            ["themeToggle"] = true,
            ["backToTop"] = true,
            ["progress"] = false,
            ["format"] = "web",
            ["pageSize"] = "a5",
            ["margins"] = "normal",
            ["pageNumbers"] = true,
            ["runningHead"] = "",
            ["chapterStyle"] = "word",
            ["recto"] = false,
            ["justify"] = true,
            ["indent"] = true
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonNode?>>
        BlockDefaults =
            new Dictionary<string, IReadOnlyDictionary<string, JsonNode?>>(StringComparer.Ordinal)
            {
                ["hero"] = Defaults(
                    ("title", "Um título que diz tudo"),
                    ("subtitle", "Uma frase curta que explica o que é, para quem é e por que importa."),
                    ("layout", "center"),
                    ("height", "tall"),
                    ("stack", false)),
                ["text"] = Defaults(
                    ("markdown", "Escreva aqui em **Markdown**."),
                    ("columns", 1)),
                ["note"] = Defaults(("showTitle", true), ("showMeta", false)),
                ["notes"] = Defaults(
                    ("source", "folder"),
                    ("layout", "cards"),
                    ("columns", 3),
                    ("limit", 24),
                    ("sort", "title"),
                    ("excerpt", true),
                    ("expand", false)),
                ["image"] = Defaults(("size", "wide"), ("rounded", true)),
                ["faq"] = Defaults(("openFirst", true)),
                ["divider"] = Defaults(("style", "line")),
                ["bookcover"] = Defaults(
                    ("title", "O título do livro"),
                    ("author", "Nome do autor"),
                    ("style", "classic")),
                ["titlepage"] = Defaults(
                    ("title", "O título do livro"),
                    ("author", "Nome do autor")),
                ["booktoc"] = Defaults(("title", "Sumário")),
                ["part"] = Defaults(("title", "Título da parte")),
                ["chapter"] = Defaults(
                    ("title", "Título do capítulo"),
                    ("source", "text"),
                    ("numbered", true),
                    ("dropCap", true)),
                ["chapters"] = Defaults(("sort", "path"), ("dropCap", true)),
                ["about"] = Defaults(("title", "Sobre o autor")),
                ["dedication"] = Defaults(("kind", "dedication")),
                ["free"] = Defaults(("sheet", "page"))
            };

    public static PageDocument Compact(PageDocument page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.IsReadOnly)
            throw new InvalidOperationException("Página futura/inválida não pode ser compactada.");

        var normalized = PageNormalizer.Normalize(page);
        if (!normalized.IsValid)
        {
            throw new InvalidOperationException(
                "Página inválida não pode ser compactada sem risco de perda.");
        }

        var root = normalized.Page.Raw;
        RemoveDefaults(root, "meta", MetaDefaults);
        RemoveDefaults(root, "theme", ThemeDefaults);
        RemoveDefaults(root, "layout", LayoutDefaults);

        if (root["sections"] is JsonArray sections)
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                var type = PageDocument.StringValue(section["type"]) ?? string.Empty;

                if (section["style"] is JsonObject style)
                {
                    RemoveIfEqual(style, "hidden", false);
                    RemoveIfEqual(style, "menu", false);
                    if (style.Count == 0)
                        section.Remove("style");
                }

                if (section["props"] is JsonObject props)
                {
                    if (BlockDefaults.TryGetValue(type, out var defaults))
                    {
                        foreach (var pair in defaults)
                            RemoveIfEqual(props, pair.Key, pair.Value);
                    }
                    if (props.Count == 0)
                        section.Remove("props");
                }
            }
        }

        root["version"] ??= PageDocument.CurrentVersion;
        return PageDocument.Parse(root);
    }

    private static void RemoveDefaults(
        JsonObject parent,
        string key,
        IReadOnlyDictionary<string, JsonNode?> defaults)
    {
        if (parent[key] is not JsonObject obj)
            return;

        foreach (var pair in defaults)
            RemoveIfEqual(obj, pair.Key, pair.Value);

        if (obj.Count == 0)
            parent.Remove(key);
    }

    private static void RemoveIfEqual(
        JsonObject obj,
        string key,
        JsonNode? expected)
    {
        if (obj.TryGetPropertyValue(key, out var actual) &&
            JsonNode.DeepEquals(actual, expected))
        {
            obj.Remove(key);
        }
    }

    private static IReadOnlyDictionary<string, JsonNode?> Defaults(
        params (string Key, object? Value)[] entries)
    {
        var output = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            output[entry.Key] = entry.Value switch
            {
                null => null,
                string value => JsonValue.Create(value),
                bool value => JsonValue.Create(value),
                int value => JsonValue.Create(value),
                long value => JsonValue.Create(value),
                double value => JsonValue.Create(value),
                _ => throw new InvalidOperationException(
                    "Tipo de default não suportado: " + entry.Value.GetType().Name)
            };
        }
        return output;
    }
}
