using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Lunet.Ai;

/// <summary>A model the provider says it serves.</summary>
public sealed record LunetAiModelInfo(string Id, string Name, long? Context, bool? Tools, string? Alias = null);

/// <summary>Result of the "Testar conexão" button: what worked, how long it took and what to fix.</summary>
public sealed record LunetAiConnectionCheck(bool Ok, string Message, long Milliseconds);

/// <summary>
/// Lists the provider's models and checks a key (LUNET-498), like the notes product's settings: the person picks from
/// what the provider actually serves instead of typing an id from memory, and finds out a key is wrong before asking.
/// </summary>
public sealed class LunetAiModelCatalog(HttpClient http)
{
    private readonly ConcurrentDictionary<(string Provider, Uri Endpoint, string Account), CachedModels> _cache = new();
    private sealed record CachedModels(DateTimeOffset CheckedAt, IReadOnlyList<LunetAiModelInfo> Models);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(3);

    /// <summary>The OpenAI-compatible <c>/models</c> address next to a <c>/chat/completions</c> endpoint.</summary>
    public static Uri ModelsUrl(string chatEndpoint)
    {
        var url = LunetAiEndpoint.Validate(chatEndpoint).ToString().TrimEnd('/');
        const string suffix = "/chat/completions";
        var root = url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? url[..^suffix.Length] : url;
        return new Uri(root + "/models");
    }

