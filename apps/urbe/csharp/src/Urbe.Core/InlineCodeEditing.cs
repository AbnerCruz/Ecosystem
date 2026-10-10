namespace Urbe.Core;

/// <summary>
/// Applies the Source editor's inline-code command using UTF-16 offsets
/// (the same units used by HTMLTextAreaElement.selectionStart/End).
/// The input Markdown is the sole authority; browser code only reads and
/// restores caret positions.
/// </summary>
public static class InlineCodeEditing
{
    public static InlineCodeChange? Toggle(string? markdown, int selectionStart, int selectionEnd)
    {
        var source = markdown ?? string.Empty;
        if (selectionStart < 0 ||
            selectionEnd < selectionStart ||
            selectionEnd > source.Length ||
            SplitsSurrogate(source, selectionStart) ||
            SplitsSurrogate(source, selectionEnd))
            return null;

        if (selectionStart == selectionEnd)
        {
            // Pressing twice on an untouched empty pair undoes the insertion.
            if (selectionStart > 0 && selectionStart < source.Length &&
                source[selectionStart - 1] == '`' &&
                source[selectionStart] == '`' &&
                (selectionStart < 2 || source[selectionStart - 2] != '`') &&
                (selectionStart + 1 == source.Length || source[selectionStart + 1] != '`'))
            {
                var withoutPair = source[..(selectionStart - 1)] +
                                  source[(selectionStart + 1)..];
                return new InlineCodeChange(withoutPair, selectionStart - 1, selectionStart - 1);
            }

            var inserted = source[..selectionStart] + "``" + source[selectionStart..];
            return new InlineCodeChange(inserted, selectionStart + 1, selectionStart + 1);
        }

        var selected = source[selectionStart..selectionEnd];
        // A single inline code span must not silently collapse selected line
        // breaks into spaces. Fenced code is a separate editor command.
        if (selected.IndexOfAny(['\n', '\r']) >= 0)
            return null;

        var longestTicks = 0;
        var run = 0;
        foreach (var character in selected)
        {
            run = character == '`' ? run + 1 : 0;
            if (run > longestTicks)
                longestTicks = run;
        }

        var delimiter = new string('`', longestTicks + 1);
        // A padding space separates a backtick at the boundary of the
        // selected content from the delimiter. It is not part of the
        // editor selection and is removed together with the delimiter.
        var pad = selected[0] == '`' || selected[^1] == '`' ? " " : string.Empty;
        var left = delimiter + pad;
        var right = pad + delimiter;

        if (selectionStart >= left.Length &&
            selectionEnd + right.Length <= source.Length &&
            source.AsSpan(selectionStart - left.Length, left.Length).SequenceEqual(left) &&
            source.AsSpan(selectionEnd, right.Length).SequenceEqual(right))
        {
            var unwrapped = source[..(selectionStart - left.Length)] +
                            selected + source[(selectionEnd + right.Length)..];
            return new InlineCodeChange(
                unwrapped,
                selectionStart - left.Length,
                selectionStart - left.Length + selected.Length);
        }

        var wrapped = source[..selectionStart] + left + selected + right + source[selectionEnd..];
        return new InlineCodeChange(
            wrapped,
            selectionStart + left.Length,
            selectionEnd + left.Length);
    }

    private static bool SplitsSurrogate(string source, int offset) =>
        offset > 0 && offset < source.Length &&
        char.IsHighSurrogate(source[offset - 1]) && char.IsLowSurrogate(source[offset]);
}

public sealed record InlineCodeChange(string Markdown, int SelectionStart, int SelectionEnd);
