namespace Lunet.Editor;

/// <summary>Regras de indentação automática (4 espaços).</summary>
public static class IndentationService
{
    public const string Indent = "    ";

    /// <summary>Edição para a tecla Enter: mantém a indentação e aprofunda após '{'. Entre '{' e '}' cria a linha do meio.</summary>
    public static TextEdit NewLine(string text, int caret, int selectionLength = 0)
    {
        var lineStart = LineStart(text, caret);
        var before = text.AsSpan(lineStart, caret - lineStart);
        var indent = LeadingWhitespace(before);
        var trimmed = before.TrimEnd();
        var opensBlock = trimmed.EndsWith("{");
        var afterCaret = caret + selectionLength;
        var nextChar = afterCaret < text.Length ? text[afterCaret] : '\0';

        if (opensBlock && nextChar == '}')
        {
            var middle = "\n" + indent + Indent;
            return new TextEdit(caret, selectionLength, middle + "\n" + indent, caret + middle.Length);
        }
        var insert = "\n" + indent + (opensBlock ? Indent : "");
        return new TextEdit(caret, selectionLength, insert, caret + insert.Length);
    }

    /// <summary>Ao digitar '}' numa linha só com espaços, remove um nível de indentação. Devolve nulo se não se aplica.</summary>
    public static TextEdit? CloseBrace(string text, int caret)
    {
        var lineStart = LineStart(text, caret);
        var before = text.AsSpan(lineStart, caret - lineStart);
        if (before.Length == 0 || !before.TrimStart(' ').IsEmpty) return null;
        var remove = Math.Min(Indent.Length, before.Length);
        return new TextEdit(caret - remove, remove, "}", caret - remove + 1);
    }

    /// <summary>Insere um nível de indentação (tecla Tab).</summary>
    public static TextEdit Tab(int caret, int selectionLength = 0) => new(caret, selectionLength, Indent, caret + Indent.Length);

    private static int LineStart(string text, int caret)
    {
        var i = Math.Clamp(caret, 0, text.Length);
        while (i > 0 && text[i - 1] != '\n') i--;
        return i;
    }

    private static string LeadingWhitespace(ReadOnlySpan<char> line)
    {
        var n = 0;
        while (n < line.Length && (line[n] == ' ' || line[n] == '\t')) n++;
        return line[..n].ToString();
    }
}
