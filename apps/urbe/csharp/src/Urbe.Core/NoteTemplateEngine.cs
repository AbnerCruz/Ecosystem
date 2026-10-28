using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// A note template is an ordinary Markdown note under Modelos/, or one named *.modelo.md anywhere (CST-044: the
/// owner's own extension; the file is still Markdown, as .page.json and .grafo.json are still JSON). No parallel
/// template registry is introduced. Fields are explicit {{name}} placeholders; all other bytes stay intact.
/// </summary>
public static partial class NoteTemplateEngine
{
    public const string Folder = "Modelos";
    public const string Suffix = ".modelo.md";
    public const int MaxFields = 32;

    public static bool IsTemplatePath(string? path)
    {
        var normalized = DocumentModel.NormalizePath(path);
        return (normalized.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
            && !ArtifactModel.IsSystem(normalized)
            && ArtifactModel.IsNote(normalized);
    }

    /// <summary>
    /// Returns fields in first-appearance order. Invalid placeholder syntax is
    /// literal Markdown. Repeated names represent one input.
    /// </summary>
    public static IReadOnlyList<string> Fields(string? markdown)
    {
        var fields = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in FieldRegex().Matches(markdown ?? string.Empty))
        {
            var name = match.Groups[1].Value;
            if (seen.Add(name))
                fields.Add(name);
        }

        return fields.AsReadOnly();
    }

    /// <summary>
    /// Rejects missing/extra values and excessively large field sets without
    /// producing partial content. Empty string is an intentional valid value.
    /// MatchEvaluator inserts values literally (including '$' and backslashes).
    /// </summary>
    public static bool TryRender(
        string? markdown,
        IReadOnlyDictionary<string, string?>? values,
        out string rendered)
    {
        rendered = string.Empty;
        if (values is null)
            return false;

        var source = markdown ?? string.Empty;
        var fields = Fields(source);
        if (fields.Count > MaxFields || fields.Count != values.Count)
            return false;

        foreach (var field in fields)
        {
            if (!values.ContainsKey(field))
                return false;
        }

        // Never parse the inserted text again: even {{like_this}} in an input
        // remains a literal part of the newly created document.
        rendered = FieldRegex().Replace(
            source,
            match => values[match.Groups[1].Value] ?? string.Empty);
        return true;
    }

    /// <summary>The templates among the vault's paths (UC-44): notes under Modelos/ at the root, by name.</summary>
    public static IReadOnlyList<string> Templates(IEnumerable<string>? paths) =>
        (paths ?? []).Where(IsTemplatePath).Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ThenBy(p => p, StringComparer.Ordinal).ToArray();

