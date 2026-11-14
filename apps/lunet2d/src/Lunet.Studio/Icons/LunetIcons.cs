namespace Lunet.Studio;

/// <summary>
/// Catálogo de ícones do IDE, desenhados como contorno (estilo atual de apps móveis) em vez de caracteres Unicode,
/// que variam de aparência entre aparelhos e não comunicam a ação. Cada ícone tem nome estável; o Android só desenha.
/// </summary>
public static class LunetIcons
{
    private static readonly Dictionary<string, string> Definitions = new(StringComparer.Ordinal)
    {
        // Navegação
        ["back"] = "M15 5 L8 12 L15 19",
        ["arrow-left"] = "M19 12 H5 M11 6 L5 12 L11 18",
        ["close"] = "M6 6 L18 18 M18 6 L6 18",
        ["more"] = "~O 5 12 1.7|~O 12 12 1.7|~O 19 12 1.7",
        ["more-vertical"] = "~O 12 5 1.7|~O 12 12 1.7|~O 12 19 1.7",
        ["chevron-right"] = "M9 6 L15 12 L9 18",
        ["chevron-down"] = "M6 9 L12 15 L18 9",
        ["home"] = "M4 11 L12 4 L20 11|M6 9.5 V20 H10 V14 H14 V20 H18 V9.5",
        ["settings"] = "M4 7 H9 M15 7 H20 M4 17 H13 M19 17 H20|O 12 7 3|O 16 17 3",
        ["check"] = "M5 12.5 L10 17.5 L19 7",
        ["plus"] = "M12 5 V19 M5 12 H19",
        ["search"] = "O 11 11 7|M16.5 16.5 L21 21",
        // Execução
        ["play"] = "~M7 4.5 L19 12 L7 19.5 Z|M7 4.5 L19 12 L7 19.5 Z",
        ["stop"] = "~R 6 6 12 12 2|R 6 6 12 12 2",
        ["pause"] = "~R 6 5 4 14 1|~R 14 5 4 14 1",
        ["step"] = "~M5 5 L15 12 L5 19 Z|M5 5 L15 12 L5 19 Z|M19 5 V19",
        ["restart"] = "M21 12 C21 7 17 3 12 3 C8.5 3 5.5 4.9 4 7.6 M3.5 3.5 V8 H8 M3 12 C3 17 7 21 12 21 C15.5 21 18.5 19.1 20 16.4 M20.5 20.5 V16 H16",
        ["chart"] = "M5 20 V11 M12 20 V4 M19 20 V14",
        ["inspect"] = "R 4 4 16 16 3|O 12 12 3.5|M12 4 V7 M12 17 V20 M4 12 H7 M17 12 H20",
        // Edição e arquivos
        ["undo"] = "M9 14 L4 9 L9 4 M4 9 H15 C18.3 9 21 11.7 21 15 C21 18.3 18.3 21 15 21 H11",
        ["redo"] = "M15 14 L20 9 L15 4 M20 9 H9 C5.7 9 3 11.7 3 15 C3 18.3 5.7 21 9 21 H13",
        ["send"] = "M12 19 V5 M6 11 L12 5 L18 11",
        ["copy"] = "R 9 9 11 11 2|M5 15 V6 C5 5.4 5.4 5 6 5 H15",
        ["import"] = "M12 4 V15 M7 10 L12 15 L17 10 M5 20 H19",
        ["trash"] = "M4 7 H20 M10 11 V17 M14 11 V17 M6 7 L7 19 C7 20.1 7.9 21 9 21 H15 C16.1 21 17 20.1 17 19 L18 7 M9 7 V4 H15 V7",
        ["sparkles"] = "M12 3 C12.6 8.4 15.6 11.4 21 12 C15.6 12.6 12.6 15.6 12 21 C11.4 15.6 8.4 12.6 3 12 C8.4 11.4 11.4 8.4 12 3 Z",
        ["code"] = "M8 7 L3 12 L8 17 M16 7 L21 12 L16 17 M14 4 L10 20",
        ["folder"] = "M3 7 C3 5.9 3.9 5 5 5 H9.5 L12 8 H19 C20.1 8 21 8.9 21 10 V17 C21 18.1 20.1 19 19 19 H5 C3.9 19 3 18.1 3 17 Z",
        ["file"] = "M7 3 H14 L19 8 V19 C19 20.1 18.1 21 17 21 H7 C5.9 21 5 20.1 5 19 V5 C5 3.9 5.9 3 7 3 Z|M14 3 V8 H19",
        ["note"] = "M7 3 H14 L19 8 V19 C19 20.1 18.1 21 17 21 H7 C5.9 21 5 20.1 5 19 V5 C5 3.9 5.9 3 7 3 Z|M14 3 V8 H19|M9 13 H15 M9 17 H15",
        ["image"] = "R 3 4 18 16 2|~O 8.5 9.5 1.7|M21 16 L16 11 L6 20",
        ["sprite"] = "R 4 4 16 16 2|~R 8 8 4 4 0|~R 12 12 4 4 0",
        ["tilemap"] = "R 3 3 7 7 1.5|R 14 3 7 7 1.5|R 3 14 7 7 1.5|R 14 14 7 7 1.5",
        ["audio"] = "M4 9 H8 L13 5 V19 L8 15 H4 Z|M16.5 9 C17.8 10.2 17.8 13.8 16.5 15 M19 6.5 C21.7 9.2 21.7 14.8 19 17.5",
        ["sparkle"] = "M12 3 L13.6 10.4 L21 12 L13.6 13.6 L12 21 L10.4 13.6 L3 12 L10.4 10.4 Z|O 19 5 1|O 5 19 1",
        ["palette"] = "M12 3 C7 3 3 7 3 12 C3 17 7 21 12 21 C13.5 21 14 20 13.5 18.8 C13 17.6 13.8 16.5 15 16.5 H17 C19.5 16.5 21 15 21 12 C21 7 17 3 12 3 Z|O 7.5 11 1.3|O 10 7 1.3|O 14.5 7 1.3|O 17 10.5 1.3",
        ["waveform"] = "M3 11 V13 M6.5 8 V16 M10 4.5 V19.5 M13.5 9 V15 M17 6 V18 M20.5 10 V14",
        ["music"] = "M9 18 V5 L20 3 V16|O 6 18 3|O 17 16 3",
        ["font"] = "M5 20 L12 4 L19 20 M8 14 H16",
        ["data"] = "M8 4 C6 4 5 5 5 7 V9 C5 10.5 4.5 11.5 3 12 C4.5 12.5 5 13.5 5 15 V17 C5 19 6 20 8 20 M16 4 C18 4 19 5 19 7 V9 C19 10.5 19.5 11.5 21 12 C19.5 12.5 19 13.5 19 15 V17 C19 19 18 20 16 20",
        ["book"] = "M4 5 C4 4.4 4.4 4 5 4 H12 V20 H5 C4.4 20 4 19.6 4 19 Z|M20 5 C20 4.4 19.6 4 19 4 H12 V20 H19 C19.6 20 20 19.6 20 19 Z",
        ["git"] = "O 6 5 2.2|O 6 19 2.2|O 18 8 2.2|M6 7.2 V16.8|M18 10.2 C18 14.5 12 13 7.7 17.6",
        // Estúdio e IDE
        ["studio"] = "M12 3 C7 3 3 7 3 12 C3 17 7 21 12 21 C13.4 21 14 20 14 19 C14 18 13.3 17.6 13.3 16.6 C13.3 15.6 14.1 15 15 15 H17 C19.2 15 21 13.2 21 11 C21 6.6 17 3 12 3 Z|~O 7.5 11 1.2|~O 10.5 7.5 1.2|~O 15 7.5 1.2",
        ["problems"] = "M12 4 L21 19 H3 Z|M12 10 V14|~O 12 16.7 1.1",
        ["console"] = "M4 6 L10 12 L4 18 M12 18 H20",
        ["moon"] = "M20 14 C18.8 14.6 17.5 15 16 15 C11.6 15 8 11.4 8 7 C8 5.5 8.4 4.2 9 3 C5.5 4 3 7.2 3 11 C3 15.9 7.1 20 12 20 C15.8 20 19 17.5 20 14 Z",
        ["dot"] = "~O 12 12 4",
        ["scissors"] = "O 6 6 3|O 6 18 3|M8.1 8.1 L20 20 M8.1 15.9 L20 4",
        ["loop"] = "M17 3 L21 7 L17 11|M3 11 V9 C3 7.9 3.9 7 5 7 H21|M7 21 L3 17 L7 13|M21 13 V15 C21 16.1 20.1 17 19 17 H3",
        // Desenho
        ["pencil"] = "M4 20 L8.5 19 L19.5 8 C20.5 7 20.5 5.5 19.5 4.5 C18.5 3.5 17 3.5 16 4.5 L5 15.5 Z|M14 6.5 L17.5 10",
        ["eraser"] = "M6 20 L3.5 17.5 C2.8 16.8 2.8 15.7 3.5 15 L13.5 5 C14.2 4.3 15.3 4.3 16 5 L20 9 C20.7 9.7 20.7 10.8 20 11.5 L11.5 20 Z|M9 11.5 L14.5 17|M11.5 20 H21",
        ["bucket"] = "M5 12 L11.5 5.5 L18.5 12.5 L12 19 Z|M8 8.5 L15.5 16|~M20 14 C20 14 18 16.6 18 18 C18 19.1 18.9 20 20 20 C21.1 20 22 19.1 22 18 C22 16.6 20 14 20 14 Z",
        ["picker"] = "M20.5 3.5 C19.4 2.4 17.6 2.4 16.5 3.5 L14 6 L12.7 4.7 L11.3 6.1 L12.6 7.4 L4 16 C3.4 16.6 3.1 17.4 3.1 18.2 L3 21 L5.8 20.9 C6.6 20.9 7.4 20.6 8 20 L16.6 11.4 L17.9 12.7 L19.3 11.3 L18 10 L20.5 7.5 C21.6 6.4 21.6 4.6 20.5 3.5 Z",
        ["line"] = "M6 18 L18 6|O 5 19 2|O 19 5 2",
        ["rect"] = "R 4 6 16 12 1.5",
        ["rect-filled"] = "~R 4 6 16 12 2",
        ["select"] = "M4 8 V5 C4 4.4 4.4 4 5 4 H8 M16 4 H19 C19.6 4 20 4.4 20 5 V8 M20 16 V19 C20 19.6 19.6 20 19 20 H16 M8 20 H5 C4.4 20 4 19.6 4 19 V16|M9 12 H15 M12 9 V15",
        ["lasso"] = "M12 5 C7 5 3.5 7.2 3.5 10 C3.5 12.8 7 15 12 15 C17 15 20.5 12.8 20.5 10 C20.5 7.2 17 5 12 5 Z|M8.5 14.5 C7.8 17 8.5 19 10.8 20 C12.4 20.6 13.4 19.5 12.4 18.4",
        ["wand"] = "M4 20 L14 10 M10.5 6.5 L17.5 13.5|M18 3 V7 M16 5 H20 M7 3.5 V6 M5.75 4.75 H8.25 M19.5 15 V18 M18 16.5 H21",
        ["move"] = "M12 3 V21 M3 12 H21 M9 6 L12 3 L15 6 M9 18 L12 21 L15 18 M6 9 L3 12 L6 15 M18 9 L21 12 L18 15",
        ["rotate"] = "M20 11 C19.5 6.8 16.2 3.5 12 3.5 C7.3 3.5 3.5 7.3 3.5 12 C3.5 16.7 7.3 20.5 12 20.5 C14.9 20.5 17.4 19 19 16.8|M20.5 4 V11 H13.5",
        ["rotate-90"] = "R 4 9 11 11 1.5|M12 4 H17 C18.7 4 20 5.3 20 7 V12 M17 9.5 L20 12.5 L23 9.5",
        ["flip-horizontal"] = "M12 3 V6 M12 10.5 V13.5 M12 18 V21|M8 7 L3 17 H8 Z|M16 7 L21 17 H16 Z",
        ["flip-vertical"] = "M3 12 H6 M10.5 12 H13.5 M18 12 H21|M7 8 L17 8 L12 3 Z|M7 16 L17 16 L12 21 Z",
        ["grid"] = "R 4 4 16 16 2|M4 9.3 H20 M4 14.7 H20 M9.3 4 V20 M14.7 4 V20",
        ["fit"] = "M4 9 V5 C4 4.4 4.4 4 5 4 H9 M15 4 H19 C19.6 4 20 4.4 20 5 V9 M20 15 V19 C20 19.6 19.6 20 19 20 H15 M9 20 H5 C4.4 20 4 19.6 4 19 V15|R 9 9 6 6 1",
        ["layers"] = "M12 3 L21 8 L12 13 L3 8 Z|M3 12 L12 17 L21 12|M3 16 L12 21 L21 16",
        ["brush-size"] = "~O 6 12 1.5|~O 12 12 3|~O 19 12 4.5",
        ["confirm"] = "O 12 12 9|M8 12.5 L11 15.5 L16 9",
        ["cancel"] = "O 12 12 9|M9 9 L15 15 M15 9 L9 15",
        ["duplicate"] = "R 3 3 12 12 2|R 9 9 12 12 2",
    };

