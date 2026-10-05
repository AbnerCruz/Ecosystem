using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

internal abstract class VisualHtmlNode
{
    public VisualHtmlElement? Parent { get; internal set; }
}

internal sealed class VisualHtmlText(string value) : VisualHtmlNode
{
    public string Value { get; } = value;
}

internal sealed class VisualHtmlElement(string name) : VisualHtmlNode
{
    public string Name { get; } = name.ToLowerInvariant();
    public Dictionary<string, string> Attributes { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<VisualHtmlNode> Children { get; } = [];

    public string? Attribute(string name) =>
        Attributes.TryGetValue(name, out var value) ? value : null;

    public bool HasAttribute(string name) => Attributes.ContainsKey(name);

    public bool HasClass(string name)
    {
        var classes = Attribute("class");
        if (string.IsNullOrEmpty(classes))
            return false;
        return classes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Contains(name, StringComparer.Ordinal);
    }

    public IEnumerable<VisualHtmlElement> ChildElements() =>
        Children.OfType<VisualHtmlElement>();

    public IEnumerable<VisualHtmlElement> Descendants(string? name = null)
    {
        foreach (var child in ChildElements())
        {
            if (name is null || string.Equals(child.Name, name, StringComparison.Ordinal))
                yield return child;
            foreach (var nested in child.Descendants(name))
                yield return nested;
        }
    }

    public VisualHtmlElement? FirstByClass(string className)
    {
        if (HasClass(className))
            return this;
        return Descendants().FirstOrDefault(element => element.HasClass(className));
    }
}

internal static class VisualHtmlParser
{
    private static readonly HashSet<string> VoidTags =
        new(
            new[] { "area", "base", "br", "col", "embed", "hr", "img", "input",
                    "link", "meta", "param", "source", "track", "wbr" },
            StringComparer.OrdinalIgnoreCase);

    public static VisualHtmlElement Parse(string? html)
    {
        var root = new VisualHtmlElement("#root");
        var stack = new Stack<VisualHtmlElement>();
        stack.Push(root);

        var source = html ?? string.Empty;
        var index = 0;

        while (index < source.Length)
        {
            if (source[index] != '<')
            {
                var next = source.IndexOf('<', index);
                if (next < 0) next = source.Length;
                var text = WebUtility.HtmlDecode(source[index..next]);
                if (text.Length > 0)
                    Add(stack.Peek(), new VisualHtmlText(text));
                index = next;
                continue;
            }

            if (source.AsSpan(index).StartsWith("<!--".AsSpan(), StringComparison.Ordinal))
            {
                var endComment = source.IndexOf("-->", index + 4, StringComparison.Ordinal);
                index = endComment < 0 ? source.Length : endComment + 3;
                continue;
            }

            var end = TagEnd(source, index + 1);
            if (end < 0)
            {
                Add(stack.Peek(), new VisualHtmlText(WebUtility.HtmlDecode(source[index..])));
                break;
            }

            var token = source[(index + 1)..end].Trim();
            index = end + 1;
            if (token.Length == 0)
                continue;

            if (token[0] == '!')
                continue;

            if (token[0] == '/')
            {
                var closing = token[1..].Trim().Split((char[]?)null, 2)[0].ToLowerInvariant();
                while (stack.Count > 1)
                {
                    var popped = stack.Pop();
                    if (string.Equals(popped.Name, closing, StringComparison.Ordinal))
                        break;
                }
                continue;
            }

            var selfClosing = token.EndsWith("/", StringComparison.Ordinal);
            if (selfClosing)
                token = token[..^1].TrimEnd();

            var element = ParseOpeningTag(token);
            Add(stack.Peek(), element);

            if (!selfClosing && !VoidTags.Contains(element.Name))
                stack.Push(element);
        }

        return root;
    }

    private static int TagEnd(string source, int start)
    {
        char quote = '\0';
        for (var i = start; i < source.Length; i++)
        {
            var ch = source[i];
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';
                continue;
            }

            if (ch is '"' or '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '>')
                return i;
        }

        return -1;
    }

    private static VisualHtmlElement ParseOpeningTag(string token)
    {
        var index = 0;
        while (index < token.Length && !char.IsWhiteSpace(token[index]))
            index++;

        var name = token[..index];
        var element = new VisualHtmlElement(name);

        while (index < token.Length)
        {
            while (index < token.Length && char.IsWhiteSpace(token[index])) index++;
            if (index >= token.Length) break;

            var nameStart = index;
            while (index < token.Length &&
                   !char.IsWhiteSpace(token[index]) &&
                   token[index] != '=')
                index++;

            var attributeName = token[nameStart..index];
            while (index < token.Length && char.IsWhiteSpace(token[index])) index++;

            var value = string.Empty;
            if (index < token.Length && token[index] == '=')
            {
                index++;
                while (index < token.Length && char.IsWhiteSpace(token[index])) index++;

                if (index < token.Length && token[index] is '"' or '\'')
                {
                    var quote = token[index++];
                    var valueStart = index;
                    while (index < token.Length && token[index] != quote) index++;
                    value = token[valueStart..Math.Min(index, token.Length)];
                    if (index < token.Length) index++;
                }
                else
                {
                    var valueStart = index;
                    while (index < token.Length && !char.IsWhiteSpace(token[index])) index++;
                    value = token[valueStart..index];
                }
            }

            if (attributeName.Length > 0)
                element.Attributes[attributeName] = WebUtility.HtmlDecode(value);
        }

        return element;
    }