    public static IReadOnlyList<LunetAiModelInfo> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement list = root.ValueKind == JsonValueKind.Array ? root
            : root.ValueKind != JsonValueKind.Object ? default
            : root.TryGetProperty("data", out var data) ? data
            : root.TryGetProperty("models", out var models) ? models : default;
        if (list.ValueKind != JsonValueKind.Array) return [];
        var result = new List<LunetAiModelInfo>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind is not (JsonValueKind.Object or JsonValueKind.String)) continue;
            string? id = item.ValueKind == JsonValueKind.String ? item.GetString()
                : item.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString()
                : item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (id.StartsWith("models/", StringComparison.Ordinal)) id = id["models/".Length..]; // Gemini
            string name = item.ValueKind == JsonValueKind.Object &&
                          (item.TryGetProperty("displayName", out var nm) || item.TryGetProperty("display_name", out nm) || item.TryGetProperty("name", out nm)) &&
                          nm.ValueKind == JsonValueKind.String ? nm.GetString() ?? id : id;
            long? context = item.ValueKind == JsonValueKind.Object &&
                            (item.TryGetProperty("context_length", out var c) || item.TryGetProperty("context_window", out c) || item.TryGetProperty("inputTokenLimit", out c)) &&
                            c.ValueKind == JsonValueKind.Number && c.TryGetInt64(out var cl) ? cl : null;
            bool? tools = null;
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("supported_parameters", out var sp) && sp.ValueKind == JsonValueKind.Array)
                tools = sp.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.String && p.GetString() is "tools" or "tool_choice");
            var alias = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("canonical_slug", out var slug)
                && slug.ValueKind == JsonValueKind.String ? slug.GetString() : null;
            result.Add(new LunetAiModelInfo(id, name, context, tools, alias == id ? null : alias));
        }
        // Tool-capable first (the agent needs tools), then alphabetical; without data, the provider order stays.
        return result.DistinctBy(m => m.Id).OrderBy(m => m.Tools == false ? 1 : 0).ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Authorize(HttpRequestMessage request, string providerId, string apiKey)
    {
        if (providerId == "anthropic")
        {
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public bool? KnownTools(LunetAiLoadedSettings ai)
    {
        if (!ai.IsConfigured) return null;
        var cacheKey = CacheKey(ai.Settings.ProviderId, ai.Settings.Endpoint, ai.ApiKey!);
        return _cache.TryGetValue(cacheKey, out var cached) && DateTimeOffset.UtcNow - cached.CheckedAt < CacheLifetime
            ? cached.Models.FirstOrDefault(model => model.Id == ai.Settings.Model || model.Alias == ai.Settings.Model)?.Tools
            : null;
    }

    private static (string Provider, Uri Endpoint, string Account) CacheKey(string provider, string endpoint, string apiKey)
    {
        var url = provider == "gemini" ? new Uri("https://generativelanguage.googleapis.com/v1beta/models") : ModelsUrl(endpoint);
        // Memory-only and isolated by provider, endpoint and account.
        return (provider, url, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))));
    }

    public async Task<IReadOnlyList<LunetAiModelInfo>> ListAsync(string providerId, string endpoint, string apiKey,
        CancellationToken cancellationToken, bool refresh = false)
    {
        var key = CacheKey(providerId, endpoint, apiKey);
        var models = key.Endpoint;
        if (!refresh && _cache.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow - cached.CheckedAt < CacheLifetime)
            return cached.Models;

        var found = new Dictionary<string, LunetAiModelInfo>(StringComparer.Ordinal);
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var url = cursor is null ? models : new UriBuilder(models)
            {
                Query = (providerId == "anthropic" ? "after_id=" : "pageToken=") + Uri.EscapeDataString(cursor)
            }.Uri;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (providerId == "gemini") request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            else Authorize(request, providerId, apiKey);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException(Explain((int)response.StatusCode, body));
            try
            {
                using var envelope = JsonDocument.Parse(body);
                var root = envelope.RootElement;
                bool validList = root.ValueKind == JsonValueKind.Array || root.ValueKind == JsonValueKind.Object &&
                    (root.TryGetProperty("data", out var list) || root.TryGetProperty("models", out list)) &&
                    list.ValueKind == JsonValueKind.Array;
                if (!validList) throw new JsonException();
                foreach (var model in Parse(body))
                {
                    if (found.TryGetValue(model.Id, out var existing))
                        found[model.Id] = existing with
                        {
                            Context = existing.Context ?? model.Context, Tools = existing.Tools ?? model.Tools,
                            Alias = existing.Alias ?? model.Alias
                        };
                    else found.Add(model.Id, model);
                }
                cursor = null;
                if (root.ValueKind == JsonValueKind.Object && providerId == "anthropic" &&
                    root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True)
                {
                    if (!root.TryGetProperty("last_id", out var last) || last.ValueKind != JsonValueKind.String ||
                        string.IsNullOrEmpty(last.GetString())) throw new JsonException();
                    cursor = last.GetString();
                }
                else if (root.ValueKind == JsonValueKind.Object && providerId == "gemini" &&
                    root.TryGetProperty("nextPageToken", out var token) && token.ValueKind == JsonValueKind.String)
                    cursor = token.GetString();
                if (!string.IsNullOrEmpty(cursor) && !cursors.Add(cursor)) throw new JsonException();
            }
            catch (JsonException) { throw new HttpRequestException("O provedor não devolveu uma lista completa de modelos."); }
        }
        while (!string.IsNullOrEmpty(cursor));

        var result = Array.AsReadOnly(found.Values.OrderBy(m => m.Tools == false ? 1 : 0)
            .ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase).ToArray());
        foreach (var item in _cache)
            if (DateTimeOffset.UtcNow - item.Value.CheckedAt >= CacheLifetime) _cache.TryRemove(item.Key, out _);
        _cache[key] = new CachedModels(DateTimeOffset.UtcNow, result);
        return result;
    }

    /// <summary>One tiny request to the chat endpoint with the chosen model: proves endpoint, key, model and tools at once.</summary>
    public async Task<LunetAiConnectionCheck> TestAsync(string providerId, string endpoint, string apiKey, string model, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model,
                max_tokens = 16,
                messages = new object[] { new { role = "user", content = "Responda apenas: ok" } },
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, LunetAiEndpoint.Validate(endpoint))
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            watch.Stop();
            return response.IsSuccessStatusCode
                ? new LunetAiConnectionCheck(true, $"Conectado: {model} respondeu em {watch.ElapsedMilliseconds} ms.", watch.ElapsedMilliseconds)
                : new LunetAiConnectionCheck(false, Explain((int)response.StatusCode, body), watch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new LunetAiConnectionCheck(false, "Sem conexão com o provedor (internet ou endereço).", watch.ElapsedMilliseconds);
        }
    }

    /// <summary>Plain-language reason for an HTTP failure, without echoing the key.</summary>
    public static string Explain(int status, string body)
    {
        _ = body;
        return status switch
        {
            401 or 403 => "A chave foi recusada. Confira se colou a chave certa deste provedor.",
            404 => "Modelo ou endereço não encontrado. Escolha um modelo da lista do provedor.",
            402 => "Sem saldo/créditos no provedor.",
            429 => "Limite de uso do provedor atingido. Aguarde ou verifique o plano.",
            >= 500 => "O provedor está com problemas agora. Tente novamente em instantes.",
            _ => $"O provedor respondeu {status}.",
        };
    }
}
