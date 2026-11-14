using System.Text;
using System.Text.RegularExpressions;

namespace Lunet.Ai;

/// <summary>
/// Refuses whole-file overwrites that would silently destroy the person's code (LUNET-507): a model that only saw an
/// excerpt, or that elides unchanged code with "...", must not replace the file with what it happened to write.
/// </summary>
public static class LunetAiWriteGuard
{
    /// <summary>Files shorter than this many non-blank lines can be rewritten freely (tiny files, new stubs).</summary>
    public const int SmallFileLines = 15;
    /// <summary>An overwrite may not drop below this fraction of the original non-blank lines.</summary>
    public const double MinimumKeptFraction = 0.6;

    private static readonly Regex Placeholder = new(
        @"^\s*(//|/\*|\*|#|<!--|--)?\s*(\.\.\.|…|\[\.\.\.\]|\(\.\.\.\))" +
        @"|^\s*(//|/\*|#|<!--).*(resto do c|restante do c|demais m[ée]todos|rest of (the )?(code|file|class)|existing code|c[óo]digo existente|unchanged|inalterad|same as before|como antes|igual ao anterior|omitid|omitted)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static void EnsureSafeOverwrite(string path, string before, string after)
    {
        if (string.IsNullOrWhiteSpace(after) && !string.IsNullOrWhiteSpace(before))
            throw new InvalidOperationException(
                $"Recusado: a gravação esvaziaria {path}. Para apagar trechos use lunet.files.replace com new_text vazio no trecho exato.");
        var beforeLines = NonBlank(before);
        var afterLines = NonBlank(after);
        var placeholders = afterLines.Where(l => Placeholder.IsMatch(l)).Except(beforeLines, StringComparer.Ordinal).ToArray();
        if (placeholders.Length > 0)
            throw new InvalidOperationException(
                $"Recusado: o conteúdo para {path} contém marcador de código omitido (\"{placeholders[0].Trim()}\"). " +
                "Gravar isso apagaria o código existente. Use lunet.files.replace para trocar só o trecho que muda.");
        if (beforeLines.Count < SmallFileLines) return;
        if (afterLines.Count < beforeLines.Count * MinimumKeptFraction)
            throw new InvalidOperationException(
                $"Recusado: a gravação reduziria {path} de {beforeLines.Count} para {afterLines.Count} linhas com código, " +
                "apagando a maior parte do arquivo. Edite com lunet.files.replace (um trecho por vez; new_text vazio apaga um trecho). " +
                "Se você só leu um trecho, nunca regrave o arquivo inteiro.");
    }

    private static List<string> NonBlank(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
}

/// <summary>Finds the span an agent edit targets: exact first, then a unique whitespace-insensitive line match.</summary>
public static class LunetAiTextMatch
{
    private static readonly Regex NumberPrefix = new(@"^\s*\d+\| ?", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Prefixes each line with its number ("12| code") so the model can cite and target lines.</summary>
    public static string Numbered(string content, int firstLine)
    {
        var lines = content.Split('\n');
        var width = (firstLine + lines.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var text = new StringBuilder(content.Length + lines.Length * (width + 2));
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) text.Append('\n');
            text.Append((firstLine + i).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width)).Append("| ").Append(lines[i]);
        }
        return text.ToString();
    }

    /// <summary>Returns the (start, length) of the unique span in <paramref name="content"/> that <paramref name="oldText"/> denotes.</summary>
    public static (int Start, int Length) FindUnique(string content, string oldText)
    {
        var exact = AllExact(content, oldText);
        if (exact.Count == 1) return (exact[0], oldText.Length);
        if (exact.Count > 1)
            throw new InvalidOperationException(
                $"Trecho ambíguo: aparece {exact.Count} vezes (linhas {string.Join(", ", exact.Take(6).Select(i => LineOf(content, i)))}). " +
                "Inclua linhas vizinhas para torná-lo único.");

        // Models sometimes copy the "N| " prefixes of numbered reads; strip them if every line has one.
        var wanted = oldText.Replace("\r\n", "\n").Split('\n').ToList();
        if (wanted.Where(l => l.Trim().Length > 0).All(l => NumberPrefix.IsMatch(l)))
            wanted = wanted.Select(l => NumberPrefix.Replace(l, "", 1)).ToList();
        while (wanted.Count > 0 && wanted[0].Trim().Length == 0) wanted.RemoveAt(0);
        while (wanted.Count > 0 && wanted[^1].Trim().Length == 0) wanted.RemoveAt(wanted.Count - 1);
        if (wanted.Count == 0) throw new InvalidOperationException("Trecho original vazio.");

        var lines = SplitWithOffsets(content);
        var matches = new List<int>();
        for (int i = 0; i + wanted.Count <= lines.Count; i++)
        {
            bool ok = true;
            for (int j = 0; j < wanted.Count && ok; j++)
                ok = string.Equals(lines[i + j].Text.Trim(), wanted[j].Trim(), StringComparison.Ordinal);
            if (ok) matches.Add(i);
        }
        if (matches.Count == 1)
        {
            var first = lines[matches[0]];
            var last = lines[matches[0] + wanted.Count - 1];
            return (first.Start, last.Start + last.Text.Length - first.Start);
        }
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Trecho ambíguo: aparece {matches.Count} vezes (linhas {string.Join(", ", matches.Take(6).Select(m => m + 1))}). " +
                "Inclua linhas vizinhas para torná-lo único.");

        var anchor = wanted.FirstOrDefault(l => l.Trim().Length > 3)?.Trim();
        var near = anchor is null ? [] : lines.Select((l, i) => (l, i))
            .Where(x => x.l.Text.Contains(anchor, StringComparison.Ordinal)).Select(x => x.i + 1).Take(5).ToArray();
        throw new InvalidOperationException("Trecho não encontrado no arquivo atual." +
            (near.Length > 0 ? $" A primeira linha aparece nas linhas {string.Join(", ", near)}; releia esse trecho com lunet.files.excerpt e copie-o exatamente."
                             : " Releia o arquivo (lunet.files.excerpt) e copie o trecho exatamente como está; não regrave o arquivo inteiro."));
    }

    private static List<int> AllExact(string content, string text)
    {
        var found = new List<int>();
        int index = content.IndexOf(text, StringComparison.Ordinal);
        while (index >= 0 && found.Count < 50)
        {
            found.Add(index);
            index = content.IndexOf(text, index + Math.Max(1, text.Length), StringComparison.Ordinal);
        }
        return found;
    }

    private static int LineOf(string content, int offset)
    {
        int line = 1;
        for (int i = 0; i < offset && i < content.Length; i++) if (content[i] == '\n') line++;
        return line;
    }

    private readonly record struct Line(int Start, string Text);

    private static List<Line> SplitWithOffsets(string content)
    {
        var result = new List<Line>();
        int start = 0;
        for (int i = 0; i <= content.Length; i++)
        {
            if (i == content.Length || content[i] == '\n')
            {
                var end = i > start && content[i - 1] == '\r' ? i - 1 : i;
                result.Add(new Line(start, content[start..end]));
                start = i + 1;
            }
        }
        return result;
    }
}
