using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>
/// A block of a normalized message (providers.js): text, a tool call (tool_use) or a tool's answer (tool_result).
/// </summary>
public sealed record AiBlock(string Type, string Text = "", string Id = "", string Name = "", JsonNode? Input = null, string ToolUseId = "", string Content = "", bool IsError = false)
{
    public static AiBlock TextBlock(string text) => new("text", Text: text);
    public static AiBlock ToolUse(string id, string name, JsonNode? input) => new("tool_use", Id: id, Name: name, Input: input);
    public static AiBlock ToolResult(string toolUseId, string content, bool isError) => new("tool_result", ToolUseId: toolUseId, Content: content, IsError: isError);
}

/// <summary>{role: 'user' | 'assistant', content: [block]}.</summary>
public sealed record AiMessage(string Role, IReadOnlyList<AiBlock> Content);

/// <summary>A tool as the model sees it: name, description and its JSON Schema.</summary>
public sealed record AiToolSchema(string Name, string Description, JsonObject Parameters);

/// <summary>What the agent asks a provider.</summary>
public sealed record AiRequest(string Model, IReadOnlyList<AiMessage> Messages, string? System = null, IReadOnlyList<AiToolSchema>? Tools = null, int? MaxTokens = null, double? Temperature = null);

/// <summary>Tokens and cost of one answer.</summary>
public sealed record AiUsage(long Input, long Output, double? Cost);

/// <summary>{content, reasoning, stopReason ('tool_use' | 'max_tokens' | 'end'), usage, model}.</summary>
public sealed record AiResult(IReadOnlyList<AiBlock> Content, string Reasoning, string StopReason, AiUsage Usage, string Model);

/// <summary>What streams while the answer comes: 'text', 'reasoning', 'tool_start', 'tool_delta'.</summary>
public sealed record AiStreamEvent(string Type, string? Delta = null, int? Index = null, string? Name = null, string? Partial = null);

/// <summary>A provider as the person configured it (store.js providers[]).</summary>
public sealed record AiProviderConfig(string Id, string Preset, string? Name = null, string? ApiKey = null, string? BaseUrl = null, string? Kind = null, bool? StreamUsage = null);

/// <summary>A model a provider offers.</summary>
public sealed record AiModelInfo(string Id, string Name, long? Context, (double Input, double Output)? Price, bool? Tools);

/// <summary>ProviderError: a message the person can act on, with the HTTP status when there was one.</summary>
public sealed class AiProviderException(string message, int? status = null, bool network = false) : Exception(message)
{
    public int? Status { get; } = status;
    public bool Network { get; } = network;
}

/// <summary>One preset of providers.js PRESETS.</summary>
public sealed record AiPreset(string Kind, string Name, string BaseUrl, bool NeedsKey, bool StreamUsage, string KeyHint, string? Site = null, IReadOnlyList<string>? Models = null, JsonObject? Extra = null);

