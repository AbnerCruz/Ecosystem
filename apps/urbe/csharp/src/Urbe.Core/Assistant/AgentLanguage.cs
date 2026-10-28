using System.Globalization;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-077: the language the agents answer in. By default the system's (the device's display language); the person may
/// choose another in the Assistente's settings; an explicit request in the conversation still wins, as the rule tells
/// the model. Notes are never translated without being asked.
/// </summary>
public static class AgentLanguage
{
    /// <summary>The choices in the settings: the system's first ("" stored), then common languages.</summary>
    public static IReadOnlyList<(string Code, string Name)> Choices { get; } =
    [
        ("", "Idioma do sistema (" + Name(Effective("", CultureInfo.CurrentUICulture)) + ")"),
        ("pt-BR", "Português (Brasil)"), ("pt-PT", "Português (Portugal)"), ("en-US", "English"), ("es", "Español"),
        ("fr", "Français"), ("de", "Deutsch"), ("it", "Italiano"), ("ja", "日本語"), ("zh-Hans", "中文")
    ];

    /// <summary>The configured language, or the system's when none ("" or "system"); pt-BR when the system has none.</summary>
    public static string Effective(string? configured, CultureInfo? system = null)
    {
        if (configured is { Length: > 0 } c && c != "system") return c;
        var culture = system ?? CultureInfo.CurrentUICulture;
        return string.IsNullOrEmpty(culture.Name) ? "pt-BR" : culture.Name;
    }

    /// <summary>The language's own name ("Português (Brasil)", "English (United States)").</summary>
    public static string Name(string code)
    {
        try
        {
            var native = new CultureInfo(code).NativeName;
            return native.Length > 0 ? char.ToUpper(native[0], CultureInfo.InvariantCulture) + native[1..] : code;
        }
        catch (CultureNotFoundException) { return code; }
    }

    /// <summary>The line of the agents' guidance about the language.</summary>
    public static string Rule(string code) =>
        "- Idioma: responda por padrão em " + Name(code) + " (" + code + "). Se o usuário pedir explicitamente outro idioma, siga o pedido; " +
        "não traduza o conteúdo das notas sem que ele peça.";
}
