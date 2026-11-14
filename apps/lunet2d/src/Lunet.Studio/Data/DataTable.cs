using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lunet.Studio.Data;

public enum DataColumnType { Text, Integer, Number, Boolean, Choice, Reference, Json }

/// <summary>A column of a game data table. <see cref="Choices"/> for Choice; <see cref="ReferenceTable"/> for Reference (an id of another table).</summary>
public sealed record DataColumn
{
    public required string Name { get; init; }
    public DataColumnType Type { get; init; } = DataColumnType.Text;
    public bool Required { get; init; }
    /// <summary>Unique, non-empty values (the row's identity, e.g. "id").</summary>
    public bool Key { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public IReadOnlyList<string> Choices { get; init; } = [];
    public string? ReferenceTable { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>The schema saved as <c>&lt;table&gt;.schema.json</c> next to the data.</summary>
public sealed record DataSchema
{
    public int Version { get; init; } = 1;
    public string ClassName { get; init; } = "Registro";
    public List<DataColumn> Columns { get; init; } = [];
}

/// <summary>A problem found by <see cref="DataTable.Validate"/>; <see cref="Row"/> -1 means the whole column.</summary>
public sealed record DataIssue(int Row, string Column, string Message);

/// <summary>
/// Data Studio (LUNET-505): game data as a table — a JSON array of flat objects (enemies, items, levels, dialogue), with
/// a typed schema, cell parsing and validation, row/column editing, CSV and a generated C# class so the game reads it
/// typed with <c>Content.LoadJson&lt;List&lt;T&gt;&gt;</c>. The JSON stays the source of truth (NN-001); the schema only describes it.
/// </summary>
public sealed class DataTable
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions SchemaJson = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public DataSchema Schema { get; private set; }
    public List<JsonObject> Rows { get; } = [];
    public IReadOnlyList<DataColumn> Columns => Schema.Columns;

    private DataTable(DataSchema schema) => Schema = schema;

    public static DataTable Create(string className, params DataColumn[] columns) =>
        new(new DataSchema { ClassName = className, Columns = columns.ToList() });

    /// <summary>Reads the data and, when given, its schema; without a schema the columns are inferred from the values.</summary>
    public static DataTable Parse(string dataJson, string? schemaJson = null, string className = "Registro")
    {
        JsonNode? root;
        try { root = JsonNode.Parse(dataJson, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException ex) { throw new InvalidDataException("JSON inválido: " + ex.Message, ex); }
        var rows = root switch
        {
            JsonArray array => array.Select(n => n as JsonObject ?? throw new InvalidDataException("A tabela precisa ser uma lista de objetos.")).ToList(),
            JsonObject single => [single],
            null => [],
            _ => throw new InvalidDataException("A tabela precisa ser uma lista de objetos."),
        };
        DataSchema schema;
        if (schemaJson is not null)
        {
            try { schema = JsonSerializer.Deserialize<DataSchema>(schemaJson, SchemaJson) ?? throw new InvalidDataException("Esquema vazio."); }
            catch (JsonException ex) { throw new InvalidDataException("Esquema inválido: " + ex.Message, ex); }
            if (schema.Version != 1) throw new InvalidDataException("Versão de esquema não suportada.");
        }
        else schema = Infer(rows, className);
        var table = new DataTable(schema);
        foreach (var row in rows) table.Rows.Add((JsonObject)row.DeepClone());
        // Columns present in the data but missing from the schema are kept as Json so nothing is lost.
        foreach (var name in rows.SelectMany(r => r.Select(p => p.Key)).Distinct(StringComparer.Ordinal))
            if (table.Schema.Columns.All(c => c.Name != name)) table.Schema.Columns.Add(new DataColumn { Name = name, Type = DataColumnType.Json });
        return table;
    }

    private static DataSchema Infer(List<JsonObject> rows, string className)
    {
        var names = rows.SelectMany(r => r.Select(p => p.Key)).Distinct(StringComparer.Ordinal).ToList();
        var columns = new List<DataColumn>();
        foreach (var name in names)
        {
            var values = rows.Select(r => r[name]).Where(v => v is not null).ToList();
            DataColumnType type =
                values.Count == 0 ? DataColumnType.Text
                : values.All(v => v is JsonValue j && j.TryGetValue<bool>(out _)) ? DataColumnType.Boolean
                : values.All(v => v is JsonValue j && j.GetValueKind() == JsonValueKind.Number && j.TryGetValue<long>(out _)) ? DataColumnType.Integer
                : values.All(v => v is JsonValue j && j.GetValueKind() == JsonValueKind.Number) ? DataColumnType.Number
                : values.All(v => v is JsonValue j && j.GetValueKind() == JsonValueKind.String) ? DataColumnType.Text
                : DataColumnType.Json;
            bool key = name.Equals("id", StringComparison.OrdinalIgnoreCase) && values.Count == rows.Count &&
                       values.Select(v => v!.ToJsonString()).Distinct().Count() == values.Count;
            columns.Add(new DataColumn { Name = name, Type = type, Key = key, Required = key });
        }
        return new DataSchema { ClassName = className, Columns = columns };
    }

    public string ToJson() => new JsonArray(Rows.Select(r => (JsonNode)r.DeepClone()).ToArray()).ToJsonString(Pretty);
    public string SchemaToJson() => JsonSerializer.Serialize(Schema, SchemaJson);

    // ---------- Cells ----------

    public string Display(int row, string column)
    {
        var value = Rows[row][column];
        return value switch
        {
            null => "",
            JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
            JsonValue v when v.GetValueKind() is JsonValueKind.True or JsonValueKind.False => v.GetValue<bool>() ? "sim" : "não",
            _ => value.ToJsonString(),
        };
    }

    /// <summary>Parses <paramref name="text"/> by the column type and stores it; returns the problem instead of throwing.</summary>
    public string? Set(int row, string column, string text)
    {
        var col = Column(column);
        text = text.Trim();
        if (text.Length == 0) { Rows[row].Remove(column); return col.Required ? $"{column} é obrigatório." : null; }
        JsonNode? node;
        switch (col.Type)
        {
            case DataColumnType.Integer:
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return $"\"{text}\" não é um número inteiro.";
                node = JsonValue.Create(l);
                break;
            case DataColumnType.Number:
                if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || !double.IsFinite(d)) return $"\"{text}\" não é um número.";
                node = JsonValue.Create(d);
                break;
            case DataColumnType.Boolean:
                var lower = text.ToLowerInvariant();
                if (lower is not ("sim" or "não" or "nao" or "true" or "false" or "1" or "0" or "s" or "n")) return "Use sim ou não.";
                node = JsonValue.Create(lower is "sim" or "true" or "1" or "s");
                break;
            case DataColumnType.Choice:
                if (col.Choices.Count > 0 && !col.Choices.Contains(text, StringComparer.Ordinal)) return $"Escolha uma das opções: {string.Join(", ", col.Choices)}.";
                node = JsonValue.Create(text);
                break;
            case DataColumnType.Json:
                try { node = JsonNode.Parse(text); }
                catch (JsonException) { node = JsonValue.Create(text); }
                break;
            default:
                node = JsonValue.Create(text);
                break;
        }
        Rows[row][column] = node;
        return Check(row, col);
    }

    public DataColumn Column(string name) => Schema.Columns.FirstOrDefault(c => c.Name == name) ?? throw new KeyNotFoundException("Coluna desconhecida: " + name);

    // ---------- Rows ----------

    /// <summary>Adds a row with sensible defaults (next id for integer keys, "novo-N" for text keys, first choice, false, 0).</summary>
    public int AddRow()
    {
        var row = new JsonObject();
        foreach (var c in Schema.Columns)
        {
            if (c.Key)
            {
                if (c.Type == DataColumnType.Integer)
                    row[c.Name] = (Rows.Select(r => r[c.Name] is JsonValue v && v.TryGetValue<long>(out var n) ? n : 0).DefaultIfEmpty(0).Max()) + 1;
                else
                {
                    int i = Rows.Count + 1;
                    while (Rows.Any(r => r[c.Name]?.ToString() == "novo-" + i)) i++;
                    row[c.Name] = "novo-" + i;
                }
                continue;
            }
            if (!c.Required) continue;
            row[c.Name] = c.Type switch
            {
                DataColumnType.Integer => JsonValue.Create((long)(c.Min ?? 0)),
                DataColumnType.Number => JsonValue.Create(c.Min ?? 0),
                DataColumnType.Boolean => JsonValue.Create(false),
                DataColumnType.Choice => JsonValue.Create(c.Choices.FirstOrDefault() ?? ""),
                _ => JsonValue.Create(""),
            };
        }
        Rows.Add(row);
        return Rows.Count - 1;
    }

    public void RemoveRow(int row) => Rows.RemoveAt(row);

    public int DuplicateRow(int row)
    {
        var copy = (JsonObject)Rows[row].DeepClone();
        Rows.Insert(row + 1, copy);
        foreach (var key in Schema.Columns.Where(c => c.Key))
        {
            copy.Remove(key.Name);
            var probe = AddRow();
            copy[key.Name] = Rows[probe][key.Name]?.DeepClone();
            Rows.RemoveAt(probe);
        }
        return row + 1;
    }

    public void MoveRow(int from, int to)
    {
        var row = Rows[from];
        Rows.RemoveAt(from);
        Rows.Insert(Math.Clamp(to, 0, Rows.Count), row);
    }

    public void SortBy(string column, bool descending = false)
    {
        var col = Column(column);
        IComparable Key(JsonObject r) => col.Type is DataColumnType.Integer or DataColumnType.Number
            ? (r[column] is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.MinValue)
            : (IComparable)(r[column]?.ToString() ?? "");
        var sorted = descending ? Rows.OrderByDescending(Key).ToList() : Rows.OrderBy(Key).ToList();
        Rows.Clear();
        Rows.AddRange(sorted);
    }

    // ---------- Columns ----------

    public void AddColumn(DataColumn column)
    {
        if (string.IsNullOrWhiteSpace(column.Name) || Schema.Columns.Any(c => c.Name == column.Name)) throw new ArgumentException("Nome de coluna vazio ou repetido.");
        Schema.Columns.Add(column);
    }

    public void ReplaceColumn(string name, DataColumn column)
    {
        int index = Schema.Columns.FindIndex(c => c.Name == name);
        if (index < 0) throw new KeyNotFoundException(name);
        if (column.Name != name)
        {
            if (Schema.Columns.Any(c => c.Name == column.Name)) throw new ArgumentException("Já existe uma coluna com esse nome.");
            foreach (var row in Rows)
                if (row.Remove(name, out var value)) row[column.Name] = value;
        }
        Schema.Columns[index] = column;
    }

    public void RemoveColumn(string name)
    {
        Schema.Columns.RemoveAll(c => c.Name == name);
        foreach (var row in Rows) row.Remove(name);
    }

    public void SetClassName(string className) => Schema = Schema with { ClassName = className };

    // ---------- Validation ----------

    private string? Check(int row, DataColumn c, IReadOnlyDictionary<string, HashSet<string>>? tables = null)
    {
        var value = Rows[row][c.Name];
        if (value is null) return c.Required || c.Key ? $"{c.Name} é obrigatório." : null;
        switch (c.Type)
        {
            case DataColumnType.Integer when value is not JsonValue iv || iv.GetValueKind() != JsonValueKind.Number || !iv.TryGetValue<long>(out _):
                return $"{c.Name} deve ser inteiro.";
            case DataColumnType.Number when value is not JsonValue nv || nv.GetValueKind() != JsonValueKind.Number:
                return $"{c.Name} deve ser número.";
            case DataColumnType.Boolean when value is not JsonValue bv || bv.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False):
                return $"{c.Name} deve ser sim ou não.";
            case DataColumnType.Choice when c.Choices.Count > 0 && !c.Choices.Contains(value.ToString(), StringComparer.Ordinal):
                return $"{c.Name}: \"{value}\" não está entre as opções.";
            case DataColumnType.Reference when tables is not null && c.ReferenceTable is { } t && tables.TryGetValue(t, out var ids) && !ids.Contains(value.ToString()):
                return $"{c.Name}: \"{value}\" não existe em {t}.";
        }
        if ((c.Min is not null || c.Max is not null) && value is JsonValue n && n.TryGetValue<double>(out var x))
        {
            if (c.Min is { } min && x < min) return $"{c.Name} deve ser pelo menos {min.ToString(CultureInfo.InvariantCulture)}.";
            if (c.Max is { } max && x > max) return $"{c.Name} deve ser no máximo {max.ToString(CultureInfo.InvariantCulture)}.";
        }
        return null;
    }

    /// <summary>Every problem of the table; <paramref name="otherTables"/> maps table names to their key values for references.</summary>
    public IReadOnlyList<DataIssue> Validate(IReadOnlyDictionary<string, HashSet<string>>? otherTables = null)
    {
        var issues = new List<DataIssue>();
        for (int r = 0; r < Rows.Count; r++)
            foreach (var c in Schema.Columns)
                if (Check(r, c, otherTables) is { } problem) issues.Add(new DataIssue(r, c.Name, problem));
        foreach (var key in Schema.Columns.Where(c => c.Key))
            foreach (var group in Rows.Select((row, i) => (Value: row[key.Name]?.ToString(), Row: i)).Where(x => x.Value is not null).GroupBy(x => x.Value).Where(g => g.Count() > 1))
                foreach (var dup in group.Skip(1)) issues.Add(new DataIssue(dup.Row, key.Name, $"{key.Name} \"{group.Key}\" repetido."));
        return issues;
    }

    /// <summary>Key values of this table (for other tables' Reference columns).</summary>
    public HashSet<string> KeyValues()
    {
        var key = Schema.Columns.FirstOrDefault(c => c.Key);
        return key is null ? [] : Rows.Select(r => r[key.Name]?.ToString()).OfType<string>().ToHashSet(StringComparer.Ordinal);
    }

    // ---------- CSV ----------

    public string ToCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Schema.Columns.Select(c => Quote(c.Name))));
        for (int r = 0; r < Rows.Count; r++)
            builder.AppendLine(string.Join(',', Schema.Columns.Select(c => Quote(Display(r, c.Name)))));
        return builder.ToString();
        static string Quote(string s) => s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Replaces the rows with a CSV whose header matches column names (unknown columns become Text). Returns problems found.</summary>
    public IReadOnlyList<DataIssue> ImportCsv(string csv)
    {
        var lines = ParseCsv(csv);
        if (lines.Count == 0) return [];
        var header = lines[0];
        foreach (var name in header.Where(n => n.Length > 0 && Schema.Columns.All(c => c.Name != n)))
            Schema.Columns.Add(new DataColumn { Name = name });
        Rows.Clear();
        var issues = new List<DataIssue>();
        foreach (var line in lines.Skip(1).Where(l => l.Any(v => v.Length > 0)))
        {
            Rows.Add(new JsonObject());
            for (int i = 0; i < header.Count && i < line.Count; i++)
                if (header[i].Length > 0 && Set(Rows.Count - 1, header[i], line[i]) is { } problem)
                    issues.Add(new DataIssue(Rows.Count - 1, header[i], problem));
        }
        return issues;
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char ch = csv[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch is ',' or ';') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n') { row.Add(cell.ToString().TrimEnd('\r')); cell.Clear(); rows.Add(row); row = []; }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().TrimEnd('\r')); rows.Add(row); }
        return rows;
    }

    // ---------- C# ----------

    /// <summary>A C# class matching the columns, for <c>Content.LoadJson&lt;List&lt;T&gt;&gt;("Data/x.json")</c>.</summary>
    public string ToCSharp(string? @namespace = null)
    {
        var className = ProjectPalette.Identifier(Schema.ClassName);
        var builder = new StringBuilder();
        builder.AppendLine("// Gerado pelo Data Studio do Lunet a partir do esquema. Gere de novo quando mudar as colunas.");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Text.Json.Nodes;");
        builder.AppendLine("using System.Text.Json.Serialization;");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(@namespace)) builder.Append("namespace ").Append(@namespace).AppendLine(";").AppendLine();
        builder.Append("public sealed class ").AppendLine(className).AppendLine("{");
        var used = new HashSet<string>(StringComparer.Ordinal) { className };
        foreach (var c in Schema.Columns)
        {
            var name = ProjectPalette.Identifier(c.Name);
            for (int i = 2; !used.Add(name); i++) name = ProjectPalette.Identifier(c.Name) + i;
            var type = c.Type switch
            {
                DataColumnType.Integer => c.Required || c.Key ? "long" : "long?",
                DataColumnType.Number => c.Required ? "double" : "double?",
                DataColumnType.Boolean => c.Required ? "bool" : "bool?",
                DataColumnType.Json => "JsonNode?",
                _ => "string",
            };
            if (c.Description.Length > 0) builder.Append("    /// <summary>").Append(System.Security.SecurityElement.Escape(c.Description)).AppendLine("</summary>");
            builder.Append("    [JsonPropertyName(\"").Append(c.Name.Replace("\"", "\\\"")).AppendLine("\")]");
            builder.Append("    public ").Append(type).Append(' ').Append(name).Append(" { get; set; }")
                .AppendLine(type == "string" ? " = \"\";" : "");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }
}
