namespace Urbe.Core;

/// <summary>A node of the rendered note: the element tree the 1.8.4 renderer would put in #renderedPreview.</summary>
public sealed record RichNode(string Tag, IReadOnlyDictionary<string, string> Attrs, string Text, IReadOnlyList<RichNode> Children)
{
    public const string TextTag = "#text";
    public string Attr(string name) => Attrs.TryGetValue(name, out var v) ? v : "";
    public bool HasClass(string name) => Attr("class").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal);
}

public static class MarkdownTree
{
    /// <summary>Markdown → the rendered element tree (the same HTML as <see cref="MarkdownEngine.Render"/>, parsed).</summary>
    public static IReadOnlyList<RichNode> Parse(string? markdown) => FromHtml(MarkdownEngine.Render(markdown));

    public static IReadOnlyList<RichNode> FromHtml(string html) =>
        VisualHtmlParser.Parse(html).Children.Select(Convert).ToArray();

    private static RichNode Convert(VisualHtmlNode node) => node switch
    {
        VisualHtmlText t => new RichNode(RichNode.TextTag, new Dictionary<string, string>(), t.Value, []),
        VisualHtmlElement e => new RichNode(e.Name, new Dictionary<string, string>(e.Attributes), "", e.Children.Select(Convert).ToArray()),
        _ => new RichNode(RichNode.TextTag, new Dictionary<string, string>(), "", []),
    };
}
