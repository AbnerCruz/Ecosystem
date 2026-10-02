using System.Text.Json;

namespace Hub.Core;

/// <summary>
/// Cache do último estado lido, para funcionar offline. É projeção, não autoridade: a fonte continua sendo <c>ecosystem.json</c>.
/// Tudo que volta do cache sai marcado <see cref="Availability.Stale"/> (NN-021).
/// </summary>
public static class ProductCache
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Serialize(IReadOnlyList<ProductSummary> products) => JsonSerializer.Serialize(products, Options);

    /// <summary>Lê o cache; conteúdo ausente ou inválido vira lista vazia, nunca exceção.</summary>
    public static IReadOnlyList<ProductSummary> Load(string? cached)
    {
        if (string.IsNullOrWhiteSpace(cached)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<ProductSummary>>(cached, Options);
            return list?.Where(p => p is { Id.Length: > 0 }).Select(p => p.AsStale()).ToList() ?? [];
        }
        catch (JsonException) { return []; }
    }
}
