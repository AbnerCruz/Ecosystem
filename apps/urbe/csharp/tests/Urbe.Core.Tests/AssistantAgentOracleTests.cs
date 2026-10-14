using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The agent loop of the 1.8.4's assistant (src/ai/agent.js) with the real tools and a scripted model, recorded by
/// csharp/tools/assistant-agent-oracle.mjs: what is sent to the model at each step, the events the screen gets, the
/// approvals asked, the conversation after, the notes after and how the run ended must be the original's; and
/// prepare(), systemPrompt(), titleFrom(), the agents and their tool policy on their own.
/// </summary>
public sealed class AssistantAgentOracleTests
{
    private sealed class Host : IAssistantHost
    {
        private int _seq = 100;
        public Host(JsonArray notes)
        {
            int i = 0;
            foreach (var n in notes)
                Documents.Upsert(new DocumentInput { Id = "doc_" + i++, Path = (string)n!["path"]!, Content = (string)n["content"]! });
            Knowledge = new KnowledgeIndex(Documents);
        }
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public List<string> Memory { get; } = [];
        public List<string> Opened { get; } = [];
        public int ReadChars => 2000;
        public (string? Path, string? Selection) EditorContext() => ("Comece.md", "");
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = "doc_" + _seq++, Path = path, Content = content }));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = content, Created = doc.Created, Modified = doc.Modified }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = newPath, Content = doc.Content, Created = doc.Created, Modified = doc.Modified }));
        public Task TrashNoteAsync(UrbeDocument doc) { Documents.Remove(doc.Id); return Task.CompletedTask; }
        public Task CreateFolderAsync(string path) => Task.CompletedTask;
        public void OpenNote(string path) => Opened.Add(path);
        public void Remember(string fact) => Memory.Add(fact);
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static JsonNode Fixture() =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "assistant", "agent-run-a.json")))!;

    /// <summary>JSON.stringify with the keys sorted, so the order fields were set in does not matter.</summary>
    private static string S(JsonNode? n) => LegacyCustomize.Stringify(Canon(n));

    private static JsonNode? Canon(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Canon(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Canon).ToArray()),
        _ => n?.DeepClone()
    };

    private static AiBlock Block(JsonNode b) => (string)b["type"]! switch
    {
        "text" => AiBlock.TextBlock((string)b["text"]!),
        "tool_use" => AiBlock.ToolUse((string)b["id"]!, (string)b["name"]!, b["input"]?.DeepClone()),
        _ => AiBlock.ToolResult((string)b["tool_use_id"]!, (string)b["content"]!, (bool)b["is_error"]!)
    };

    private static JsonObject BlockJson(AiBlock b) => b.Type switch
    {
        "text" => new JsonObject { ["type"] = "text", ["text"] = b.Text },
        "tool_use" => new JsonObject { ["type"] = "tool_use", ["id"] = b.Id, ["name"] = b.Name, ["input"] = b.Input?.DeepClone() },
        _ => new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = b.ToolUseId, ["content"] = b.Content, ["is_error"] = b.IsError }
    };

    private static AiUsage Usage(JsonNode? u) => u is null ? new AiUsage(10, 5, null) : new AiUsage((long)u["input"]!, (long)u["output"]!, (double?)u["cost"]);

    private static JsonObject Snap(AiChatMessage m) => new()
    {
        ["role"] = m.Role,
        ["text"] = string.Join("|", m.Content.Where(b => b.Type == "text").Select(b => b.Text)),
        ["tools"] = string.Join(",", m.Content.Where(b => b.Type == "tool_use").Select(b => b.Name + (b.Pending ? "*" : "") + (b.Partial is { Length: > 0 } ? "~" : ""))),
        ["reasoning"] = string.IsNullOrEmpty(m.Meta?.Reasoning) ? null : m.Meta.Reasoning
    };

    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var s in Fixture()["scenarios"]!.AsArray()) data.Add((string)s!["name"]!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task EachRunIsTheOriginals(string name)
    {
        var f = Fixture();
        var s = f["scenarios"]!.AsArray().Single(x => (string)x!["name"]! == name)!;
        var host = new Host(f["notes"]!.AsArray());
        var script = new Queue<JsonNode>(s["script"]!.AsArray().Select(x => x!.DeepClone()));
        var approvals = new Queue<JsonNode?>(s["approve"]!.AsArray().Select(x => x?.DeepClone()));
        var requests = new JsonArray();
        var events = new JsonArray();
        using var cts = new CancellationTokenSource();
        var messages = (s["history"] as JsonArray ?? [new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "Pedido do usuário" }) }])
            .Select((m, i) => { var c = AiChatMessage.FromJson(m!); c.Id = "h_" + i; return c; }).ToList();
        var memory = s["memory"] is JsonArray mem ? mem.Select(x => (string)x!).ToList() : null;

        var result = await AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById((string)s["agent"]!), Policy = (string)s["policy"]!, Messages = messages, Host = host, Cancel = cts.Token,
            Provider = new AiProviderConfig("p1", ""), Model = "m-1", VaultName = "Cofre", Now = new DateTime(2025, 10, 9, 15, 0, 0),
            ContextTokens = (long?)s["contextTokens"], Instructions = (string?)s["instructions"], Memory = memory, MaxSteps = (int?)s["maxSteps"],
            Temperature = (double?)s["temperature"], MaxTokens = (int?)s["maxTokens"],
            Stream = (cfg, req, onEvent, cancel) =>
            {
                requests.Add(new JsonObject
                {
                    ["provider"] = cfg.Id, ["model"] = req.Model, ["system"] = req.System,
                    ["messages"] = new JsonArray(req.Messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = new JsonArray(m.Content.Select(b => (JsonNode)BlockJson(b)).ToArray()) }).ToArray()),
                    ["tools"] = new JsonArray(req.Tools!.Select(t => (JsonNode)JsonValue.Create(t.Name)).ToArray()), ["maxTokens"] = req.MaxTokens, ["temperature"] = req.Temperature
                });
                if (!script.TryDequeue(out var r)) throw new InvalidOperationException("no response scripted");
                foreach (var e in r["events"] as JsonArray ?? [])
                    onEvent(new AiStreamEvent((string)e!["type"]!, (string?)e["delta"], (int?)e["index"], (string?)e["name"], (string?)e["partial"]));
                if (cancel.IsCancellationRequested) throw new OperationCanceledException(cancel);
                if (r["error"] is { } err)
                {
                    if ((bool?)r["abort"] == true) throw new OperationCanceledException();
                    throw new AiProviderException((string)err!);
                }
                return Task.FromResult(new AiResult(r["content"]!.AsArray().Select(b => Block(b!)).ToList(), (string?)r["reasoning"] ?? "", (string?)r["stopReason"] ?? "end", Usage(r["usage"]), (string?)r["model"] ?? "m-1"));
            },
            Approve = (req, _) =>
            {
                events.Add(new JsonObject
                {
                    ["type"] = "approve", ["name"] = req.Call.Name, ["label"] = req.Label, ["access"] = req.Access,
                    ["preview"] = req.Preview is not { } p ? null : Preview(p)
                });
                var a = approvals.TryDequeue(out var x) ? x : null;
                if (a is JsonValue v && (string)v! == "abort") { cts.Cancel(); return Task.FromResult(AiApproval.Approve); }
                if (a is JsonObject o && o["reason"] is { } reason) return Task.FromResult(AiApproval.Reject((string)reason!));
                return Task.FromResult(a is JsonValue av && (string)av! == "approve_all" ? AiApproval.ApproveAll : AiApproval.Approve);
            },
            OnUpdate = e =>
            {
                var o = new JsonObject { ["type"] = e.Type };
                if (e.Message is not null) foreach (var (k, v) in Snap(e.Message)) o[k] = v?.DeepClone();
                if (e.Call is not null) o["call"] = e.Call.Name;
                if (e.Status is not null) o["status"] = e.Status;
                if (e.Result is not null) o["result"] = new JsonObject { ["content"] = e.Result.Content, ["is_error"] = e.Result.IsError, ["status"] = e.Result.Status, ["changes"] = e.Result.Changes };
                if (e.Steps is not null) o["steps"] = new JsonArray(e.Steps.Select(x => (JsonNode)new JsonObject { ["step"] = x.Step, ["status"] = x.Status }).ToArray());
                if (e.Text is not null) o["text"] = e.Text;
                if (e.Usage is not null) o["usage"] = AssistantAgent.UsageJson(e.Usage);
                events.Add(o);
            }
        });

        var expectedRequests = s["requests"]!.AsArray();
        Assert.Equal(expectedRequests.Count, requests.Count);
        for (int i = 0; i < expectedRequests.Count; i++)
        {
            var e = expectedRequests[i]!;
            var a = requests[i]!;
            Assert.Equal((string)e["system"]!, (string)a["system"]!);
            Assert.Equal(S(e["tools"]), S(a["tools"]));
            Assert.Equal(S(e["messages"]), S(a["messages"]));
            Assert.Equal(S(e), S(a));
        }
        var expectedEvents = s["events"]!.AsArray();
        for (int i = 0; i < Math.Min(expectedEvents.Count, events.Count); i++) Assert.Equal(S(expectedEvents[i]), S(events[i]));
        Assert.Equal(expectedEvents.Count, events.Count);
        Assert.Equal(S(s["messages"]), S(new JsonArray(messages.Select(m => (JsonNode)m.ToJson(withId: false)).ToArray())));

        var r0 = s["result"]!;
        Assert.Equal(S(r0["changes"]), S(new JsonArray(result.Changes.Select(x =>
        {
            var o = new JsonObject { ["id"] = x.Id, ["before"] = Note(x.Before), ["after"] = Note(x.After) };
            if (x.Trashed) o["trashed"] = true;
            return (JsonNode)o;
        }).ToArray())));
        Assert.Equal(S(r0["usage"]), S(AssistantAgent.UsageJson(result.Usage)));
        Assert.Equal(((int)r0["steps"]!, (string)r0["stopped"]!, (string?)r0["error"]), (result.Steps, result.Stopped, result.Error));
        Assert.Equal(S(r0["plan"]), result.Plan is null ? "null" : S(new JsonArray(result.Plan.Select(x => (JsonNode)new JsonObject { ["step"] = x.Step, ["status"] = x.Status }).ToArray())));
        Assert.Equal(s["notes"]!.AsArray().Select(n => (string)n!["path"]! + "\n" + (string)n["content"]!).Order(StringComparer.Ordinal),
            host.Documents.List().Select(d => d.Path + "\n" + d.Content).Order(StringComparer.Ordinal));
        Assert.Equal(s["remembered"]!.AsArray().Select(x => (string)x!), host.Memory);
    }

    private static JsonObject Preview(AiPreview p)
    {
        var o = new JsonObject { ["path"] = p.Path, ["before"] = p.Before, ["after"] = p.After };
        if (p.Note is not null) o["note"] = p.Note;
        return o;
    }

    private static JsonNode? Note(AiNoteSnap? s) => s is null ? null : new JsonObject { ["path"] = s.Path, ["content"] = s.Content };

    [Fact]
    public void PrepareIsTheOriginals()
    {
        foreach (var p in Fixture()["prepares"]!.AsArray())
        {
            var messages = p!["messages"]!.AsArray().Select(m => AiChatMessage.FromJson(m!)).ToList();
            var output = AssistantAgent.Prepare(messages, (int)p["budget"]!);
            Assert.Equal(S(p["out"]), S(new JsonArray(output.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = new JsonArray(m.Content.Select(b => (JsonNode)BlockJson(b)).ToArray()) }).ToArray())));
        }
    }

    [Fact]
    public void PromptsTitlesAgentsAndPolicyAreTheOriginals()
    {
        var f = Fixture();
        foreach (var p in f["prompts"]!.AsArray())
        {
            var mem = p!["memory"]!.AsArray().Select(x => (string)x!).ToList();
            Assert.Equal((string)p["out"]!, AssistantAgent.SystemPrompt(AssistantAgent.AgentById((string)p["agent"]!), (string)p["policy"]!, (string?)p["vaultName"],
                (int)p["noteCount"]!, (string?)p["openNote"], (string?)p["instructions"], mem, new DateTime(2025, 10, 9, 15, 0, 0)));
        }
        foreach (var t in f["titles"]!.AsArray()) Assert.Equal((string)t!["out"]!, AssistantAgent.TitleFrom((string?)t["text"]));
        var agents = f["agents"]!.AsArray();
        Assert.Equal(agents.Count, AssistantAgent.Agents.Count);
        for (int i = 0; i < agents.Count; i++)
        {
            var a = agents[i]!;
            var mine = AssistantAgent.Agents[i];
            Assert.Equal(((string)a["id"]!, (string)a["name"]!, (string)a["icon"]!, (string)a["description"]!, (string)a["tools"]!, (bool?)a["delegate"] ?? false, (string)a["prompt"]!),
                (mine.Id, mine.Name, mine.Icon, mine.Description, mine.Tools, mine.Delegate, mine.Prompt));
        }
        foreach (var x in f["allow"]!.AsArray())
        {
            var agent = AssistantAgent.AgentById((string)x!["agent"]!);
            Assert.Equal((bool)x["allow"]!, AssistantAgent.AllowTool(agent, (string)x["policy"]!, (string)x["access"]!));
            Assert.Equal((bool)x["approval"]![0]!, AssistantAgent.NeedsApproval((string)x["access"]!, (string)x["policy"]!, false));
            Assert.Equal((bool)x["approval"]![1]!, AssistantAgent.NeedsApproval((string)x["access"]!, (string)x["policy"]!, true));
        }
        Assert.Equal("Pesquisador: " + new string('a', 70), AssistantAgent.Delegate.Label(new JsonObject { ["task"] = new string('a', 90) }));
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
