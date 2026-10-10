namespace Urbe.Core;

/// <summary>
/// UC-18 / REQ-027 / REQ-109: caret-aware wikilink completion logic.
/// This is pure C#; the native editor's DOM bridge only reads the caret and
/// restores its position after the canonical DocumentStore is updated.
/// </summary>
public static class WikiLinkCompletion
{
    public sealed record Context(int Start, int End, int Cursor, string Query);
    public sealed record Result(string Markdown, int Cursor);

    public static Context? Find(string? markdown, int cursor)
    {
        if (markdown is null || cursor < 0 || cursor > markdown.Length)
            return null;

        var open = markdown.LastIndexOf("[[", Math.Max(0, cursor - 1),
            Math.Max(0, cursor));
        if (open < 0 || cursor < open + 2)
            return null;

        var fragment = markdown[(open + 2)..cursor];
        if (fragment.Length > 90 ||
            fragment.Contains('\\n') || fragment.Contains('\\r') ||
            fragment.Contains("[[", StringComparison.Ordinal) ||
            fragment.Contains("]]", StringComparison.Ordinal) ||
            fragment.Contains('|'))
            return null;

        // Do not walk across a line or a new token. If the cursor is in
        // the middle of [[partially typed text]], replace the whole token.
        var end = cursor;
        while (end < markdown.Length && end - cursor <= 90)
        {
            if (markdown[end] is '\\n' or '\\r' ||
                (markdown[end] == '[' && end + 1 < markdown.Length &&
                 markdown[end + 1] == '['))
                break;
            if (markdown[end] == ']' && end + 1 < markdown.Length &&
                markdown[end + 1] == ']')
            {
                end += 2;
                break;
            }
            if (markdown[end] == '|')
                break;
            end++;
        }

        // Only consume a recognized closing pair. Never eat arbitrary
        // prose following an unfinished [[token.
        if (end > cursor &&
            (end < 2 || markdown[(end - 2)..end] != "]]"))
            end = cursor;

        return new Context(open, end, cursor, fragment.Trim());
    }

    public static Result? Complete(string? markdown, Context context, string? target)
    {
        if (markdown is null || string.IsNullOrWhiteSpace(target) ||
            target.Contains("[[", StringComparison.Ordinal) ||
            target.Contains("]]", StringComparison.Ordinal) ||
            target.Contains('\\n') || target.Contains('\\r') ||
            target.Contains('|') || target.Length > 160)
            return null;

        var active = Find(markdown, context.Cursor);
        if (active is null || active.Start != context.Start ||
            active.End != context.End ||
            !string.Equals(active.Query, context.Query, StringComparison.Ordinal))
            return null;

        var token = "[[" + target + "]]";
        var next = markdown[..active.Start] + token + markdown[active.End..];
        return new Result(next, active.Start + token.Length);
    }
}
