using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// The chat format of Gemma 4 (Google's canonical <c>chat_template.jinja</c>, 2026-07-09) for the on-device model: the
/// conversation, with the agent's tools declared in the model's own notation, written as the text the model was trained
/// on, and the model's answer read back into text, reasoning and tool calls. Pure text handling; the model itself
/// runs in the heads (<see cref="ILocalTextRuntime"/>). Proved against the real template (tests/fixtures/gemma4-chat).
/// </summary>
public static class Gemma4Chat
{
    private const string Q = "<|\"|>";

    /// <summary>What generation stops at: the end of the turn, and the point where a tool's answer is expected.</summary>
    public static IReadOnlyList<string> StopTexts { get; } = ["<turn|>", "<|tool_response>"];

    // ------------------------------------------------------------------ writing the prompt

    /// <summary>The prompt for <paramref name="request"/>: the system part with the tools, every turn, and the open turn of the model.</summary>
    public static string Render(AiRequest request, bool thinking = false)
    {
        var sb = new StringBuilder("<bos>");
        var tools = request.Tools is { Count: > 0 } ? request.Tools : null;
        if (thinking || tools is not null || !string.IsNullOrWhiteSpace(request.System))
        {
            sb.Append("<|turn>system\n");
            if (thinking) sb.Append("<|think|>\n");
            if (!string.IsNullOrWhiteSpace(request.System)) sb.Append(request.System.Trim());
            if (tools is not null)
                foreach (var tool in tools) sb.Append("<|tool>").Append(Declaration(tool).Trim()).Append("<tool|>");
            sb.Append("<turn|>\n");
        }

        // the normalized messages become the template's: a tool_result block is a "tool" message after its assistant message
        var turns = new List<Turn>();
        foreach (var message in request.Messages)
        {
            var text = new StringBuilder();
            var calls = new List<AiBlock>();
            foreach (var block in message.Content)
            {
                switch (block.Type)
                {
                    case "text": text.Append(block.Text); break;
                    case "attachment": text.Append("[anexo: ").Append(block.FileName).Append(']'); break;
                    case "tool_use": calls.Add(block); break;
                    case "tool_result": turns.Add(new Turn("tool", block.Content, [], block.ToolUseId)); break;
                }
            }
            if (message.Role == "assistant") turns.Add(new Turn("assistant", text.ToString(), calls, ""));
            else if (text.Length > 0 || message.Content.All(b => b.Type != "tool_result")) turns.Add(new Turn(message.Role, text.ToString(), [], ""));
        }

        string? prevType = null;      // the template's ns.prev_message_type
        string? prevNonTool = null;   // ns.prev_non_tool_role
        for (int i = 0; i < turns.Count; i++)
        {
            var turn = turns[i];
            if (turn.Role == "tool") continue;
            prevType = null;
            var role = turn.Role == "assistant" ? "model" : turn.Role;
            bool continuing = role == "model" && prevNonTool == "assistant";
            if (!continuing) sb.Append("<|turn>").Append(role).Append('\n');

            bool responded = false;
            if (turn.Calls.Count > 0)
            {
                foreach (var call in turn.Calls)
                {
                    sb.Append("<|tool_call>call:").Append(call.Name).Append('{');
                    if (call.Input is JsonObject args)
                    {
                        bool first = true;
                        foreach (var (key, value) in Sorted(args))
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            sb.Append(key).Append(':').Append(Argument(value, escapeKeys: false));
                        }
                    }
                    sb.Append("}<tool_call|>");
                }
                prevType = "tool_call";
                for (int k = i + 1; k < turns.Count && turns[k].Role == "tool"; k++)
                {
                    var name = turn.Calls.FirstOrDefault(c => c.Id == turns[k].ToolUseId)?.Name ?? "unknown";
                    sb.Append("<|tool_response>response:").Append(name).Append("{value:").Append(Argument(JsonValue.Create(turns[k].Text), escapeKeys: false)).Append("}<tool_response|>");
                    responded = true;
                    prevType = "tool_response";
                }
            }

            var content = role == "model" ? StripThinking(turn.Text) : turn.Text.Trim();
            sb.Append(content);
            bool hasContent = content.Trim().Length > 0;
            string? nextRole = null;
            for (int j = i + 1; j < turns.Count; j++) if (turns[j].Role != "tool") { nextRole = turns[j].Role; break; }
            bool continuesIntoNext = role == "model" && nextRole == "assistant" && (turn.Calls.Count == 0 || responded);
            if (continuesIntoNext) { }
            else if (!(responded && !hasContent && nextRole is null)) sb.Append("<turn|>\n");
            prevNonTool = turn.Role;
        }

