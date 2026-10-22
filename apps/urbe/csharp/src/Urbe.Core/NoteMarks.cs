using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// Markdown marks the 1.8.4 did not have, approved by the owner as plain syntax in the .md file (DEC-0045, UC-43), so the
/// note reads the same in any editor:
/// <list type="bullet">
/// <item><c>%%comentário%%</c> — a comment: shown, set apart, in the editor; left out of pages and exported HTML.</item>
/// <item><c>#Tag: texto</c> at the start of a paragraph — the paragraph takes the style of that tag (#Todo, #Ideia…).</item>
/// <item><c>palavra{"comentário"}{"outro"}</c> — comments on a word (UC-49): a 💬 over the word opens them; left out of
/// pages and exported HTML like <c>%%…%%</c>.</item>
/// <item><c>{ia:"modelo"}</c> — the mark the Assistente leaves after the text it wrote (UC-51): shown as a tiny green
/// "modelo" in the editor.</item>
/// </list>
/// Applied over the rendered tree (<see cref="MarkdownTree"/>), never inside the 1.8.4 renderer, whose output stays as the
/// goldens prove.
/// </summary>
public static partial class NoteMarks
{
    public const string CommentClass = "urbe-comment";
    public const string TagClass = "urbe-tag";
    public const string NotesClass = "urbe-notes";
    public const string AiClass = "urbe-ai";

    /// <summary>The mark after text written by the Assistente: <c>{ia:"modelo"}</c>, the model's short name.</summary>
    public static string AiMark(string model)
    {
        var name = model.Split('/').Last().Replace("\"", "").Replace("\n", " ").Trim();
        if (name.Length > 60) name = name[..60];
        return "{ia:\"" + (name.Length > 0 ? name : "IA") + "\"}";
    }

    /// <summary>The text with the Assistente's mark at its end (once: an existing mark there is kept).</summary>
    public static string WithAiMark(string text, string model)
    {
        var trimmed = text.TrimEnd();
        if (AiMarkRegex().Match(trimmed) is { Success: true } m && m.Index + m.Length == trimmed.Length) return text;
        return trimmed + " " + AiMark(model) + text[trimmed.Length..];
    }

    /// <summary>A run of word comments in the source: where it is and the comments in it, in order.</summary>
    public sealed record CommentGroup(int Start, int Length, IReadOnlyList<string> Comments);

    /// <summary>The <c>{"…"}</c> comment runs of a text, outside code, in order.</summary>
    public static IReadOnlyList<CommentGroup> CommentGroups(string? markdown)
    {
        var text = markdown ?? "";
        var result = new List<CommentGroup>();
        if (!text.Contains("{\"", StringComparison.Ordinal)) return result;
        var code = CodeRegex().Matches(text).Select(m => (m.Index, End: m.Index + m.Length)).ToList();
        foreach (Match m in NotesRegex().Matches(text))
            if (!code.Any(c => m.Index >= c.Index && m.Index < c.End))
                result.Add(new CommentGroup(m.Index, m.Length, Comments(m.Value)));
        return result;
    }

    /// <summary>The comments of one run (<c>{"a"}{"b \"c\""}</c> → a, b "c").</summary>
    public static IReadOnlyList<string> Comments(string group) =>
        NoteRegex().Matches(group).Select(m => Regex.Replace(m.Groups[1].Value, @"\\(.)", "$1")).ToArray();

    /// <summary>The text with <paramref name="comment"/> added at the end of the comment run at <paramref name="group"/>.</summary>
    public static string AddComment(string text, CommentGroup group, string comment) =>
        text.Insert(group.Start + group.Length, Quote(comment));

    /// <summary>One comment as written in the note: <c>{"…"}</c>, with quotes and backslashes escaped.</summary>
    public static string Quote(string comment) =>
        "{\"" + comment.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace('\n', ' ').Trim() + "\"}";

    /// <summary>The rendered tree of a note with its comments and tagged paragraphs marked.</summary>
    public static IReadOnlyList<RichNode> Parse(string? markdown) => Apply(MarkdownTree.Parse(markdown));

    public static IReadOnlyList<RichNode> Apply(IReadOnlyList<RichNode> nodes) => nodes.Select(n => Block(n)).ToArray();

    /// <summary>The tag a paragraph starts with (<c>#Todo:</c> → "Todo"), or null.</summary>
    public static string? TagOf(string text)
    {
        var m = TagRegex().Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// The Markdown without its comments (pages, books and exported HTML); code keeps its text as it is. The
    /// Assistente's mark stays, as "· IA: modelo" or as <paramref name="aiMark"/> writes it (UC-82: pages show it discreetly).
    /// </summary>
    public static string StripComments(string? markdown, Func<string, string>? aiMark = null)
    {
        var text = markdown ?? "";
        if (!text.Contains("%%", StringComparison.Ordinal) && !text.Contains("{\"", StringComparison.Ordinal) && !text.Contains("{ia:", StringComparison.Ordinal)) return text;
        // fenced code and inline code stay untouched; everything else loses %%…%%
        var parts = CodeRegex().Split(text);
        var codes = CodeRegex().Matches(text);
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            // the Assistente's mark stays visible in pages, as plain text (UC-51: AI text is always marked)
            sb.Append(AiMarkRegex().Replace(NotesRegex().Replace(CommentRegex().Replace(parts[i], ""), ""), m => aiMark?.Invoke(m.Groups[1].Value) ?? "· IA: " + m.Groups[1].Value));
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
            foreach (Match m in MarksRegex().Matches(c.Text))
            {
                if (m.Index > at) output.Add(c with { Text = c.Text[at..m.Index] });
                if (m.Value.StartsWith("{ia:", StringComparison.Ordinal))
                {
                    output.Add(new RichNode("span", new Dictionary<string, string>
                    {
                        ["class"] = AiClass, ["data-model"] = AiMarkRegex().Match(m.Value).Groups[1].Value
                    }, "", []));
                }
                else if (m.Value.StartsWith("%%", StringComparison.Ordinal))
                {
                    var inner = m.Groups[1].Value.Trim();
                    output.Add(new RichNode("span", new Dictionary<string, string> { ["class"] = CommentClass },
                        "", [new RichNode(RichNode.TextTag, new Dictionary<string, string>(), inner, [])]));
                }
                else
                {
                    var notes = Comments(m.Value);
                    output.Add(new RichNode("span", new Dictionary<string, string>
                    {
                        ["class"] = NotesClass,
                        ["data-comments"] = System.Text.Json.JsonSerializer.Serialize(notes)
                    }, "", []));
                }
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

    // {"comentário"} runs: one or more quoted comments in braces, \" and \\ escaped inside
    [GeneratedRegex(@"(?:\{""(?:[^""\\\n]|\\.)*""\})+")]
    private static partial Regex NotesRegex();

    [GeneratedRegex(@"\{""((?:[^""\\\n]|\\.)*)""\}")]
    private static partial Regex NoteRegex();

    [GeneratedRegex(@"\{ia:""([^""\n]{1,80})""\}")]
    private static partial Regex AiMarkRegex();

    [GeneratedRegex(@"%%(.+?)%%|(?:\{""(?:[^""\\\n]|\\.)*""\})+|\{ia:""[^""\n]{1,80}""\}", RegexOptions.Singleline)]
    private static partial Regex MarksRegex();

    [GeneratedRegex(@"```[\s\S]*?(?:```|$)|`[^`\n]*`")]
    private static partial Regex CodeRegex();
}
