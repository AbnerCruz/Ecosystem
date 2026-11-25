namespace Lunet.Client.Shell;

/// <summary>What a key of the Lunet keyboard does.</summary>
public enum KeyAction { Text, Shift, Backspace, Enter, Space, Symbols, Letters, System }

/// <summary>One key: what it types (or does), its relative width, and its long-press variants.</summary>
public sealed record KeyDef(string Label, KeyAction Action = KeyAction.Text, double Width = 1, IReadOnlyList<string>? Variants = null)
{
    /// <summary>Keys that repeat while held (study 25/11: delete, space; arrows live in Atalhos).</summary>
    public bool Repeats => Action is KeyAction.Backspace or KeyAction.Space;
}

/// <summary>Shift state: off, one capital letter, or caps lock (double tap).</summary>
public enum ShiftState { Off, Once, Locked }

/// <summary>
/// The "Teclas" tab: a Gboard-like layout (QWERTY, numbers row, symbols page, PT-BR accents on long press) and its
/// timing rules (AOSP LatinIME: repeat starts at 400 ms, every 50 ms; long press at 400 ms). No UI dependency.
/// </summary>
public static class KeyboardLayout
{
    public static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan RepeatStart = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(50);
    /// <summary>Two Shift taps within this time lock capitals.</summary>
    public static readonly TimeSpan DoubleTap = TimeSpan.FromMilliseconds(350);
    /// <summary>Finger travel on the space bar (dp) that moves the caret one character (Gboard "glide cursor").</summary>
    public const double CursorStep = 12;
    /// <summary>Finger travel left from Backspace (dp) that deletes one more word (Gboard "glide delete").</summary>
    public const double WordStep = 28;

    /// <summary>PT-BR variants shown on long press (FlorisBoard/Gboard "extended popups").</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Accents = new Dictionary<string, IReadOnlyList<string>>
    {
        ["a"] = ["á", "à", "â", "ã", "ä"],
        ["e"] = ["é", "ê", "è", "ë"],
        ["i"] = ["í", "î", "ì", "ï"],
        ["o"] = ["ó", "ô", "õ", "ò", "ö"],
        ["u"] = ["ú", "ü", "û", "ù"],
        ["c"] = ["ç"],
        ["n"] = ["ñ"],
    };

    private static KeyDef L(string c) => new(c, Variants: Accents.GetValueOrDefault(c));

    /// <summary>The letters page: numbers row, three letter rows and the bottom row.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> Letters =
    [
        "1234567890".Select(c => new KeyDef(c.ToString())).ToArray(),
        "qwertyuiop".Select(c => L(c.ToString())).ToArray(),
        "asdfghjkl".Select(c => L(c.ToString())).ToArray(),
        [new("⇧", KeyAction.Shift, 1.5), .. "zxcvbnm".Select(c => L(c.ToString())), new("⌫", KeyAction.Backspace, 1.5)],
        [new("?123", KeyAction.Symbols, 1.5), new("🌐", KeyAction.System, 1), new(",", Variants: [";", ":"]), new("espaço", KeyAction.Space, 4), new(".", Variants: ["…", "!", "?"]), new("⏎", KeyAction.Enter, 1.5)],
    ];

    /// <summary>The symbols page (what code needs most, in Gboard's order where it has one).</summary>
    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> Symbols =
    [
        "1234567890".Select(c => new KeyDef(c.ToString())).ToArray(),
        "@#$_&-+()/".Select(c => new KeyDef(c.ToString())).ToArray(),
        "*\"':;!?{}".Select(c => new KeyDef(c.ToString())).ToArray(),
        [new("=", Variants: ["==", "!=", "=>"]), .. "<>[]|\\%~".Select(c => new KeyDef(c.ToString())), new("⌫", KeyAction.Backspace, 1.5)],
        [new("ABC", KeyAction.Letters, 1.5), new("🌐", KeyAction.System, 1), new(","), new("espaço", KeyAction.Space, 4), new("."), new("⏎", KeyAction.Enter, 1.5)],
    ];

    /// <summary>The text a letter key types with the current Shift state.</summary>
    public static string Typed(KeyDef key, ShiftState shift) =>
        shift == ShiftState.Off || key.Action != KeyAction.Text ? key.Label : key.Label.ToUpperInvariant();

    /// <summary>Shift after a Shift tap (a second tap within <see cref="DoubleTap"/> locks; a tap on lock releases).</summary>
    public static ShiftState Toggle(ShiftState current, TimeSpan sinceLastShiftTap) => current switch
    {
        ShiftState.Off => ShiftState.Once,
        ShiftState.Once when sinceLastShiftTap <= DoubleTap => ShiftState.Locked,
        _ => ShiftState.Off,
    };

    /// <summary>Shift after typing a character: one capital, then back to lower case; caps lock stays.</summary>
    public static ShiftState AfterTyping(ShiftState current) => current == ShiftState.Once ? ShiftState.Off : current;

    /// <summary>
    /// The edit of Backspace: the selection, else one character, else (glide delete) <paramref name="words"/> words
    /// before the caret, spaces included. Returns null at the start of the text.
    /// </summary>
    public static CodeEdit? Backspace(string text, int selectionStart, int selectionEnd, int words = 0)
    {
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        if (end > start) return new(start, end, "", start);
        if (start == 0) return null;
        if (words <= 0) return new(start - 1, start, "", start - 1);
        int i = start;
        for (int w = 0; w < words && i > 0; w++)
        {
            while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;
            if (i > 0 && !IsWord(text[i - 1])) { i--; continue; }
            while (i > 0 && IsWord(text[i - 1])) i--;
        }
        return new(i, start, "", i);
    }

    /// <summary>Enter keeps the indentation of the current line (and adds one level after an opening brace).</summary>
    public static CodeEdit Enter(string text, int selectionStart, int selectionEnd)
    {
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        int i = lineStart;
        while (i < start && (text[i] == ' ' || text[i] == '\t')) i++;
        var indent = text[lineStart..i];
        var before = text[lineStart..start].TrimEnd();
        if (before.EndsWith('{')) indent += "    ";
        var insert = "\n" + indent;
        return new(start, end, insert, start + insert.Length);
    }

    /// <summary>The caret after dragging on the space bar by <paramref name="dx"/> dp from the caret where the drag began.</summary>
    public static int GlideCursor(string text, int caretAtStart, double dx) =>
        Math.Clamp(caretAtStart + (int)(dx / CursorStep), 0, text.Length);

    /// <summary>How many words a left drag from Backspace deletes (0 = a normal backspace).</summary>
    public static int GlideWords(double dx) => dx >= 0 ? 0 : (int)(-dx / WordStep);

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
}