    /// <summary>Caracteres antigos (usados em botões de texto) e o ícone que os substitui.</summary>
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        ["‹"] = "back", ["←"] = "arrow-left", ["✕"] = "close", ["⋯"] = "more", ["⋮"] = "more-vertical", ["⚙"] = "settings",
        ["＋"] = "plus", ["▶"] = "play", ["■"] = "stop", ["⏸"] = "pause", ["⏭"] = "step", ["↻"] = "restart",
        ["▥"] = "chart", ["🔍"] = "inspect", ["↶"] = "undo", ["↷"] = "redo", ["↑"] = "send", ["⧉"] = "copy", ["⇩"] = "import",
        ["✦"] = "sparkles", ["⌨"] = "code", ["▤"] = "folder", ["◧"] = "studio", ["⚠"] = "problems", ["❯"] = "console",
        ["☾"] = "moon", ["✎"] = "pencil", ["⌫"] = "eraser", ["◍"] = "bucket", ["⊙"] = "picker", ["╱"] = "line",
        ["▭"] = "rect", ["✓"] = "check", ["✂"] = "scissors", ["▦"] = "image", ["¶"] = "note", ["∿"] = "waveform", ["⌂"] = "home",
        ["●"] = "dot", ["▸"] = "chevron-right", ["▾"] = "chevron-down", ["📎"] = "file",
    };

    private static readonly Dictionary<string, VectorIcon> Cache = new(StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Names => Definitions.Keys;
    public static IReadOnlyDictionary<string, string> GlyphMap => Glyphs;

    public static VectorIcon Get(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            if (!Definitions.TryGetValue(name, out var definition)) throw new KeyNotFoundException($"Ícone '{name}' não existe.");
            return Cache[name] = VectorIcon.Parse(name, definition);
        }
    }

    public static bool TryGet(string name, out VectorIcon icon)
    {
        if (!Definitions.ContainsKey(name)) { icon = null!; return false; }
        icon = Get(name);
        return true;
    }

    /// <summary>Procura o ícone de um caractere antigo (ex.: "↶" → undo).</summary>
    public static bool TryFromGlyph(string glyph, out VectorIcon icon)
    {
        if (Glyphs.TryGetValue(glyph, out var name)) { icon = Get(name); return true; }
        icon = null!;
        return false;
    }

    /// <summary>"▶ Rodar" → (play, "Rodar"); "⌨\nCódigo" → (code, "Código", acima). Sem ícone conhecido devolve false.</summary>
    public static bool TrySplitLabel(string text, out VectorIcon icon, out string label, out bool stacked)
    {
        icon = null!; label = text; stacked = false;
        int cut = 0;
        while (cut < text.Length && !char.IsWhiteSpace(text[cut])) cut++;
        if (cut == 0 || cut > 2 || !TryFromGlyph(text[..cut], out icon)) return false;
        stacked = cut < text.Length && text[cut] == '\n';
        label = text[cut..].Trim();
        return true;
    }
}
