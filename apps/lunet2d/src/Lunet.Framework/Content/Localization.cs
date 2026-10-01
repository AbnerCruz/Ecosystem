using System.Globalization;
using System.Text.Json;

namespace Lunet.Content;

/// <summary>
/// Textos traduzidos. Cada idioma é um JSON simples <c>{ "chave": "texto" }</c> em <c>Data/strings.&lt;idioma&gt;.json</c>.
/// Se a chave não existir no idioma atual, usa o idioma reserva; se também não existir, devolve a própria chave.
/// </summary>
/// <example>
/// <code>
/// localization.SetLanguage("pt");
/// string title = localization.Get("menu.title");
/// string score = localization.Get("hud.score", 42);
/// </code>
/// </example>
public sealed class Localization
{
    private readonly IContentSource _source;
    private Dictionary<string, string> _current = new();
    private Dictionary<string, string> _fallback = new();

    /// <summary>Cria o localizador.</summary>
    /// <param name="source">De onde ler os arquivos de idioma.</param>
    public Localization(IContentSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>Pasta e prefixo dos arquivos. Padrão: <c>Data/strings</c> → <c>Data/strings.pt.json</c>.</summary>
    public string BasePath { get; set; } = "Data/strings";

    /// <summary>Idioma atual (ex.: pt).</summary>
    public string Language { get; private set; } = "";
    /// <summary>Idioma reserva, usado quando falta a chave no atual.</summary>
    public string FallbackLanguage { get; private set; } = "";

    /// <summary>Carrega <paramref name="language"/> (ex.: "pt"), com <paramref name="fallbackLanguage"/> como reserva. Idiomas ausentes são ignorados.</summary>
    /// <param name="language">Código do idioma, por exemplo `pt` ou `en`.</param>
    /// <param name="fallbackLanguage">Idioma usado quando falta a tradução (padrão `en`).</param>
    /// <returns>Verdadeiro se o idioma pedido existia.</returns>
    public bool SetLanguage(string language, string fallbackLanguage = "en")
    {
        Language = language;
        FallbackLanguage = fallbackLanguage;
        _fallback = language == fallbackLanguage ? new() : Load(fallbackLanguage) ?? new();
        var loaded = Load(language);
        _current = loaded ?? new();
        return loaded is not null;
    }

    /// <summary>Usa o idioma do aparelho (duas letras) se houver tradução; senão o reserva.</summary>
    /// <param name="fallbackLanguage">Idioma usado quando falta a tradução (padrão `en`).</param>
    /// <returns>O idioma escolhido.</returns>
    public string UseDeviceLanguage(string fallbackLanguage = "en")
    {
        var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (!SetLanguage(device, fallbackLanguage)) SetLanguage(fallbackLanguage, fallbackLanguage);
        return Language;
    }

    /// <summary>Devolve o texto da chave (ou a própria chave se não existir).</summary>
    /// <param name="key">Chave do texto.</param>
    /// <returns>O texto traduzido.</returns>
    public string Get(string key) =>
        _current.TryGetValue(key, out var text) ? text : _fallback.TryGetValue(key, out text) ? text : key;

    /// <summary>Texto com <see cref="string.Format(string, object[])"/>: <c>"Olá, {0}!"</c>.</summary>
    /// <param name="key">Chave do texto ou do dado.</param>
    /// <param name="args">Valores que substituem `{0}`, `{1}`… no texto.</param>
    /// <returns>O texto traduzido, ou a própria chave se não houver tradução.</returns>
    public string Get(string key, params object[] args)
    {
        var format = Get(key);
        try { return string.Format(CultureInfo.CurrentCulture, format, args); }
        catch (FormatException) { return format; }
    }

    /// <summary>Diz se a chave existe no idioma atual ou no reserva.</summary>
    /// <param name="key">Chave do texto.</param>
    /// <returns>Verdadeiro se existe.</returns>
    public bool Contains(string key) => _current.ContainsKey(key) || _fallback.ContainsKey(key);

    private Dictionary<string, string>? Load(string language)
    {
        var path = $"{BasePath}.{language}.json";
        if (!_source.Exists(path)) return null;
        using var stream = _source.Open(path);
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new(); }
        catch (JsonException ex) { throw new InvalidDataException($"{path} inválido: {ex.Message}", ex); }
    }
}
