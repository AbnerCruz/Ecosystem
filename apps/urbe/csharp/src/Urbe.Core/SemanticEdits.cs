using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-224: edits by the structure of a note — sections, properties, tasks and tables — instead of copying exact
/// text. The same operations serve the Assistente, plugins and the app; all of a call's operations apply or none
/// does, and anything ambiguous or missing is an explicit error, never a guess or a rewrite of the whole note:
/// <list type="bullet">
/// <item><c>replace_section</c> {heading, content} · <c>append_to_section</c> {heading, content} · <c>delete_section</c> {heading}</item>
/// <item><c>insert_section</c> {title, content, level?, after?} (after a section, or at the end) · <c>rename_heading</c> {heading, title}</item>
/// <item><c>set_property</c> {name, value} · <c>remove_property</c> {name}</item>
/// <item><c>set_task</c> {text, done} · <c>add_task</c> {text, heading?}</item>
/// <item><c>table_set_cell</c> {row, column, value, heading?} · <c>table_add_row</c> {values, heading?}</item>
/// </list>
/// A heading is named by its text (accents and case ignored), or with its #'s to say the level.
/// </summary>
public static partial class SemanticEdits
{
    public sealed class EditError(string message) : Exception(message);

    public static string Apply(string markdown, JsonArray ops)
    {
        var text = markdown.Replace("\r\n", "\n");
        int n = 0;
        foreach (var node in ops)
        {
            n++;
            if (node is not JsonObject op) throw new EditError("Operação " + n + " não é um objeto.");
            var kind = (string?)op["op"] ?? throw new EditError("Operação " + n + " sem \"op\".");
            try { text = One(text, kind, op); }
            catch (EditError ex) { throw new EditError("Operação " + n + " (" + kind + "): " + ex.Message + " Nada foi alterado."); }
        }
        return text;
    }