/// <summary>
/// The provider layer of the 1.8.4's assistant (src/ai/providers.js), ported: the agent knows only the normalized
/// format; each protocol (OpenAI-compatible and Anthropic) turns it into its wire format, streams SSE and returns
/// {content, stopReason, usage, model}. Errors become messages the person can act on and transient ones are retried.
/// The HTTP handler and the waits are injectable (tests). Proved by csharp/tools/assistant-providers-oracle.mjs.
/// </summary>
public sealed class AssistantProviders
{
    public static IReadOnlyDictionary<string, AiPreset> Presets { get; } = new Dictionary<string, AiPreset>(StringComparer.Ordinal)
    {
        ["openrouter"] = new("openai", "OpenRouter", "https://openrouter.ai/api/v1", true, true, "sk-or-…", "https://openrouter.ai/keys", Extra: new JsonObject { ["usage"] = new JsonObject { ["include"] = true } }),
        ["anthropic"] = new("anthropic", "Anthropic", "https://api.anthropic.com/v1", true, false, "sk-ant-…", "https://console.anthropic.com/settings/keys", ["claude-opus-5-5", "claude-sonnet-5", "claude-haiku-4-5-20251001"]),
        ["openai"] = new("openai", "OpenAI", "https://api.openai.com/v1", true, true, "sk-…", "https://platform.openai.com/api-keys"),
        ["gemini"] = new("openai", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", true, false, "AIza…", "https://aistudio.google.com/apikey"),
        ["ollama"] = new("openai", "Ollama (local)", "http://localhost:11434/v1", false, false, "não precisa"),
        ["custom"] = new("openai", "Compatível com OpenAI", "", false, false, "se o servidor exigir")
    };

    private static readonly int[] Retry = [408, 409, 425, 429, 500, 502, 503, 504, 529];

    private readonly HttpClient _http;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public AssistantProviders(HttpMessageHandler? handler = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _delay = delay ?? ((t, c) => Task.Delay(t, c));
    }

    /// <summary>resolve(cfg): the preset's defaults under what the person set.</summary>
    public sealed record Resolved(string Id, string Preset, string Kind, string Name, string BaseUrl, string ApiKey, bool StreamUsage, JsonObject? Extra);

    public static Resolved Resolve(AiProviderConfig cfg)
    {
        var pre = Presets.TryGetValue(cfg.Preset ?? "", out var p) ? p : Presets["custom"];
        return new Resolved(cfg.Id, cfg.Preset ?? "", string.IsNullOrEmpty(cfg.Kind) ? pre.Kind : cfg.Kind!, string.IsNullOrEmpty(cfg.Name) ? pre.Name : cfg.Name!,
            Trim(string.IsNullOrEmpty(cfg.BaseUrl) ? pre.BaseUrl : cfg.BaseUrl!), Trim(cfg.ApiKey ?? ""), cfg.StreamUsage ?? pre.StreamUsage, pre.Extra);
    }

    private static string Trim(string s) => LegacyCustomize.JsTrim(s);

    // ---------------------------------------------------------------- errors

    /// <summary>friendly(status, body, provider): what to tell the person.</summary>
    public static string Friendly(int status, JsonNode? body, string providerName)
    {
        JsonNode? rawNode = null;
        if (body is JsonObject o)
        {
            if (o["error"] is JsonObject e) rawNode = Truthy(e["message"]) ? e["message"] : e;
            else if (Truthy(o["error"])) rawNode = o["error"];
            if (!Truthy(rawNode)) rawNode = o["message"];
        }
        var raw = rawNode is null || !Truthy(rawNode) ? "" : rawNode is JsonValue v && v.TryGetValue<string>(out var s) ? s : LegacyCustomize.Stringify(rawNode);
        var low = raw.ToLowerInvariant();
        var name = string.IsNullOrEmpty(providerName) ? "provedor" : providerName;
        if (status is 401 or 403) return "A chave de " + name + " foi recusada. Confira a chave nas configurações.";
        if (status == 402) return "Sem créditos em " + name + ". Adicione créditos ou troque de provedor.";
        if (status == 404 && low.Contains("model", StringComparison.Ordinal)) return "Modelo não encontrado em " + name + ": " + raw;
        if (low.Contains("tool", StringComparison.Ordinal) && (low.Contains("support", StringComparison.Ordinal) || low.Contains("endpoint", StringComparison.Ordinal)))
            return "Este modelo não aceita ferramentas. Escolha outro modelo (ex.: Claude, GPT, Gemini, Qwen).";
        if (status == 413 || Regex.IsMatch(low, "context|too long|maximum.*tokens")) return "A conversa ficou grande demais para este modelo. Comece uma nova conversa ou use um modelo com mais contexto.";
        if (status == 429) return name + " limitou as requisições. Tentando de novo em instantes…";
        if (status >= 500) return name + " está instável (" + status + "). " + raw;
        return raw.Length > 0 ? raw : "Erro " + status + " em " + name;
    }

    private static bool Truthy(JsonNode? n) => n switch
    {
        null => false,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => LegacyCustomize.JsToNumber(v) != 0,
        _ => true
    };

    // ---------------------------------------------------------------- HTTP

    private async Task<HttpResponseMessage> RequestAsync(Resolved provider, string url, Func<HttpRequestMessage> make, CancellationToken cancel)
    {
        AiProviderException? last = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            cancel.ThrowIfCancellationRequested();
            HttpResponseMessage res;
            try { res = await _http.SendAsync(make(), HttpCompletionOption.ResponseHeadersRead, cancel); }
            catch (HttpRequestException)
            {
                last = new AiProviderException("Não foi possível conectar a " + provider.Name + ". Verifique a internet" +
                    (provider.Kind == "openai" && Regex.IsMatch(url, @"localhost|127\.0\.0\.1") ? " e se o servidor local está aberto (e aceita CORS)" : "") + ".", network: true);
                if (attempt < 2) { await _delay(TimeSpan.FromMilliseconds(800 * (attempt + 1)), cancel); continue; }
                throw last;
            }
            if (res.IsSuccessStatusCode) return res;
            var txt = "";
            try { txt = await res.Content.ReadAsStringAsync(cancel); } catch (HttpRequestException) { }
            JsonNode? body;
            try { body = JsonNode.Parse(txt); }
            catch (JsonException) { body = new JsonObject { ["message"] = txt.Length > 300 ? txt[..300] : txt }; }
            int status = (int)res.StatusCode;
            last = new AiProviderException(Friendly(status, body, provider.Name), status);
            if (Array.IndexOf(Retry, status) >= 0 && attempt < 3)
            {
                double ra = res.Headers.TryGetValues("retry-after", out var values) && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
                await _delay(TimeSpan.FromMilliseconds(ra > 0 ? Math.Min(ra * 1000, 20000) : 1000 * Math.Pow(2, attempt)), cancel);
                continue;
            }
            throw last;
        }
        throw last!;
    }