        if (prevType is not ("tool_response" or "tool_call")) sb.Append("<|turn>model\n");
        else if (prevType == "tool_response" && thinking) sb.Append("<|channel>thought\n");
        return sb.ToString();
    }

    private sealed record Turn(string Role, string Text, IReadOnlyList<AiBlock> Calls, string ToolUseId);

    // jinja's dictsort: by key, without case
    private static IEnumerable<KeyValuePair<string, JsonNode?>> Sorted(JsonObject o) =>
        o.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Key, StringComparer.Ordinal);

    private static string StripThinking(string text)
    {
        var result = new StringBuilder();
        foreach (var part in text.Split("<channel|>"))
            result.Append(part.Contains("<|channel>", StringComparison.Ordinal) ? part[..part.IndexOf("<|channel>", StringComparison.Ordinal)] : part);
        return result.ToString().Trim();
    }

    /// <summary>format_argument: a value in the model's notation (strings between <c>&lt;|"|&gt;</c>).</summary>
    private static string Argument(JsonNode? value, bool escapeKeys)
    {
        switch (value)
        {
            case null: return "null";
            case JsonObject o:
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var (key, v) in Sorted(o))
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(escapeKeys ? Q + key + Q : key).Append(':').Append(Argument(v, escapeKeys));
                }
                return sb.Append('}').ToString();
            }
            case JsonArray a: return "[" + string.Join(",", a.Select(v => Argument(v, escapeKeys))) + "]";
            case JsonValue v when v.TryGetValue<string>(out var s): return Q + s + Q;
            case JsonValue v when v.TryGetValue<bool>(out var b): return b ? "true" : "false";
            case JsonValue v: return v.ToJsonString();
            default: return value.ToJsonString();
        }
    }

    // ------------------------------------------------------------------ the tools

    private static string Declaration(AiToolSchema tool)
    {
        var sb = new StringBuilder("declaration:").Append(tool.Name).Append("{description:").Append(Q).Append(tool.Description).Append(Q);
        var p = tool.Parameters;
        if (p.Count > 0)
        {
            sb.Append(",parameters:{");
            if (p["properties"] is JsonObject props && props.Count > 0)
                sb.Append("properties:{").Append(Parameters(props)).Append("},");
            if (p["required"] is JsonArray req && req.Count > 0)
                sb.Append("required:[").Append(string.Join(",", req.Select(r => Q + (string?)r + Q))).Append("],");
            if (p["type"] is JsonNode type) sb.Append("type:").Append(Q).Append(((string?)type)?.ToUpperInvariant()).Append(Q).Append('}');
        }
        return sb.Append('}').ToString();
    }

    /// <summary>format_parameters: each property by name, with its description, enum, items, nested properties and type.</summary>
    private static string Parameters(JsonObject properties, bool filterKeys = false)
    {
        string[] standard = ["description", "type", "properties", "required", "nullable"];
        var sb = new StringBuilder();
        bool found = false;
        foreach (var (key, node) in Sorted(properties))
        {
            if (filterKeys && standard.Contains(key)) continue;
            if (node is not JsonObject value) continue;
            if (found) sb.Append(',');
            found = true;
            sb.Append(key).Append(":{");
            bool comma = false;
            void Sep() { if (comma) sb.Append(','); else comma = true; }
            var type = ((string?)value["type"] ?? "").ToUpperInvariant();
            if (value["description"] is JsonNode d && !string.IsNullOrEmpty((string?)d)) { sb.Append("description:").Append(Q).Append((string?)d).Append(Q); comma = true; }
            if (type == "STRING" && value["enum"] is JsonArray en && en.Count > 0) { Sep(); sb.Append("enum:").Append(Argument(en, true)); }
            else if (type == "ARRAY" && value["items"] is JsonObject items && items.Count > 0)
            {
                Sep();
                sb.Append("items:{");
                bool firstItem = false;
                foreach (var (ik, iv) in Sorted(items))
                {
                    if (iv is null) continue;
                    if (firstItem) sb.Append(',');
                    firstItem = true;
                    if (ik == "properties")
                    {
                        sb.Append("properties:{");
                        if (iv is JsonObject ip) sb.Append(Parameters(ip));
                        sb.Append('}');
                    }
                    else if (ik == "required") sb.Append("required:[").Append(string.Join(",", ((JsonArray)iv).Select(r => Q + (string?)r + Q))).Append(']');
                    else if (ik == "type") sb.Append("type:").Append(iv is JsonValue tv && tv.TryGetValue<string>(out var ts) ? Q + ts.ToUpperInvariant() + Q : Argument(new JsonArray([.. ((JsonArray)iv).Select(x => (JsonNode?)JsonValue.Create(((string?)x)?.ToUpperInvariant()))]), true));
                    else sb.Append(ik).Append(':').Append(Argument(iv, true));
                }
                sb.Append('}');
            }
            if (value["nullable"] is JsonValue nv && nv.TryGetValue<bool>(out var nullable) && nullable) { Sep(); sb.Append("nullable:true"); }
            if (type == "OBJECT")
            {
                if (value["properties"] is JsonObject sub) { Sep(); sb.Append("properties:{").Append(Parameters(sub)).Append('}'); }
                if (value["required"] is JsonArray r && r.Count > 0) { Sep(); sb.Append("required:[").Append(string.Join(",", r.Select(x => Q + (string?)x + Q))).Append(']'); }
            }
            Sep();
            sb.Append("type:").Append(Q).Append(type).Append(Q).Append('}');
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ reading the answer

    /// <summary>The model's answer as the agent wants it: the visible text, the reasoning, and the tool calls it made.</summary>
    public static (string Text, string Reasoning, IReadOnlyList<AiBlock> Calls) Parse(string output)
    {
        var text = output;
        var reasoning = new StringBuilder();
        // <|channel>thought … <channel|>
        while (text.IndexOf("<|channel>", StringComparison.Ordinal) is var start and >= 0)
        {
            var end = text.IndexOf("<channel|>", start, StringComparison.Ordinal);
            var inner = end < 0 ? text[(start + 10)..] : text[(start + 10)..end];
            if (inner.StartsWith("thought\n", StringComparison.Ordinal)) inner = inner[8..];
            if (reasoning.Length > 0) reasoning.Append('\n');
            reasoning.Append(inner.Trim());
            text = text[..start] + (end < 0 ? "" : text[(end + 10)..]);
        }
        var calls = new List<AiBlock>();
        int n = 0;
        while (text.IndexOf("<|tool_call>", StringComparison.Ordinal) is var s and >= 0)
        {
            var body = text[(s + 12)..];
            var close = body.IndexOf("<tool_call|>", StringComparison.Ordinal);
            var call = close < 0 ? body : body[..close];
            text = text[..s] + (close < 0 ? "" : body[(close + 12)..]);
            if (!call.StartsWith("call:", StringComparison.Ordinal)) continue;
            var brace = call.IndexOf('{');
            var name = (brace < 0 ? call[5..] : call[5..brace]).Trim();
            var args = new JsonObject();
            if (brace >= 0)
            {
                int at = brace;
                if (ReadValue(call, ref at) is JsonObject o) args = o;
            }
            if (name.Length > 0) calls.Add(AiBlock.ToolUse("local_" + (++n).ToString(CultureInfo.InvariantCulture), name, args));
        }
        foreach (var stop in StopTexts) { var i = text.IndexOf(stop, StringComparison.Ordinal); if (i >= 0) text = text[..i]; }
        text = text.Replace("<bos>", "").Replace("<eos>", "");
        return (text.Trim(), reasoning.ToString(), calls);
    }

    private static JsonNode? ReadValue(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length) return null;
        if (string.CompareOrdinal(s, i, Q, 0, Q.Length) == 0)
        {
            i += Q.Length;
            var end = s.IndexOf(Q, i, StringComparison.Ordinal);
            if (end < 0) { var rest = s[i..]; i = s.Length; return JsonValue.Create(rest); }
            var value = s[i..end];
            i = end + Q.Length;
            return JsonValue.Create(value);
        }
        if (s[i] == '{')
        {
            i++;
            var o = new JsonObject();
            while (i < s.Length)
            {
                while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                string key;
                if (string.CompareOrdinal(s, i, Q, 0, Q.Length) == 0) key = ((string?)ReadValue(s, ref i)) ?? "";
                else
                {
                    int k = i;
                    while (k < s.Length && s[k] != ':' && s[k] != '}') k++;
                    key = s[i..k].Trim();
                    i = k;
                }
                if (i < s.Length && s[i] == ':') i++;
                o[key] = ReadValue(s, ref i);
            }
            return o;
        }
        if (s[i] == '[')
        {
            i++;
            var a = new JsonArray();
            while (i < s.Length)
            {
                while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                a.Add(ReadValue(s, ref i));
            }
            return a;
        }
        int j = i;
        while (j < s.Length && s[j] != ',' && s[j] != '}' && s[j] != ']') j++;
        var token = s[i..j].Trim();
        i = j;
        if (token == "null") return null;
        if (token == "true") return JsonValue.Create(true);
        if (token == "false") return JsonValue.Create(false);
        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l is >= int.MinValue and <= int.MaxValue ? JsonValue.Create((int)l) : JsonValue.Create(l);
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
        return JsonValue.Create(token);
    }

    // ------------------------------------------------------------------ the answer as it streams

    /// <summary>
    /// Splits the text the model generates, piece by piece, into what the person reads ("text"), what the model thought
    /// ("reasoning") and nothing for a tool call (it is read whole at the end by <see cref="Parse"/>). A mark of the
    /// notation cut in two pieces is held until it is complete, so none of it ever shows.
    /// </summary>
    public sealed class Streamer
    {
        private static readonly string[] Marks = ["<|channel>", "<channel|>", "<|tool_call>", "<tool_call|>", "<turn|>", "<|tool_response>", "<bos>", "<eos>"];
        private readonly StringBuilder _hold = new();
        private string _mode = "text";      // text | thought | tool
        private bool _fresh;                // just entered a thought: its first word is the channel's name

        public IEnumerable<(string Kind, string Text)> Push(string piece)
        {
            _hold.Append(piece);
            return Drain(final: false);
        }

        public IEnumerable<(string Kind, string Text)> Flush() => Drain(final: true);

        private IEnumerable<(string Kind, string Text)> Drain(bool final)
        {
            var output = new List<(string, string)>();
            while (_hold.Length > 0)
            {
                var text = _hold.ToString();
                // the first mark in the text
                int at = -1;
                string? mark = null;
                foreach (var m in Marks)
                {
                    var i = text.IndexOf(m, StringComparison.Ordinal);
                    if (i >= 0 && (at < 0 || i < at)) { at = i; mark = m; }
                }
                if (at < 0)
                {
                    // everything before a possible start of a mark is safe
                    int safe = text.Length;
                    if (!final)
                    {
                        int lt = text.LastIndexOf('<');
                        if (lt >= 0 && Marks.Any(m => m.StartsWith(text[lt..], StringComparison.Ordinal))) safe = lt;
                    }
                    Emit(output, text[..safe], final);
                    _hold.Remove(0, safe);
                    break;
                }
                Emit(output, text[..at], false);
                _hold.Remove(0, at + mark!.Length);
                switch (mark)
                {
                    case "<|channel>": _mode = "thought"; _fresh = true; break;
                    case "<channel|>": _mode = "text"; break;
                    case "<|tool_call>": _mode = "tool"; break;
                    case "<tool_call|>": _mode = "text"; break;
                }
            }
            return output;
        }

        private readonly StringBuilder _lead = new();

        private void Emit(List<(string, string)> output, string text, bool final)
        {
            if (_mode == "tool") return;
            if (_mode == "thought" && _fresh)
            {
                // "thought\n" names the channel; it may come in pieces
                _lead.Append(text);
                var lead = _lead.ToString();
                if (!final && "thought\n".StartsWith(lead, StringComparison.Ordinal)) return;
                _fresh = false;
                _lead.Clear();
                text = lead.StartsWith("thought\n", StringComparison.Ordinal) ? lead[8..] : lead;
            }
            if (text.Length == 0) return;
            output.Add((_mode == "thought" ? "reasoning" : "text", text));
        }
    }
}
