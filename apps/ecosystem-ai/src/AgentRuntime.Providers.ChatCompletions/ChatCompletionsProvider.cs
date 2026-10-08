using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentRuntime;

namespace AgentRuntime.Providers.ChatCompletions;

/// <summary>
/// Adapter concreto para endpoints /chat/completions compatíveis com mensagens e function tools.
/// Não pertence ao AgentRuntime.Core e não interpreta tarefas: apenas traduz contratos do provider.
/// A chave vem do Host em memória; erros não ecoam payloads/respostas HTTP ao log.
/// </summary>
public sealed class ChatCompletionsProvider : IModelProvider
{
    private readonly HttpClient _client;
    private readonly Uri _endpoint;
    private readonly string? _token;
    private readonly decimal _inputUsdPerMillion;
    private readonly decimal _outputUsdPerMillion;

    public string ProviderId => "chat-completions";
    public bool LastCostEstimated { get; private set; }

    /// <summary>Nome de wire compatível com function tools; ID de capability canônico nunca é alterado.</summary>
    public static string WireName(string capabilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        return "c_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(capabilityId)))
            .ToLowerInvariant()[..32];
    }

    public ChatCompletionsProvider(HttpClient client, Uri endpoint, string? token,
        decimal inputUsdPerMillion, decimal outputUsdPerMillion)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.UserInfo.Length > 0 || endpoint.Fragment.Length > 0 ||
            (endpoint.Scheme != Uri.UriSchemeHttps &&
             !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
            throw new ArgumentException("Somente HTTPS ou HTTP loopback explícito.", nameof(endpoint));
        if (inputUsdPerMillion < 0 || outputUsdPerMillion < 0)
            throw new ArgumentOutOfRangeException(nameof(inputUsdPerMillion), "Preços não podem ser negativos.");
        _client = client;
        _endpoint = endpoint;
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
        _inputUsdPerMillion = inputUsdPerMillion;
        _outputUsdPerMillion = outputUsdPerMillion;
    }

    public async ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wireToId = request.Tools.ToDictionary(t => WireName(t.Name), t => t.Name, StringComparer.Ordinal);
        var messages = new List<object> { new { role = "system", content = request.System } };
        foreach (var message in request.Messages)
        {
            if (message.Role == MessageRole.Assistant)
            {
                var text = string.Join("\n", message.Content.OfType<TextBlock>().Select(t => t.Text));
                var calls = message.Content.OfType<ToolUseBlock>()
                    .Select(t => new { id = t.Id, type = "function", function = new { name = WireName(t.Name), arguments = t.Input.GetRawText() } }).ToArray();
                if (calls.Length > 0)
                    messages.Add(new { role = "assistant", content = text.Length == 0 ? null : text, tool_calls = calls });
                else messages.Add(new { role = "assistant", content = text });
            }
            else
            {
                var text = string.Join("\n", message.Content.OfType<TextBlock>().Select(t => t.Text));
                if (text.Length > 0) messages.Add(new { role = "user", content = text });
                foreach (var tool in message.Content.OfType<ToolResultBlock>())
                    messages.Add(new { role = "tool", tool_call_id = tool.ToolUseId, content = tool.Content });
            }
        }
        var exposed = request.Profile.Capabilities.Tools
            ? request.Tools.Select(t => new {
                type = "function", function = new { name = WireName(t.Name), description = t.Description,
                    parameters = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson) }
            }).ToArray()
            : [];
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model"] = request.Profile.Model,
            ["messages"] = messages
        };
        if (exposed.Length > 0) body["tools"] = exposed;

        using var http = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (_token is not null) http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        HttpResponseMessage response;
        try { response = await _client.SendAsync(http, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new ProviderException("timeout", "Timeout do provider; efeito remoto é desconhecido, sem retry automático.", false); }
        catch (HttpRequestException) { throw new ProviderException("transport", "Falha de transporte; efeito remoto é desconhecido, sem retry automático.", false); }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new ProviderException($"http-{(int)response.StatusCode}", "Endpoint recusou a operação; detalhes não são copiados para logs.",
                    response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout);
            if (response.Content.Headers.ContentLength is > 2_000_000)
                throw new ProviderException("response-too-large", "Resposta do provider excedeu o limite.", false);

            string json;
            try { json = await response.Content.ReadAsStringAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { throw new ProviderException("read-response", "Falha ao ler resposta do provider.", true); }
            if (json.Length > 2_000_000) throw new ProviderException("response-too-large", "Resposta do provider excedeu o limite.", false);
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                    throw new JsonException("choices vazio");
                var first = choices[0];
                var payload = first.GetProperty("message");
                var blocks = new List<ContentBlock>();
                if (payload.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(content.GetString())) blocks.Add(new TextBlock(content.GetString()!));
                if (payload.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                {
                    foreach (var call in calls.EnumerateArray())
                    {
                        var function = call.GetProperty("function");
                        var name = function.GetProperty("name").GetString();
                        var id = call.GetProperty("id").GetString();
                        var arguments = function.GetProperty("arguments").GetString();
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) || arguments is null)
                            throw new JsonException("tool call malformada");
                        using var input = JsonDocument.Parse(arguments);
                        if (input.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("arguments deve ser objeto");
                        blocks.Add(new ToolUseBlock(id, wireToId.GetValueOrDefault(name, name), input.RootElement.Clone()));
                    }
                }
                if (blocks.Count == 0) throw new JsonException("resposta sem conteúdo nem chamada");

                long prompt = 0, completion = 0;
                decimal? reportedUsd = null;
                if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                {
                    if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.TryGetInt64(out var p)) prompt = p;
                    if (usage.TryGetProperty("completion_tokens", out var ct) && ct.TryGetInt64(out var c)) completion = c;
                    if (usage.TryGetProperty("cost", out var cost) && cost.ValueKind == JsonValueKind.Number
                        && cost.TryGetDecimal(out var usd)) reportedUsd = usd;
                }
                if (prompt < 0 || completion < 0 || reportedUsd < 0) throw new JsonException("uso ou custo negativo");
                if (reportedUsd is null && (prompt + completion == 0 || (_inputUsdPerMillion == 0 && _outputUsdPerMillion == 0)))
                    throw new ProviderException("cost-unknown", "Sem custo informado nem tarifa configurada; operação não pode ser conciliada com segurança.", false);
                LastCostEstimated = reportedUsd is null;
                var usdAmount = reportedUsd ?? (prompt * _inputUsdPerMillion + completion * _outputUsdPerMillion) / 1_000_000m;
                var cents = checked((long)decimal.Ceiling(usdAmount * 100m));
                var finish = first.TryGetProperty("finish_reason", out var finishProperty) ? finishProperty.GetString() : null;
                if (finish == "length")
                    throw new ProviderException("truncated", "Saída truncada não equivale a tarefa concluída.", false);
                if (finish is "content_filter" or "error")
                    throw new ProviderException("filtered", "Provider não entregou conclusão utilizável.", false);
                if (finish is not (null or "stop" or "tool_calls"))
                    throw new ProviderException("finish-unknown", "Motivo de conclusão não reconhecido.", false);
                var stop = blocks.OfType<ToolUseBlock>().Any() ? StopReason.ToolUse : StopReason.EndTurn;
                return new ModelResponse(blocks, stop, new Usage(prompt, completion, new Money(cents, "USD")));
            }
            catch (ProviderException) { throw; }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or OverflowException or FormatException)
            { throw new ProviderException("bad-response", "Resposta incompatível com o contrato Chat Completions.", false); }
        }
    }
}