    /// <summary>The name shown for a template: its path under Modelos/ without the extension.</summary>
    public static string NameOf(string? path)
    {
        var p = DocumentModel.NormalizePath(path);
        if (p.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            p = p[..^Suffix.Length];
            return p[(p.LastIndexOf('/') + 1)..];
        }
        if (p.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)) p = p[(Folder.Length + 1)..];
        var dot = p.LastIndexOf('.');
        return dot > p.LastIndexOf('/') + 1 ? p[..dot] : p;
    }

    /// <summary>
    /// The content of a note made from a template (UC-44): {{titulo}} (or {{título}}) becomes the new note's name and
    /// {{data}} the day (yyyy-MM-dd); any other placeholder and every other byte stay as written.
    /// </summary>
    public static string Fill(string? markdown, string title, DateTime date) => Fill(markdown, title, date, null);

    /// <summary>
    /// <see cref="Fill(string?, string, DateTime)"/> plus the values the person typed for the other fields (UC-44); a field
    /// without a value stays as written. Values are inserted literally and never read again as placeholders.
    /// </summary>
    public static string Fill(string? markdown, string title, DateTime date, IReadOnlyDictionary<string, string>? values) =>
        FieldRegex().Replace(WithoutFormMetadata(markdown ?? string.Empty), match => match.Groups[1].Value switch
        {
            var name when markdown?.Contains("<!-- urbe-form ", StringComparison.Ordinal) == true && values is not null && values.TryGetValue(name, out var explicitValue) => explicitValue,
            "titulo" or "título" => title,
            "data" => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            var name when values is not null && values.TryGetValue(name, out var v) => v ?? string.Empty,
            _ => match.Value
        });

    /// <summary>The fields the person is asked for when a note is made from the template: every distinct placeholder but
    /// {{titulo}}/{{título}} and {{data}}, in order of first appearance, at most <see cref="MaxFields"/>.</summary>
    public static IReadOnlyList<string> InputFields(string? markdown) =>
        Fields(WithoutFormMetadata(markdown ?? string.Empty)).Where(f => f is not ("titulo" or "título" or "data")).Take(MaxFields).ToArray();

    /// <summary>
    /// CST-050: the note made when the person leaves the form to finish the sheet in the editor: the answers given are
    /// applied, {{titulo}} and {{data}} as always, and every field not answered is left blank to be written there.
    /// </summary>
    public static string FillPartially(string? markdown, string title, DateTime date, IReadOnlyDictionary<string, string> answered) =>
        FieldRegex().Replace(Fill(markdown, title, date, answered), "");

    /// <summary>
    /// CST-046: text written in [brackets] as if it were a field ("[Nome]"): it is not one. Links, tasks, notes,
    /// footnotes and code are left out. Distinct, in order.
    /// </summary>
    public static IReadOnlyList<string> BracketPlaceholders(string? markdown)
    {
        var found = new List<string>();
        foreach (Match m in BracketMatches(markdown ?? ""))
            if (!found.Contains(m.Groups[1].Value.Trim(), StringComparer.Ordinal)) found.Add(m.Groups[1].Value.Trim());
        return found;
    }

    /// <summary>CST-046: each [Texto] of <see cref="BracketPlaceholders"/> becomes the field {{Texto}} (spaces as _).</summary>
    public static string ConvertBrackets(string markdown)
    {
        var text = new System.Text.StringBuilder(markdown.Length);
        int at = 0;
        foreach (Match m in BracketMatches(markdown))
        {
            text.Append(markdown, at, m.Index - at).Append("{{").Append(FieldName(m.Groups[1].Value)).Append("}}");
            at = m.Index + m.Length;
        }
        return text.Append(markdown, at, markdown.Length - at).ToString();
    }

    /// <summary>How a field is named in the form: its name with _ read as spaces.</summary>
    public static string LabelOf(string field) => field.Replace('_', ' ');

    private static string FieldName(string text)
    {
        var name = Regex.Replace(text.Trim(), @"\s+", "_");
        name = Regex.Replace(name, @"[^\p{L}\p{N}_-]", "");
        if (name.Length == 0 || !char.IsLetter(name[0]) && name[0] != '_') name = "_" + name;
        return name.Length > 64 ? name[..64] : name;
    }

    private static IEnumerable<Match> BracketMatches(string markdown)
    {
        var code = new List<(int Start, int End)>();
        foreach (Match c in CodeRegex().Matches(markdown)) code.Add((c.Index, c.Index + c.Length));
        foreach (Match m in BracketRegex().Matches(markdown))
        {
            var inner = m.Groups[1].Value.Trim();
            if (inner.Length == 0 || inner is "x" or "X" || inner.StartsWith('^')) continue;   // a task, a footnote
            if (code.Any(c => m.Index >= c.Start && m.Index < c.End)) continue;
            yield return m;
        }
    }

    public static string WithoutFormMetadata(string markdown)
    {
        const string marker = "<!-- urbe-form ";
        int start = markdown.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return markdown;
        int end = markdown.IndexOf("-->", start, StringComparison.Ordinal);
        if (end < 0) return markdown;
        end += 3;
        if (end < markdown.Length && markdown[end] == '\r') end++;
        if (end < markdown.Length && markdown[end] == '\n') end++;
        return markdown.Remove(start, end - start);
    }

    // [texto] that is not [[a link]], ![an image], [a](link), [a][ref] or part of a longer bracket run
    [GeneratedRegex(@"(?<![\[!\]])\[([^\[\]\n]{1,60})\](?![\[\(\]:])")]
    private static partial Regex BracketRegex();

    [GeneratedRegex(@"```[\s\S]*?```|`[^`\n]+`")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(
        @"\{\{([\p{L}_][\p{L}\p{N}_-]{0,63})\}\}",
        RegexOptions.CultureInvariant)]
    private static partial Regex FieldRegex();
}
