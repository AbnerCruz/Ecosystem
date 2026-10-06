namespace Urbe.Core;

public sealed record MathFormula(
    int Start, int End, string Tex, string Open, string Close, bool Display, bool Block);

public sealed record MathCommand(string Cmd, string Snip, string Desc, string? Prev);
public sealed record MathSymbol(string Label, string Tex);
public sealed record MathSymbolGroup(string Name, IReadOnlyList<MathSymbol> Items);
public sealed record MathSnippet(string Text, int Caret);

/// <summary>Pure editing semantics from UrbeMath. Positions use UTF-16, like the editor.</summary>
public static partial class MathEditing
{
    public static IReadOnlyList<MathFormula> Scan(string? markdown) =>
        MarkdownEngine.ScanMath(markdown ?? string.Empty).AsReadOnly();

    /// <summary>Includes the boundaries of TeX, but excludes its delimiters.</summary>
    public static MathFormula? FindAt(string? markdown, int index) =>
        Scan(markdown).FirstOrDefault(item =>
            index >= item.Start + item.Open.Length && index <= item.End - item.Close.Length);

    public static string RawOf(string? open, string? tex, string? close) =>
        MarkdownEngine.RawMath(open, tex, close);

    public static MathSnippet Snippet(string? template)
    {
        // String(null) in the legacy snippet helper.
        var text = template ?? "null";
        var caret = text.IndexOf('●');
        return new(text.Replace("●", string.Empty, StringComparison.Ordinal),
            caret < 0 ? text.Length : caret);
    }

    public static IReadOnlyList<MathCommand> Complete(string? prefix, int limit = 8)
    {
        var value = prefix ?? string.Empty;
        if (value.Length == 0 || value[0] != '\\' ||
            value.Skip(1).Any(c => !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z')))
            return Array.Empty<MathCommand>();

        // Legacy limit || 8 and Array.slice(0, limit), including negative limits.
        var matches = Commands.DistinctBy(c => c.Cmd, StringComparer.Ordinal)
            .Where(c => c.Cmd.StartsWith(value, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Cmd.StartsWith(value, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(c => c.Cmd.Length).ToArray();
        var count = limit == 0 ? 8 : limit < 0 ? Math.Max(0, matches.Length + limit) : limit;
        return Array.AsReadOnly(matches.Take(count).ToArray());
    }

    // ECMAScript whitespace: .NET differs for U+0085 and U+FEFF.
    internal static bool IsWhitespace(char value) => value is
        '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\u00A0' or '\u1680' or
        >= '\u2000' and <= '\u200A' or '\u2028' or '\u2029' or '\u202F' or
        '\u205F' or '\u3000' or '\uFEFF';
}
