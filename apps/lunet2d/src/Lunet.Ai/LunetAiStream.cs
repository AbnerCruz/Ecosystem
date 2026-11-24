using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lunet.Ai;

/// <summary>Adapts Chat Completions SSE into the existing provider's validated, complete response envelope.</summary>
public sealed class LunetAiStreamHandler(HttpMessageHandler transport, string providerId, Action<string>? text, Action? beginResponse = null)
    : DelegatingHandler(transport)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null && request.Method == HttpMethod.Post)
        {
            var payload = JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
            payload["stream"] = true;
            if (providerId is "openai" or "openrouter")
                payload["stream_options"] = new JsonObject { ["include_usage"] = true };
            request.Content.Dispose();
            request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/event-stream")
            return response;
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            beginResponse?.Invoke();
            var envelope = await LunetAiStream.ReadAsync(stream, text, cancellationToken).ConfigureAwait(false);
            response.Content.Dispose();
            response.Content = new StringContent(envelope, Encoding.UTF8, "application/json");
            return response;
        }
        catch (OperationCanceledException)
        {
            response.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or InvalidOperationException or KeyNotFoundException)
        {
            response.Dispose();
            throw new AgentRuntime.ProviderException("stream-interrupted", "A resposta não terminou de forma válida.", false);
        }
    }
}

public static class LunetAiStream
{
    public const int MaxCharacters = 2_000_000;

    private sealed class ToolParts
    {
        public string? Id;
        public string? Name;
        public StringBuilder Arguments { get; } = new();
    }

    public static async Task<string> ReadAsync(Stream source, Action<string>? observe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, leaveOpen: true);
        var visible = new StringBuilder();
        var data = new StringBuilder();
        var calls = new SortedDictionary<int, ToolParts>();
        var receivedIds = new HashSet<string>(StringComparer.Ordinal);
        string? eventId = null;
        string? finish = null;
        JsonElement? usage = null;
        int consumed = 0;
        bool done = false;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            consumed += line.Length + 1;
            if (consumed > MaxCharacters) throw new InvalidDataException("Resposta em streaming grande demais.");
            if (line.StartsWith("id:", StringComparison.Ordinal))
                eventId = line[3..].TrimStart();
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line.AsSpan(5).TrimStart());
            }
            else if (line.Length == 0)
            {
                if (ConsumeEvent()) break;
            }
        }
        // Accommodate streams ending immediately after the terminal frame.
        if (data.Length > 0 && !done) ConsumeEvent();
        if (!done && finish is null)
            throw new InvalidDataException("A transmissão foi interrompida antes da confirmação final.");
        if (calls.Count > 0 && finish != "tool_calls")
            throw new InvalidDataException("O provedor não confirmou as chamadas de ferramenta.");
        var message = new Dictionary<string, object?> { ["content"] = visible.ToString() };
        if (calls.Count > 0) message["tool_calls"] = calls.Values.Select(call => new
        {
            id = call.Id, type = "function", function = new { name = call.Name, arguments = call.Arguments.ToString() }
        }).ToArray();
        return JsonSerializer.Serialize(new { choices = new[] { new { message, finish_reason = finish } }, usage });

        bool ConsumeEvent()
        {
            var payload = data.ToString();
            data.Clear();
            var id = eventId;
            eventId = null;
            if (payload.Length == 0 || id is { Length: > 0 } && !receivedIds.Add(id)) return false;
            if (payload == "[DONE]") return done = true;

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null)
                throw new InvalidDataException("O provedor interrompeu a transmissão com erro.");
            if (root.TryGetProperty("usage", out var meter) && meter.ValueKind == JsonValueKind.Object)
                usage = meter.Clone();
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("index", out var index) && index.GetInt32() != 0) continue;
                var reason = String(choice, "finish_reason");
                if (reason is not null) finish = reason;
                if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object) continue;
                if (String(delta, "content") is { Length: > 0 } fragment)
                {
                    visible.Append(fragment);
                    observe?.Invoke(fragment);
                }
                if (!delta.TryGetProperty("tool_calls", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    var part = change.GetProperty("index").GetInt32();
                    if (!calls.TryGetValue(part, out var call)) calls[part] = call = new ToolParts();
                    call.Id = KnownValue(call.Id, String(change, "id"));
                    if (!change.TryGetProperty("function", out var fn)) continue;
                    call.Name = KnownValue(call.Name, String(fn, "name"));
                    call.Arguments.Append(String(fn, "arguments"));
                }
            }
            return false;
        }
    }

    private static string? String(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? KnownValue(string? previous, string? incoming)
    {
        if (string.IsNullOrEmpty(incoming)) return previous;
        if (previous is null || previous == incoming) return incoming;
        throw new InvalidDataException("O provedor trocou a identidade de uma chamada de ferramenta durante a transmissão.");
    }
}
