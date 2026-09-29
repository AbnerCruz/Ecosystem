using System.Text.RegularExpressions;

namespace Lunet.Core;

/// <summary>Uma ocorrência da busca no projeto. <see cref="Line"/> e <see cref="Column"/> começam em 1.</summary>
public sealed record SearchMatch(string Path, int Line, int Column, int Length, string Preview);

public sealed record ProjectSearchOptions(bool MatchCase = false, bool WholeWord = false, bool UseRegex = false, int MaxResults = 500);

/// <summary>Busca de texto em todos os arquivos de texto do projeto (ignora binários, arquivos grandes e pastas de cache).</summary>
public static class ProjectSearch
{
    private const long MaxFileBytes = 1_000_000;

    public static IReadOnlyList<SearchMatch> Search(LunetProject project, string query, ProjectSearchOptions? options = null)
    {
        options ??= new ProjectSearchOptions();
        var results = new List<SearchMatch>();
        if (string.IsNullOrEmpty(query)) return results;

        var pattern = options.UseRegex ? query : Regex.Escape(query);
        if (options.WholeWord) pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
        var regexOptions = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        Regex regex;
        try { regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(1)); }
        catch (ArgumentException) { return results; }

        foreach (var path in project.ListFiles())
        {
            if (results.Count >= options.MaxResults) break;
            string text;
            try
            {
                if (new FileInfo(Path.Combine(project.Directory, path)).Length > MaxFileBytes) continue;
                text = project.ReadText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException) { continue; }
            if (text.Contains('\0')) continue;

            var lineNumber = 0;
            foreach (var line in text.Split('\n'))
            {
                lineNumber++;
                var clean = line.TrimEnd('\r');
                try
                {
                    foreach (Match match in regex.Matches(clean))
                    {
                        if (match.Length == 0) continue;
                        results.Add(new SearchMatch(path, lineNumber, match.Index + 1, match.Length, clean.Trim()));
                        if (results.Count >= options.MaxResults) return results;
                    }
                }
                catch (RegexMatchTimeoutException) { return results; }
            }
        }
        return results;
    }
}
