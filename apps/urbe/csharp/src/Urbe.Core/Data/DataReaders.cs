using System.Text.Json;

namespace Urbe.Core.Data;

/// <summary>Reads tables from what the vault holds: CSV/TSV files, JSON files and the Markdown tables of notes.</summary>
public static class DataReaders
{
    /// <summary>Limits that keep a phone responsive; above them the reader says so instead of freezing.</summary>
    public const int MaxRows = 200_000, MaxColumns = 500;

    public static DataTable Read(string path, string text)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv" or ".tsv" or ".txt" => Csv(path, text),
            ".json" => Json(path, text),
            ".md" or ".markdown" => MarkdownTable(path, text, null),
            _ => throw new InvalidDataException("Formato sem leitor de dados: " + ext + ". Use CSV, JSON ou uma nota com tabela.")
        };
    }

    /// <summary>RFC 4180 CSV with the separator found in the header (; , tab or |), quotes and line breaks inside quotes.</summary>
    public static DataTable Csv(string source, string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var delimiter = Sniff(text);
        var records = new List<List<string>>();
        var lines = new List<int>();
        var field = new System.Text.StringBuilder();
        var record = new List<string>();
        bool quoted = false;
        int line = 1, recordLine = 1;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else { if (c == '\n') line++; field.Append(c); }
                continue;
            }
            if (c == '"' && field.Length == 0) { quoted = true; continue; }
            if (c == delimiter) { record.Add(field.ToString()); field.Clear(); continue; }
            if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                record.Add(field.ToString()); field.Clear();
                if (record.Count > 1 || record[0].Length > 0) { records.Add(record); lines.Add(recordLine); }
                record = [];
                line++;
                recordLine = line;
                if (records.Count > MaxRows + 1) throw new InvalidDataException("O arquivo passa de " + MaxRows.ToString("N0", DataTable.PtBr) + " linhas; divida-o para analisar.");
                continue;
            }
            field.Append(c);
        }
        if (quoted) throw new InvalidDataException("Aspas abertas sem fechar a partir da linha " + recordLine + ".");
        record.Add(field.ToString());
        if (record.Count > 1 || record[0].Length > 0) { records.Add(record); lines.Add(recordLine); }
        if (records.Count == 0) throw new InvalidDataException("O arquivo de dados está vazio.");
        if (records[0].Count > MaxColumns) throw new InvalidDataException("Colunas demais (" + records[0].Count + ").");
        return DataTable.FromText(source, records[0], records.Skip(1).Select(r => (IReadOnlyList<string>)r).ToList(), lines.Skip(1).ToList());
    }

    private static char Sniff(string text)
    {
        var first = text.Split('\n', 2)[0];
        var counts = new[] { ';', ',', '\t', '|' }.Select(d => (d, n: first.Count(ch => ch == d))).OrderByDescending(x => x.n).ToList();
        return counts[0].n > 0 ? counts[0].d : ',';
    }

    /// <summary>A JSON array of objects (or an object with one such array): the keys of all items are the columns.</summary>
    public static DataTable Json(string source, string text)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }).RootElement; }
        catch (JsonException ex) { throw new InvalidDataException("JSON inválido: " + ex.Message); }
        if (root.ValueKind == JsonValueKind.Object)
            root = root.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Array);
        if (root.ValueKind != JsonValueKind.Array) throw new InvalidDataException("O JSON precisa ser uma lista de objetos.");
        var items = root.EnumerateArray().ToList();
        if (items.Count > MaxRows) throw new InvalidDataException("A lista passa de " + MaxRows.ToString("N0", DataTable.PtBr) + " itens.");
        var columns = new List<string>();
        foreach (var item in items.Where(i => i.ValueKind == JsonValueKind.Object))
            foreach (var p in item.EnumerateObject()) if (!columns.Contains(p.Name)) columns.Add(p.Name);
        if (columns.Count == 0) throw new InvalidDataException("A lista não tem objetos com campos.");
        var cells = items.Select(item => (IReadOnlyList<string>)columns.Select(c => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(c, out var v) ? Text(v) : "").ToList()).ToList();
        return DataTable.FromText(source, columns, cells);

        static string Text(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => v.GetRawText()
        };
    }

    /// <summary>
    /// The first Markdown table of a note, or the first under the heading <paramref name="section"/>; its rows keep
    /// the line of the note they are on.
    /// </summary>
    public static DataTable MarkdownTable(string source, string markdown, string? section)
    {
        var lines = markdown.Replace("\r", "").Split('\n');
        bool inSection = section is null, fence = false;
        for (int i = 0; i + 1 < lines.Length; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal)) { fence = !fence; continue; }
            if (fence) continue;
            if (section is not null && t.StartsWith('#'))
            {
                inSection = DataTable.Fold(t.TrimStart('#')) == DataTable.Fold(section);
                continue;
            }
            if (!inSection || !t.StartsWith('|') || !IsDivider(lines[i + 1])) continue;
            var header = Cells(lines[i]);
            var rows = new List<IReadOnlyList<string>>();
            var at = new List<int>();
            for (int j = i + 2; j < lines.Length && lines[j].TrimStart().StartsWith('|'); j++) { rows.Add(Cells(lines[j])); at.Add(j + 1); }
            return DataTable.FromText(source, header, rows, at);
        }
        throw new InvalidDataException(section is null ? "A nota não tem tabela." : "Não há tabela sob o título \"" + section + "\".");
    }

    private static bool IsDivider(string line)
    {
        var t = line.Trim();
        return t.StartsWith('|') && t.Trim('|').Split('|').All(c => c.Trim().Trim(':').Length > 0 && c.Trim().Trim(':').All(ch => ch == '-'));
    }

    private static List<string> Cells(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|') && !t.EndsWith("\\|")) t = t[..^1];
        var cells = new List<string>();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < t.Length; i++)
        {
            if (t[i] == '\\' && i + 1 < t.Length && t[i + 1] == '|') { sb.Append('|'); i++; continue; }
            if (t[i] == '|') { cells.Add(sb.ToString().Trim()); sb.Clear(); continue; }
            sb.Append(t[i]);
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }
}
