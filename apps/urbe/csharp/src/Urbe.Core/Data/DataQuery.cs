using System.Globalization;
using System.Text.RegularExpressions;
using Urbe.Core.Calculus;

namespace Urbe.Core.Data;

/// <summary>The result of a query: the table, the steps as understood, and notes the person should read.</summary>
public sealed record DataResult(DataTable Table, IReadOnlyList<string> Steps, IReadOnlyList<string> Notes);

/// <summary>
/// CST-207: reproducible transformations of a table, written one step per line (in a <c>```dados</c> block or by the
/// Assistente), applied in order to the source as it is now — the source is never changed:
/// <list type="bullet">
/// <item><c>filtrar valor &gt; 100 e categoria = "Mercado"</c> (= != &gt; &lt; &gt;= &lt;=, contém, começa com; e / ou)</item>
/// <item><c>colunas data, categoria, valor</c></item>
/// <item><c>ordenar por valor desc, data</c></item>
/// <item><c>agrupar por categoria: soma(valor), media(valor), contar</c></item>
/// <item><c>calcular total = preco * qtd</c> (a column computed by the calculator, columns as names)</item>
/// <item><c>limitar 10</c>, <c>distintos categoria</c>, <c>resumo</c></item>
/// </list>
/// A column whose name has spaces is written in quotes: <c>"valor total" &gt; 10</c>.
/// </summary>
public static partial class DataQuery
{
    public static DataResult Run(DataTable table, IEnumerable<string> steps)
    {
        var done = new List<string>();
        var notes = new List<string>();
        foreach (var raw in steps)
        {
            var step = raw.Trim();
            if (step.Length == 0 || step.StartsWith("//", StringComparison.Ordinal)) continue;
            table = Step(table, step, notes);
            done.Add(step);
        }
        return new DataResult(table, done, notes);
    }

    private static DataTable Step(DataTable t, string step, List<string> notes)
    {
        if (Verb(step, "filtrar", "filter", "onde", "where") is { } cond) return Filter(t, cond);
        if (Verb(step, "colunas", "columns", "select", "selecionar") is { } cols) return Select(t, SplitList(cols));
        if (Verb(step, "ordenar por", "ordenar", "sort by", "order by") is { } order) return Sort(t, order);
        if (Verb(step, "agrupar por", "group by") is { } group) return Group(t, group);
        if (Verb(step, "calcular", "compute") is { } calc) return Compute(t, calc, notes);
        if (Verb(step, "limitar", "limit", "primeiros", "top") is { } n)
            return int.TryParse(n.Trim(), out var k) && k >= 0 ? Slice(t, k) : throw new InvalidDataException("“limitar” precisa de um número inteiro.");
        if (Verb(step, "distintos", "distinct", "unicos", "únicos") is { } d) return Distinct(t, SplitList(d));
        if (Verb(step, "resumo", "summary", "describe") is not null || step.Equals("resumo", StringComparison.OrdinalIgnoreCase)) return Summary(t);
        throw new InvalidDataException("Etapa desconhecida: “" + step + "”. Use filtrar, colunas, ordenar por, agrupar por, calcular, limitar, distintos ou resumo.");
    }

    private static string? Verb(string step, params string[] verbs)
    {
        foreach (var v in verbs)
            if (step.Equals(v, StringComparison.OrdinalIgnoreCase)) return "";
            else if (step.StartsWith(v + " ", StringComparison.OrdinalIgnoreCase) || step.StartsWith(v + ":", StringComparison.OrdinalIgnoreCase))
                return step[v.Length..].TrimStart(' ', ':');
        return null;
    }

    private static List<string> SplitList(string text) =>
        Tokens(text).Where(t => t != ",").ToList();

    /// <summary>Words, quoted names and symbols of a step.</summary>
    private static List<string> Tokens(string text)
    {
        var list = new List<string>();
        foreach (Match m in TokenRe().Matches(text))
            list.Add(m.Groups["q"].Success ? m.Groups["q"].Value : m.Value);
        return list;
    }

    private static int Column(DataTable t, string name)
    {
        var i = t.ColumnIndex(name.Trim().Trim('"', '`'));
        if (i < 0) throw new InvalidDataException("Não há coluna “" + name.Trim() + "”. Colunas: " + string.Join(", ", t.Columns) + ".");
        return i;
    }

    private static DataTable Rebuild(DataTable t, IEnumerable<int> rowIndexes) =>
        new(t.Source, t.Columns, t.Types, rowIndexes.Select(i => t.Rows[i]).ToList(), rowIndexes.Select(i => t.SourceRows[i]).ToList());

