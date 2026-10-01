using System.Text;
using System.Text.RegularExpressions;

namespace Lunet.Git;

/// <summary>Regras de um arquivo .gitignore (padrões com *, ?, **, [], negação com ! e diretórios com / no fim).</summary>
public sealed class GitIgnore
{
    private sealed record Rule(Regex Pattern, bool Negate, bool DirectoryOnly);

    private readonly List<Rule> _rules = [];

    public static GitIgnore Parse(string text)
    {
        var ignore = new GitIgnore();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            var negate = line[0] == '!';
            if (negate) line = line[1..];
            line = line.TrimEnd(' ');
            if (line.Length == 0) continue;
            var directoryOnly = line.EndsWith('/');
            line = line.TrimEnd('/');
            var anchored = line.Contains('/');
            line = line.TrimStart('/');
            ignore._rules.Add(new Rule(ToRegex(line, anchored), negate, directoryOnly));
        }
        return ignore;
    }

    /// <summary>Verdadeiro/falso se uma regra decide; nulo se nenhuma regra se aplica. O caminho é relativo à pasta do .gitignore, com '/'.</summary>
    public bool? IsIgnored(string relativePath, bool isDirectory)
    {
        bool? result = null;
        foreach (var rule in _rules)
        {
            if (rule.DirectoryOnly && !isDirectory) continue;
            if (rule.Pattern.IsMatch(relativePath)) result = !rule.Negate;
        }
        return result;
    }

    private static Regex ToRegex(string glob, bool anchored)
    {
        var builder = new StringBuilder(anchored ? "^" : "^(?:.*/)?");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            switch (c)
            {
                case '*':
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        i++;
                        if (i + 1 < glob.Length && glob[i + 1] == '/') { i++; builder.Append("(?:.*/)?"); }
                        else builder.Append(".*");
                    }
                    else builder.Append("[^/]*");
                    break;
                case '?': builder.Append("[^/]"); break;
                case '[':
                    var close = glob.IndexOf(']', i + 1);
                    if (close < 0) { builder.Append("\\["); break; }
                    var body = glob[(i + 1)..close];
                    if (body.StartsWith('!')) body = "^" + body[1..];
                    builder.Append('[').Append(body.Replace("\\", "\\\\")).Append(']');
                    i = close;
                    break;
                case '\\' when i + 1 < glob.Length:
                    builder.Append(Regex.Escape(glob[++i].ToString()));
                    break;
                default:
                    builder.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }
        builder.Append("(?:/.*)?$");
        return new Regex(builder.ToString(), RegexOptions.CultureInvariant);
    }
}
