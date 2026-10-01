using System.Text.RegularExpressions;

namespace Lunet.Editor;

public readonly record struct FindOptions(bool MatchCase = false, bool WholeWord = false, bool UseRegex = false);

public readonly record struct FindMatch(int Start, int Length);

/// <summary>Busca e substituição em texto. Regex tem limite de tempo para não travar o editor.</summary>
public static class FindReplace
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static IReadOnlyList<FindMatch> FindAll(string text, string query, FindOptions options = default)
    {
        if (string.IsNullOrEmpty(query)) return [];
        var regex = Build(query, options);
        try
        {
            return regex.Matches(text).Where(m => m.Length > 0).Select(m => new FindMatch(m.Index, m.Length)).ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    /// <summary>Próxima ocorrência a partir de <paramref name="from"/>, dando a volta ao fim do texto.</summary>
    public static FindMatch? FindNext(string text, string query, int from, FindOptions options = default)
    {
        var all = FindAll(text, query, options);
        if (all.Count == 0) return null;
        foreach (var m in all) if (m.Start >= from) return m;
        return all[0];
    }

    public static string ReplaceAll(string text, string query, string replacement, FindOptions options, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(query)) return text;
        var regex = Build(query, options);
        var n = 0;
        try
        {
            var result = regex.Replace(text, m => { n++; return options.UseRegex ? m.Result(replacement) : replacement; });
            count = n;
            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }

    private static Regex Build(string query, FindOptions options)
    {
        var pattern = options.UseRegex ? query : Regex.Escape(query);
        if (options.WholeWord) pattern = $@"\b(?:{pattern})\b";
        var flags = RegexOptions.CultureInvariant | RegexOptions.Multiline | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new Regex(pattern, flags, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new FormatException("Expressão regular inválida: " + ex.Message, ex);
        }
    }
}