    private static string S(JsonObject op, string key, bool required = true) =>
        op[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : required ? throw new EditError("Falta \"" + key + "\".") : "";

    private static string One(string text, string kind, JsonObject op) => kind switch
    {
        "replace_section" => WithSection(text, S(op, "heading"), (head, _) => head + Block(S(op, "content"))),
        "append_to_section" => WithSection(text, S(op, "heading"), (head, body) => head + body.TrimEnd('\n') + (body.Trim().Length > 0 ? "\n\n" : "\n") + Block(S(op, "content"))),
        "delete_section" => WithSection(text, S(op, "heading"), (_, _) => ""),
        "rename_heading" => WithSection(text, S(op, "heading"), (head, body) => head[..(head.IndexOf(' ') + 1)] + S(op, "title").Trim() + "\n" + body),
        "insert_section" => InsertSection(text, op),
        "set_property" => SetProperty(text, S(op, "name"), op["value"]),
        "remove_property" => RemoveProperty(text, S(op, "name")),
        "set_task" => SetTask(text, S(op, "text"), op["done"] is JsonValue d && d.TryGetValue<bool>(out var done) ? done : throw new EditError("Falta \"done\" (true ou false).")),
        "add_task" => AddTask(text, S(op, "text"), S(op, "heading", required: false)),
        "table_set_cell" => TableSetCell(text, op),
        "table_add_row" => TableAddRow(text, op),
        _ => throw new EditError("Operação desconhecida. Use replace_section, append_to_section, delete_section, insert_section, rename_heading, set_property, remove_property, set_task, add_task, table_set_cell ou table_add_row.")
    };

    private static string Block(string content) => content.Replace("\r\n", "\n").Trim('\n') + "\n\n";

    // ------------------------------------------------------------------ sections

    private sealed record Section(int Start, int BodyStart, int End, int Level, string Title);

    /// <summary>The note's headings outside code, each with where its section ends (next heading of its level or above).</summary>
    private static List<Section> Sections(string text)
    {
        var heads = new List<(int Start, int BodyStart, int Level, string Title)>();
        bool fence = false;
        int at = 0;
        foreach (var line in text.Split('\n'))
        {
            var t = line.TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal)) fence = !fence;
            else if (!fence && HeadingRe().Match(line) is { Success: true } m)
                heads.Add((at, Math.Min(text.Length, at + line.Length + 1), m.Groups[1].Length, m.Groups[2].Value.Trim().TrimEnd('#').Trim()));
            at += line.Length + 1;
        }
        var result = new List<Section>();
        for (int i = 0; i < heads.Count; i++)
        {
            var end = text.Length;
            for (int j = i + 1; j < heads.Count; j++) if (heads[j].Level <= heads[i].Level) { end = heads[j].Start; break; }
            result.Add(new Section(heads[i].Start, heads[i].BodyStart, end, heads[i].Level, heads[i].Title));
        }
        return result;
    }

    private static Section Find(string text, string heading)
    {
        var level = heading.TrimStart().TakeWhile(c => c == '#').Count();
        var wanted = Fold(heading.TrimStart('#', ' '));
        var found = Sections(text).Where(s => Fold(s.Title) == wanted && (level == 0 || s.Level == level)).ToList();
        if (found.Count == 0)
            throw new EditError("Não há seção \"" + heading.Trim() + "\". Seções: " + string.Join(", ", Sections(text).Select(s => new string('#', s.Level) + " " + s.Title).Take(30)) + ".");
        if (found.Count > 1) throw new EditError("Há " + found.Count + " seções \"" + heading.Trim() + "\"; diga o nível (ex.: \"## " + found[0].Title + "\").");
        return found[0];
    }

    private static string WithSection(string text, string heading, Func<string, string, string> rewrite)
    {
        var s = Find(text, heading);
        var head = text[s.Start..s.BodyStart];
        if (!head.EndsWith('\n')) head += "\n";
        var body = text[s.BodyStart..s.End];
        return text[..s.Start] + rewrite(head, body) + text[s.End..];
    }

    private static string InsertSection(string text, JsonObject op)
    {
        var title = S(op, "title").Trim();
        int level = op["level"] is JsonValue lv && lv.TryGetValue<int>(out var l) ? Math.Clamp(l, 1, 6) : 2;
        var section = new string('#', level) + " " + title + "\n\n" + Block(S(op, "content", required: false));
        var after = S(op, "after", required: false);
        if (after.Length == 0) return text.TrimEnd('\n') + "\n\n" + section;
        var s = Find(text, after);
        return text[..s.End].TrimEnd('\n') + "\n\n" + section + text[s.End..];
    }

    // ------------------------------------------------------------------ properties (frontmatter)

    private static string SetProperty(string text, string name, JsonNode? value)
    {
        if (!PropertyNameRe().IsMatch(name)) throw new EditError("Nome de propriedade inválido: " + name);
        var rendered = value switch
        {
            JsonArray a => "[" + string.Join(", ", a.Select(x => Yaml(x?.ToString() ?? ""))) + "]",
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
            JsonValue v when v.TryGetValue<double>(out var d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValue v when v.TryGetValue<string>(out var str) => Yaml(str),
            null => throw new EditError("Falta \"value\"."),
            _ => Yaml(value.ToString())
        };
        var line = name + ": " + rendered;
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) return "---\n" + line + "\n---\n\n" + text.TrimStart('\n');
        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) throw new EditError("O cabeçalho de propriedades (---) da nota não fecha.");
        var lines = text[4..end].Split('\n').ToList();
        var i = lines.FindIndex(x => x.StartsWith(name + ":", StringComparison.Ordinal));
        if (i >= 0)
        {
            // a list written over several lines (- item) goes with its key
            int j = i + 1;
            while (j < lines.Count && lines[j].StartsWith("  ", StringComparison.Ordinal) | lines[j].StartsWith("- ", StringComparison.Ordinal)) j++;
            lines.RemoveRange(i, j - i);
            lines.Insert(i, line);
        }
        else lines.Add(line);
        return "---\n" + string.Join("\n", lines.Where((x, k) => k < lines.Count)) + text[end..];
    }

    private static string RemoveProperty(string text, string name)
    {
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) throw new EditError("A nota não tem propriedades.");
        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        var lines = text[4..end].Split('\n').ToList();
        var i = lines.FindIndex(x => x.StartsWith(name + ":", StringComparison.Ordinal));
        if (i < 0) throw new EditError("Não há a propriedade \"" + name + "\".");
        int j = i + 1;
        while (j < lines.Count && (lines[j].StartsWith("  ", StringComparison.Ordinal) || lines[j].StartsWith("- ", StringComparison.Ordinal))) j++;
        lines.RemoveRange(i, j - i);
        return lines.Count == 0 ? text[(end + 4)..].TrimStart('\n') : "---\n" + string.Join("\n", lines) + text[end..];
    }

    private static string Yaml(string s) => s.Length == 0 || s.IndexOfAny([':', '#', '[', ']', '{', '}', ',', '"', '\'']) >= 0 || s != s.Trim()
        ? "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : s;

    // ------------------------------------------------------------------ tasks

    private static string SetTask(string text, string task, bool done)
    {
        var matches = TaskRe().Matches(text).Where(m => !InCode(text, m.Index) && m.Groups[3].Value.Contains(task.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) throw new EditError("Não há tarefa com \"" + task + "\".");
        if (matches.Count > 1) throw new EditError("\"" + task + "\" aparece em " + matches.Count + " tarefas; use um trecho que só uma tenha.");
        var box = matches[0].Groups[2];
        return text[..box.Index] + (done ? "x" : " ") + text[(box.Index + box.Length)..];
    }

    private static string AddTask(string text, string task, string heading)
    {
        var line = "- [ ] " + task.Trim().Replace("\n", " ");
        if (heading.Length == 0) return text.TrimEnd('\n') + "\n" + line + "\n";
        return WithSection(text, heading, (head, body) =>
        {
            // after the section's last task when it has a list, else at its end
            var lines = body.TrimEnd('\n').Split('\n').ToList();
            var last = lines.FindLastIndex(l => TaskRe().IsMatch(l));
            if (last >= 0) lines.Insert(last + 1, line); else { if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add(""); lines.Add(line); }
            return head + string.Join("\n", lines).TrimStart('\n') + "\n\n";
        });
    }

    private static bool InCode(string text, int index)
    {
        bool fence = false;
        int at = 0;
        foreach (var line in text.Split('\n'))
        {
            if (at > index) break;
            var t = line.TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal)) fence = !fence;
            at += line.Length + 1;
        }
        return fence;
    }

    // ------------------------------------------------------------------ tables

    private sealed record Table(int Start, int End, List<string> Header, List<List<string>> Rows);

    private static Table FindTable(string text, string heading)
    {
        int from = 0, to = text.Length;
        if (heading.Length > 0) { var s = Find(text, heading); from = s.BodyStart; to = s.End; }
        var lines = text[from..to].Split('\n');
        int at = from;
        for (int i = 0; i + 1 < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('|') && DividerRe().IsMatch(lines[i + 1].Trim()))
            {
                var start = at;
                var header = Cells(lines[i]);
                var rows = new List<List<string>>();
                int j = i + 2;
                int end = at + lines[i].Length + 1 + lines[i + 1].Length + 1;
                for (; j < lines.Length && lines[j].TrimStart().StartsWith('|'); j++) { rows.Add(Cells(lines[j])); end += lines[j].Length + 1; }
                return new Table(start, Math.Min(end, text.Length), header, rows);
            }
            at += lines[i].Length + 1;
        }
        throw new EditError(heading.Length > 0 ? "Não há tabela na seção \"" + heading + "\"." : "A nota não tem tabela.");
    }

    private static List<string> Cells(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|')) t = t[..^1];
        return Regex.Split(t, @"(?<!\\)\|").Select(c => c.Trim()).ToList();
    }

    private static string WriteTable(string text, Table t, string divider)
    {
        string Row(IEnumerable<string> cells) => "| " + string.Join(" | ", cells.Select(c => c.Replace("|", "\\|").Replace("\n", " "))) + " |";
        var sb = new StringBuilder();
        sb.Append(Row(t.Header)).Append('\n').Append(divider).Append('\n');
        foreach (var r in t.Rows) sb.Append(Row(r.Concat(Enumerable.Repeat("", Math.Max(0, t.Header.Count - r.Count))).Take(t.Header.Count))).Append('\n');
        return text[..t.Start] + sb + text[t.End..];
    }

    private static string Divider(string text, Table t) => text[t.Start..t.End].Split('\n')[1].Trim();

    private static string TableSetCell(string text, JsonObject op)
    {
        var t = FindTable(text, S(op, "heading", required: false));
        var column = Column(t, S(op, "column"));
        var row = S(op, "row").Trim();
        var hits = t.Rows.Select((r, i) => (r, i)).Where(x => x.r.Count > 0 && string.Equals(x.r[0].Trim(), row, StringComparison.OrdinalIgnoreCase)).ToList();
        if (hits.Count == 0) throw new EditError("Nenhuma linha com \"" + row + "\" na primeira coluna (" + t.Header[0] + ").");
        if (hits.Count > 1) throw new EditError("Há " + hits.Count + " linhas \"" + row + "\" na tabela.");
        var cells = hits[0].r;
        while (cells.Count < t.Header.Count) cells.Add("");
        cells[column] = ValueText(op["value"]);
        return WriteTable(text, t, Divider(text, t));
    }

    private static string TableAddRow(string text, JsonObject op)
    {
        var t = FindTable(text, S(op, "heading", required: false));
        var row = Enumerable.Repeat("", t.Header.Count).ToList();
        switch (op["values"])
        {
            case JsonArray a:
                if (a.Count > t.Header.Count) throw new EditError("A linha tem " + a.Count + " valores e a tabela " + t.Header.Count + " colunas.");
                for (int i = 0; i < a.Count; i++) row[i] = ValueText(a[i]);
                break;
            case JsonObject o:
                foreach (var (k, v) in o) row[Column(t, k)] = ValueText(v);
                break;
            default: throw new EditError("Falta \"values\" (lista na ordem das colunas, ou objeto coluna → valor).");
        }
        t.Rows.Add(row);
        return WriteTable(text, t, Divider(text, t));
    }

    private static int Column(Table t, string name)
    {
        var i = t.Header.FindIndex(h => Fold(h) == Fold(name));
        return i >= 0 ? i : throw new EditError("Não há coluna \"" + name + "\". Colunas: " + string.Join(", ", t.Header) + ".");
    }

    private static string ValueText(JsonNode? v) => v switch
    {
        null => "",
        JsonValue jv when jv.TryGetValue<string>(out var s) => s,
        _ => v.ToJsonString()
    };

    private static string Fold(string s) =>
        string.Concat(s.Trim().Normalize(NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)).ToLowerInvariant();

    [GeneratedRegex(@"^ {0,3}(#{1,6})\s+(.+?)\s*$")]
    private static partial Regex HeadingRe();
    [GeneratedRegex(@"^[A-Za-z_][\w.-]*$")]
    private static partial Regex PropertyNameRe();
    [GeneratedRegex(@"^([ \t]*(?:[-+*]|\d+[.)])\s+\[)([ xX])(\]\s*(.*))$", RegexOptions.Multiline)]
    private static partial Regex TaskRe();
    [GeneratedRegex(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")]
    private static partial Regex DividerRe();
}