    private static void Add(VisualHtmlElement parent, VisualHtmlNode child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
    }
}

public static partial class VisualMarkdown
{
    private const char Backtick = '\x60';

    public static string FromHtml(string? html, string? frontmatter = null)
    {
        var root = VisualHtmlParser.Parse(html);
        var output = new List<string>();

        foreach (var child in root.Children)
            Block(child, output);

        var body = ExcessNewlines().Replace(string.Join("\n\n", output), "\n\n").Trim();
        var fm = frontmatter ?? string.Empty;
        var markdown = (fm.Length > 0 ? fm + (body.Length > 0 ? "\n" : string.Empty) : string.Empty) +
                       (body.Length > 0 ? body + "\n" : string.Empty);

        return Normalize(markdown);
    }

    public static string FromHtmlUsingEditorSource(string? html, string? editorMarkdown)
    {
        var frontmatter = MarkdownEngine.SplitFrontmatter(editorMarkdown).Raw;
        return FromHtml(html, frontmatter);
    }

    private static void Block(VisualHtmlNode node, List<string> output)
    {
        if (node is VisualHtmlText text)
        {
            var value = text.Value.Trim();
            if (value.Length > 0)
                output.Add(value);
            return;
        }

        if (node is not VisualHtmlElement element)
            return;

        if (element.HasAttribute("data-frontmatter-card"))
            return;

        if (element.HasClass("umath-block"))
        {
            output.Add(RawMath(element));
            return;
        }

        var callout = element.Attribute("data-callout");
        if (!string.IsNullOrEmpty(callout))
        {
            var title = element.FirstByClass("callout-title");
            var body = element.FirstByClass("callout-body");
            var titleText = title is null
                ? string.Empty
                : TextInline(title).Replace("\n", " ", StringComparison.Ordinal).Trim();
            var bodyText = body is null
                ? string.Empty
                : TrailingNewlines().Replace(TextInline(body), string.Empty);

            var lines = new List<string>
            {
                "> [!" + callout + "]" + (titleText.Length > 0 ? " " + titleText : string.Empty)
            };

            if (bodyText.Trim().Length > 0)
            {
                lines.AddRange(bodyText.Split('\n')
                    .Select(line => line.Length > 0 ? "> " + line : ">"));
            }

            output.Add(string.Join('\n', lines));
            return;
        }

        if (element.Name.Length == 2 &&
            element.Name[0] == 'h' &&
            element.Name[1] is >= '1' and <= '6')
        {
            output.Add(new string('#', element.Name[1] - '0') + " " + TextInline(element).Trim());
            return;
        }

        if (element.Name == "div" &&
            element.ChildElements().Any(child => IsBlockTag(child.Name)))
        {
            foreach (var child in element.Children)
                Block(child, output);
            return;
        }

        if (element.Name is "p" or "div")
        {
            var value = TextInline(element).Trim();
            if (value.Length > 0)
                output.Add(value);
            return;
        }

        if (element.Name == "blockquote")
        {
            var buffer = new StringBuilder();
            foreach (var child in element.Children)
            {
                if (child is VisualHtmlElement block && block.Name is "p" or "div")
                    buffer.Append(TextInline(block)).Append('\n');
                else
                    buffer.Append(TextInline(child));
            }

            var text = TrailingNewlines().Replace(buffer.ToString(), string.Empty);
            output.Add(string.Join(
                '\n',
                text.Split('\n').Select(line => line.Length > 0 ? "> " + line : ">")));
            return;
        }

        if (element.Name == "table")
        {
            SerializeTable(element, output);
            return;
        }

        if (element.Name == "pre")
        {
            var fence = new string(Backtick, 3);
            var language = element.Attribute("data-lang") ?? string.Empty;
            var text = InnerText(element);
            text = TrailingNewlines().Replace(text, string.Empty);
            output.Add(fence + language + "\n" + text + "\n" + fence);
            return;
        }

        if (element.Name == "hr")
        {
            output.Add("---");
            return;
        }

        if (element.Name is "ul" or "ol")
        {
            var items = new List<string>();
            SerializeList(element, string.Empty, items);
            if (items.Count > 0)
                output.Add(string.Join('\n', items));
            return;
        }

        foreach (var child in element.Children)
            Block(child, output);
    }

