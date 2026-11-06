using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Urbe.Core.Data;

/// <summary>The type a column was read as (from all its values, not from the first row).</summary>
public enum DataType { Text, Number, Date, Boolean }

/// <summary>A value of a cell: the text as written and, for typed columns, the number/date/boolean it means.</summary>
public readonly record struct DataValue(string Text, double? Number = null, DateTime? Date = null, bool? Boolean = null)
{
    public bool IsEmpty => Text.Length == 0;
    public static readonly DataValue Empty = new("");

    /// <summary>How it compares when sorting: numbers and dates by value, text by the pt-BR order.</summary>
    public int CompareTo(DataValue other)
    {
        if (IsEmpty || other.IsEmpty) return IsEmpty == other.IsEmpty ? 0 : IsEmpty ? 1 : -1; // empties last
        if (Number is { } a && other.Number is { } b) return a.CompareTo(b);
        if (Date is { } d1 && other.Date is { } d2) return d1.CompareTo(d2);
        return string.Compare(Text, other.Text, DataTable.PtBr, CompareOptions.IgnoreCase);
    }
}

/// <summary>
/// CST-207: a table of data read from the vault (CSV, JSON, or a Markdown table of a note), typed per column, with
/// where it came from (file and row) kept for each row so a result can always point back to its source.
/// </summary>
public sealed class DataTable
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public string Source { get; }
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<DataType> Types { get; }
    public IReadOnlyList<IReadOnlyList<DataValue>> Rows { get; }
    /// <summary>The 1-based line (CSV), item (JSON) or table row of the source of each row.</summary>
    public IReadOnlyList<int> SourceRows { get; }

    public DataTable(string source, IReadOnlyList<string> columns, IReadOnlyList<DataType> types, IReadOnlyList<IReadOnlyList<DataValue>> rows, IReadOnlyList<int>? sourceRows = null)
    {
        Source = source;
        Columns = columns;
        Types = types;
        Rows = rows;
        SourceRows = sourceRows ?? Enumerable.Range(1, rows.Count).ToArray();
    }

    /// <summary>A table from raw text cells: each column typed by what all its non-empty values are.</summary>
    public static DataTable FromText(string source, IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<string>> cells, IReadOnlyList<int>? sourceRows = null)
    {
        var columns = Unique(header);
        var types = new DataType[columns.Count];
        for (int c = 0; c < columns.Count; c++)
        {
            var values = cells.Select(r => c < r.Count ? r[c].Trim() : "").Where(v => v.Length > 0).ToList();
            types[c] = values.Count == 0 ? DataType.Text
                : values.All(v => ParseNumber(v) is not null) ? DataType.Number
                : values.All(v => ParseDate(v) is not null) ? DataType.Date
                : values.All(v => ParseBool(v) is not null) ? DataType.Boolean
                : DataType.Text;
        }
        var rows = cells.Select(r => (IReadOnlyList<DataValue>)Enumerable.Range(0, columns.Count).Select(c => Value(c < r.Count ? r[c].Trim() : "", types[c])).ToArray()).ToList();
        return new DataTable(source, columns, types, rows, sourceRows);
    }

    public static DataValue Value(string text, DataType type) => text.Length == 0 ? DataValue.Empty : type switch
    {
        DataType.Number => new DataValue(text, ParseNumber(text)),
        DataType.Date => new DataValue(text, Date: ParseDate(text)),
        DataType.Boolean => new DataValue(text, Boolean: ParseBool(text)),
        _ => new DataValue(text)
    };

    /// <summary>1.234,56 (pt-BR), 1,234.56, 1234.56, 12,5, R$ 10, 15% — the forms a spreadsheet exports.</summary>
    public static double? ParseNumber(string raw)
    {
        var s = raw.Trim().Replace("R$", "").Replace("US$", "").Replace("€", "").Replace(" ", "").Replace("\u00a0", "");
        var percent = s.EndsWith('%');
        if (percent) s = s[..^1];
        if (s.Length == 0 || s.Count(ch => ch is '.' or ',') > 6) return null;
        int lastComma = s.LastIndexOf(','), lastDot = s.LastIndexOf('.');
        string normal;
        if (lastComma >= 0 && lastDot >= 0) normal = lastComma > lastDot ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        else if (lastComma >= 0) normal = s.Count(ch => ch == ',') == 1 ? s.Replace(',', '.') : s.Replace(",", "");
        else if (lastDot >= 0 && s.Count(ch => ch == '.') > 1) normal = s.Replace(".", "");
        else normal = s;
        if (!double.TryParse(normal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var v)) return null;
        return percent ? v / 100 : v;
    }

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ssZ", "dd/MM/yyyy HH:mm", "yyyy/MM/dd"];

    public static DateTime? ParseDate(string raw) =>
        DateTime.TryParseExact(raw.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    public static bool? ParseBool(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "sim" or "true" or "verdadeiro" or "x" or "✓" or "yes" => true,
        "não" or "nao" or "false" or "falso" or "no" => false,
        _ => null
    };

    private static List<string> Unique(IReadOnlyList<string> header)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        for (int i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim();
            if (name.Length == 0) name = "coluna" + (i + 1);
            var unique = name;
            for (int n = 2; !seen.Add(unique); n++) unique = name + " " + n;
            result.Add(unique);
        }
        return result;
    }

    /// <summary>The index of a column by name (accents and case ignored), or -1.</summary>
    public int ColumnIndex(string name)
    {
        var wanted = Fold(name);
        for (int i = 0; i < Columns.Count; i++) if (Fold(Columns[i]) == wanted) return i;
        return -1;
    }

    internal static string Fold(string s) =>
        string.Concat(s.Trim().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();

    // ------------------------------------------------------------------ writing

    public string ToMarkdown(int? limit = null)
    {
        var sb = new StringBuilder();
        sb.Append("| ").Append(string.Join(" | ", Columns.Select(Cell))).Append(" |\n|");
        foreach (var t in Types) sb.Append(t == DataType.Number ? " ---: |" : " --- |");
        sb.Append('\n');
        foreach (var row in limit is { } l ? Rows.Take(l) : Rows) sb.Append("| ").Append(string.Join(" | ", row.Select(v => Cell(v.Text)))).Append(" |\n");
        return sb.ToString();
        static string Cell(string t) => t.Replace("|", "\\|").Replace("\n", " ");
    }

    public string ToCsv(char delimiter = ';')
    {
        var sb = new StringBuilder();
        string Quote(string t) => t.IndexOfAny([delimiter, '"', '\n', '\r']) >= 0 ? "\"" + t.Replace("\"", "\"\"") + "\"" : t;
        sb.Append(string.Join(delimiter, Columns.Select(Quote))).Append("\r\n");
        foreach (var row in Rows) sb.Append(string.Join(delimiter, row.Select(v => Quote(v.Text)))).Append("\r\n");
        return sb.ToString();
    }

    public string ToJson()
    {
        var list = Rows.Select(row =>
        {
            var o = new Dictionary<string, object?>();
            for (int c = 0; c < Columns.Count; c++)
                o[Columns[c]] = row[c].IsEmpty ? null : Types[c] switch
                {
                    DataType.Number => row[c].Number,
                    DataType.Boolean => row[c].Boolean,
                    DataType.Date => row[c].Date?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                    _ => row[c].Text
                };
            return o;
        });
        return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
