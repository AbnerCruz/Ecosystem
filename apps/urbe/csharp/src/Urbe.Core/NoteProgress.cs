using System.Globalization;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>A disposable projection. The fenced Markdown directive and real tasks own all data.</summary>
public sealed record NoteProgressResult(string Label, decimal Value, decimal Total, string? Error = null)
{
    /// <summary>The unchecked tasks counted, as (line of the note, text); empty for a numeric goal or another note's tasks.</summary>
    public IReadOnlyList<(int Line, string Text)> Pending { get; init; } = [];
    public bool IsValid => Error is null;
    public decimal Remaining => IsValid ? Total - Value : 0;
    public decimal Percent => IsValid && Total > 0 ? Value / Total * 100 : 0;
    public string Summary => Error ?? (Total == 0 ? "Sem tarefas" :
        $"{Value.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))}/{Total.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))} · {Percent.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))}% · faltam {Remaining.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))}");
}

/// <summary>CST-227/228: safe declarative progress, shared by native views and publication outputs.</summary>
public static partial class NoteProgress
{
    public static bool IsLanguage(string? language) => language is "progress" or "progresso";

    public static NoteProgressResult Evaluate(string body, string markdown, Func<string, string?>? resolveNote = null)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        NoteProgressResult Fail(string message) => new(fields.GetValueOrDefault("label", "Progresso"), 0, 0, message);
        foreach (var line in body.Replace("\r", "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) return Fail("Use campo: valor na configuração do progresso.");
            var key = line[..colon].Trim();
            if (key is not ("label" or "value" or "total" or "source" or "note" or "section"))
                return Fail("Campo de progresso desconhecido: " + key);
            if (!fields.TryAdd(key, line[(colon + 1)..].Trim())) return Fail("Campo repetido: " + key);
        }
        var label = fields.GetValueOrDefault("label", "Progresso");
        var linkedNote = false;
        if (fields.TryGetValue("note", out var id))
        {
            if (id.Length == 0 || resolveNote?.Invoke(id) is not { } linked) return Fail("Nota de origem indisponível.");
            markdown = linked;
            linkedNote = true;
        }
        var parts = MarkdownEngine.SplitFrontmatter(markdown);
        if (fields.GetValueOrDefault("source") is { } mode && mode != "tasks") return Fail("Fonte de progresso desconhecida.");
        if (fields.GetValueOrDefault("source") == "tasks")
        {
            if (fields.ContainsKey("value") || fields.ContainsKey("total")) return Fail("O total e o avanço são calculados pelas tarefas.");
            var lines = NoteMarks.StripComments(parts.Body).Replace("\r", "").Split('\n');
            var scoped = new List<string>();
            var section = fields.GetValueOrDefault("section");
            var inScope = section is null;
            int headingLevel = 0, matches = 0;
            char fence = '\0'; int fenceLength = 0;
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart(' ', '\t');
                var run = trimmed.Length == 0 ? 0 : trimmed.TakeWhile(c => c == trimmed[0]).Count();
                if (fence != '\0')
                {
                    if (trimmed.Length > 0 && trimmed[0] == fence && run >= fenceLength && trimmed[run..].Trim().Length == 0)
                        fence = '\0';
                    continue;
                }
                if (trimmed.Length > 0 && trimmed[0] is '`' or '~' && run >= 3)
                { fence = trimmed[0]; fenceLength = run; continue; }
                var heading = Heading().Match(line);
                if (heading.Success && section is not null)
                {
                    var level = heading.Groups[1].Length;
                    if (heading.Groups[2].Value.Trim().TrimEnd('#').Trim() == section)
                    { matches++; inScope = true; headingLevel = level; }
                    else if (inScope && level <= headingLevel) inScope = false;
                    continue;
                }
                if (inScope) scoped.Add(line);
            }
            if (section is not null && matches != 1) return Fail(matches == 0 ? "Seção de origem não encontrada." : "Título de seção ambíguo.");
            decimal count = 0, done = 0;
            var inList = false;
            var pending = new List<string>();
            foreach (var line in scoped)
            {
                var match = TaskLine().Match(line);
                var spaces = line.TakeWhile(c => c is ' ' or '\t').Count();
                // An isolated indented example is code; nested task items belong to an existing list.
                // Every checkbox counts once, a sub-task as much as its parent (no double counting, no hidden weights).
                if (match.Success && (spaces < 4 || inList))
                {
                    count++; inList = true;
                    if (match.Groups[1].Value is "x" or "X") done++; else pending.Add(line);
                }
                else if (!string.IsNullOrWhiteSpace(line) && spaces < 4) inList = ListLine().IsMatch(line);
            }
            return new(label, done, count) { Pending = linkedNote ? [] : Locate(markdown, pending) };
        }
        if (fields.ContainsKey("section")) return Fail("Seleção de seção requer source: tasks.");
        bool Number(string key, out decimal value)
        {
            var raw = fields.GetValueOrDefault(key, "");
            if (raw.StartsWith('$')) raw = parts.Fields.FirstOrDefault(p => p.Key == raw[1..])?.Value ?? "";
            return decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value);
        }
        if (!Number("value", out var current) || !Number("total", out var total)) return Fail("Informe valor e meta numéricos (ou $propriedade).");
        if (current < 0 || total <= 0 || current > total) return Fail("Use meta maior que zero e valor entre zero e a meta.");
        return new(label, current, total);
    }

    // The counted lines are found again in the note as written (comments and seals stripped line by line), in order.
    private static List<(int, string)> Locate(string markdown, List<string> pending)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var found = new List<(int, string)>();
        int at = 0;
        foreach (var task in pending)
            for (; at < lines.Length; at++)
                if (lines[at] == task || NoteMarks.StripComments(lines[at]) == task)
                { found.Add((at, TaskLine().Replace(task, "").Trim())); at++; break; }
        return found;
    }

    /// <summary>
    /// The note with a numeric goal's current value moved by <paramref name="delta"/> (kept between zero and the goal),
    /// written where the value lives: the block's <c>value:</c> line, or the note property it names (<c>value: $lidas</c>).
    /// Null when the block is not a valid numeric goal.
    /// </summary>
    public static string? Step(string note, int blockStart, int blockEnd, decimal delta)
    {
        var block = note[blockStart..blockEnd];
        var lines = block.Split('\n');
        var body = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```") && !l.TrimStart().StartsWith("~~~")));
        var result = Evaluate(body, note);
        if (!result.IsValid || body.Contains("source:", StringComparison.Ordinal)) return null;
        var next = Math.Clamp(result.Value + delta, 0, result.Total);
        if (next == result.Value) return null;
        var number = next.ToString(CultureInfo.InvariantCulture);
        var index = Array.FindIndex(lines, l => l.TrimStart().StartsWith("value:", StringComparison.Ordinal));
        var raw = lines[index][(lines[index].IndexOf(':') + 1)..].Trim();
        if (raw.StartsWith('$'))
            return NoteProperties.Parse(note).Set(raw[1..], PropertyKind.Number, number).Render();
        lines[index] = "value: " + number;
        return note[..blockStart] + string.Join('\n', lines) + note[blockEnd..];
    }

    /// <summary>Each progress block of a note as it reads on screen ("Hoje: 1/3 · 33,3% · faltam 2"), for agents and plugins.</summary>
    public static IEnumerable<string> Summaries(string markdown, Func<string, string?>? resolveNote = null)
    {
        var text = markdown.Replace("\r", "");
        foreach (var block in MarkdownEngine.SplitBlocks(text).Where(b => b.Kind == "code"))
        {
            var lines = text[block.Start..block.End].TrimEnd('\n').Split('\n');
            if (lines.Length < 2 || !IsLanguage(lines[0].TrimStart('`', '~').Trim())) continue;
            var r = Evaluate(string.Join('\n', lines[1..^1]), text, resolveNote);
            yield return r.Label + ": " + r.Summary;
        }
    }

    public static string Manual(string label, decimal value, decimal total) =>
        "```progress\nlabel: " + label.Replace('\r', ' ').Replace('\n', ' ') +
        "\nvalue: " + value.ToString(CultureInfo.InvariantCulture) + "\ntotal: " + total.ToString(CultureInfo.InvariantCulture) + "\n```";

    public static string RenderHtml(string html, string markdown, Func<string, string?>? resolveNote = null) =>
        HtmlBlock().Replace(html, match =>
        {
            var result = Evaluate(System.Net.WebUtility.HtmlDecode(match.Groups[1].Value), markdown, resolveNote);
            var caption = MarkdownEngine.EscapeHtml(result.Label + ": " + result.Summary);
            return "<figure class=\"urbe-progress\"><figcaption>" + caption + "</figcaption>" +
                (result.IsValid && result.Total > 0 ? "<progress max=\"100\" value=\"" + result.Percent.ToString(CultureInfo.InvariantCulture) + "\" aria-label=\"" + caption + "\"></progress>" : "") + "</figure>";
        });

    [GeneratedRegex("<pre data-lang=\"(?:progress|progresso)\"><code>(.*?)</code></pre>", RegexOptions.Singleline)]
    private static partial Regex HtmlBlock();

    [GeneratedRegex(@"^ {0,3}(#{1,6})\s+(.+)$")]
    private static partial Regex Heading();
    [GeneratedRegex(@"^[ \t]*(?:[-+*]|\d+[.)])\s+\[([ xX])\](?:\s+|$)")]
    private static partial Regex TaskLine();
    [GeneratedRegex(@"^[ \t]*(?:[-+*]|\d+[.)])\s+")]
    private static partial Regex ListLine();
}
