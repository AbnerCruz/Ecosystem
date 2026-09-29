namespace Lunet.Editor;

/// <summary>Uma seleção (ou cursor, quando vazia). <see cref="Start"/> ≤ <see cref="End"/>.</summary>
public readonly record struct Selection(int Start, int End)
{
    public int Length => End - Start;
    public bool IsEmpty => Start == End;
}

/// <summary>Resultado de uma edição com vários cursores: o novo texto e as novas seleções (todas vazias após digitar).</summary>
public sealed record MultiEditResult(string Text, IReadOnlyList<Selection> Selections);

/// <summary>Multi-seleção: edita vários pontos do texto ao mesmo tempo.</summary>
public static class MultiCursor
{
    /// <summary>Ordena, remove duplicatas e funde seleções que se sobrepõem.</summary>
    public static List<Selection> Normalize(IEnumerable<Selection> selections)
    {
        var result = new List<Selection>();
        foreach (var s in selections.OrderBy(s => s.Start).ThenBy(s => s.End))
        {
            if (result.Count > 0 && (s.Start < result[^1].End || s == result[^1]))
            {
                result[^1] = new Selection(result[^1].Start, Math.Max(result[^1].End, s.End));
                continue;
            }
            result.Add(s);
        }
        return result;
    }

    /// <summary>Substitui cada seleção por <paramref name="insert"/> (digitar, colar).</summary>
    public static MultiEditResult Replace(string text, IReadOnlyList<Selection> selections, string insert)
    {
        var ordered = Normalize(selections);
        var builder = new System.Text.StringBuilder(text.Length + insert.Length * ordered.Count);
        var carets = new List<Selection>(ordered.Count);
        var cursor = 0;
        foreach (var s in ordered)
        {
            builder.Append(text, cursor, s.Start - cursor);
            builder.Append(insert);
            var caret = builder.Length;
            carets.Add(new Selection(caret, caret));
            cursor = s.End;
        }
        builder.Append(text, cursor, text.Length - cursor);
        return new MultiEditResult(builder.ToString(), carets);
    }

    /// <summary>Backspace em todos os cursores: apaga a seleção, ou o caractere anterior quando vazia.</summary>
    public static MultiEditResult Backspace(string text, IReadOnlyList<Selection> selections) =>
        Replace(text, Normalize(selections).Select(s => s.IsEmpty ? new Selection(Math.Max(0, s.Start - 1), s.Start) : s).ToList(), "");

    /// <summary>Delete em todos os cursores.</summary>
    public static MultiEditResult DeleteForward(string text, IReadOnlyList<Selection> selections) =>
        Replace(text, Normalize(selections).Select(s => s.IsEmpty ? new Selection(s.Start, Math.Min(text.Length, s.Start + 1)) : s).ToList(), "");

    /// <summary>Adiciona a próxima ocorrência do texto selecionado (ou da palavra sob o cursor), como Ctrl+D do VS Code.</summary>
    public static List<Selection> SelectNextOccurrence(string text, IReadOnlyList<Selection> selections)
    {
        var current = Normalize(selections);
        var last = current[^1];
        if (last.IsEmpty)
        {
            var word = WordAt(text, last.Start);
            if (word is null) return current;
            current[^1] = word.Value;
            return current;
        }
        var needle = text.Substring(last.Start, last.Length);
        var from = last.End;
        var index = text.IndexOf(needle, from, StringComparison.Ordinal);
        if (index < 0) index = text.IndexOf(needle, 0, StringComparison.Ordinal);
        if (index < 0 || current.Any(s => s.Start == index)) return current;
        current.Add(new Selection(index, index + needle.Length));
        return current;
    }

