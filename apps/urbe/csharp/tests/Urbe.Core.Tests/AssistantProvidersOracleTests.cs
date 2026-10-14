using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The provider layer of the 1.8.4's assistant (src/ai/providers.js), recorded by csharp/tools/assistant-providers-oracle.mjs
/// with a scripted server: the requests sent (URL, method, headers, body), the stream events, the normalized result or
/// the error, retries and the models list must be the original's.
/// </summary>
public sealed class AssistantProvidersOracleTests
{
    private sealed class Script(JsonArray responses) : HttpMessageHandler
    {
        private int _next;
        public List<JsonObject> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new JsonObject();
            if (request.Content is { } content) headers["Content-Type"] = content.Headers.ContentType?.ToString();
            foreach (var h in request.Headers) headers[h.Key] = string.Join(",", h.Value);
            Seen.Add(new JsonObject
            {
                ["url"] = request.RequestUri!.ToString(), ["method"] = request.Method.Method, ["headers"] = headers,
                ["body"] = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))
            });
            if (_next >= responses.Count) throw new InvalidOperationException("no response scripted");
            var r = (JsonObject)responses[_next++]!;
            if (r["network"]?.GetValue<bool>() == true) throw new HttpRequestException("Failed to fetch");
            var body = r["body"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : (r["body"] ?? "").ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var res = new HttpResponseMessage((HttpStatusCode)r["status"]!.GetValue<int>())
            {
                Content = new StreamContent(new Chunked(Encoding.UTF8.GetBytes(body), r["chunk"]?.GetValue<int>() ?? 7))
            };
            res.Content.Headers.ContentType = null;
            if (r["headers"] is JsonObject hs)
                foreach (var (k, hv) in hs)
                {
                    if (k == "content-type") res.Content.Headers.TryAddWithoutValidation("Content-Type", (string)hv!);
                    else res.Headers.TryAddWithoutValidation(k, (string)hv!);
                }
            return res;
        }
    }

    /// <summary>The body in small pieces, cut in the middle of lines and of UTF-8 characters.</summary>
    private sealed class Chunked(byte[] bytes, int size) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, size));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer[..Math.Min(buffer.Length, size)], cancellationToken);
    }

    private static JsonDocument Fixture() =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "assistant", "providers-run-a.json")));

    public static TheoryData<string> Scenarios()
    {
        using var json = Fixture();
        var data = new TheoryData<string>();
        foreach (var s in json.RootElement.GetProperty("scenarios").EnumerateArray()) data.Add(s.GetProperty("name").GetString()!);
        return data;
    }

    private static AiProviderConfig Config(JsonNode c) => new((string?)c["id"] ?? "", (string?)c["preset"] ?? "", (string?)c["name"], (string?)c["apiKey"], (string?)c["baseUrl"], (string?)c["kind"]);

    private static AiBlock Block(JsonNode b) => (string)b["type"]! switch
    {
        "text" => AiBlock.TextBlock((string)b["text"]!),
        "tool_use" => AiBlock.ToolUse((string)b["id"]!, (string)b["name"]!, b["input"]?.DeepClone()),
        _ => AiBlock.ToolResult((string)b["tool_use_id"]!, (string)b["content"]!, (bool)b["is_error"]!)
    };

    private static AiRequest Request(JsonNode r) => new(
        (string)r["model"]!,
        r["messages"]!.AsArray().Select(m => new AiMessage((string)m!["role"]!, m["content"]!.AsArray().Select(b => Block(b!)).ToList())).ToList(),
        (string?)r["system"],
        r["tools"]?.AsArray().Select(t => new AiToolSchema((string)t!["name"]!, (string)t["description"]!, (JsonObject)t["parameters"]!.DeepClone())).ToList(),
        r["maxTokens"] is { } mt ? (int)mt : null,
        r["temperature"] is { } temp ? (double)temp : null);

    private static JsonObject BlockJson(AiBlock b) => b.Type switch
    {
        "text" => new JsonObject { ["type"] = "text", ["text"] = b.Text },
        "tool_use" => new JsonObject { ["type"] = "tool_use", ["id"] = b.Id, ["name"] = b.Name, ["input"] = b.Input?.DeepClone() },
        _ => new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = b.ToolUseId, ["content"] = b.Content, ["is_error"] = b.IsError }
    };

    private static string S(JsonNode? n) => LegacyCustomize.Stringify(n);

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task TheStreamIsTheOriginals(string name)
    {
        using var json = Fixture();
        var s = JsonNode.Parse(json.RootElement.GetProperty("scenarios").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name).GetRawText())!;
        var script = new Script((JsonArray)s["responses"]!.DeepClone());
        var waits = new List<TimeSpan>();
        var providers = new AssistantProviders(script, (t, _) => { waits.Add(t); return Task.CompletedTask; });
        var events = new JsonArray();
        AiResult? result = null;
        AiProviderException? error = null;
        try
        {
            result = await providers.StreamAsync(Config(s["cfg"]!), Request(s["req"]!), e =>
            {
                var o = new JsonObject { ["type"] = e.Type };
                if (e.Delta is not null) o["delta"] = e.Delta;
                if (e.Index is not null) o["index"] = e.Index;
                if (e.Name is not null) o["name"] = e.Name;
                if (e.Partial is not null) o["partial"] = e.Partial;
                events.Add(o);
            }, TestContext.Current.CancellationToken);
        }
        catch (AiProviderException e) { error = e; }

        // what was sent
        var expectedRequests = s["requests"]!.AsArray();
        Assert.Equal(expectedRequests.Count, script.Seen.Count);
        for (int i = 0; i < expectedRequests.Count; i++)
        {
            var e = expectedRequests[i]!;
            var a = script.Seen[i];
            Assert.Equal((string)e["url"]!, (string)a["url"]!);
            Assert.Equal((string)e["method"]!, (string)a["method"]!);
            Assert.Equal(S(e["body"]), S(a["body"]));
            foreach (var (k, v) in (JsonObject)e["headers"]!)
                if (!(k == "Content-Type" && (string)e["method"]! == "GET")) Assert.Equal((string?)v, (string?)a["headers"]![k]);
        }
        Assert.Equal(S(s["events"]), S(events));
        if (s["error"] is JsonObject err)
        {
            Assert.NotNull(error);
            Assert.Equal((string)err["message"]!, error!.Message);
            Assert.Equal((int?)err["status"], error.Status);
            Assert.Equal((bool)err["network"]!, error.Network);
        }
        else
        {
            Assert.Null(error);
            var r = s["result"]!;
            Assert.Equal(S(r["content"]), S(new JsonArray(result!.Content.Select(b => (JsonNode)BlockJson(b)).ToArray())));
            Assert.Equal((string)r["reasoning"]!, result.Reasoning);
            Assert.Equal((string)r["stopReason"]!, result.StopReason);
            Assert.Equal((string)r["model"]!, result.Model);
            Assert.Equal((long)r["usage"]!["input"]!, result.Usage.Input);
            Assert.Equal((long)r["usage"]!["output"]!, result.Usage.Output);
            Assert.Equal((double?)r["usage"]!["cost"], result.Usage.Cost);
        }
        if (name == "http-429-then-ok") Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)], waits);   // retry-after, then 1000·2^1
    }

    [Fact]
    public async Task TheModelsAreTheOriginals()
    {
        using var json = Fixture();
        foreach (var m in JsonNode.Parse(json.RootElement.GetProperty("models").GetRawText())!.AsArray())
        {
            var script = new Script((JsonArray)m!["responses"]!.DeepClone());
            var providers = new AssistantProviders(script, (_, _) => Task.CompletedTask);
            List<AiModelInfo>? list = null;
            AiProviderException? error = null;
            try { list = await providers.ListModelsAsync(Config(m["cfg"]!), TestContext.Current.CancellationToken); }
            catch (AiProviderException e) { error = e; }
            Assert.Equal((string)m["requests"]![0]!["url"]!, (string)script.Seen[0]["url"]!);
            if (m["error"] is JsonObject err) { Assert.Equal((string)err["message"]!, error!.Message); continue; }
            var expected = m["list"]!.AsArray();
            Assert.Equal(expected.Count, list!.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i]!;
                var a = list[i];
                Assert.Equal(((string)e["id"]!, (string)e["name"]!, (long?)e["context"], (bool?)e["tools"]), (a.Id, a.Name, a.Context, a.Tools));
                if (e["price"] is JsonObject price) Assert.Equal(((double)price["input"]!, (double)price["output"]!), a.Price!.Value);
                else Assert.Null(a.Price);
            }
        }
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