    private static DataTable Slice(DataTable t, int n) => Rebuild(t, Enumerable.Range(0, Math.Min(n, t.Rows.Count)).ToList());

    // ------------------------------------------------------------------ filter

    private static DataTable Filter(DataTable t, string condition)
    {
        var predicate = ParseCondition(t, condition);
        return Rebuild(t, Enumerable.Range(0, t.Rows.Count).Where(i => predicate(t.Rows[i])).ToList());
    }

    private static Func<IReadOnlyList<DataValue>, bool> ParseCondition(DataTable t, string text)
    {
        var ors = SplitWord(text, "ou", "or");
        var anyOf = ors.Select(part => SplitWord(part, "e", "and").Select(c => Comparison(t, c)).ToList()).ToList();
        return row => anyOf.Any(all => all.All(p => p(row)));
    }

    /// <summary>Splits on a word outside quotes (“e”, “ou”).</summary>
    private static List<string> SplitWord(string text, params string[] words)
    {
        var parts = new List<string>();
        int start = 0;
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '"') { quoted = !quoted; continue; }
            if (quoted || i > 0 && !char.IsWhiteSpace(text[i - 1])) continue;
            foreach (var w in words)
                if (string.Compare(text, i, w, 0, w.Length, StringComparison.OrdinalIgnoreCase) == 0 && i + w.Length < text.Length && char.IsWhiteSpace(text[i + w.Length]))
                {
                    parts.Add(text[start..i]);
                    start = i + w.Length;
                    i = start;
                    break;
                }
        }
        parts.Add(text[start..]);
        return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    private static Func<IReadOnlyList<DataValue>, bool> Comparison(DataTable t, string text)
    {
        var m = ComparisonRe().Match(text.Trim());
        if (!m.Success) throw new InvalidDataException("Não entendi a condição “" + text + "”. Exemplo: valor > 100");
        var col = Column(t, m.Groups["col"].Value);
        var op = m.Groups["op"].Value.ToLowerInvariant();
        var rawValue = m.Groups["val"].Value.Trim();
        var quoted = rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"';
        var value = quoted ? rawValue[1..^1] : rawValue;
        var type = t.Types[col];
        var target = quoted && type == DataType.Text ? new DataValue(value) : DataTable.Value(value, type);
        if (!quoted && type == DataType.Number && target.Number is null)
            throw new InvalidDataException("“" + value + "” não é um número para comparar com " + t.Columns[col] + ".");
        if (!quoted && type == DataType.Date && target.Date is null)
            throw new InvalidDataException("“" + value + "” não é uma data (use 2026-11-05 ou 05/11/2026).");
        return op switch
        {
            "contém" or "contem" or "contains" => row => row[col].Text.Contains(value, StringComparison.OrdinalIgnoreCase),
            "começa com" or "comeca com" or "starts with" => row => row[col].Text.StartsWith(value, StringComparison.OrdinalIgnoreCase),
            "=" or "==" => row => Equal(row[col], target),
            "!=" or "<>" or "≠" => row => !Equal(row[col], target),
            ">" => row => !row[col].IsEmpty && row[col].CompareTo(target) > 0,
            "<" => row => !row[col].IsEmpty && row[col].CompareTo(target) < 0,
            ">=" or "≥" => row => !row[col].IsEmpty && row[col].CompareTo(target) >= 0,
            "<=" or "≤" => row => !row[col].IsEmpty && row[col].CompareTo(target) <= 0,
            _ => throw new InvalidDataException("Operador desconhecido: " + op)
        };
    }

    private static bool Equal(DataValue a, DataValue b) =>
        a.Number is { } x && b.Number is { } y ? Math.Abs(x - y) < 1e-9 :
        a.Date is { } d1 && b.Date is { } d2 ? d1 == d2 :
        a.Boolean is { } p && b.Boolean is { } q ? p == q :
        string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ select, sort, distinct

    private static DataTable Select(DataTable t, List<string> names)
    {
        if (names.Count == 0) throw new InvalidDataException("Diga quais colunas: colunas data, valor");
        var idx = names.Select(n => Column(t, n)).ToList();
        return new DataTable(t.Source, idx.Select(i => t.Columns[i]).ToList(), idx.Select(i => t.Types[i]).ToList(),
            t.Rows.Select(r => (IReadOnlyList<DataValue>)idx.Select(i => r[i]).ToArray()).ToList(), t.SourceRows);
    }

    private static DataTable Sort(DataTable t, string text)
    {
        var keys = text.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).Select(k =>
        {
            var desc = DescRe().Match(k) is { Success: true } dm;
            var name = desc ? DescRe().Replace(k, "").Trim() : AscRe().Replace(k, "").Trim();
            return (Col: Column(t, name), Desc: desc);
        }).ToList();
        if (keys.Count == 0) throw new InvalidDataException("Diga por qual coluna ordenar.");
        var order = Enumerable.Range(0, t.Rows.Count).ToList();
        order.Sort((a, b) =>
        {
            foreach (var (col, desc) in keys)
            {
                var ra = t.Rows[a][col]; var rb = t.Rows[b][col];
                if (ra.IsEmpty || rb.IsEmpty) { if (ra.IsEmpty != rb.IsEmpty) return ra.IsEmpty ? 1 : -1; continue; } // empties last either way
                var c = ra.CompareTo(rb);
                if (c != 0) return desc ? -c : c;
            }
            return a.CompareTo(b); // stable
        });
        return Rebuild(t, order);
    }

    private static DataTable Distinct(DataTable t, List<string> names)
    {
        var idx = names.Count == 0 ? Enumerable.Range(0, t.Columns.Count).ToList() : names.Select(n => Column(t, n)).ToList();
        var seen = new HashSet<string>();
        var keep = new List<int>();
        for (int i = 0; i < t.Rows.Count; i++)
            if (seen.Add(string.Join("\u001f", idx.Select(c => t.Rows[i][c].Text.ToLowerInvariant())))) keep.Add(i);
        var sub = Rebuild(t, keep);
        return names.Count == 0 ? sub : Select(sub, names);
    }

    // ------------------------------------------------------------------ group

    private static DataTable Group(DataTable t, string text)
    {
        var colon = text.IndexOf(':');
        var byText = colon < 0 ? text : text[..colon];
        var aggText = colon < 0 ? "contar" : text[(colon + 1)..];
        var by = SplitList(byText).Select(n => Column(t, n)).ToList();
        var aggs = AggRe().Matches(aggText).Select(m =>
        {
            var fn = MathFunctions.Canonical(m.Groups["fn"].Value.ToLowerInvariant()) switch
            {
                "contar" or "count" or "tamanho" => "contar",
                var f => f
            };
            if (fn is not ("contar" or "soma" or "media" or "min" or "max" or "mediana" or "desvio"))
                throw new InvalidDataException("Agregação desconhecida: " + m.Groups["fn"].Value + ". Use soma, media, contar, min, max, mediana, desvio.");
            int? col = m.Groups["col"].Success && m.Groups["col"].Value.Trim().Length > 0 ? Column(t, m.Groups["col"].Value) : null;
            if (fn != "contar" && col is null) throw new InvalidDataException(fn + " precisa de uma coluna: " + fn + "(valor)");
            if (fn != "contar" && t.Types[col!.Value] != DataType.Number) throw new InvalidDataException(t.Columns[col.Value] + " não é numérica para " + fn + ".");
            var label = m.Groups["as"].Success ? m.Groups["as"].Value.Trim().Trim('"') : col is null ? "contagem" : fn + " de " + t.Columns[col.Value];
            return (Fn: fn, Col: col, Label: label);
        }).ToList();
        if (aggs.Count == 0) throw new InvalidDataException("Diga o que calcular por grupo: agrupar por categoria: soma(valor), contar");
        var groups = Enumerable.Range(0, t.Rows.Count)
            .GroupBy(i => string.Join("\u001f", by.Select(c => t.Rows[i][c].Text)), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var g in groups)
        {
            var first = g.First();
            var cells = by.Select(c => t.Rows[first][c].Text).ToList();
            foreach (var (fn, col, _) in aggs)
            {
                if (fn == "contar") { cells.Add(g.Count().ToString(CultureInfo.InvariantCulture)); continue; }
                var values = g.Select(i => t.Rows[i][col!.Value].Number).Where(v => v is not null).Select(v => v!.Value).ToArray();
                double? r = values.Length == 0 ? null : fn switch
                {
                    "soma" => values.Sum(),
                    "media" => values.Average(),
                    "min" => values.Min(),
                    "max" => values.Max(),
                    "mediana" => Median(values),
                    "desvio" => values.Length < 2 ? null : Math.Sqrt(values.Sum(v => (v - values.Average()) * (v - values.Average())) / (values.Length - 1)),
                    _ => null
                };
                cells.Add(r is null ? "" : MathFormat.Number(r.Value));
            }
            rows.Add(cells);
        }
        return DataTable.FromText(t.Source, by.Select(c => t.Columns[c]).Concat(aggs.Select(a => a.Label)).ToList(), rows);
    }

    private static double Median(double[] v)
    {
        var s = v.Order().ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    // ------------------------------------------------------------------ computed column

    private static DataTable Compute(DataTable t, string text, List<string> notes)
    {
        var eq = text.IndexOf('=');
        if (eq <= 0) throw new InvalidDataException("Use: calcular nome = expressão (ex.: calcular total = preco * qtd)");
        var name = text[..eq].Trim().Trim('"');
        var expression = text[(eq + 1)..].Trim();
        // columns become names the calculator reads: spaces and accents folded (“valor total” → valor_total)
        var aliases = t.Columns.Select((c, i) => (Name: Identifier(c), Index: i)).ToList();
        foreach (Match q in QuotedRe().Matches(expression))
            expression = expression.Replace(q.Value, Identifier(q.Groups[1].Value));
        MathExpr parsed;
        try { parsed = MathParser.Parse(expression); }
        catch (MathError ex) { throw new InvalidDataException("Expressão de “" + name + "”: " + ex.Message); }
        var cells = new List<IReadOnlyList<string>>();
        int errors = 0;
        foreach (var row in t.Rows)
        {
            var vars = new Dictionary<string, MathValue>(StringComparer.Ordinal);
            foreach (var (alias, i) in aliases)
                if (row[i].Number is { } n) vars[alias] = Scalar.Of(n);
            string value;
            try { value = Calculator.Show(MathEvaluator.Evaluate(parsed, vars)); }
            catch (MathError) { value = ""; errors++; }
            cells.Add(row.Select(v => v.Text).Append(value).ToList());
        }
        var result = DataTable.FromText(t.Source, t.Columns.Append(name).ToList(), cells, t.SourceRows);
        if (errors > 0) notes.Add(errors + " linha(s) ficaram sem \u201c" + name + "\u201d: valor vazio ou conta impossível nelas.");
        return result;
    }

    internal static string Identifier(string column) =>
        Regex.Replace(DataTable.Fold(column), @"[^a-z0-9_]+", "_").Trim('_') is { Length: > 0 } id && !char.IsDigit(id[0]) ? id : "c_" + DataTable.Fold(column);

    // ------------------------------------------------------------------ summary

    private static DataTable Summary(DataTable t)
    {
        var rows = new List<IReadOnlyList<string>>();
        for (int c = 0; c < t.Columns.Count; c++)
        {
            var filled = t.Rows.Count(r => !r[c].IsEmpty);
            if (t.Types[c] == DataType.Number)
            {
                var v = t.Rows.Select(r => r[c].Number).Where(x => x is not null).Select(x => x!.Value).ToArray();
                rows.Add([t.Columns[c], "número", filled.ToString(CultureInfo.InvariantCulture), MathFormat.Number(v.Sum()), MathFormat.Number(v.Average()), MathFormat.Number(v.Min()), MathFormat.Number(v.Max())]);
            }
            else
            {
                var distinct = t.Rows.Select(r => r[c].Text).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                rows.Add([t.Columns[c], t.Types[c] switch { DataType.Date => "data", DataType.Boolean => "sim/não", _ => "texto" }, filled.ToString(CultureInfo.InvariantCulture), "", "", "", distinct + " distintos"]);
            }
        }
        return DataTable.FromText(t.Source, ["coluna", "tipo", "preenchidos", "soma", "média", "mínimo", "máximo"], rows);
    }

    [GeneratedRegex(@"""(?<q>[^""]*)""|[^\s,]+|,")]
    private static partial Regex TokenRe();
    [GeneratedRegex(@"^(?<col>""[^""]+""|[^\s<>=!≠≥≤]+)\s*(?<op>>=|<=|!=|<>|==|=|>|<|≠|≥|≤|contém|contem|contains|começa com|comeca com|starts with)\s*(?<val>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ComparisonRe();
    [GeneratedRegex(@"\s+(desc|decrescente|descending|maior primeiro)$", RegexOptions.IgnoreCase)]
    private static partial Regex DescRe();
    [GeneratedRegex(@"\s+(asc|crescente|ascending)$", RegexOptions.IgnoreCase)]
    private static partial Regex AscRe();
    [GeneratedRegex(@"(?<fn>[A-Za-zçãéí]+)\s*(?:\(\s*(?<col>""[^""]+""|[^)]*)\s*\))?(?:\s+(?:como|as)\s+(?<as>""[^""]+""|[^\s,]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex AggRe();
    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex QuotedRe();
}
