using System.Text.RegularExpressions;

namespace Urbe.Core.Data;

/// <summary>
/// CST-207: a <c>```dados</c> block of a note — <c>fonte: Planilhas/gastos.csv</c> (or a note, <c>fonte: Gastos.md#Março</c>
/// for the table under that heading) and then the steps of <see cref="DataQuery"/>. Read again on every render: the
/// note keeps the source and the steps, never a copy of the result.
/// </summary>
public static partial class DataBlock
{
    public static bool IsLanguage(string? lang) => lang is not null && lang.Trim().ToLowerInvariant() is "dados" or "data" or "tabela-dados";

    /// <summary>The block run against the vault (<paramref name="readText"/> gives a file's or note's text, or null).</summary>
    public static DataResult Run(string block, Func<string, string?> readText)
    {
        var lines = block.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal)).ToList();
        var sourceLine = lines.FirstOrDefault(l => SourceRe().IsMatch(l))
            ?? throw new InvalidDataException("Diga de onde vêm os dados na primeira linha: fonte: Pasta/arquivo.csv");
        var source = SourceRe().Match(sourceLine).Groups[1].Value.Trim().Trim('"');
        string? section = null;
        var hash = source.IndexOf('#');
        if (hash > 0) { section = source[(hash + 1)..].Trim(); source = source[..hash].Trim(); }
        var text = readText(source) ?? readText(source + ".md")
            ?? throw new InvalidDataException("Fonte não encontrada no vault: " + source);
        var table = section is not null || source.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || !Path.HasExtension(source)
            ? DataReaders.MarkdownTable(source, text, section)
            : DataReaders.Read(source, text);
        return DataQuery.Run(table, lines.Where(l => !ReferenceEquals(l, sourceLine)));
    }

    /// <summary>The sources the <c>```dados</c> blocks of a note read (to load them before an export).</summary>
    public static IReadOnlyList<string> Sources(string markdown)
    {
        var result = new List<string>();
        bool inBlock = false;
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal)) { inBlock = !inBlock && IsLanguage(line[3..]); continue; }
            if (inBlock && SourceRe().Match(line) is { Success: true } m)
            {
                var path = m.Groups[1].Value.Trim().Trim('"');
                if (path.IndexOf('#') is > 0 and var hash) path = path[..hash].Trim();
                if (!result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
            }
        }
        return result;
    }

    /// <summary>The <c>```dados</c> blocks of rendered HTML as their result tables (publications, exports).</summary>
    public static string RenderHtml(string html, Func<string, string?> readText) => BlockRe().Replace(html, m =>
    {
        try
        {
            var result = Run(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value), readText);
            var sb = new System.Text.StringBuilder("<table class=\"urbe-data\"><thead><tr>");
            foreach (var c in result.Table.Columns) sb.Append("<th>").Append(MarkdownEngine.EscapeHtml(c)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var row in result.Table.Rows)
            {
                sb.Append("<tr>");
                for (int i = 0; i < row.Count; i++)
                    sb.Append(result.Table.Types[i] == DataType.Number ? "<td style=\"text-align:right\">" : "<td>").Append(MarkdownEngine.EscapeHtml(row[i].Text)).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table><p class=\"urbe-data-source\">Fonte: ").Append(MarkdownEngine.EscapeHtml(result.Table.Source)).Append("</p>");
            return sb.ToString();
        }
        catch (InvalidDataException ex)
        {
            return "<p class=\"missing\" role=\"status\">Dados indisponíveis: " + MarkdownEngine.EscapeHtml(ex.Message) + "</p>";
        }
    });

    [GeneratedRegex(@"^(?:fonte|source|de|from)\s*:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SourceRe();
    [GeneratedRegex("<pre data-lang=\"(?:dados|data|tabela-dados)\"><code>(.*?)</code></pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockRe();
}
