using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-175: before a publication leaves the device, what in its text looks private — e-mails, phone numbers, CPFs and
/// keys or passwords — is said, so the person decides with it in sight. A warning, never a block or a change to the text.
/// </summary>
public static partial class PublicationPrivacy
{
    public sealed record Finding(string Kind, string Sample);

    public static IReadOnlyList<Finding> Scan(string text)
    {
        var plain = Tag().Replace(text, " ");
        var found = new List<Finding>();
        void Add(string kind, Regex regex)
        {
            foreach (var value in regex.Matches(plain).Select(m => m.Value.Trim()).Distinct(StringComparer.Ordinal))
                found.Add(new(kind, kind == "chave ou senha" ? value[..Math.Min(value.Length, 12)] + "…" : value));
        }
        Add("e-mail", Email());
        Add("telefone", Phone());
        Add("CPF", Cpf());
        Add("chave ou senha", Secret());
        return found;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex Email();
    [GeneratedRegex(@"(?<!\d)(?:\+?55\s?)?\(?\d{2}\)?\s?9?\d{4}[-\s]?\d{4}(?!\d)")]
    private static partial Regex Phone();
    [GeneratedRegex(@"(?<!\d)\d{3}\.\d{3}\.\d{3}-\d{2}(?!\d)")]
    private static partial Regex Cpf();
    [GeneratedRegex(@"(?i)\bsk-[A-Za-z0-9_-]{12,}\b|\b(?:api[_ -]?key|senha|password|token)\s*[:=]\s*\S+")]
    private static partial Regex Secret();
}
