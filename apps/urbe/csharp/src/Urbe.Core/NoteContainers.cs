using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// One `:::tipo [título]` … `:::` block of a note (UC-44): the source range of the whole block (opening line to the end
/// of the closing line), of its body (between the two lines) and the blocks nested in it.
/// </summary>
public sealed record NoteContainer(int Start, int End, int BodyStart, int BodyEnd, string Type, string Title, IReadOnlyList<NoteContainer> Children);

/// <summary>
/// Reusable nested blocks written as Markdown containers (DEC-0045, UC-44): a line `:::tipo` (optionally followed by a
/// title) opens a block, a line `:::` closes the innermost one. Fenced code is never read; an opening line that is never
/// closed, and a stray `:::`, stay plain text. The note's Markdown is untouched: this only finds the ranges.
/// </summary>
public static partial class NoteContainers
{
    /// <summary>The top-level containers of a note, in order, with their nested ones.</summary>
    public static IReadOnlyList<NoteContainer> Parse(string? markdown)
    {
        var text = markdown ?? string.Empty;
        var top = new List<NoteContainer>();
        var stack = new List<Frame>();
        string? fence = null;
        for (int at = 0; at <= text.Length;)
        {
            var nl = text.IndexOf('\n', at);
            var end = nl < 0 ? text.Length : nl;
            var line = text[at..end].TrimEnd('\r');
            var trimmed = line.Trim();
            if (fence is not null)
            {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.TrimStart(fence[0]).Length == 0) fence = null;
            }
            else if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
                fence = trimmed[..3];
            else if (trimmed == ":::")
            {
                if (stack.Count > 0)
                {
                    var f = stack[^1];
                    stack.RemoveAt(stack.Count - 1);
                    var made = new NoteContainer(f.Start, end, f.BodyStart, Math.Max(f.BodyStart, at), f.Type, f.Title, f.Children.AsReadOnly());
                    (stack.Count > 0 ? stack[^1].Children : top).Add(made);
                }
            }
            else if (OpenRegex().Match(trimmed) is { Success: true } m)
                stack.Add(new Frame(at, nl < 0 ? text.Length : nl + 1, m.Groups[1].Value, m.Groups[2].Value.Trim()));
            if (nl < 0) break;
            at = nl + 1;
        }
        // never closed: the opening line is text, what closed inside it moves up
        while (stack.Count > 0)
        {
            var f = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            (stack.Count > 0 ? stack[^1].Children : top).AddRange(f.Children);
        }
        top.Sort((a, b) => a.Start.CompareTo(b.Start));
        return top.AsReadOnly();
    }

    private sealed record Frame(int Start, int BodyStart, string Type, string Title)
    {
        public List<NoteContainer> Children { get; } = [];
    }

    [GeneratedRegex(@"^:::([\p{L}\p{N}_-]+)(?:[ \t]+(.*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex OpenRegex();
}
