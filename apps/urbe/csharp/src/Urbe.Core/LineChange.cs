namespace Urbe.Core;

/// <summary>
/// UC-80: what an edit changed in a note, in whole lines — the lines of the text before (<see cref="Start"/> to
/// <see cref="End"/>) and what takes their place. The editor shows it there as a suggestion to accept or refuse.
/// </summary>
public sealed record LineChange(int Start, int End, string Replacement)
{
    /// <summary>The change from <paramref name="before"/> to <paramref name="after"/>, widened to whole lines; null when equal.</summary>
    public static LineChange? Of(string before, string after)
    {
        if (string.Equals(before, after, StringComparison.Ordinal)) return null;
        int prefix = 0, max = Math.Min(before.Length, after.Length);
        while (prefix < max && before[prefix] == after[prefix]) prefix++;
        int suffix = 0;
        while (suffix < max - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;
        // whole lines: back to the start of the first changed line, on past the break of the last one
        int start = prefix > 0 ? before.LastIndexOf('\n', prefix - 1) + 1 : 0;
        int end = before.Length - suffix;
        if (end > start && before[end - 1] != '\n')
        {
            int lineEnd = before.IndexOf('\n', end);
            end = lineEnd < 0 ? before.Length : lineEnd + 1;
        }
        int shift = after.Length - before.Length;
        return new LineChange(start, end, after[start..(end + shift)]);
    }
}
