namespace Lunet.Ai;

/// <summary>Network trust boundary for API-key-backed model calls.</summary>
public static class LunetAiEndpoint
{
    public static Uri Validate(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url)
            || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(url.Host)
            || !string.IsNullOrEmpty(url.UserInfo)
            || !string.IsNullOrEmpty(url.Query)
            || !string.IsNullOrEmpty(url.Fragment)
            || !url.AbsolutePath.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Use endpoint HTTPS completo terminado em /chat/completions, sem credenciais, parâmetros ou fragmento.",
                nameof(endpoint));
        return url;
    }
}
