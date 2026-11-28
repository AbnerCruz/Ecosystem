using System.Text.RegularExpressions;

namespace Lunet.Core;

/// <summary>Uma ocorrência da busca no projeto. <see cref="Line"/> e <see cref="Column"/> começam em 1.</summary>
public sealed record SearchMatch(string Path, int Line, int Column, int Length, string Preview);

public sealed record ProjectSearchOptions(bool MatchCase = false, bool WholeWord = false, bool UseRegex = false, int MaxResults = 500);

/// <summary>Busca de texto em todos os arquivos de texto do projeto (ignora binários, arquivos grandes e pastas de cache).</summary>
public static class ProjectSearch
{
    private const long MaxFileBytes = 1_000_000;

    public static IReadOnlyList<SearchMatch> Search(LunetProject project, string query, ProjectSearchOptions? options = null) =>
        SearchCancellable(project, query, options, CancellationToken.None);

    /// <summary>The pattern the options make of the query, or null when a regex is invalid.</summary>
    public static Regex? PatternFor(string query, ProjectSearchOptions options)
    {
        if (string.IsNullOrEmpty(query)) return null;
        var pattern = options.UseRegex ? query : Regex.Escape(query);
        if (options.WholeWord) pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
        var regexOptions = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try { return new Regex(pattern, regexOptions, TimeSpan.FromSeconds(1)); }
        catch (ArgumentException) { return null; }
    }

    /// <summary>
    /// Replaces every match in every text file (VS Code's "Replace All"), through the game's files. Returns the files
    /// changed and how many replacements. <paramref name="only"/> limits it to those files (the ones the person kept).
    /// </summary>
    public static (IReadOnlyList<string> Files, int Count) ReplaceAll(LunetProject project, string query, string replacement,
        ProjectSearchOptions? options = null, IReadOnlyCollection<string>? only = null)
    {
        options ??= new ProjectSearchOptions();
        var regex = PatternFor(query, options);
        var changed = new List<string>();
        var count = 0;
        if (regex is null) return (changed, 0);
        foreach (var path in Search(project, query, options with { MaxResults = int.MaxValue }).Select(m => m.Path).Distinct())
        {
            if (only is not null && !only.Contains(path)) continue;
            var text = project.ReadText(path);
            var n = 0;
            string result;
            try { result = regex.Replace(text, m => { n++; return options.UseRegex ? m.Result(replacement) : replacement; }); }
            catch (RegexMatchTimeoutException) { continue; }
            if (n == 0 || result == text) continue;
            project.WriteText(path, result);
            changed.Add(path);
            count += n;
        }
        return (changed, count);
    }

    /// <summary>The search, stopping (with what it found so far) once <paramref name="cancel"/> is set.</summary>
    public static IReadOnlyList<SearchMatch> SearchCancellable(LunetProject project, string query, ProjectSearchOptions? options, CancellationToken cancel)
    {
        options ??= new ProjectSearchOptions();
        var results = new List<SearchMatch>();
        if (string.IsNullOrEmpty(query)) return results;

        if (PatternFor(query, options) is not { } regex) return results;

        foreach (var path in project.ListFiles())
        {
            if (results.Count >= options.MaxResults || cancel.IsCancellationRequested) break;
            string text;
            try
            {
                if (project.ReadBytes(path).Length > MaxFileBytes) continue;
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
