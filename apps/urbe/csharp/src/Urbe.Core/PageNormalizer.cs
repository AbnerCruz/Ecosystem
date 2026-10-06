using System.Text.Json.Nodes;

namespace Urbe.Core;

public sealed record PageNormalizationIssue(string Path, string Message);

public sealed record PageNormalizationResult(
    PageDocument Page,
    IReadOnlyList<PageNormalizationIssue> Errors,
    IReadOnlyList<PageNormalizationIssue> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Lossless defaults/diagnostics for page documents. Unlike the legacy JS
/// normalizer, unknown fields are never dropped: REQ-049 requires forward data
/// to survive an open/save cycle.
/// </summary>
public static class PageNormalizer
{
    private static readonly IReadOnlySet<string> KnownBlockTypes =
        new HashSet<string>(
            [
                "hero", "text", "note", "notes", "features", "cards",
                "gallery", "image", "video", "quote", "testimonials",
                "stats", "timeline", "faq", "cta", "columns", "pricing",
                "contact", "countdown", "code", "toc", "divider", "html",
                "bookcover", "titlepage", "copyright", "dedication",
                "booktoc", "part", "chapter", "chapters", "about", "colophon",
                "free"
            ],
            StringComparer.Ordinal);

    public static PageNormalizationResult Normalize(PageDocument page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var errors = new List<PageNormalizationIssue>();
        var warnings = new List<PageNormalizationIssue>();

        if (page.IsReadOnly)
        {
            errors.Add(
                new PageNormalizationIssue(
                    "version",
                    page.State == PageDocumentState.Future
                        ? "Página futura é somente leitura; normalização recusada."
                        : "Página inválida é somente leitura; normalização recusada."));
            return Result(page, errors, warnings);
        }

        var root = page.Raw;

        if (root["kind"] is null)
        {
            root["kind"] = "urbe-page";
        }
        else
        {
            var kind = PageDocument.StringValue(root["kind"]);
            if (kind is not "urbe-page" and not "urbe-template")
            {
                warnings.Add(
                    new PageNormalizationIssue(
                        "kind",
                        "Kind desconhecido foi preservado sem alteração."));
            }
        }

        ApplyObjectDefaults(
            root,
            "meta",
            "meta",
            errors,
            new Dictionary<string, JsonNode?>
            {
                ["title"] = "Página sem título",
                ["lang"] = "pt-BR"
            });

        ApplyObjectDefaults(
            root,
            "theme",
            "theme",
            errors,
            new Dictionary<string, JsonNode?>
            {
                ["preset"] = "aurora",
                ["mode"] = "",
                ["spacing"] = "comfortable",
                ["scale"] = 1,
                ["width"] = 1120,
                ["shadow"] = "soft",
                ["animations"] = true
            });

        if (root["theme"] is JsonObject normalizedTheme)
            PageThemeCatalog.Validate(normalizedTheme, errors);

        ApplyObjectDefaults(
            root,
            "layout",
            "layout",
            errors,
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
            });

        if (root["sections"] is null)
        {
            root["sections"] = new JsonArray();
        }
        else if (root["sections"] is not JsonArray sections)
        {
            errors.Add(
                new PageNormalizationIssue(
                    "sections",
                    "Sections deve ser uma lista; valor original preservado."));
        }
        else
        {
            NormalizeSections(sections, errors, warnings);
        }

        return Result(PageDocument.Parse(root), errors, warnings);
    }

    private static void NormalizeSections(
        JsonArray sections,
        List<PageNormalizationIssue> errors,
        List<PageNormalizationIssue> warnings)
    {
        for (var index = 0; index < sections.Count; index++)
        {
            var path = "sections[" + index + "]";
            if (sections[index] is not JsonObject section)
            {
                errors.Add(
                    new PageNormalizationIssue(
                        path,
                        "Seção não é um objeto; valor original preservado."));
                continue;
            }

            var type = PageDocument.StringValue(section["type"]);
            if (string.IsNullOrWhiteSpace(type))
            {
                errors.Add(
                    new PageNormalizationIssue(
                        path + ".type",
                        "Tipo da seção ausente; seção preservada."));
            }
            else if (!KnownBlockTypes.Contains(type))
            {
                warnings.Add(
                    new PageNormalizationIssue(
                        path + ".type",
                        "Tipo desconhecido preservado para compatibilidade futura."));
            }

            EnsureObject(section, "props", path + ".props", errors);
            EnsureObject(section, "style", path + ".style", errors);

            if (section["style"] is JsonObject style)
            {
                if (style["hidden"] is null)
                    style["hidden"] = false;
                if (style["menu"] is null)
                    style["menu"] = false;
                PageThemeCatalog.ValidateSectionStyle(style, path + ".style", errors);
            }

            if (section["props"] is JsonObject props && type is not null)
            {
                ApplyBlockDefaults(type, props);
                if (type == "free")
                {
                    Default(props, "sheet", "page");
                    props["root"] = PageFreeLayout.NormalizeRoot(
                        props["root"],
                        path + ".props.root",
                        errors,
                        warnings);
                }
            }
        }
    }

    private static void ApplyBlockDefaults(string type, JsonObject props)
    {
        switch (type)
        {
            case "hero":
                Default(props, "title", "Um título que diz tudo");
                Default(props, "subtitle", "Uma frase curta que explica o que é, para quem é e por que importa.");
                Default(props, "layout", "center");
                Default(props, "height", "tall");
                Default(props, "stack", false);
                break;
            case "text":
                Default(props, "markdown", "Escreva aqui em **Markdown**.");
                Default(props, "columns", 1);
                break;
            case "note":
                Default(props, "showTitle", true);
                Default(props, "showMeta", false);
                break;
            case "notes":
                Default(props, "source", "folder");
                Default(props, "layout", "cards");
                Default(props, "columns", 3);
                Default(props, "limit", 24);
                Default(props, "sort", "title");
                Default(props, "excerpt", true);
                Default(props, "expand", false);
                break;
            case "image":
                Default(props, "size", "wide");
                Default(props, "rounded", true);
                break;
            case "faq":
                Default(props, "openFirst", true);
                break;
            case "divider":
                Default(props, "style", "line");
                break;
            case "bookcover":
                Default(props, "title", "O título do livro");
                Default(props, "author", "Nome do autor");
                Default(props, "style", "classic");
                break;
            case "titlepage":
                Default(props, "title", "O título do livro");
                Default(props, "author", "Nome do autor");
                break;
            case "booktoc":
                Default(props, "title", "Sumário");
                break;
            case "part":
                Default(props, "title", "Título da parte");
                break;
            case "chapter":
                Default(props, "title", "Título do capítulo");
                Default(props, "source", "text");
                Default(props, "numbered", true);
                Default(props, "dropCap", true);
                break;
            case "chapters":
                Default(props, "sort", "path");
                Default(props, "dropCap", true);
                break;
            case "about":
                Default(props, "title", "Sobre o autor");
                break;
            case "dedication":
                Default(props, "kind", "dedication");
                break;
        }
    }

    private static void ApplyObjectDefaults(
        JsonObject root,
        string key,
        string path,
        List<PageNormalizationIssue> errors,
        IReadOnlyDictionary<string, JsonNode?> defaults)
    {
        if (root[key] is null)
            root[key] = new JsonObject();

        if (root[key] is not JsonObject obj)
        {
            errors.Add(
                new PageNormalizationIssue(
                    path,
                    "Esperado objeto; valor original preservado."));
            return;
        }

        foreach (var pair in defaults)
            Default(obj, pair.Key, pair.Value);
    }

    private static void EnsureObject(
        JsonObject parent,
        string key,
        string path,
        List<PageNormalizationIssue> errors)
    {
        if (parent[key] is null)
        {
            parent[key] = new JsonObject();
            return;
        }

        if (parent[key] is not JsonObject)
        {
            errors.Add(
                new PageNormalizationIssue(
                    path,
                    "Esperado objeto; valor original preservado."));
        }
    }

    private static void Default(JsonObject obj, string key, JsonNode? value)
    {
        if (obj[key] is null)
            obj[key] = value?.DeepClone();
    }

    private static PageNormalizationResult Result(
        PageDocument page,
        List<PageNormalizationIssue> errors,
        List<PageNormalizationIssue> warnings) =>
        new(
            page,
            errors.AsReadOnly(),
            warnings.AsReadOnly());
}
