using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// Markdown marks the 1.8.4 did not have, approved by the owner as plain syntax in the .md file (DEC-0045, UC-43), so the
/// note reads the same in any editor:
/// <list type="bullet">
/// <item><c>%%comentário%%</c> — a comment: shown, set apart, in the editor; left out of pages and exported HTML.</item>
/// <item><c>#Tag: texto</c> at the start of a paragraph — the paragraph takes the style of that tag (#Todo, #Ideia…).</item>
/// </list>
/// Applied over the rendered tree (<see cref="MarkdownTree"/>), never inside the 1.8.4 renderer, whose output stays as the
/// goldens prove.
/// </summary>
public static partial class NoteMarks
{
    public const string CommentClass = "urbe-comment";
    public const string TagClass = "urbe-tag";

    /// <summary>The rendered tree of a note with its comments and tagged paragraphs marked.</summary>
    public static IReadOnlyList<RichNode> Parse(string? markdown) => Apply(MarkdownTree.Parse(markdown));

    public static IReadOnlyList<RichNode> Apply(IReadOnlyList<RichNode> nodes) => nodes.Select(n => Block(n)).ToArray();

    /// <summary>The tag a paragraph starts with (<c>#Todo:</c> → "Todo"), or null.</summary>
    public static string? TagOf(string text)
    {
        var m = TagRegex().Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>The Markdown without its comments (pages, books and exported HTML); code keeps its text as it is.</summary>
    public static string StripComments(string? markdown)
    {
        var text = markdown ?? "";
        if (!text.Contains("%%", StringComparison.Ordinal)) return text;
        // fenced code and inline code stay untouched; everything else loses %%…%%
        var parts = CodeRegex().Split(text);
        var codes = CodeRegex().Matches(text);
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            sb.Append(CommentRegex().Replace(parts[i], ""));
            if (i < codes.Count) sb.Append(codes[i].Value);
        }
        return sb.ToString();
    }

    private static RichNode Block(RichNode n)
    {
        if (n.Tag is "pre" or "code") return n;
        var children = Inline(n.Children);
        if (n.Tag == "p" && children.Count > 0 && children[0].Tag == RichNode.TextTag && TagOf(children[0].Text) is { } tag)
        {
            var first = children[0];
            var rest = TagRegex().Replace(first.Text, "", 1);
            var kids = new List<RichNode>(children);
            if (rest.Length > 0) kids[0] = first with { Text = rest }; else kids.RemoveAt(0);
            var attrs = new Dictionary<string, string>(n.Attrs)
            {
                ["class"] = (n.Attr("class") + " " + TagClass).Trim(),
                ["data-tag"] = tag
            };
            return n with { Attrs = attrs, Children = kids };
        }
        return n with { Children = children };
    }

    private static List<RichNode> Inline(IReadOnlyList<RichNode> children)
    {
        var output = new List<RichNode>();
        foreach (var c in children)
        {
            if (c.Tag == "code" || c.Tag == "pre") { output.Add(c); continue; }
            if (c.Tag != RichNode.TextTag) { output.Add(Block(c)); continue; }
            int at = 0;
            foreach (Match m in CommentRegex().Matches(c.Text))
            {
                if (m.Index > at) output.Add(c with { Text = c.Text[at..m.Index] });
                var inner = m.Groups[1].Value.Trim();
                output.Add(new RichNode("span", new Dictionary<string, string> { ["class"] = CommentClass },
                    "", [new RichNode(RichNode.TextTag, new Dictionary<string, string>(), inner, [])]));
                at = m.Index + m.Length;
            }
            if (at == 0) output.Add(c);
            else if (at < c.Text.Length) output.Add(c with { Text = c.Text[at..] });
        }
        return output;
    }

    // #Tag: at the very start of a paragraph — a letter first, then letters, digits, _ or -
    [GeneratedRegex(@"^#(\p{L}[\p{L}\p{N}_-]{0,39}):[ \t]?")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"%%(.+?)%%", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"```[\s\S]*?(?:```|$)|`[^`\n]*`")]
    private static partial Regex CodeRegex();
}
