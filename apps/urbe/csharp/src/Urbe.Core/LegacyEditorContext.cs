using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>editor/context.js (1.8.4): the outline of a note (its # headings) for "Estrutura e backlinks".</summary>
public static partial class LegacyEditorContext
{
    public sealed record Heading(int Level, string Title, int Line, int Offset);

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex HeadingLine();

    /// <summary>outline: every line /^(#{1,6})\s+(.+?)\s*#*\s*$/ with its level, trimmed title, 1-based line and offset.</summary>
    public static IReadOnlyList<Heading> Outline(string? content)
    {
        var result = new List<Heading>();
        int offset = 0, line = 0;
        foreach (var text in (content ?? "").Split('\n'))
        {
            line++;
            var m = HeadingLine().Match(text);
            if (m.Success) result.Add(new Heading(m.Groups[1].Length, m.Groups[2].Value.Trim(), line, offset));
            offset += text.Length + 1;
        }
        return result;
    }
}
