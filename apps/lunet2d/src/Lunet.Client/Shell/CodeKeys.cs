namespace Lunet.Client.Shell;

/// <summary>Direction of a swipe that starts on a key.</summary>
public enum SwipeDirection { None, Up, Down, Left, Right }

/// <summary>
/// One key of the code bar: a symbol on tap and up to four more by swiping toward a side (the Unexpected Keyboard
/// model, made for programming in Termux — study 25/11 §3). A command key (arrows, Tab, undo) has no symbols.
/// </summary>
public sealed record CodeKey(string Tap, string? Up = null, string? Down = null, string? Left = null, string? Right = null)
{
    /// <summary>The symbol a swipe gives, falling back to the tap symbol when that side is empty.</summary>
    public string For(SwipeDirection direction) => direction switch
    {
        SwipeDirection.Up => Up ?? Tap,
        SwipeDirection.Down => Down ?? Tap,
        SwipeDirection.Left => Left ?? Tap,
        SwipeDirection.Right => Right ?? Tap,
        _ => Tap,
    };
}

/// <summary>An edit to apply to the text: replace [Start, End) by <see cref="Insert"/>, then put the caret at <see cref="Caret"/>.</summary>
public readonly record struct CodeEdit(int Start, int End, string Insert, int Caret);

/// <summary>The code bar of the editor and what each key does to the text. No UI dependency: fully tested.</summary>
public static class CodeKeys
{
    public const string Tab = "⇥", Left = "←", Right = "→", Up = "↑", Down = "↓";

    /// <summary>Minimum finger travel (dp) for a swipe; below it the touch is a tap.</summary>
    public const double SwipeThreshold = 18;

    /// <summary>
    /// The bar, in order. Each key holds the symbols that appear together in C# (pairs on opposite sides), so the
    /// ~30 symbols of the old 21-key strip fit in 10 keys of 48 dp.
    /// </summary>
    public static readonly IReadOnlyList<CodeKey> Bar =
    [
        new(Tab),
        new("{", Up: "}", Down: "[", Right: "]"),
        new("(", Up: ")", Down: "<", Right: ">"),
        new(";", Up: ":", Down: ",", Right: "."),
        new("=", Up: "+", Down: "-", Left: "!", Right: "=>"),
        new("\"", Up: "'", Down: "$\"", Right: "\\"),
        new("&", Up: "|", Down: "?", Right: "??"),
        new("*", Up: "/", Down: "%", Right: "_"),
        new(Left, Up: Up, Down: Down, Right: Right),
        new(Right, Up: Up, Down: Down, Left: Left),
    ];

    /// <summary>Which direction a finger travelled (dp), or None for a tap.</summary>
    public static SwipeDirection Resolve(double dx, double dy)
    {
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < SwipeThreshold) return SwipeDirection.None;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? SwipeDirection.Right : SwipeDirection.Left
            : dy > 0 ? SwipeDirection.Down : SwipeDirection.Up;
    }

    /// <summary>
    /// What a symbol does at a selection: arrows move, Tab indents, an opening brace makes an indented block,
    /// pairs wrap the selection or put the caret between them. Every result is one edit, so it undoes in one step.
    /// Returns null for arrows (the caller moves the caret with <see cref="Move"/>).
    /// </summary>
    public static CodeEdit? Apply(string text, int selectionStart, int selectionEnd, string symbol)
    {
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        var selected = text[start..end];
        switch (symbol)
        {
            case Left or Right or Up or Down: return null;
            case Tab: return new(start, end, "    ", start + 4);
            case "{":
            {
                var indent = LineIndent(text, start);
                var inner = indent + "    ";
                var body = selected.Length == 0 ? "" : string.Join("\n", selected.Split('\n').Select(l => inner + l.TrimStart()));
                var insert = "{\n" + inner + body + "\n" + indent + "}";
                return new(start, end, insert, start + 2 + inner.Length + body.Length);
            }
            case "(" or "[" or "\"" or "'":
            {
                var close = symbol switch { "(" => ")", "[" => "]", "\"" => "\"", _ => "'" };
                return new(start, end, symbol + selected + close, start + 1 + selected.Length);
            }
            default: return new(start, end, symbol, start + symbol.Length);
        }
    }

    /// <summary>The caret after an arrow key (up/down keep the column when the line is long enough).</summary>
    public static int Move(string text, int caret, string arrow)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        int lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        int column = caret - lineStart;
        switch (arrow)
        {
            case Left: return Math.Max(0, caret - 1);
            case Right: return Math.Min(text.Length, caret + 1);
            case Up:
            {
                if (lineStart == 0) return 0;
                int prevEnd = lineStart - 1; // the '\n' that ends the previous line
                int prevStart = prevEnd == 0 ? 0 : text.LastIndexOf('\n', prevEnd - 1) + 1;
                return Math.Min(prevStart + column, prevEnd);
            }
            case Down:
            {
                int next = text.IndexOf('\n', caret);
                if (next < 0) return text.Length;
                int nextEnd = text.IndexOf('\n', next + 1);
                if (nextEnd < 0) nextEnd = text.Length;
                return Math.Min(next + 1 + column, nextEnd);
            }
            default: return caret;
        }
    }

    private static string LineIndent(string text, int position)
    {
        int lineStart = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        int i = lineStart;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
        return text[lineStart..i];
    }
}