    /// <summary>Seleciona todas as ocorrências do texto selecionado (ou da palavra sob o cursor).</summary>
    public static List<Selection> SelectAllOccurrences(string text, IReadOnlyList<Selection> selections, bool wholeWord = true)
    {
        var primary = Normalize(selections)[0];
        if (primary.IsEmpty)
        {
            var word = WordAt(text, primary.Start);
            if (word is null) return [primary];
            primary = word.Value;
        }
        var needle = text.Substring(primary.Start, primary.Length);
        var result = new List<Selection>();
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            if (wholeWord && (index > 0 && IsWordChar(text[index - 1]) || index + needle.Length < text.Length && IsWordChar(text[index + needle.Length]))) continue;
            result.Add(new Selection(index, index + needle.Length));
        }
        return result;
    }

    /// <summary>Adiciona um cursor na linha acima ou abaixo do cursor principal, na mesma coluna (ou no fim da linha, se curta).</summary>
    public static List<Selection> AddCursorVertically(string text, IReadOnlyList<Selection> selections, bool above)
    {
        var current = Normalize(selections);
        var anchor = above ? current[0] : current[^1];
        var lineStart = anchor.Start;
        while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
        var column = anchor.Start - lineStart;
        int targetStart;
        if (above)
        {
            if (lineStart == 0) return current;
            targetStart = lineStart - 1;
            while (targetStart > 0 && text[targetStart - 1] != '\n') targetStart--;
        }
        else
        {
            var end = anchor.Start;
            while (end < text.Length && text[end] != '\n') end++;
            if (end >= text.Length) return current;
            targetStart = end + 1;
        }
        var targetEnd = targetStart;
        while (targetEnd < text.Length && text[targetEnd] != '\n') targetEnd++;
        var caret = Math.Min(targetStart + column, targetEnd);
        current.Add(new Selection(caret, caret));
        return Normalize(current);
    }

    /// <summary>
    /// Replica uma edição feita no cursor principal (o texto mudou de <paramref name="before"/> para <paramref name="after"/>)
    /// nos demais cursores. Devolve nulo se a mudança não puder ser interpretada como uma edição única.
    /// </summary>
    public static MultiEditResult? Replicate(string before, string after, Selection primaryBefore, IReadOnlyList<Selection> others)
    {
        // Diferença mínima: prefixo e sufixo comuns.
        var prefix = 0;
        var max = Math.Min(before.Length, after.Length);
        while (prefix < max && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < max - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;
        var deleted = before.Length - prefix - suffix;
        var inserted = after.Substring(prefix, after.Length - prefix - suffix);

        // A edição precisa estar no cursor principal (ou tocá-lo).
        var editStart = prefix;
        var editEnd = prefix + deleted;
        var touches = editStart <= primaryBefore.End && editEnd >= primaryBefore.Start;
        if (!touches) return null;

        // O deslocamento da seleção principal em relação à edição, aplicado a cada outro cursor.
        var leftExtra = primaryBefore.Start - editStart; // >0: a edição começa antes do cursor (backspace)
        var rightExtra = editEnd - primaryBefore.End;    // >0: a edição vai além do cursor (delete)
        var all = new List<(Selection Sel, bool Primary)> { (new Selection(primaryBefore.Start, primaryBefore.End), true) };
        all.AddRange(others.Select(o => (o, false)));
        all = all.OrderBy(x => x.Sel.Start).ToList();

        var builder = new System.Text.StringBuilder();
        var cursor = 0;
        var carets = new List<Selection>();
        foreach (var (sel, primary) in all)
        {
            var start = Math.Max(cursor, sel.Start - Math.Max(0, leftExtra));
            var end = Math.Min(before.Length, sel.End + Math.Max(0, rightExtra));
            if (start < cursor) start = cursor;
            builder.Append(before, cursor, start - cursor);
            builder.Append(inserted);
            carets.Add(new Selection(builder.Length, builder.Length));
            cursor = Math.Max(end, start);
        }
        builder.Append(before, cursor, before.Length - cursor);
        return new MultiEditResult(builder.ToString(), carets);
    }

    private static Selection? WordAt(string text, int position)
    {
        var start = Math.Min(position, text.Length);
        var end = start;
        while (start > 0 && IsWordChar(text[start - 1])) start--;
        while (end < text.Length && IsWordChar(text[end])) end++;
        return end > start ? new Selection(start, end) : null;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
