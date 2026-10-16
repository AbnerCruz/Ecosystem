using System.Globalization;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// UC-56: quick notes (spoken or typed). One note per day in "Notas rápidas/yyyy-MM-dd.md",
/// each entry a line "- HH:mm — texto". Pure: the client writes the result to the vault.
/// </summary>
public static partial class QuickNotes
{
    public const string Folder = "Notas rápidas";

    /// <summary>The day's note for a local time.</summary>
    public static string PathFor(DateTime local) =>
        Folder + "/" + local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md";

    /// <summary>The heading of a new day's note ("# Notas rápidas — 16/10/2026").</summary>
    public static string Header(DateTime local) =>
        "# Notas rápidas — " + local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>The text as one list line (whitespace and line breaks collapsed); null when empty.</summary>
    public static string? Clean(string? text)
    {
        var t = Spaces().Replace(text ?? "", " ").Trim();
        return t.Length == 0 ? null : t;
    }

    /// <summary>The entry for a text at a local time; null when the text is empty.</summary>
    public static string? Line(string? text, DateTime local) =>
        Clean(text) is { } t ? "- " + local.ToString("HH:mm", CultureInfo.InvariantCulture) + " — " + t : null;

    /// <summary>
    /// The day's note with the entry appended: <paramref name="existing"/> is its current content, or null when the
    /// note does not exist yet (it then starts with the heading). Null when the text is empty.
    /// </summary>
    public static (string Path, string Content)? Append(string? existing, string? text, DateTime local)
    {
        if (Line(text, local) is not { } line) return null;
        string content;
        if (string.IsNullOrWhiteSpace(existing)) content = Header(local) + "\n\n" + line + "\n";
        else content = existing + (existing.EndsWith('\n') ? "" : "\n") + line + "\n";
        return (PathFor(local), content);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
