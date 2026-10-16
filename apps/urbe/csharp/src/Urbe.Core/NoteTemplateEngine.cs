using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// A note template is an ordinary Markdown note under Modelos/.
/// No new vault format or parallel template registry is introduced.
/// Fields are explicit {{name}} placeholders; all other bytes stay intact.
/// </summary>
public static partial class NoteTemplateEngine
{
    public const string Folder = "Modelos";
    public const int MaxFields = 32;

    public static bool IsTemplatePath(string? path)
    {
        var normalized = DocumentModel.NormalizePath(path);
        return normalized.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)
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
        if (p.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)) p = p[(Folder.Length + 1)..];
        var dot = p.LastIndexOf('.');
        return dot > p.LastIndexOf('/') + 1 ? p[..dot] : p;
    }

    /// <summary>
    /// The content of a note made from a template (UC-44): {{titulo}} (or {{título}}) becomes the new note's name and
    /// {{data}} the day (yyyy-MM-dd); any other placeholder and every other byte stay as written.
    /// </summary>
    public static string Fill(string? markdown, string title, DateTime date) =>
        FieldRegex().Replace(markdown ?? string.Empty, match => match.Groups[1].Value switch
        {
            "titulo" or "título" => title,
            "data" => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            _ => match.Value
        });

    [GeneratedRegex(
        @"\{\{([\p{L}_][\p{L}\p{N}_-]{0,63})\}\}",
        RegexOptions.CultureInvariant)]
    private static partial Regex FieldRegex();
}
