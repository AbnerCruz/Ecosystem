using System.Globalization;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// UC-56: quick notes (spoken or typed). One note per month in "Notas rápidas/yyyy-MM.md" (observation of 22/10/2026:
/// monthly, not one per day), a section per day ("## 22/10 — quinta-feira"), each entry a line "- HH:mm — texto".
/// The day notes written before ("yyyy-MM-dd.md") stay as they are. Pure: the client writes the result to the vault.
/// </summary>
public static partial class QuickNotes
{
    public const string Folder = "Notas rápidas";

    private static readonly string[] Months = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];
    private static readonly string[] Weekdays = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    /// <summary>The month's note for a local time.</summary>
    public static string PathFor(DateTime local) =>
        Folder + "/" + local.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".md";

    /// <summary>The heading of a new month's note ("# Notas rápidas — outubro de 2026").</summary>
    public static string Header(DateTime local) =>
        "# Notas rápidas — " + Months[local.Month - 1] + " de " + local.Year.ToString(CultureInfo.InvariantCulture);

    /// <summary>The heading of a day's section ("## 22/10 — quinta-feira").</summary>
    public static string DayHeading(DateTime local) =>
        "## " + local.ToString("dd/MM", CultureInfo.InvariantCulture) + " — " + Weekdays[(int)local.DayOfWeek];

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
    /// The month's note with the entry in its day's section: <paramref name="existing"/> is the note's current content,
    /// or null when it does not exist yet (it then starts with the heading). A day without a section gets one at the
    /// end; a day whose section is not the last (the person moved things) gets the entry at the end of its section.
    /// Nothing else of the note changes. Null when the text is empty.
    /// </summary>
    public static (string Path, string Content)? Append(string? existing, string? text, DateTime local)
    {
        if (Line(text, local) is not { } line) return null;
        var day = DayHeading(local);
        if (string.IsNullOrWhiteSpace(existing)) return (PathFor(local), Header(local) + "\n\n" + day + "\n\n" + line + "\n");
        var lines = existing.Split('\n');
        int at = Array.FindIndex(lines, l => l.TrimEnd('\r', ' ') == day);
        string content;
        if (at < 0) content = existing.TrimEnd('\n', '\r', ' ') + "\n\n" + day + "\n\n" + line + "\n";
        else
        {
            int next = Array.FindIndex(lines, at + 1, l => l.StartsWith("## ", StringComparison.Ordinal) || l.StartsWith("# ", StringComparison.Ordinal));
            if (next < 0) content = existing + (existing.EndsWith('\n') ? "" : "\n") + line + "\n";
            else
            {
                int end = next;
                while (end > at + 1 && lines[end - 1].Trim().Length == 0) end--;
                content = string.Join("\n", [.. lines[..end], line, "", .. lines[next..]]);
            }
        }
        return (PathFor(local), content);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
