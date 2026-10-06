using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

internal sealed record FreeNodeType(bool IsContainer);

internal static partial class PageFreeLayout
{
    private const int MaxNodes = 1500;
    private const int MaxDepth = 24;

    private static readonly IReadOnlyDictionary<string, FreeNodeType> Types =
        new Dictionary<string, FreeNodeType>(StringComparer.Ordinal)
        {
            ["box"] = new(true),
            ["heading"] = new(false),
            ["text"] = new(false),
            ["image"] = new(false),
            ["button"] = new(false),
            ["list"] = new(false),
            ["quote"] = new(false),
            ["icon"] = new(false),
            ["divider"] = new(false),
            ["spacer"] = new(false),
            ["video"] = new(false),
            ["note"] = new(false),
            ["html"] = new(false),
            ["table"] = new(false),
            ["code"] = new(false),
            ["formula"] = new(false),
            ["badge"] = new(false),
            ["embed"] = new(false),
            ["pagebreak"] = new(false)
        };

    private static readonly IReadOnlyDictionary<string, string[]> SelectStyles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["display"] = ["stack", "row", "grid", "none"],
            ["justify"] = ["start", "center", "end", "between", "around", "evenly"],
            ["align"] = ["stretch", "start", "center", "end", "baseline"],
            ["self"] = ["auto", "start", "center", "end", "stretch"],
            ["aspect"] = ["1/1", "4/3", "3/2", "16/9", "21/9", "3/4", "2/3", "9/16"],
            ["fit"] = ["cover", "contain"],
            ["borderStyle"] = ["solid", "dashed", "dotted", "double"],
            ["shadow"] = ["none", "soft", "strong", "glow"],
            ["font"] = ["heading", "body", "inter", "manrope", "grotesk", "dmsans", "outfit", "playfair", "fraunces", "lora", "merriweather", "garamond", "cormorant", "crimson", "baskerville", "mono", "system", "serif"],
            ["weight"] = ["300", "400", "500", "600", "700", "800", "900"],
            ["textAlign"] = ["left", "center", "right", "justify"],
            ["transform"] = ["none", "uppercase", "lowercase", "capitalize"],
            ["italic"] = ["yes", "no"]
        };

    private static readonly HashSet<string> ManyLengths =
        new(["padding", "margin", "radius"], StringComparer.Ordinal);

    private static readonly HashSet<string> Lengths =
        new(
            [
                "gap", "minCol", "width", "maxWidth", "minHeight", "height",
                "padding", "margin", "border", "radius", "size", "spacing"
            ],
            StringComparer.Ordinal);

    private static readonly HashSet<string> Colors =
        new(["bg", "bg2", "color", "borderColor"], StringComparer.Ordinal);

    private static readonly HashSet<string> Numbers =
        new(
            [
                "columns", "order", "angle", "opacity", "lineHeight"
            ],
            StringComparer.Ordinal);

    private static readonly HashSet<string> KnownStyles =
        new(
            SelectStyles.Keys
                .Concat(Lengths)
                .Concat(Colors)
                .Concat(Numbers)
                .Concat(["bgImage", "className", "css"]),
            StringComparer.Ordinal);

    public static JsonObject NormalizeRoot(
        JsonNode? source,
        string path,
        List<PageNormalizationIssue> errors,
        List<PageNormalizationIssue> warnings)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var sequence = 0;
        var count = 0;

        JsonObject NormalizeNode(JsonNode? node, string nodePath, int depth)
        {
            if (node is not JsonObject input)
            {
                errors.Add(
                    new PageNormalizationIssue(
                        nodePath,
                        "Cada elemento livre deve ser um objeto {type, …}."));
                return Starter(ref sequence, used);
            }

            var type = PageDocument.StringValue(input["type"]);
            if (type is null || !Types.TryGetValue(type, out var definition))
            {
                errors.Add(
                    new PageNormalizationIssue(
                        nodePath + ".type",
                        "Tipo de elemento livre desconhecido."));
                return Starter(ref sequence, used);
            }

            count++;
            if (count > MaxNodes)
            {
                errors.Add(
                    new PageNormalizationIssue(
                        nodePath,
                        "Elementos demais no layout livre (máximo 1500)."));
                return Starter(ref sequence, used);
            }

            var requestedId = PageDocument.StringValue(input["id"]);
            var id = ValidId(requestedId) && used.Add(requestedId!)
                ? requestedId!
                : NextId(ref sequence, used);

            var output = new JsonObject
            {
                ["id"] = id,
                ["type"] = type
            };

            var content = NormalizeContent(
                type,
                input["content"] as JsonObject);
            if (content.Count > 0)
                output["content"] = content;

            foreach (var key in new[] { "style", "tablet", "mobile" })
            {
                var cleaned = NormalizeStyle(
                    input[key],
                    nodePath + "." + key,
                    errors,
                    warnings);
                if (cleaned.Count > 0)
                    output[key] = cleaned;
            }

            if (definition.IsContainer)
            {
                var children = input["children"] as JsonArray ?? new JsonArray();
                var normalized = new JsonArray();

                if (depth > MaxDepth)
                {
                    errors.Add(
                        new PageNormalizationIssue(
                            nodePath,
                            "Aninhamento fundo demais no layout livre."));
                }
                else
                {
                    for (var index = 0; index < children.Count; index++)
                    {
                        var child = children[index];
                        if (child is not JsonObject childObject)
                        {
                            errors.Add(
                                new PageNormalizationIssue(
                                    nodePath + ".children[" + index + "]",
                                    "Cada elemento livre deve ser um objeto {type, …}."));
                            continue;
                        }

                        var childType =
                            PageDocument.StringValue(childObject["type"]);
                        if (childType is null ||
                            !Types.ContainsKey(childType))
                        {
                            errors.Add(
                                new PageNormalizationIssue(
                                    nodePath + ".children[" + index + "].type",
                                    "Tipo de elemento livre desconhecido."));
                            continue;
                        }

                        normalized.Add(
                            NormalizeNode(
                                childObject,
                                nodePath + ".children[" + index + "]",
                                depth + 1));
                    }
                }

                output["children"] = normalized;
            }
            else if (input["children"] is JsonArray extra &&
                     extra.Count > 0)
            {
                warnings.Add(
                    new PageNormalizationIssue(
                        nodePath + ".children",
                        "Só containers têm filhos; filhos foram ignorados."));
            }

            return output;
        }

        if (source is null)
            return Starter(ref sequence, used);

        var root = NormalizeNode(source, path, 0);
        if (PageDocument.StringValue(root["type"]) == "box")
            return root;

        return new JsonObject
        {
            ["id"] = NextId(ref sequence, used),
            ["type"] = "box",
            ["children"] = new JsonArray(root)
        };
    }

    public static bool IsContainer(string type) =>
        Types.TryGetValue(type, out var definition) &&
        definition.IsContainer;

    private static JsonObject NormalizeContent(
        string type,
        JsonObject? input)
    {
        var source = input ?? new JsonObject();
        var output = new JsonObject();

        void Text(string key, string? fallback = null)
        {
            var value = PageDocument.StringValue(source[key]);
            if (!string.IsNullOrEmpty(value))
                output[key] = value;
            else if (fallback is not null)
                output[key] = fallback;
        }

        void Bool(string key, bool fallback)
        {
            output[key] = source[key] is null
                ? fallback
                : PageDocument.BoolValue(source[key], fallback);
        }

        switch (type)
        {
            case "box":
            {
                var tag = PageDocument.StringValue(source["tag"]) ?? "div";
                output["tag"] =
                    new[] { "div", "section", "header", "footer", "article", "aside", "nav" }
                        .Contains(tag, StringComparer.Ordinal)
                        ? tag
                        : "div";
                Text("link");
                break;
            }
            case "heading":
                Text("text", "Um título");
                output["level"] = Math.Clamp(
                    PageDocument.IntValue(source["level"]) ?? 2,
                    1,
                    6);
                break;
            case "text":
                Text("text", "Escreva aqui.");
                break;
            case "image":
                Text("src");
                Text("alt");
                Text("caption");
                Text("link");
                break;
            case "button":
            {
                Text("label", "Saiba mais");
                Text("url", "#");
                var variant =
                    PageDocument.StringValue(source["variant"]) ?? "primary";
                output["variant"] =
                    new[] { "primary", "secondary", "ghost" }
                        .Contains(variant, StringComparer.Ordinal)
                        ? variant
                        : "primary";
                break;
            }
            case "list":
                Text("items", "Primeiro item\nSegundo item");
                Bool("ordered", false);
                break;
            case "quote":
                Text("text", "Uma frase marcante.");
                Text("author");
                break;
            case "icon":
                Text("emoji", "✨");
                break;
            case "video":
                Text("url");
                break;
            case "note":
                Text("path");
                Bool("showTitle", false);
                break;
            case "html":
                Text("code", "<div>Olá</div>");
                break;
            case "table":
                Text(
                    "md",
                    "| Item | Valor |\n|---|---|\n| Primeiro | 10 |\n| Segundo | 20 |");
                break;
            case "code":
                Text("code", "console.log(\"olá\")");
                Text("lang", "js");
                break;
            case "formula":
                Text("tex", "e^{i\\pi}+1=0");
                break;
            case "badge":
                Text("text", "Novo");
                break;
            case "embed":
                Text("url");
                Text("title", "Conteúdo incorporado");
                break;
        }

        return output;
    }

    private static JsonObject NormalizeStyle(
        JsonNode? node,
        string path,
        List<PageNormalizationIssue> errors,
        List<PageNormalizationIssue> warnings)
    {
        var output = new JsonObject();
        if (node is null)
            return output;
        if (node is not JsonObject source)
        {
            errors.Add(
                new PageNormalizationIssue(
                    path,
                    "Estilo deve ser um objeto."));
            return output;
        }

        foreach (var pair in source)
        {
            var key = pair.Key;
            var value = pair.Value;
            if (value is null)
                continue;

            if (!KnownStyles.Contains(key))
            {
                warnings.Add(
                    new PageNormalizationIssue(
                        path + "." + key,
                        "Estilo desconhecido foi ignorado."));
                continue;
            }

            if (SelectStyles.TryGetValue(key, out var options))
            {
                var text = PageDocument.StringValue(value);
                if (text is not null &&
                    options.Contains(text, StringComparer.Ordinal))
                {
                    output[key] = text;
                }
                else
                {
                    errors.Add(
                        new PageNormalizationIssue(
                            path + "." + key,
                            "Valor de estilo inválido."));
                }
                continue;
            }

            if (Lengths.Contains(key))
            {
                var text = JsonScalarText(value).Trim();
                var valid = ManyLengths.Contains(key)
                    ? LengthsPattern().IsMatch(text)
                    : LengthPattern().IsMatch(text);
                if (valid)
                    output[key] = text;
                else
                {
                    errors.Add(
                        new PageNormalizationIssue(
                            path + "." + key,
                            "Medida CSS inválida."));
                }
                continue;
            }

            if (Colors.Contains(key))
            {
                var text = JsonScalarText(value).Trim();
                if (ColorPattern().IsMatch(text))
                    output[key] = text;
                else
                {
                    errors.Add(
                        new PageNormalizationIssue(
                            path + "." + key,
                            "Cor inválida."));
                }
                continue;
            }

            if (Numbers.Contains(key))
            {
                if (!TryNumber(value, out var number))
                {
                    errors.Add(
                        new PageNormalizationIssue(
                            path + "." + key,
                            "Valor deve ser numérico."));
                    continue;
                }

                output[key] = key switch
                {
                    "columns" => Math.Clamp(number, 0, 12),
                    "order" => Math.Clamp(number, -10, 10),
                    "angle" => Math.Clamp(number, 0, 360),
                    "opacity" => Math.Clamp(number, 0, 1),
                    "lineHeight" => Math.Clamp(number, .8, 3),
                    _ => number
                };
                continue;
            }

            if (key == "bgImage")
            {
                var safe = MarkdownEngine.SafeUrl(JsonScalarText(value));
                if (!string.IsNullOrWhiteSpace(safe) && safe != "#")
                    output[key] = CssImageEncode(safe);
                continue;
            }

            if (key == "className")
            {
                var clean = ClassPattern()
                    .Replace(JsonScalarText(value), string.Empty)
                    .Trim();
                if (clean.Length > 80)
                    clean = clean[..80];
                if (clean.Length > 0)
                    output[key] = clean;
                continue;
            }

            if (key == "css")
            {
                var clean = CssSafe(JsonScalarText(value));
                if (clean.Length > 0)
                    output[key] = clean.Length <= 4000 ? clean : clean[..4000];
            }
        }

        return output;
    }

    public static string CssSafe(string? value)
    {
        var css = value ?? string.Empty;
        css = StyleClosePattern().Replace(css, string.Empty);
        css = ScriptTagPattern().Replace(css, string.Empty);
        css = ImportPattern().Replace(css, string.Empty);
        css = ExpressionPattern().Replace(css, string.Empty);
        css = JavascriptUrlPattern().Replace(css, "url()");
        css = NullControlPattern().Replace(css, string.Empty);
        return css;
    }

    private static JsonObject Starter(
        ref int sequence,
        ISet<string> used)
    {
        var root = new JsonObject
        {
            ["id"] = NextId(ref sequence, used),
            ["type"] = "box",
            ["style"] = new JsonObject
            {
                ["padding"] = "24px 0",
                ["gap"] = "16px"
            }
        };

        root["children"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = NextId(ref sequence, used),
                ["type"] = "heading",
                ["content"] = new JsonObject
                {
                    ["text"] = "Um título",
                    ["level"] = 2
                }
            },
            new JsonObject
            {
                ["id"] = NextId(ref sequence, used),
                ["type"] = "text",
                ["content"] = new JsonObject
                {
                    ["text"] =
                        "Toque num elemento da prévia para editar. Toque de novo num texto para escrever direto nele."
                }
            }
        };
        return root;
    }

    private static string NextId(
        ref int sequence,
        ISet<string> used)
    {
        string id;
        do
        {
            sequence++;
            id = "n" + sequence.ToString(
                CultureInfo.InvariantCulture);
        } while (!used.Add(id));

        return id;
    }

    private static bool ValidId(string? id) =>
        id is not null && IdPattern().IsMatch(id);

    private static string JsonScalarText(JsonNode node)
    {
        if (node is not JsonValue value)
            return string.Empty;
        if (value.TryGetValue<string>(out var text))
            return text;
        if (value.TryGetValue<double>(out var number))
            return number.ToString(CultureInfo.InvariantCulture);
        return value.ToJsonString();
    }

    private static bool TryNumber(
        JsonNode node,
        out double number)
    {
        number = 0;
        if (node is not JsonValue value)
            return false;
        if (value.TryGetValue<double>(out number))
            return double.IsFinite(number);
        if (value.TryGetValue<string>(out var text) &&
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number))
        {
            return double.IsFinite(number);
        }
        return false;
    }

    private static string CssImageEncode(string value) =>
        string.Concat(
            value.Select(ch =>
                char.IsWhiteSpace(ch) ||
                ch is '\'' or '"' or '(' or ')' or '<' or '>' or '\\'
                    ? "%" + ((int)ch).ToString("X2", CultureInfo.InvariantCulture)
                    : ch.ToString()));

    [GeneratedRegex(@"^[\w-]{1,24}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex(
        @"^(-?(\d+(\.\d+)?|\.\d+)(px|rem|em|%|vw|vh|svh|dvh|ch)?|auto|0|(clamp|min|max|calc)\([\w.%,+*/\s()-]{1,80}\))$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LengthPattern();

    [GeneratedRegex(
        @"^(-?(\d+(\.\d+)?|\.\d+)(px|rem|em|%|vw|vh|svh|dvh|ch)?|auto|0|(clamp|min|max|calc)\([\w.%,+*/\s()-]{1,80}\))(\s+(-?(\d+(\.\d+)?|\.\d+)(px|rem|em|%|vw|vh|svh|dvh|ch)?|auto|0|(clamp|min|max|calc)\([\w.%,+*/\s()-]{1,80}\))){0,3}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LengthsPattern();

    [GeneratedRegex(
        @"^(#[0-9a-f]{3,8}|(rgb|hsl)a?\([\d\s.,%/+-]+\)|transparent|currentColor|[a-z]{3,20})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();

    [GeneratedRegex(@"[^\w\s-]", RegexOptions.CultureInvariant)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"</style", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleClosePattern();

    [GeneratedRegex(@"</?script", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptTagPattern();

    [GeneratedRegex(@"@import", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExpressionPattern();

    [GeneratedRegex(
        @"url\s*\(\s*['""]?\s*javascript:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JavascriptUrlPattern();

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", RegexOptions.CultureInvariant)]
    private static partial Regex NullControlPattern();
}
