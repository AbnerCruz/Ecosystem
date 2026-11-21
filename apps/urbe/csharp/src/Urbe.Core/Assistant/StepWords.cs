namespace Urbe.Core.Assistant;

/// <summary>
/// A finished step says what was done, as Claude's steps do ("Leu Fotossíntese"): the label's first word, a gerund
/// ("Lendo") or an infinitive ("Editar"), in the past (3rd person); any other first word stays as it is.
/// </summary>
public static class StepWords
{
    private static readonly Dictionary<string, string> Irregular = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vendo"] = "viu", ["ver"] = "viu", ["fazendo"] = "fez", ["fazer"] = "fez", ["trazendo"] = "trouxe", ["trazer"] = "trouxe",
        ["pondo"] = "pôs", ["pôr"] = "pôs", ["dizendo"] = "disse", ["dizer"] = "disse", ["indo"] = "foi", ["ir"] = "foi"
    };

    public static string Past(string label)
    {
        var space = label.IndexOf(' ');
        var word = space < 0 ? label : label[..space];
        var rest = space < 0 ? "" : label[space..];
        string? past = Irregular.TryGetValue(word, out var known) ? known
            : word.Length >= 5 && word.EndsWith("ando", StringComparison.Ordinal) ? word[..^4] + "ou"
            : word.Length >= 5 && word.EndsWith("endo", StringComparison.Ordinal) ? word[..^4] + "eu"
            : word.Length >= 5 && word.EndsWith("indo", StringComparison.Ordinal) ? word[..^4] + "iu"
            : word.Length > 2 && word.EndsWith("ar", StringComparison.Ordinal) ? word[..^2] + "ou"
            : word.Length > 2 && word.EndsWith("er", StringComparison.Ordinal) ? word[..^2] + "eu"
            : word.Length > 2 && word.EndsWith("ir", StringComparison.Ordinal) ? word[..^2] + "iu"
            : null;
        if (past is null) return label;
        if (char.IsUpper(word[0])) past = char.ToUpper(past[0]) + past[1..];
        return past + rest;
    }
}
