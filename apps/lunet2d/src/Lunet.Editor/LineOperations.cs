namespace Lunet.Editor;

/// <summary>Comandos de edição por linha usados pelos atalhos e pelo menu (duplicar, apagar, mover, comentar, indentar).</summary>
public static class LineOperations
{
    /// <summary>Intervalo [início, fim) das linhas tocadas pela seleção, incluindo a quebra de linha final.</summary>
    private static (int Start, int End) Block(string text, int selStart, int selEnd)
    {
        selStart = Math.Clamp(selStart, 0, text.Length);
        selEnd = Math.Clamp(Math.Max(selStart, selEnd), 0, text.Length);
        var start = selStart;
        while (start > 0 && text[start - 1] != '\n') start--;
        // Seleção que termina exatamente no começo de uma linha não inclui essa linha.
        var effectiveEnd = selEnd > selStart && text[selEnd - 1] == '\n' ? selEnd - 1 : selEnd;
        var end = effectiveEnd;
        while (end < text.Length && text[end] != '\n') end++;
        if (end < text.Length) end++;
        return (start, end);
    }

    public static EditResult DuplicateLines(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var block = text[start..end];
        var withBreak = block.EndsWith('\n') ? block : "\n" + block;
        var insertAt = end;
        var insert = block.EndsWith('\n') ? block : withBreak;
        var shift = insert.Length;
        return new EditResult(insertAt, 0, insert, Math.Min(selStart + shift, text.Length + shift), Math.Min(selEnd + shift, text.Length + shift));
    }

    public static EditResult DeleteLines(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        // Última linha sem quebra final: apaga também a quebra anterior.
        if (end == text.Length && start > 0 && !text[start..end].EndsWith('\n')) start--;
        return new EditResult(start, end - start, "", start, start);
    }

    /// <summary>Move as linhas para cima (<paramref name="up"/>) ou para baixo; devolve nulo na borda do arquivo.</summary>
    public static EditResult? MoveLines(string text, int selStart, int selEnd, bool up)
    {
        var (start, end) = Block(text, selStart, selEnd);
        if (up)
        {
            if (start == 0) return null;
            var prevStart = start - 1;
            while (prevStart > 0 && text[prevStart - 1] != '\n') prevStart--;
            var prev = text[prevStart..start];
            var block = text[start..end];
            if (!block.EndsWith('\n')) { block += "\n"; prev = prev.TrimEnd('\n'); }
            var shift = prevStart - start;
            return new EditResult(prevStart, end - prevStart, block + prev, Math.Max(prevStart, selStart + shift), Math.Max(prevStart, selEnd + shift));
        }
        else
        {
            if (end >= text.Length) return null;
            var nextEnd = end;
            while (nextEnd < text.Length && text[nextEnd] != '\n') nextEnd++;
            var hadBreak = nextEnd < text.Length;
            if (hadBreak) nextEnd++;
            var next = text[end..nextEnd];
            var block = text[start..end];
            if (!next.EndsWith('\n')) { next += "\n"; block = block.TrimEnd('\n'); }
            var shift = next.Length;
            return new EditResult(start, nextEnd - start, next + block, selStart + shift, selEnd + shift);
        }
    }

    /// <summary>Comenta ou descomenta (//) as linhas; descomenta quando todas as linhas não vazias já estão comentadas.</summary>
    public static EditResult ToggleComment(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var lines = text[start..end].Split('\n');
        var content = lines.Where(l => l.Trim().Length > 0).ToList();
        var allCommented = content.Count > 0 && content.All(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal));
        var indent = content.Count == 0 ? 0 : content.Min(l => l.Length - l.TrimStart().Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0) continue;
            if (allCommented)
            {
                var at = line.Length - line.TrimStart().Length;
                var removeLength = line.AsSpan(at).StartsWith("// ") ? 3 : 2;
                lines[i] = line.Remove(at, removeLength);
            }
            else lines[i] = line.Insert(indent, "// ");
        }
        var replaced = string.Join('\n', lines);
        return new EditResult(start, end - start, replaced, start, start + replaced.TrimEnd('\n').Length);
    }

    public static EditResult Indent(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var lines = text[start..end].Split('\n');
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].Trim().Length > 0) lines[i] = IndentationService.Indent + lines[i];
        var replaced = string.Join('\n', lines);
        return new EditResult(start, end - start, replaced, start, start + replaced.TrimEnd('\n').Length);
    }

    public static EditResult Outdent(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var lines = text[start..end].Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var remove = 0;
            while (remove < IndentationService.Indent.Length && remove < lines[i].Length && lines[i][remove] == ' ') remove++;
            if (remove == 0 && lines[i].StartsWith('\t')) remove = 1;
            lines[i] = lines[i][remove..];
        }
        var replaced = string.Join('\n', lines);
        return new EditResult(start, end - start, replaced, start, start + replaced.TrimEnd('\n').Length);
    }
}