    private static string TextInline(VisualHtmlNode node)
    {
        if (node is VisualHtmlText text)
            return text.Value;

        if (node is not VisualHtmlElement element)
            return string.Empty;

        if (element.HasClass("umath"))
            return RawMath(element);

        var inner = string.Concat(element.Children.Select(TextInline));
        switch (element.Name)
        {
            case "strong":
            case "b":
                return "**" + inner + "**";
            case "em":
            case "i":
                return "_" + inner + "_";
            case "del":
            case "s":
                return "~~" + inner + "~~";
            case "code" when element.Parent?.Name != "pre":
                return new string(Backtick, 1) + inner + new string(Backtick, 1);
            case "a":
                return "[" + inner + "](" + (element.Attribute("href") ?? string.Empty) + ")";
            case "img":
                return "![" + (element.Attribute("alt") ?? string.Empty) + "](" +
                       (element.Attribute("src") ?? string.Empty) + ")";
            case "ul":
            case "ol":
            case "input":
                return string.Empty;
            case "br":
                return "\n";
        }

        if (element.HasClass("wikilink"))
        {
            var target = element.Attribute("data-note-name") ?? inner;
            var label = inner.Trim();
            return label.Length > 0 && !string.Equals(label, target, StringComparison.Ordinal)
                ? "[[" + target + "|" + label + "]]"
                : "[[" + target + "]]";
        }

        return inner;
    }

    private static void SerializeTable(VisualHtmlElement table, List<string> output)
    {
        var rows = table.Descendants("tr").ToList();
        if (rows.Count == 0)
            return;

        string Cell(VisualHtmlElement cell) =>
            TextInline(cell).Replace("\n", " ", StringComparison.Ordinal)
                .Replace("|", "\\|", StringComparison.Ordinal).Trim();

        var head = rows[0].ChildElements().Select(Cell).ToList();
        var alignData = (table.Attribute("data-align") ?? string.Empty).Split(',');
        var separators = new List<string>();

        var headCells = rows[0].ChildElements().ToList();
        for (var i = 0; i < head.Count; i++)
        {
            var align = i < alignData.Length ? alignData[i] : string.Empty;
            if (align.Length == 0 && i < headCells.Count)
                align = StyleValue(headCells[i], "text-align");

            separators.Add(align switch
            {
                "center" => ":---:",
                "right" => "---:",
                "left" => ":---",
                _ => "---"
            });
        }

        var lines = new List<string>
        {
            "| " + string.Join(" | ", head) + " |",
            "| " + string.Join(" | ", separators) + " |"
        };

        foreach (var row in rows.Skip(1))
        {
            var cells = row.ChildElements().Select(Cell).ToList();
            while (cells.Count < head.Count) cells.Add(string.Empty);
            lines.Add("| " + string.Join(" | ", cells) + " |");
        }

        output.Add(string.Join('\n', lines));
    }

    private static void SerializeList(
        VisualHtmlElement list,
        string indent,
        List<string> output)
    {
        var ordered = list.Name == "ol";
        var number = 0;

        foreach (var item in list.ChildElements())
        {
            if (item.Name != "li")
                continue;

            number++;
            var checkbox = item.ChildElements()
                .FirstOrDefault(child =>
                    child.Name == "input" &&
                    string.Equals(child.Attribute("type"), "checkbox", StringComparison.OrdinalIgnoreCase));

            var content = MultipleNewlines().Replace(TextInline(item), " ").Trim();
            var marker = checkbox is not null
                ? "- [" + (checkbox.HasAttribute("checked") ? "x" : " ") + "] "
                : ordered ? number + ". " : "- ";

            output.Add(indent + marker + content);

            foreach (var nested in item.ChildElements().Where(child => child.Name is "ul" or "ol"))
            {
                var extra = ordered ? number.ToString(System.Globalization.CultureInfo.InvariantCulture).Length + 2 : 2;
                SerializeList(nested, indent + new string(' ', extra), output);
            }
        }
    }

    private static string RawMath(VisualHtmlElement element) =>
        MarkdownEngine.RawMath(
            element.Attribute("data-open"),
            element.Attribute("data-tex"),
            element.Attribute("data-close"));

    private static string InnerText(VisualHtmlNode node)
    {
        if (node is VisualHtmlText text)
            return text.Value;

        if (node is not VisualHtmlElement element)
            return string.Empty;

        if (element.Name == "br")
            return "\n";

        return string.Concat(element.Children.Select(InnerText));
    }

    private static string StyleValue(VisualHtmlElement element, string property)
    {
        var style = element.Attribute("style");
        if (string.IsNullOrEmpty(style))
            return string.Empty;

        foreach (var declaration in style.Split(';'))
        {
            var parts = declaration.Split(':', 2);
            if (parts.Length == 2 &&
                string.Equals(parts[0].Trim(), property, StringComparison.OrdinalIgnoreCase))
                return parts[1].Trim();
        }

        return string.Empty;
    }

    private static bool IsBlockTag(string tag) =>
        tag is "p" or "div" or "ul" or "ol" or "blockquote" or "pre" or "hr" or "table" ||
        (tag.Length == 2 && tag[0] == 'h' && tag[1] is >= '1' and <= '6');

    private static string Normalize(string markdown) =>
        EmptyHeadingLine().Replace(
            markdown.Replace("\u200b", string.Empty, StringComparison.Ordinal),
            "$1 ");

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex ExcessNewlines();

    [GeneratedRegex(@"\n+$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNewlines();

    [GeneratedRegex(@"\n+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleNewlines();

    [GeneratedRegex(@"^(#{1,6})[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex EmptyHeadingLine();
}