    /// <summary>readSSE: tolerant of broken lines, \r\n and comments; a JSON answer (not a stream) is one 'json' event.</summary>
    private static async Task ReadSseAsync(HttpResponseMessage res, Action<string, string> onEvent, CancellationToken cancel)
    {
        var ct = res.Content.Headers.ContentType?.ToString() ?? "";
        if (!ct.Contains("text/event-stream", StringComparison.Ordinal) && ct.Contains("application/json", StringComparison.Ordinal))
        {
            onEvent("json", await res.Content.ReadAsStringAsync(cancel));
            return;
        }
        await using var stream = await res.Content.ReadAsStreamAsync(cancel);
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[4096];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var buf = new StringBuilder();
        string evt = "message";
        var data = new List<string>();
        void Flush()
        {
            if (data.Count > 0) onEvent(evt, string.Join("\n", data));
            evt = "message";
            data.Clear();
        }
        void Line(string l)
        {
            if (l.Length == 0) Flush();
            else if (l[0] == ':') { }
            else if (l.StartsWith("event:", StringComparison.Ordinal)) evt = LegacyCustomize.JsTrim(l[6..]);
            else if (l.StartsWith("data:", StringComparison.Ordinal)) data.Add(l[5..].StartsWith(' ') ? l[6..] : l[5..]);
        }
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            int n = await stream.ReadAsync(bytes, cancel);
            if (n == 0) break;
            int c = decoder.GetChars(bytes, 0, n, chars, 0, flush: false);
            buf.Append(chars, 0, c);
            var text = buf.ToString();
            var lines = Regex.Split(text, "\r?\n");
            buf.Clear().Append(lines[^1]);
            for (int i = 0; i < lines.Length - 1; i++) Line(lines[i]);
        }
        if (buf.Length > 0) data.Add(Regex.Replace(buf.ToString(), "^data: ?", ""));
        Flush();
    }

    /// <summary>parseJSONLoose: arguments cut short are closed when that is enough; otherwise {__invalid}.</summary>
    public static JsonNode ParseJsonLoose(string? s)
    {
        if (string.IsNullOrEmpty(s)) return new JsonObject();
        foreach (var t in new[] { s, s + "\"}", s + "}" })
        {
            try { if (JsonNode.Parse(t) is { } n) return n; }
            catch (JsonException) { }
        }
        return new JsonObject { ["__invalid"] = s };
    }

    private static string Base36(long v)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        do { sb.Insert(0, digits[(int)(v % 36)]); v /= 36; } while (v > 0);
        return sb.ToString();
    }

    private static string Join(string baseUrl, string path) => Regex.Replace(baseUrl, "/$", "") + path;

    private static HttpRequestMessage Post(string url, JsonObject body, IEnumerable<(string, string)> headers)
    {
        var m = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json") };
        m.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        foreach (var (k, v) in headers) if (k != "Content-Type") m.Headers.TryAddWithoutValidation(k, v);
        return m;
    }

    private static HttpRequestMessage Get(string url, IEnumerable<(string, string)> headers)
    {
        var m = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (k, v) in headers) if (k != "Content-Type") m.Headers.TryAddWithoutValidation(k, v);
        return m;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static JsonObject BlockInput(AiBlock b) => b.Input as JsonObject is { } o ? (JsonObject)o.DeepClone() : new JsonObject();

    // ---------------------------------------------------------------- OpenAI-compatible

    /// <summary>openai.toWire(req, provider).</summary>
    public static JsonObject OpenAiWire(AiRequest req, Resolved provider)
    {
        var msgs = new JsonArray();
        if (!string.IsNullOrEmpty(req.System)) msgs.Add(new JsonObject { ["role"] = "system", ["content"] = req.System });
        foreach (var m in req.Messages)
        {
            if (m.Role == "assistant")
            {
                var text = string.Concat(m.Content.Where(b => b.Type == "text").Select(b => b.Text));
                var calls = m.Content.Where(b => b.Type == "tool_use").ToList();
                var o = new JsonObject { ["role"] = "assistant", ["content"] = text.Length > 0 ? text : calls.Count > 0 ? null : "" };
                if (calls.Count > 0)
                    o["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode)new JsonObject
                    {
                        ["id"] = c.Id, ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = (c.Input ?? new JsonObject()).ToJsonString(JsonOptions) }
                    }).ToArray());
                msgs.Add(o);
            }
            else
            {
                foreach (var b in m.Content.Where(b => b.Type == "tool_result"))
                    msgs.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = b.ToolUseId, ["content"] = (b.IsError ? "ERRO: " : "") + b.Content });
                var t = string.Join("\n\n", m.Content.Where(b => b.Type == "text").Select(b => b.Text));
                if (t.Length > 0) msgs.Add(new JsonObject { ["role"] = "user", ["content"] = t });
            }
        }
        var body = new JsonObject { ["model"] = req.Model, ["messages"] = msgs, ["stream"] = true };
        if (req.Tools is { Count: > 0 } tools)
        {
            body["tools"] = new JsonArray(tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.Parameters.DeepClone() }
            }).ToArray());
            body["tool_choice"] = "auto";
        }
        if (req.MaxTokens is { } mt && mt != 0) body["max_tokens"] = mt;
        if (req.Temperature is { } temp) body["temperature"] = temp;
        if (provider.StreamUsage) body["stream_options"] = new JsonObject { ["include_usage"] = true };
        if (provider.Extra is { } extra) foreach (var (k, v) in extra) body[k] = v?.DeepClone();
        return body;
    }

    /// <summary>openai.headers(p).</summary>
    public static List<(string, string)> OpenAiHeaders(Resolved p)
    {
        var h = new List<(string, string)> { ("Content-Type", "application/json") };
        if (p.ApiKey.Length > 0) h.Add(("Authorization", "Bearer " + p.ApiKey));
        if (p.Preset == "openrouter") h.Add(("X-Title", "Urbe"));
        return h;
    }

    private async Task<AiResult> OpenAiStreamAsync(Resolved p, AiRequest req, Action<AiStreamEvent> onEvent, CancellationToken cancel)
    {
        var body = OpenAiWire(req, p);
        using var res = await RequestAsync(p, Join(p.BaseUrl, "/chat/completions"), () => Post(Join(p.BaseUrl, "/chat/completions"), body, OpenAiHeaders(p)), cancel);
        var text = new StringBuilder();
        var reasoning = new StringBuilder();
        var calls = new SortedDictionary<int, (string Id, string Name, StringBuilder Args)>();
        string? finish = null;
        long input = 0, output = 0;
        double? cost = null;
        var model = req.Model;
        AiProviderException? err = null;
        void Chunk(JsonNode? j0)
        {
            if (j0 is not JsonObject j) return;
            if (Truthy(j["error"]))
            {
                // friendly(j.error.code || 500, j, p): a code that is not a number matches no status
                var raw = j["error"] is JsonObject eo ? eo["code"] : null;
                double n = LegacyCustomize.JsToNumber(raw);
                int? code = Truthy(raw) ? (double.IsFinite(n) ? (int)n : 0) : null;
                err = new AiProviderException(Friendly(code ?? 500, j, p.Name), code);
                return;
            }
            if (Truthy(j["model"])) model = LegacyCustomize.JsToString(j["model"]);
            if (j["usage"] is JsonObject u)
            {
                if (Truthy(u["prompt_tokens"])) input = (long)LegacyCustomize.JsToNumber(u["prompt_tokens"]);
                if (Truthy(u["completion_tokens"])) output = (long)LegacyCustomize.JsToNumber(u["completion_tokens"]);
                if (u["cost"] is not null) cost = LegacyCustomize.JsToNumber(u["cost"]);
            }
            if (j["choices"] is not JsonArray { Count: > 0 } choices || choices[0] is not JsonObject ch) return;
            var d = ch["delta"] as JsonObject ?? ch["message"] as JsonObject ?? new JsonObject();
            if (Truthy(d["reasoning"]) || Truthy(d["reasoning_content"]))
            {
                var r = LegacyCustomize.JsToString(Truthy(d["reasoning"]) ? d["reasoning"] : d["reasoning_content"]);
                reasoning.Append(r);
                onEvent(new AiStreamEvent("reasoning", Delta: r));
            }
            if (d["content"] is JsonValue cv && cv.TryGetValue<string>(out var content) && content.Length > 0)
            {
                text.Append(content);
                onEvent(new AiStreamEvent("text", Delta: content));
            }
            if (d["tool_calls"] is JsonArray tcs)
            {
                int k = 0;
                foreach (var tc0 in tcs)
                {
                    if (tc0 is not JsonObject tc) { k++; continue; }
                    int i = tc["index"] is { } ix ? (int)LegacyCustomize.JsToNumber(ix) : k;
                    if (!calls.TryGetValue(i, out var c)) c = ("", "", new StringBuilder());
                    if (Truthy(tc["id"])) c.Id = LegacyCustomize.JsToString(tc["id"]);
                    if (tc["function"] is JsonObject f)
                    {
                        if (Truthy(f["name"]))
                        {
                            c.Name += LegacyCustomize.JsToString(f["name"]);
                            calls[i] = c;
                            onEvent(new AiStreamEvent("tool_start", Index: i, Name: c.Name));
                        }
                        if (Truthy(f["arguments"]))
                        {
                            c.Args.Append(f["arguments"] is JsonValue av && av.TryGetValue<string>(out var a) ? a : f["arguments"]!.ToJsonString(JsonOptions));
                            calls[i] = c;
                            onEvent(new AiStreamEvent("tool_delta", Index: i, Partial: c.Args.ToString()));
                        }
                    }
                    calls[i] = c;
                    k++;
                }
            }
            if (Truthy(ch["finish_reason"])) finish = LegacyCustomize.JsToString(ch["finish_reason"]);
        }
        await ReadSseAsync(res, (evt, data) =>
        {
            if (evt == "json") { try { Chunk(JsonNode.Parse(data)); } catch (JsonException) { } return; }
            if (data == "[DONE]") return;
            try { Chunk(JsonNode.Parse(data)); } catch (JsonException) { }
        }, cancel);
        if (err is not null) throw err;
        var output2 = new List<AiBlock>();
        if (text.Length > 0) output2.Add(AiBlock.TextBlock(text.ToString()));
        int n = 0;
        foreach (var (_, c) in calls)
        {
            int i = n++;
            if (c.Name.Length == 0) continue;
            output2.Add(AiBlock.ToolUse(c.Id.Length > 0 ? c.Id : "call_" + Base36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) + "_" + i, c.Name, ParseJsonLoose(c.Args.ToString())));
        }
        var hasTools = output2.Any(b => b.Type == "tool_use");
        return new AiResult(output2, reasoning.ToString(), hasTools ? "tool_use" : finish == "length" ? "max_tokens" : "end", new AiUsage(input, output, cost), model);
    }

    private async Task<List<AiModelInfo>> OpenAiModelsAsync(Resolved p, CancellationToken cancel)
    {
        using var res = await RequestAsync(p, Join(p.BaseUrl, "/models"), () => Get(Join(p.BaseUrl, "/models"), OpenAiHeaders(p)), cancel);
        var j = JsonNode.Parse(await res.Content.ReadAsStringAsync(cancel)) as JsonObject ?? new JsonObject();
        var list = j["data"] as JsonArray ?? j["models"] as JsonArray ?? [];
        return list.OfType<JsonObject>().Select(m =>
        {
            (double, double)? price = m["pricing"] is JsonObject pr ? (LegacyCustomize.JsToNumber(pr["prompt"] ?? 0) * 1e6, LegacyCustomize.JsToNumber(pr["completion"] ?? 0) * 1e6) : null;
            bool? tools = m["supported_parameters"] is JsonArray sp ? sp.Any(x => LegacyCustomize.JsToString(x) == "tools") : null;
            var id = Truthy(m["id"]) ? LegacyCustomize.JsToString(m["id"]) : LegacyCustomize.JsToString(m["name"]);
            var name = Truthy(m["name"]) ? LegacyCustomize.JsToString(m["name"]) : LegacyCustomize.JsToString(m["id"]);
            long? context = Truthy(m["context_length"]) ? (long)LegacyCustomize.JsToNumber(m["context_length"]) : Truthy(m["context_window"]) ? (long)LegacyCustomize.JsToNumber(m["context_window"]) : null;
            return new AiModelInfo(id, name, context, price, tools);
        }).ToList();
    }

    // ---------------------------------------------------------------- Anthropic

    /// <summary>anthropic.toWire(req).</summary>
    public static JsonObject AnthropicWire(AiRequest req)
    {
        var msgs = new List<(string Role, JsonArray Content)>();
        foreach (var m in req.Messages)
        {
            var blocks = m.Content.Where(b => !(b.Type == "text" && b.Text.Length == 0)).Select(b => (JsonNode)(b.Type switch
            {
                "text" => new JsonObject { ["type"] = "text", ["text"] = b.Text },
                "tool_use" => new JsonObject { ["type"] = "tool_use", ["id"] = b.Id, ["name"] = b.Name, ["input"] = BlockInput(b) },
                _ => new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = b.ToolUseId, ["content"] = b.Content, ["is_error"] = b.IsError }
            })).ToList();
            if (blocks.Count == 0) continue;
            if (msgs.Count > 0 && msgs[^1].Role == m.Role) foreach (var b in blocks) msgs[^1].Content.Add(b);
            else msgs.Add((m.Role, new JsonArray(blocks.ToArray())));
        }
        var body = new JsonObject
        {
            ["model"] = req.Model, ["max_tokens"] = req.MaxTokens is { } mt && mt != 0 ? mt : 8192,
            ["messages"] = new JsonArray(msgs.Select(x => (JsonNode)new JsonObject { ["role"] = x.Role, ["content"] = x.Content }).ToArray()),
            ["stream"] = true
        };
        if (!string.IsNullOrEmpty(req.System)) body["system"] = req.System;
        if (req.Tools is { Count: > 0 } tools)
            body["tools"] = new JsonArray(tools.Select(t => (JsonNode)new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["input_schema"] = t.Parameters.DeepClone() }).ToArray());
        if (req.Temperature is { } temp) body["temperature"] = temp;
        return body;
    }

    /// <summary>anthropic.headers(p).</summary>
    public static List<(string, string)> AnthropicHeaders(Resolved p) =>
        [("Content-Type", "application/json"), ("x-api-key", p.ApiKey), ("anthropic-version", "2023-06-01"), ("anthropic-dangerous-direct-browser-access", "true")];

    private async Task<AiResult> AnthropicStreamAsync(Resolved p, AiRequest req, Action<AiStreamEvent> onEvent, CancellationToken cancel)
    {
        var body = AnthropicWire(req);
        using var res = await RequestAsync(p, Join(p.BaseUrl, "/messages"), () => Post(Join(p.BaseUrl, "/messages"), body, AnthropicHeaders(p)), cancel);
        var blocks = new SortedDictionary<int, (string Type, string Id, string Name, StringBuilder Text)>();
        string? stop = null;
        long input = 0, output = 0;
        var model = req.Model;
        AiProviderException? err = null;
        var reasoning = new StringBuilder();
        await ReadSseAsync(res, (evt, data) =>
        {
            JsonObject j;
            try { if (JsonNode.Parse(data) is JsonObject o) j = o; else return; }
            catch (JsonException) { return; }
            var t = Truthy(j["type"]) ? LegacyCustomize.JsToString(j["type"]) : evt;
            int Index() => (int)LegacyCustomize.JsToNumber(j["index"]);
            if (t == "message_start" && j["message"] is JsonObject msg)
            {
                if (Truthy(msg["model"])) model = LegacyCustomize.JsToString(msg["model"]);
                var u = msg["usage"] as JsonObject ?? new JsonObject();
                long N(string k) => Truthy(u[k]) ? (long)LegacyCustomize.JsToNumber(u[k]) : 0;
                input = N("input_tokens") + N("cache_read_input_tokens") + N("cache_creation_input_tokens");
                output = N("output_tokens");
            }
            else if (t == "content_block_start")
            {
                var cb = j["content_block"] as JsonObject ?? new JsonObject();
                var type = LegacyCustomize.JsToString(cb["type"] ?? "");
                blocks[Index()] = type == "tool_use" ? ("tool_use", LegacyCustomize.JsToString(cb["id"] ?? ""), LegacyCustomize.JsToString(cb["name"] ?? ""), new StringBuilder())
                    : type == "thinking" ? ("thinking", "", "", new StringBuilder())
                    : ("text", "", "", new StringBuilder(Truthy(cb["text"]) ? LegacyCustomize.JsToString(cb["text"]) : ""));
                if (type == "tool_use") onEvent(new AiStreamEvent("tool_start", Index: Index(), Name: LegacyCustomize.JsToString(cb["name"] ?? "")));
            }
            else if (t == "content_block_delta")
            {
                if (!blocks.TryGetValue(Index(), out var b)) return;
                var d = j["delta"] as JsonObject ?? new JsonObject();
                var dt = LegacyCustomize.JsToString(d["type"] ?? "");
                if (dt == "text_delta") { var s = LegacyCustomize.JsToString(d["text"] ?? ""); b.Text.Append(s); onEvent(new AiStreamEvent("text", Delta: s)); }
                else if (dt == "input_json_delta") { b.Text.Append(Truthy(d["partial_json"]) ? LegacyCustomize.JsToString(d["partial_json"]) : ""); onEvent(new AiStreamEvent("tool_delta", Index: Index(), Partial: b.Text.ToString())); }
                else if (dt == "thinking_delta") { var s = Truthy(d["thinking"]) ? LegacyCustomize.JsToString(d["thinking"]) : ""; reasoning.Append(s); onEvent(new AiStreamEvent("reasoning", Delta: s)); }
            }
            else if (t == "message_delta")
            {
                if (j["delta"] is JsonObject dd && Truthy(dd["stop_reason"])) stop = LegacyCustomize.JsToString(dd["stop_reason"]);
                if (j["usage"] is JsonObject u && u["output_tokens"] is not null) output = (long)LegacyCustomize.JsToNumber(u["output_tokens"]);
            }
            else if (t == "error")
                err = new AiProviderException(Friendly(j["error"] is JsonObject e && LegacyCustomize.JsToString(e["type"] ?? "") == "overloaded_error" ? 529 : 500, j, p.Name));
        }, cancel);
        if (err is not null) throw err;
        var content = new List<AiBlock>();
        foreach (var (_, b) in blocks)
        {
            if (b.Type == "text" && b.Text.Length > 0) content.Add(AiBlock.TextBlock(b.Text.ToString()));
            else if (b.Type == "tool_use") content.Add(AiBlock.ToolUse(b.Id, b.Name, ParseJsonLoose(b.Text.ToString())));
        }
        return new AiResult(content, reasoning.ToString(), stop == "tool_use" ? "tool_use" : stop == "max_tokens" ? "max_tokens" : "end", new AiUsage(input, output, null), model);
    }

    private async Task<List<AiModelInfo>> AnthropicModelsAsync(Resolved p, CancellationToken cancel)
    {
        try
        {
            using var res = await RequestAsync(p, Join(p.BaseUrl, "/models?limit=100"), () => Get(Join(p.BaseUrl, "/models?limit=100"), AnthropicHeaders(p)), cancel);
            var j = JsonNode.Parse(await res.Content.ReadAsStringAsync(cancel)) as JsonObject ?? new JsonObject();
            return (j["data"] as JsonArray ?? []).OfType<JsonObject>().Select(m => new AiModelInfo(LegacyCustomize.JsToString(m["id"]),
                Truthy(m["display_name"]) ? LegacyCustomize.JsToString(m["display_name"]) : LegacyCustomize.JsToString(m["id"]), 200000, null, true)).ToList();
        }
        catch (AiProviderException e) when (e.Status is not (401 or 403))
        {
            return Presets["anthropic"].Models!.Select(id => new AiModelInfo(id, id, 200000, null, true)).ToList();
        }
    }

    // ---------------------------------------------------------------- public API

    /// <summary>stream(cfg, req, {signal, onEvent}): the answer of the model, streamed.</summary>
    public Task<AiResult> StreamAsync(AiProviderConfig cfg, AiRequest req, Action<AiStreamEvent>? onEvent = null, CancellationToken cancel = default)
    {
        var p = Resolve(cfg);
        if (p.BaseUrl.Length == 0) throw new AiProviderException("Informe o endereço (URL base) do provedor " + p.Name + ".");
        if (Presets.TryGetValue(p.Preset, out var pre) && pre.NeedsKey && p.ApiKey.Length == 0) throw new AiProviderException("Configure a chave de " + p.Name + " nas configurações do Assistente.");
        if (string.IsNullOrEmpty(req.Model)) throw new AiProviderException("Escolha um modelo.");
        onEvent ??= _ => { };
        return p.Kind switch
        {
            "openai" => OpenAiStreamAsync(p, req, onEvent, cancel),
            "anthropic" => AnthropicStreamAsync(p, req, onEvent, cancel),
            _ => throw new AiProviderException("Tipo de provedor desconhecido: " + p.Kind)
        };
    }

    // ---------------------------------------------------------------- images (UC-63)

    /// <summary>
    /// Image models for the existing configured providers: OpenRouter's /images API, and the OpenAI-compatible
    /// /images/generations endpoints for OpenAI and Gemini. Custom providers need an explicitly chosen image model.
    /// </summary>
    public static string? ImageModel(string preset) => preset switch
    {
        "openai" => "gpt-image-1",
        "gemini" => "gemini-2.5-flash-image",
        "openrouter" => "bytedance-seed/seedream-4.5",
        "custom" => "",
        _ => null
    };

    /// <summary>True when the provider can draw pictures (see <see cref="ImageModel"/>).</summary>
    public static bool DrawsImages(AiProviderConfig cfg)
    {
        var p = Resolve(cfg);
        return p.Kind == "openai" && !string.IsNullOrWhiteSpace(ImageModel(p.Preset));
    }

    /// <summary>Explicit provider selection wins. Auto mode preserves the existing provider fallback.</summary>
    public static AiProviderConfig? PickImageProvider(AiProviderConfig chat, IEnumerable<AiProviderConfig> configured,
        string? selectedProviderId = null, string? selectedModel = null)
    {
        static bool Credentials(AiProviderConfig p) =>
            !Presets.TryGetValue(p.Preset, out var pre) || !pre.NeedsKey || !string.IsNullOrWhiteSpace(p.ApiKey);
        static bool Supports(AiProviderConfig p, string? model) =>
            Resolve(p).Kind == "openai" && (DrawsImages(p) || (p.Preset == "custom" && !string.IsNullOrWhiteSpace(model)));

        if (!string.IsNullOrWhiteSpace(selectedProviderId))
        {
            var chosen = configured.FirstOrDefault(p => p.Id == selectedProviderId);
            return chosen is not null && Credentials(chosen) && Supports(chosen, selectedModel) ? chosen : null;
        }
        return Credentials(chat) && Supports(chat, null) ? chat : configured.FirstOrDefault(p => Credentials(p) && Supports(p, null));
    }

    /// <summary>Only show image-capable models: OpenRouter exposes a dedicated image catalog.</summary>
    public async Task<List<AiModelInfo>> ListImageModelsAsync(AiProviderConfig cfg, CancellationToken cancel = default)
    {
        var p = Resolve(cfg);
        if (p.Kind != "openai") throw new AiProviderException("Este provedor não tem API de imagens compatível.");
        if (p.Preset == "gemini")
            return new[] { "gemini-2.5-flash-image", "gemini-3-pro-image-preview" }
                .Select(m => new AiModelInfo(m, m, null, null, null)).ToList();
        if (p.Preset == "openrouter")
        {
            var url = Join(p.BaseUrl, "/images/models");
            using var res = await RequestAsync(p, url, () => Get(url, OpenAiHeaders(p)), cancel);
            var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(cancel)) as JsonObject;
            return (json?["data"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(m => new AiModelInfo((string?)m["id"] ?? "", (string?)m["name"] ?? (string?)m["id"] ?? "", null, null, null))
                .Where(m => m.Id.Length > 0).ToList();
        }
        var catalog = await OpenAiModelsAsync(p, cancel);
        return catalog.Where(m => m.Id.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                                  m.Id.Contains("imagen", StringComparison.OrdinalIgnoreCase) ||
                                  m.Id.Contains("flux", StringComparison.OrdinalIgnoreCase) ||
                                  m.Id.Contains("seedream", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>The file extension of a picture's bytes (PNG, JPEG or WebP), or null when they are not one.</summary>
    public static string? ImageExtension(ReadOnlySpan<byte> b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4e && b[3] == 0x47 ? ".png"
        : b.Length > 3 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff ? ".jpg"
        : b.Length > 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F' && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P' ? ".webp"
        : null;

    /// <summary>
    /// UC-63: one picture drawn by the provider from <paramref name="prompt"/>, as the bytes of a PNG, JPEG or WebP
    /// (asked as base64, never a link to fetch). <paramref name="model"/> empty: the preset's image model.
    /// </summary>
    public async Task<byte[]> GenerateImageAsync(AiProviderConfig cfg, string prompt, string? model = null, string size = "1024x1024", CancellationToken cancel = default)
    {
        var p = Resolve(cfg);
        if (p.Kind != "openai" || ImageModel(p.Preset) is not { } preset)
            throw new AiProviderException(p.Name + " não gera imagens pela API configurada. Escolha um provedor de imagens compatível.");
        if (p.BaseUrl.Length == 0) throw new AiProviderException("Informe o endereço (URL base) do provedor " + p.Name + ".");
        if (Presets.TryGetValue(p.Preset, out var pre) && pre.NeedsKey && p.ApiKey.Length == 0) throw new AiProviderException("Configure a chave de " + p.Name + " nas configurações do Assistente.");
        var m = string.IsNullOrWhiteSpace(model) ? preset : model.Trim();
        if (m.Length == 0) throw new AiProviderException("Informe o modelo de imagem do servidor.");
        if (string.IsNullOrWhiteSpace(prompt)) throw new AiProviderException("Descreva a imagem.");
        bool openRouter = p.Preset == "openrouter";
        // OpenRouter's dedicated Image API uses aspect_ratio; OpenAI and Gemini use size.
        var body = openRouter
            ? new JsonObject { ["model"] = m, ["prompt"] = prompt, ["aspect_ratio"] = size switch
                { "1536x1024" => "3:2", "1024x1536" => "2:3", _ => "1:1" } }
            : new JsonObject { ["model"] = m, ["prompt"] = prompt, ["n"] = 1, ["size"] = size };
        // GPT Image models answer as base64 and refuse response_format. Gemini needs it.
        if (!openRouter && !m.StartsWith("gpt-image", StringComparison.Ordinal)) body["response_format"] = "b64_json";
        var url = Join(p.BaseUrl, openRouter ? "/images" : "/images/generations");
        using var res = await RequestAsync(p, url, () => Post(url, body, OpenAiHeaders(p)), cancel);
        JsonNode? j;
        try { j = JsonNode.Parse(await res.Content.ReadAsStringAsync(cancel)); }
        catch (JsonException) { throw new AiProviderException("O provedor devolveu uma resposta que não é JSON."); }
        var b64 = j?["data"] is JsonArray { Count: > 0 } data && data[0]?["b64_json"] is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
        if (string.IsNullOrEmpty(b64)) throw new AiProviderException("O provedor não devolveu a imagem.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new AiProviderException("O provedor devolveu uma imagem ilegível."); }
        if (ImageExtension(bytes) is null) throw new AiProviderException("O provedor devolveu algo que não é uma imagem.");
        return bytes;
    }

    /// <summary>listModels(cfg).</summary>
    public Task<List<AiModelInfo>> ListModelsAsync(AiProviderConfig cfg, CancellationToken cancel = default)
    {
        var p = Resolve(cfg);
        return p.Kind switch
        {
            "openai" => OpenAiModelsAsync(p, cancel),
            "anthropic" => AnthropicModelsAsync(p, cancel),
            _ => throw new AiProviderException("Tipo de provedor desconhecido: " + p.Kind)
        };
    }

    /// <summary>test(cfg, model): "Responda só com: ok" — whether the provider answers.</summary>
    public async Task<(bool Ok, string Text, string Model)> TestAsync(AiProviderConfig cfg, string model, CancellationToken cancel = default)
    {
        var output = new StringBuilder();
        var r = await StreamAsync(cfg, new AiRequest(model, [new AiMessage("user", [AiBlock.TextBlock("ping")])], "Responda só com: ok", MaxTokens: 16),
            e => { if (e.Type == "text") output.Append(e.Delta); }, cancel);
        var text = output.Length > 0 ? output.ToString() : r.Content.FirstOrDefault()?.Text ?? "";
        return (true, text, r.Model);
    }
}
