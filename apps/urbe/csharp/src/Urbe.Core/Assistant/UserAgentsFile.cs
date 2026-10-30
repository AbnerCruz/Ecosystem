using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The person's own instructions for the Assistente (owner, UC-50): <c>Personalização/AGENTS.md</c>, in the app's
/// notes (never an ordinary note). Each section is for a model or for all of them:
/// <code>
/// Geral: { escreva em português do Brasil }
/// Claude 5: { seja breve; cite as notas }
/// </code>
/// Markdown headings (<c>## Claude 5</c> followed by text) work the same. "Geral" (or "Todos") is always used; a named
/// section is used when every word of its name is in the model's id (<c>Claude 5</c> → <c>claude-opus-5-5</c>).
/// Read at every conversation; text outside any section counts as Geral.
/// </summary>
public static partial class UserAgentsFile
{
    public const string Path = "Personalização/AGENTS.md";

    public sealed record Section(string Name, string Text);

    public static IReadOnlyList<Section> Parse(string? markdown)
    {
        var text = (markdown ?? "").Replace("\r\n", "\n");
        var result = new List<Section>();
        var loose = new System.Text.StringBuilder();
        int i = 0;
        string? heading = null;
        var headed = new System.Text.StringBuilder();
        void CloseHeading()
        {
            if (heading is not null && headed.ToString().Trim().Length > 0) result.Add(new Section(heading, headed.ToString().Trim()));
            heading = null;
            headed.Clear();
        }
        while (i < text.Length)
        {
            int end = text.IndexOf('\n', i);
            if (end < 0) end = text.Length;
            var line = text[i..end];
            var brace = BraceStart().Match(line);
            if (brace.Success)
            {
                // Nome: { … } — the braces may span lines and nest
                int open = i + brace.Length - 1, depth = 0, j = open;
                for (; j < text.Length; j++)
                {
                    if (text[j] == '{') depth++;
                    else if (text[j] == '}' && --depth == 0) break;
                }
                if (j < text.Length)
                {
                    CloseHeading();
                    result.Add(new Section(brace.Groups[1].Value.Trim(), text[(open + 1)..j].Trim()));
                    int next = text.IndexOf('\n', j);
                    i = next < 0 ? text.Length : next + 1;
                    continue;
                }
            }
            var h = Heading().Match(line);
            if (h.Success) { CloseHeading(); heading = h.Groups[1].Value.Trim(); }
            else if (heading is not null) headed.AppendLine(line);
            else loose.AppendLine(line);
            i = end + 1;
        }
        CloseHeading();
        var rest = loose.ToString().Trim();
        if (rest.Length > 0) result.Insert(0, new Section("Geral", rest));
        return result;
    }

    /// <summary>The instructions that apply to <paramref name="model"/>: the general ones first, then the model's.</summary>
    public static string For(string? markdown, string model)
    {
        var modelWords = Words(model);
        var general = new List<string>();
        var own = new List<(string Name, string Text)>();
        foreach (var s in Parse(markdown))
        {
            var words = Words(s.Name);
            if (words.Count == 1 && words[0] is "geral" or "todos" or "todas" or "general" or "all") general.Add(s.Text);
            else if (words.Count > 0 && words.All(modelWords.Contains)) own.Add((s.Name, s.Text));
        }
        // CST-076: with a part for this model, each part says what it is, and the model's part prevails where they differ
        if (own.Count == 0) return string.Join("\n\n", general);
        var parts = new List<string>();
        if (general.Count > 0) parts.Add("Geral:\n" + string.Join("\n\n", general));
        parts.AddRange(own.Select(o => "Para este modelo (" + o.Name + "), vale sobre a parte geral onde divergirem:\n" + o.Text));
        return string.Join("\n\n", parts);
    }

    private static List<string> Words(string text) =>
        Regex.Split(text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD), @"[^\p{L}\p{N}]+")
            .Select(w => new string(w.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()))
            .Where(w => w.Length > 0).ToList();

    [GeneratedRegex(@"^\s*([^{}:\n#][^{}:\n]{0,60}):\s*\{")]
    private static partial Regex BraceStart();

    [GeneratedRegex(@"^#{1,6}\s+(.+?)\s*#*\s*$")]
    private static partial Regex Heading();
}
