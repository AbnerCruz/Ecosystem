namespace Lunet.Editor;

/// <summary>
/// Incremental re-highlighting (LUNET-483): after an edit, the tokens before the dirty region and the tokens after it
/// (shifted by the length change) are unchanged, so only the middle has to be re-spanned. Keeps typing cost proportional
/// to the edit instead of the file.
/// </summary>
public static class HighlightDelta
{
    /// <param name="previous">Tokens currently applied (coordinates before the edits).</param>
    /// <param name="next">Tokens for the current text.</param>
    /// <param name="dirtyStart">Smallest offset touched by edits since <paramref name="previous"/>, in current coordinates.</param>
    /// <param name="dirtyFromEnd">Smallest distance from the end of the text to the end of any edit, in current coordinates.</param>
    /// <param name="lengthDelta">Current length minus the length when <paramref name="previous"/> was applied.</param>
    /// <returns>How many leading and trailing tokens of both lists are identical and can keep their spans.</returns>
    public static (int Prefix, int Suffix) Compute(IReadOnlyList<TokenSpan> previous, IReadOnlyList<TokenSpan> next,
        int dirtyStart, int dirtyFromEnd, int newLength, int lengthDelta)
    {
        int prefix = 0;
        int max = Math.Min(previous.Count, next.Count);
        while (prefix < max && previous[prefix] == next[prefix] && next[prefix].Start + next[prefix].Length < dirtyStart) prefix++;

        int suffix = 0;
        int boundary = newLength - dirtyFromEnd;
        while (suffix < max - prefix)
        {
            var old = previous[previous.Count - 1 - suffix];
            var now = next[next.Count - 1 - suffix];
            if (now.Start <= boundary || old.Start + lengthDelta != now.Start || old.Length != now.Length || old.Kind != now.Kind) break;
            suffix++;
        }
        return (prefix, suffix);
    }
}
