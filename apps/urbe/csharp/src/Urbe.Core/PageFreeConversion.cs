using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// Deterministic conversion of the legacy closed page blocks that already have
/// a semantic equivalent in the free-layout tree. Conversion is in-memory only:
/// callers decide if/when a converted page is persisted.
/// </summary>
public static partial class PageFreeConversion
{
    private static readonly IReadOnlySet<string> Convertible =
        new HashSet<string>(
            [
                "text", "hero", "quote", "image", "bookcover", "titlepage",
                "dedication", "copyright", "colophon", "about", "part",
                "chapter", "note"
            ],
            StringComparer.Ordinal);

    public static IReadOnlySet<string> ConvertibleTypes => Convertible;

    public static JsonObject? FromBlock(
        PageSection section,
        DocumentStore? documents = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!Convertible.Contains(section.Type))
            return null;

        var tree = Convert(section.Type, section.Props, documents);
        if (tree is null)
            return null;

        var errors = new List<PageNormalizationIssue>();
        var warnings = new List<PageNormalizationIssue>();
        var normalized = PageFreeLayout.NormalizeRoot(
            tree,
            "free.root",
            errors,
            warnings);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Conversão para layout livre produziu uma árvore inválida: " +
                string.Join("; ", errors.Select(error => error.Path + ": " + error.Message)));
        }

        return normalized;
    }

    public static IReadOnlyList<JsonObject> Paragraphs(string? markdown)
    {
        var output = new List<JsonObject>();
        var normalized = (markdown ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var raw in BlankParagraphPattern().Split(normalized))
        {
            var text = raw.Trim();
            if (text.Length == 0)
                continue;

            var heading = HeadingPattern().Match(text);
            if (heading.Success && !text.Contains('\n'))
            {
                output.Add(
                    Node(
                        "heading",
                        new JsonObject
                        {
                            ["text"] = heading.Groups[2].Value,
                            ["level"] = Math.Min(6, heading.Groups[1].Value.Length + 1)
                        }));
                continue;
            }

            if (DividerPattern().IsMatch(text))
            {
                output.Add(Node("divider"));
                continue;
            }

            if (text.StartsWith('>') && !CalloutPattern().IsMatch(text))
            {
                output.Add(
                    Node(
                        "quote",
                        new JsonObject
                        {
                            ["text"] = QuotePrefixPattern().Replace(text, string.Empty)
                        }));
                continue;
            }

            var image = ImagePattern().Match(text);
            if (image.Success)
            {
                output.Add(
                    Node(
                        "image",
                        new JsonObject
                        {
                            ["src"] = image.Groups[2].Value,
                            ["alt"] = image.Groups[1].Value
                        }));
                continue;
            }

            output.Add(Node("text", new JsonObject { ["text"] = text }));
        }

        return output.AsReadOnly();
    }

    private static JsonObject? Convert(
        string type,
        JsonObject props,
        DocumentStore? documents)
    {
        switch (type)
        {
            case "text":
            {
                var children = new List<JsonObject>();
                var title = Text(props["title"]);
                if (!string.IsNullOrEmpty(title))
                {
                    children.Add(
                        Node(
                            "heading",
                            new JsonObject { ["text"] = title, ["level"] = 2 }));
                }
                children.AddRange(Paragraphs(Text(props["markdown"])));
                return Node(
                    "box",
                    style: new JsonObject { ["gap"] = "14px" },
                    children: children);
            }

            case "hero":
            {
                var children = new List<JsonObject>();
                var eyebrow = Text(props["eyebrow"]);
                if (!string.IsNullOrEmpty(eyebrow))
                {
                    children.Add(
                        Node(
                            "text",
                            new JsonObject { ["text"] = eyebrow },
                            new JsonObject
                            {
                                ["transform"] = "uppercase",
                                ["spacing"] = ".12em",
                                ["size"] = ".85rem",
                                ["weight"] = "700"
                            }));
                }

                children.Add(
                    Node(
                        "heading",
                        new JsonObject
                        {
                            ["text"] = Text(props["title"]) ?? string.Empty,
                            ["level"] = 1
                        }));

                children.AddRange(Paragraphs(Text(props["subtitle"])));

                var image = Text(props["image"]);
                if (!string.IsNullOrEmpty(image))
                {
                    children.Add(
                        Node(
                            "image",
                            new JsonObject
                            {
                                ["src"] = image,
                                ["alt"] = Text(props["title"]) ?? string.Empty
                            },
                            new JsonObject { ["radius"] = "16px" }));
                }

                if (props["buttons"] is JsonArray buttons)
                {
                    var buttonNodes = new List<JsonObject>();
                    foreach (var button in buttons.OfType<JsonObject>())
                    {
                        var label = Text(button["label"]);
                        if (string.IsNullOrEmpty(label))
                            continue;

                        var content = new JsonObject
                        {
                            ["label"] = label,
                            ["url"] = Text(button["url"]) ?? "#"
                        };
                        var variant = Text(button["variant"]);
                        if (!string.IsNullOrEmpty(variant))
                            content["variant"] = variant;
                        buttonNodes.Add(Node("button", content));
                    }

                    if (buttonNodes.Count > 0)
                    {
                        children.Add(
                            Node(
                                "box",
                                style: new JsonObject
                                {
                                    ["display"] = "row",
                                    ["gap"] = "12px",
                                    ["minCol"] = "160px"
                                },
                                children: buttonNodes));
                    }
                }

                var layout = Text(props["layout"]);
                var height = Text(props["height"]);
                var style = new JsonObject
                {
                    ["gap"] = "18px",
                    ["justify"] = "center"
                };
                style["align"] = layout == "center" ? "center" : "stretch";
                if (layout == "center")
                    style["textAlign"] = "center";
                if (height == "screen")
                    style["minHeight"] = "100vh";
                else if (height == "tall")
                    style["minHeight"] = "70vh";

                return Node(
                    "box",
                    new JsonObject { ["tag"] = "header" },
                    style,
                    children);
            }

            case "quote":
            {
                var author = string.Join(
                    ", ",
                    new[] { Text(props["author"]), Text(props["role"]) }
                        .Where(value => !string.IsNullOrEmpty(value)));
                var content = new JsonObject
                {
                    ["text"] = Text(props["text"]) ?? string.Empty
                };
                if (author.Length > 0)
                    content["author"] = author;
                return Node("box", children: [Node("quote", content)]);
            }

            case "image":
            {
                var content = new JsonObject
                {
                    ["src"] = Text(props["src"]) ?? Text(props["image"]) ?? string.Empty,
                    ["alt"] = Text(props["alt"]) ?? Text(props["caption"]) ?? string.Empty
                };
                var caption = Text(props["caption"]);
                if (!string.IsNullOrEmpty(caption))
                    content["caption"] = caption;
                return Node("box", children: [Node("image", content)]);
            }

            case "bookcover":
            {
                var children = new List<JsonObject>
                {
                    Node(
                        "heading",
                        new JsonObject
                        {
                            ["text"] = Text(props["title"]) ?? string.Empty,
                            ["level"] = 1
                        },
                        new JsonObject { ["size"] = "2.6em" })
                };

                var subtitle = Text(props["subtitle"]);
                if (!string.IsNullOrEmpty(subtitle))
                    children.Add(Node("text", new JsonObject { ["text"] = "*" + subtitle + "*" }));

                children.Add(Node("spacer", style: new JsonObject { ["css"] = "flex:1" }));
                children.Add(
                    Node(
                        "text",
                        new JsonObject { ["text"] = Text(props["author"]) ?? string.Empty },
                        new JsonObject
                        {
                            ["transform"] = "uppercase",
                            ["spacing"] = ".18em"
                        }));

                var publisher = Text(props["publisher"]);
                if (!string.IsNullOrEmpty(publisher))
                {
                    children.Add(
                        Node(
                            "text",
                            new JsonObject { ["text"] = publisher },
                            new JsonObject
                            {
                                ["size"] = ".78em",
                                ["transform"] = "uppercase",
                                ["spacing"] = ".14em",
                                ["opacity"] = .75
                            }));
                }

                return Node(
                    "box",
                    new JsonObject { ["tag"] = "header" },
                    new JsonObject
                    {
                        ["minHeight"] = "100%",
                        ["padding"] = "12% 10%",
                        ["gap"] = "14px",
                        ["textAlign"] = "center",
                        ["align"] = "center",
                        ["bg"] = "#7a2e1f",
                        ["color"] = "#f6efe2"
                    },
                    children);
            }

            case "titlepage":
            {
                var middle = new List<JsonObject>
                {
                    Node(
                        "heading",
                        new JsonObject
                        {
                            ["text"] = Text(props["title"]) ?? string.Empty,
                            ["level"] = 1
                        })
                };
                var subtitle = Text(props["subtitle"]);
                if (!string.IsNullOrEmpty(subtitle))
                    middle.Add(Node("text", new JsonObject { ["text"] = "*" + subtitle + "*" }));

                var footer = string.Join(
                    "\n\n",
                    new[]
                    {
                        Text(props["publisher"]),
                        string.Join(
                            " · ",
                            new[] { Text(props["place"]), Text(props["year"]) }
                                .Where(value => !string.IsNullOrEmpty(value)))
                    }.Where(value => !string.IsNullOrEmpty(value)));

                return Node(
                    "box",
                    style: new JsonObject
                    {
                        ["minHeight"] = "100%",
                        ["gap"] = "14px",
                        ["textAlign"] = "center",
                        ["align"] = "center",
                        ["justify"] = "between"
                    },
                    children:
                    [
                        Node(
                            "text",
                            new JsonObject { ["text"] = Text(props["author"]) ?? string.Empty },
                            new JsonObject
                            {
                                ["transform"] = "uppercase",
                                ["spacing"] = ".18em"
                            }),
                        Node(
                            "box",
                            style: new JsonObject { ["gap"] = "8px", ["align"] = "center" },
                            children: middle),
                        Node(
                            "text",
                            new JsonObject { ["text"] = footer },
                            new JsonObject { ["size"] = ".85em" })
                    ]);
            }

            case "dedication":
            case "copyright":
            case "colophon":
            case "about":
            case "part":
            {
                var children = new List<JsonObject>();
                var title = Text(props["title"]);
                if (!string.IsNullOrEmpty(title))
                {
                    children.Add(
                        Node(
                            "heading",
                            new JsonObject { ["text"] = title, ["level"] = 2 }));
                }
                children.AddRange(Paragraphs(Text(props["markdown"])));

                var author = Text(props["author"]);
                if (!string.IsNullOrEmpty(author))
                    children.Add(Node("text", new JsonObject { ["text"] = "— " + author }));

                var style = new JsonObject
                {
                    ["gap"] = "12px",
                    ["minHeight"] = "100%",
                    ["justify"] = type is "copyright" or "colophon" ? "end" : "center"
                };
                if (type is "dedication" or "part" or "colophon")
                    style["textAlign"] = "center";

                return Node("box", style: style, children: children);
            }

            case "chapter":
            {
                var title = Text(props["title"]) ?? string.Empty;
                var markdown = Text(props["markdown"]) ?? string.Empty;
                if (string.Equals(Text(props["source"]), "note", StringComparison.Ordinal))
                {
                    var document = FindDocument(documents, Text(props["path"]) ?? string.Empty);
                    if (document is not null)
                    {
                        if (title.Length == 0)
                            title = document.Title;
                        markdown = StripLeadingTitle(document.Content, document.Title, true);
                    }
                }

                if (title.Length == 0)
                    title = "Capítulo";

                var children = new List<JsonObject>
                {
                    Node(
                        "heading",
                        new JsonObject { ["text"] = title, ["level"] = 2 },
                        new JsonObject
                        {
                            ["textAlign"] = "center",
                            ["margin"] = "8% 0 6%"
                        })
                };

                var epigraph = Text(props["epigraph"]);
                if (!string.IsNullOrEmpty(epigraph))
                {
                    var quote = new JsonObject { ["text"] = epigraph };
                    var epigraphAuthor = Text(props["epigraphAuthor"]);
                    if (!string.IsNullOrEmpty(epigraphAuthor))
                        quote["author"] = epigraphAuthor;
                    children.Add(Node("quote", quote));
                }

                children.AddRange(Paragraphs(markdown));
                return Node(
                    "box",
                    new JsonObject { ["tag"] = "article" },
                    new JsonObject { ["gap"] = "14px" },
                    children);
            }

            case "note":
            {
                if (documents is null)
                    return null;

                var document = FindDocument(documents, Text(props["path"]) ?? string.Empty);
                var children = new List<JsonObject>();
                if (document is not null)
                {
                    var showTitle = Bool(props["showTitle"], true);
                    if (showTitle)
                    {
                        children.Add(
                            Node(
                                "heading",
                                new JsonObject { ["text"] = document.Title, ["level"] = 1 }));
                    }
                    children.AddRange(
                        Paragraphs(
                            StripLeadingTitle(
                                document.Content,
                                document.Title,
                                showTitle)));
                }

                return Node(
                    "box",
                    new JsonObject { ["tag"] = "article" },
                    new JsonObject { ["gap"] = "14px" },
                    children);
            }
        }

        return null;
    }

    private static JsonObject Node(
        string type,
        JsonObject? content = null,
        JsonObject? style = null,
        IEnumerable<JsonObject>? children = null)
    {
        var node = new JsonObject { ["type"] = type };
        if (content is { Count: > 0 })
            node["content"] = content;
        if (style is { Count: > 0 })
            node["style"] = style;
        if (children is not null)
        {
            var array = new JsonArray();
            foreach (var child in children)
                array.Add(child);
            node["children"] = array;
        }
        return node;
    }

    private static UrbeDocument? FindDocument(
        DocumentStore? documents,
        string path)
    {
        if (documents is null || string.IsNullOrWhiteSpace(path))
            return null;

        var direct = documents.Get(path);
        if (direct is not null)
            return direct;

        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            direct = documents.Get(path + ".md");
            if (direct is not null)
                return direct;
        }

        var wanted = Path.GetFileNameWithoutExtension(path);
        return documents.List().FirstOrDefault(document =>
            string.Equals(
                document.Title,
                wanted,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string StripLeadingTitle(
        string content,
        string title,
        bool titleShown)
    {
        if (!titleShown)
            return content;

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        var start = 0;
        if (lines.Count > 0 && lines[0] == "---")
        {
            var close = lines.FindIndex(1, line => line == "---");
            if (close >= 0)
                start = close + 1;
        }

        while (start < lines.Count && lines[start].Length == 0)
            start++;

        if (start < lines.Count &&
            lines[start].StartsWith("# ", StringComparison.Ordinal) &&
            string.Equals(
                lines[start][2..].Trim(),
                title.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            lines.RemoveAt(start);
        }

        return string.Join("\n", lines);
    }

    private static string? Text(JsonNode? node) =>
        PageDocument.StringValue(node);

    private static bool Bool(JsonNode? node, bool fallback) =>
        PageDocument.BoolValue(node, fallback);

    [GeneratedRegex(@"\n{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankParagraphPattern();

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(?:\*\s*){3}$|^-{3,}$", RegexOptions.CultureInvariant)]
    private static partial Regex DividerPattern();

    [GeneratedRegex(@"^>\s*\[!", RegexOptions.CultureInvariant)]
    private static partial Regex CalloutPattern();

    [GeneratedRegex(@"^>\s?", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex QuotePrefixPattern();

    [GeneratedRegex(
        @"^!\[([^\]]*)\]\(([^)\s]+)[^)]*\)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();
}
