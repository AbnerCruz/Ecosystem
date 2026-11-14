using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Lunet.Ai;

/// <summary>A model the provider says it serves.</summary>
public sealed record LunetAiModelInfo(string Id, string Name, long? Context, bool? Tools);

/// <summary>Result of the "Testar conexão" button: what worked, how long it took and what to fix.</summary>
public sealed record LunetAiConnectionCheck(bool Ok, string Message, long Milliseconds);

/// <summary>
/// Lists the provider's models and checks a key (LUNET-498), like the notes product's settings: the person picks from
/// what the provider actually serves instead of typing an id from memory, and finds out a key is wrong before asking.
/// </summary>
public sealed class LunetAiModelCatalog(HttpClient http)
{
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
            : root.TryGetProperty("data", out var data) ? data
            : root.TryGetProperty("models", out var models) ? models : default;
        if (list.ValueKind != JsonValueKind.Array) return [];
        var result = new List<LunetAiModelInfo>();
        foreach (var item in list.EnumerateArray())
        {
            string? id = item.ValueKind == JsonValueKind.String ? item.GetString()
                : item.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString()
                : item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (id.StartsWith("models/", StringComparison.Ordinal)) id = id["models/".Length..]; // Gemini
            string name = item.ValueKind == JsonValueKind.Object &&
                          (item.TryGetProperty("name", out var nm) || item.TryGetProperty("display_name", out nm)) &&
                          nm.ValueKind == JsonValueKind.String ? nm.GetString() ?? id : id;
            long? context = item.ValueKind == JsonValueKind.Object &&
                            (item.TryGetProperty("context_length", out var c) || item.TryGetProperty("context_window", out c)) &&
                            c.ValueKind == JsonValueKind.Number && c.TryGetInt64(out var cl) ? cl : null;
            bool? tools = null;
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("supported_parameters", out var sp) && sp.ValueKind == JsonValueKind.Array)
                tools = sp.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.String && p.GetString() is "tools" or "tool_choice");
            result.Add(new LunetAiModelInfo(id, name, context, tools));
        }
        // Tool-capable first (the agent needs tools), then alphabetical; without data, the provider order stays.
        return result.DistinctBy(m => m.Id).OrderBy(m => m.Tools == false ? 1 : 0).ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Authorize(HttpRequestMessage request, string providerId, string apiKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (providerId == "anthropic")
        {
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }
    }

    public async Task<IReadOnlyList<LunetAiModelInfo>> ListAsync(string providerId, string endpoint, string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ModelsUrl(endpoint));
        Authorize(request, providerId, apiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(Explain((int)response.StatusCode, body));
        try { return Parse(body); }
        catch (JsonException) { throw new HttpRequestException("O provedor não devolveu uma lista de modelos válida."); }
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
                max_tokens = 8,
                messages = new object[] { new { role = "user", content = "Responda apenas: ok" } },
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, LunetAiEndpoint.Validate(endpoint))
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            Authorize(request, providerId, apiKey);
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
        var detail = "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e))
                detail = e.ValueKind == JsonValueKind.String ? e.GetString() ?? ""
                    : e.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() ?? "" : "";
        }
        catch (JsonException) { }
        detail = System.Text.RegularExpressions.Regex.Replace(detail, @"(sk|gsk|AIza|key)[-_A-Za-z0-9*.]{6,}", "•••");
        if (detail.Length > 200) detail = detail[..200] + "…";
        var reason = status switch
        {
            401 or 403 => "A chave foi recusada. Confira se colou a chave certa deste provedor.",
            404 => "Modelo ou endereço não encontrado. Escolha um modelo da lista do provedor.",
            402 => "Sem saldo/créditos no provedor.",
            429 => "Limite de uso do provedor atingido. Aguarde ou verifique o plano.",
            >= 500 => "O provedor está com problemas agora. Tente novamente em instantes.",
            _ => $"O provedor respondeu {status}.",
        };
        return detail.Length > 0 ? reason + " (" + detail + ")" : reason;
    }
}
