using static Urbe.Core.LegacyEditorTools;

namespace Urbe.Core;

/// <summary>
/// CST-141: what an editor of code does with the indentation, for the code files of the vault (notes keep their own
/// Markdown keys). Enter keeps the line's indentation and goes one level in after an opening; between braces it opens
/// the block. Tab and Shift+Tab move the selected lines; a closing brace typed alone on its line goes one level out.
/// The unit (a tab, 2 or 4 spaces) is the file's own.
/// </summary>
public static class CodeEditing
{
    /// <summary>The file's indentation: a tab when its lines are indented with tabs, else the smallest step of spaces (4 by default).</summary>
    public static string IndentUnit(string text)
    {
        int tabs = 0, spaced = 0, step = 0;
        foreach (var line in (text ?? "").Split('\n'))
        {
            if (line.StartsWith('\t')) { tabs++; continue; }
            int n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            if (n == 0 || n == line.Length) continue;
            spaced++;
            step = step == 0 ? n : Gcd(step, n);
        }
        if (tabs > spaced) return "\t";
        return new string(' ', step is 2 or 3 or 4 or 8 ? step : 4);
    }

    /// <summary>Enter at [start, end): the new line with the indentation it should have.</summary>
    public static TextEdit NewLine(string text, int start, int end)
    {
        text ??= "";
        int a = Math.Clamp(Math.Min(start, end), 0, text.Length), b = Math.Clamp(Math.Max(start, end), 0, text.Length);
        int lineStart = a == 0 ? 0 : text.LastIndexOf('\n', a - 1) + 1;
        var line = text[lineStart..a];
        var indent = line[..(line.Length - line.TrimStart(' ', '\t').Length)];
        var before = line.TrimEnd();
        var unit = IndentUnit(text);
        bool opens = before.Length > 0 && (before[^1] is '{' or '[' or '(' || (before[^1] == ':' && !before.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        char? next = b < text.Length ? text[b] : null;
        bool closesNext = opens && next is { } c && Pair(before[^1]) == c;
        string insert;
        int caret;
        if (closesNext)
        {
            insert = "\n" + indent + unit + "\n" + indent;
            caret = a + 1 + indent.Length + unit.Length;
        }
        else
        {
            insert = "\n" + indent + (opens ? unit : "");
            caret = a + insert.Length;
        }
        var result = text[..a] + insert + text[b..];
        return new TextEdit(result, caret, caret);
    }

    /// <summary>Tab (or Shift+Tab, <paramref name="outdent"/>): the selected lines one level in or out; with no selection, one level at the caret.</summary>
    public static TextEdit Indent(string text, int start, int end, bool outdent)
    {
        text ??= "";
        int a = Math.Clamp(Math.Min(start, end), 0, text.Length), b = Math.Clamp(Math.Max(start, end), 0, text.Length);
        var unit = IndentUnit(text);
        if (a == b && !outdent)
            return new TextEdit(text[..a] + unit + text[a..], a + unit.Length, a + unit.Length);
        int first = a == 0 ? 0 : text.LastIndexOf('\n', a - 1) + 1;
        // a selection ending at the start of a line does not take that line
        int lastEnd = b > a && b > 0 && text[b - 1] == '\n' ? b - 1 : b;
        int last = text.IndexOf('\n', lastEnd);
        if (last < 0) last = text.Length;
        var lines = text[first..last].Split('\n');
        int shiftFirst = 0, shiftTotal = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            int before = lines[i].Length;
            if (outdent)
            {
                if (lines[i].StartsWith(unit, StringComparison.Ordinal)) lines[i] = lines[i][unit.Length..];
                else if (lines[i].StartsWith('\t')) lines[i] = lines[i][1..];
                else
                {
                    // fewer spaces than a level (or spaces in a file of tabs): up to one level of them goes
                    int spaces = 0, max = unit == "\t" ? 4 : unit.Length;
                    while (spaces < lines[i].Length && spaces < max && lines[i][spaces] == ' ') spaces++;
                    lines[i] = lines[i][spaces..];
                }
            }
            else if (lines[i].Length > 0 || lines.Length == 1) lines[i] = unit + lines[i];
            int delta = lines[i].Length - before;
            if (i == 0) shiftFirst = delta;
            shiftTotal += delta;
        }
        var changed = string.Join('\n', lines);
        var result = text[..first] + changed + text[last..];
        int newStart = Math.Max(first, a + shiftFirst);
        int newEnd = Math.Max(newStart, b + shiftTotal);
        return a == b ? new TextEdit(result, newStart, newStart) : new TextEdit(result, newStart, newEnd);
    }

    /// <summary>A closing brace typed where its line has only spaces: the line goes one level out and the brace follows; null otherwise.</summary>
    public static TextEdit? Closing(string text, int caret, char closing)
    {
        if (closing is not ('}' or ']' or ')')) return null;
        text ??= "";
        caret = Math.Clamp(caret, 0, text.Length);
        int lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var before = text[lineStart..caret];
        if (before.Length == 0 || before.Trim(' ', '\t').Length > 0) return null;
        var unit = IndentUnit(text);
        var indent = before.EndsWith(unit, StringComparison.Ordinal) ? before[..^unit.Length] : before.EndsWith('\t') ? before[..^1] : "";
        var result = text[..lineStart] + indent + closing + text[caret..];
        int at = lineStart + indent.Length + 1;
        return new TextEdit(result, at, at);
    }

    private static char? Pair(char open) => open switch { '{' => '}', '[' => ']', '(' => ')', _ => null };

    private static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return a; }
}
