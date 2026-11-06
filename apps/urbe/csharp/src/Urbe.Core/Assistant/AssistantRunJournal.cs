using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>What a run is about when it starts: the record its journal begins with (CST-216).</summary>
public sealed record AiRunStart(string RunId, string ConversationId, string Vault, string AgentId, string Model, string Goal,
    int BaseMessages, AiChatMessage? Request = null, IReadOnlyList<(string Path, string? Sha)>? Sources = null,
    string? Scope = null, string? Expected = null, string? OriginConversationId = null);

/// <summary>What happened to one action of an interrupted run, as checked against the vault now.</summary>
public sealed record AiRunEffect(string CallId, string Tool, string? Path, string State, string Detail)
{
    /// <summary>Done before the interruption and verified (or its result was recorded).</summary>
    public const string Done = "done";
    /// <summary>Never carried out: nothing changed.</summary>
    public const string NotDone = "not_done";
    /// <summary>Cut while it ran and impossible to verify: the person decides.</summary>
    public const string Uncertain = "uncertain";
}

/// <summary>
/// CST-216: the operational state of one run of an agent, as an append-only log on this device (never in the vault):
/// the goal, the agent and model, the inputs with their revisions, each model step, each tool call (id, name, hash of
/// its input), the person's decisions, what each effect wrote (path and revision) and how the run ended. It is the
/// authority for <em>run state and effects</em>; the conversation stays the authority for the transcript. After the
/// process dies, it tells what was done, what was not and what cannot be verified, so a resumed run never repeats an
/// effect (idempotency key: run id + tool call id; and, across a restart, tool + input hash).
/// </summary>
public sealed class AssistantRunJournal
{
    public const int SchemaVersion = 1;
    public const string Prefix = "execucao-";
    private const int ReadResultChars = 4000;

    private static readonly HashSet<string> ReadAccess = ["read", "ui", "memory", "plan", "network"];
    private static readonly HashSet<string> Agents = ["consult_agent", "delegate_to_agent"];
    private static readonly JsonSerializerOptions Line = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly IAssistantStorage _storage;
    private readonly List<JsonObject> _entries;
    private readonly object _gate = new();

    public string RunId { get; }
    /// <summary>1 for the first execution; each resume adds one.</summary>
    public int Segment { get; private set; }
    /// <summary>False once a line could not be written: a restart would not find everything.</summary>
    public bool Healthy { get; private set; } = true;
    public IReadOnlyList<JsonObject> Entries { get { lock (_gate) return _entries.ToList(); } }

    private AssistantRunJournal(IAssistantStorage storage, string runId, List<JsonObject> entries, int segment)
    {
        _storage = storage; RunId = runId; _entries = entries; Segment = segment;
    }

    public static string FileOf(string runId) => Prefix + string.Concat(runId.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_')) + ".jsonl";

    /// <summary>A new run's journal, with the start record.</summary>
    public static AssistantRunJournal Begin(IAssistantStorage storage, AiRunStart start, long? now = null)
    {
        var journal = new AssistantRunJournal(storage, start.RunId, [], 1);
        var begin = new JsonObject
        {
            ["t"] = "begin", ["run"] = start.RunId, ["conversation"] = start.ConversationId, ["vault"] = start.Vault,
            ["agent"] = start.AgentId, ["model"] = start.Model, ["goal"] = start.Goal, ["base"] = start.BaseMessages,
            ["at"] = now ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        if (start.Request is not null) begin["request"] = start.Request.ToJson();
        if (start.Sources is { Count: > 0 } sources)
            begin["sources"] = new JsonArray(sources.Select(s => (JsonNode)new JsonObject { ["path"] = s.Path, ["sha"] = s.Sha }).ToArray());
        if (start.Scope is not null) begin["scope"] = start.Scope;
        if (start.Expected is not null) begin["expected"] = start.Expected;
        if (start.OriginConversationId is not null) begin["origin"] = start.OriginConversationId;
        journal.Append(begin);
        return journal;
    }

    /// <summary>
    /// The journal of a run as stored; null when there is none. A line cut by the process dying is ignored; a journal
    /// from a newer app (schema version) is never read nor written.
    /// </summary>
    public static AssistantRunJournal? Open(IAssistantStorage storage, string runId) =>
        Parse(storage, storage.Read(FileOf(runId)), out var journal) ? journal : null;

    private static bool Parse(IAssistantStorage storage, string? text, out AssistantRunJournal? journal)
    {
        journal = null;
        if (string.IsNullOrEmpty(text)) return false;
        var entries = new List<JsonObject>();
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            JsonObject? o;
            try { o = JsonNode.Parse(line) as JsonObject; }
            catch (JsonException) { continue; }   // the last line, cut while being written
            if (o is null) continue;
            if (Num(o["v"]) > SchemaVersion) return false;
            entries.Add(o);
        }
        if (entries.Count == 0 || Str(entries[0]["t"]) != "begin") return false;
        int segment = 1 + entries.Count(e => Str(e["t"]) == "resume");
        journal = new AssistantRunJournal(storage, Str(entries[0]["run"]), entries, segment);
        return true;
    }

    /// <summary>Every journal on the device that can be read.</summary>
    public static IReadOnlyList<AssistantRunJournal> All(IAssistantStorage storage)
    {
        var list = new List<AssistantRunJournal>();
        foreach (var name in storage.List().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".jsonl", StringComparison.Ordinal)))
            if (Parse(storage, storage.Read(name), out var journal) && journal is not null) list.Add(journal);
        return list;
    }

    /// <summary>Runs that neither finished nor were closed by the person: what a restart must account for.</summary>
    public static IReadOnlyList<AssistantRunJournal> Interrupted(IAssistantStorage storage, string? vault = null) =>
        All(storage).Where(j => !j.Closed && (vault is null || j.Vault == vault)).OrderByDescending(j => j.Started).ToList();

    /// <summary>
    /// Keeps the journals of runs still interrupted and the <paramref name="keep"/> most recent closed ones; the older
    /// closed ones go (their conversation keeps the transcript and the run record).
    /// </summary>
    public static void Prune(IAssistantStorage storage, int keep = 30)
    {
        foreach (var old in All(storage).Where(j => j.Closed).OrderByDescending(j => j.Started).Skip(keep))
            storage.Delete(FileOf(old.RunId));
    }

    /// <summary>The journals of an agent's runs, removed with its conversations (verified).</summary>
    public static void DeleteFor(IAssistantStorage storage, string agentId)
    {
        foreach (var j in All(storage).Where(j => j.AgentId == agentId))
        {
            storage.Delete(FileOf(j.RunId));
            if (storage.Read(FileOf(j.RunId)) is not null) throw new IOException("Não foi possível excluir o diário de execução " + j.RunId);
        }
    }

    // ---------------------------------------------------------------- what the journal says

    private JsonObject First { get { lock (_gate) return _entries[0]; } }
    public string ConversationId => Str(First["conversation"]);
    public string Vault => Str(First["vault"]);
    public string AgentId => Str(First["agent"]);
    public string Model => Str(First["model"]);
    public string Goal => Str(First["goal"]);
    public int BaseMessages => (int)Num(First["base"]);
    public long Started => Num(First["at"]);
    public string? Origin => First["origin"] is null ? null : Str(First["origin"]);
    public AiChatMessage? RequestMessage => First["request"] is JsonObject r ? AiChatMessage.FromJson(r) : null;
    /// <summary>The run loop ended (the transcript may still need to be saved).</summary>
    public bool Finished => Last("finished") is not null;
    /// <summary>The run ended and its conversation was saved, or the person closed it.</summary>
    public bool Closed => Last("end") is not null;
    public string? Stopped => Last("finished") is { } f ? Str(f["stopped"]) : null;

    private JsonObject? Last(string type) { lock (_gate) return _entries.LastOrDefault(e => Str(e["t"]) == type); }

    /// <summary>The model's usage recorded in the journal (what was already paid for).</summary>
    public AiUsage Usage
    {
        get
        {
            var total = new AiUsage(0, 0, null);
            foreach (var r in Entries.Where(e => Str(e["t"]) == "response" && e["usage"] is JsonObject))
                total = AiUsage.Add(total, AiUsage.FromJson((JsonObject)r["usage"]!));
            return total;
        }
    }

    /// <summary>A model request was sent and its answer never recorded: it may have been charged without a result.</summary>
    public bool ModelCallCut
    {
        get
        {
            var e = Entries.ToList();
            int lastRequest = e.FindLastIndex(x => Str(x["t"]) == "request");
            return lastRequest >= 0 && !e.Skip(lastRequest + 1).Any(x => Str(x["t"]) == "response");
        }
    }

    // ---------------------------------------------------------------- writing

    private void Append(JsonObject entry)
    {
        entry["v"] = SchemaVersion;
        entry["seg"] = Segment;
        var line = entry.ToJsonString(Line) + "\n";
        lock (_gate)
        {
            _entries.Add(entry);
            if (!_storage.Append(FileOf(RunId), line)) Healthy = false;
        }
    }

    public void Request(int step) => Append(new JsonObject { ["t"] = "request", ["step"] = step });

    public void Response(int step, AiChatMessage message, AiUsage? usage)
    {
        var o = new JsonObject { ["t"] = "response", ["step"] = step, ["message"] = message.ToJson() };
        if (usage is not null) o["usage"] = AssistantAgent.UsageJson(usage);
        Append(o);
    }

    public void Approval(string callId, string kind) => Append(new JsonObject { ["t"] = "approval", ["call"] = callId, ["decision"] = kind });

    /// <summary>
    /// Written right before an action that changes something starts: the note it touches and its revision now, and the
    /// revision it will have when done (null: not knowable beforehand; <see cref="Absent"/>: the note will not exist there).
    /// </summary>
    public void Intent(string callId, string tool, string inputHash, string? path, string? before, string? targetPath, string? after)
    {
        var o = new JsonObject { ["t"] = "intent", ["call"] = callId, ["tool"] = tool, ["input"] = inputHash };
        if (path is not null) o["path"] = path;
        o["before"] = before;
        if (targetPath is not null && targetPath != path) o["target"] = targetPath;
        o["after"] = after;
        Append(o);
    }

    public const string Absent = "absent";

    /// <summary>The tool's result: the receipt of what it did (written notes with their revisions) and what it read.</summary>
    public void Result(string callId, string tool, string access, string inputHash, string content, bool isError, string? status,
        IReadOnlyList<AiChange>? changes, IEnumerable<(string Path, string? Sha)>? seen)
    {
        bool reads = ReadAccess.Contains(access) && !Agents.Contains(tool);
        var o = new JsonObject
        {
            ["t"] = "result", ["call"] = callId, ["tool"] = tool, ["input"] = inputHash, ["effect"] = !reads,
            ["content"] = reads && content.Length > ReadResultChars
                ? content[..ReadResultChars] + "\n…[leitura encurtada no diário da execução; leia a fonte de novo se precisar do restante]" : content,
            ["error"] = isError
        };
        if (status is not null) o["status"] = status;
        if (changes is { Count: > 0 })
            o["changes"] = new JsonArray(changes.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["before"] = c.Before?.Path, ["beforeSha"] = Sha(c.Before?.Content), ["after"] = c.After?.Path,
                ["afterSha"] = Sha(c.After?.Content), ["trashed"] = c.Trashed
            }).ToArray());
        if (AssistantImageTools.SavedImagePath(content) is { } image) o["image"] = image;
        var sources = seen?.Where(s => s.Path.Length > 0).DistinctBy(s => s.Path).ToList();
        if (sources is { Count: > 0 })
            o["seen"] = new JsonArray(sources.Select(s => (JsonNode)new JsonObject { ["path"] = s.Path, ["sha"] = s.Sha }).ToArray());
        Append(o);
    }

    /// <summary>The run loop ended (how).</summary>
    public void Finish(string stopped, string? error) => Append(new JsonObject { ["t"] = "finished", ["stopped"] = stopped, ["error"] = error });

    /// <summary>
    /// Closed: the run ended and its conversation was saved (<c>ended</c>), or the person chose what to do with an
    /// interrupted one (<c>reopened</c>, <c>discarded</c>). A closed journal is not offered again.
    /// </summary>
    public void Close(string reason) => Append(new JsonObject { ["t"] = "end", ["reason"] = reason, ["at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

    /// <summary>A new execution of the same run starts (Retomar): the note the model is given with it is kept too.</summary>
    public void Resume(AiChatMessage note)
    {
        Segment++;
        Append(new JsonObject { ["t"] = "resume", ["message"] = note.ToJson(), ["at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }

    // ---------------------------------------------------------------- idempotency

    /// <summary>The recorded result of this tool call of this run, if it already ran (key: run id + call id).</summary>
    public JsonObject? Completed(string callId)
    {
        lock (_gate) return _entries.LastOrDefault(e => Str(e["t"]) == "result" && Str(e["call"]) == callId);
    }

    /// <summary>
    /// Across a restart: an action with the same tool and input that changed something in an earlier execution of this
    /// run. Its state (done or uncertain) decides: done is not repeated; uncertain is not repeated without the person.
    /// </summary>
    public AiRunEffect? EarlierEffect(string tool, string inputHash, DocumentStore documents)
    {
        List<JsonObject> earlier;
        lock (_gate) earlier = _entries.Where(e => (int)Num(e["seg"]) < Segment).ToList();
        var done = earlier.LastOrDefault(e => Str(e["t"]) == "result" && e["effect"] is JsonValue fx && fx.GetValue<bool>() &&
            Str(e["tool"]) == tool && Str(e["input"]) == inputHash && !(e["error"] is JsonValue err && err.GetValue<bool>()));
        if (done is not null) return new AiRunEffect(Str(done["call"]), tool, null, AiRunEffect.Done, Str(done["content"]));
        foreach (var intent in earlier.Where(e => Str(e["t"]) == "intent" && Str(e["tool"]) == tool && Str(e["input"]) == inputHash))
            if (!earlier.Any(e => Str(e["t"]) == "result" && Str(e["call"]) == Str(intent["call"])) && Classify(intent, documents) is { State: not AiRunEffect.NotDone } state)
                return state;
        return null;
    }

    // ---------------------------------------------------------------- after a restart

    /// <summary>
    /// What each action of the run left, checked against the vault now: recorded results are done; an action that had
    /// started (intent) without a result is done if the note is at the revision it was to have, not done if it is still
    /// at the revision it had, and uncertain otherwise; an action asked for but never started was not done.
    /// </summary>
    public IReadOnlyList<AiRunEffect> Effects(DocumentStore documents)
    {
        var entries = Entries;
        var effects = new List<AiRunEffect>();
        var results = entries.Where(e => Str(e["t"]) == "result").GroupBy(e => Str(e["call"])).ToDictionary(g => g.Key, g => g.Last());
        var intents = entries.Where(e => Str(e["t"]) == "intent").GroupBy(e => Str(e["call"])).ToDictionary(g => g.Key, g => g.Last());
        foreach (var call in Calls(entries))
        {
            if (results.TryGetValue(call.Id, out var r))
            {
                if (r["effect"] is JsonValue fx && fx.GetValue<bool>() && !(r["error"] is JsonValue err && err.GetValue<bool>()))
                    effects.Add(new AiRunEffect(call.Id, call.Name, ChangedPath(r), AiRunEffect.Done, Receipt(r)));
                continue;
            }
            if (intents.TryGetValue(call.Id, out var intent)) effects.Add(Classify(intent, documents));
            else effects.Add(new AiRunEffect(call.Id, call.Name, (call.Input as JsonObject)?["path"]?.ToString(), AiRunEffect.NotDone, "não concluída"));
        }
        return effects;
    }

    /// <summary>Something cannot be verified: the person must look before the work goes on.</summary>
    public bool NeedsYou(DocumentStore documents) => Effects(documents).Any(e => e.State == AiRunEffect.Uncertain);

    private static IEnumerable<AiChatBlock> Calls(IEnumerable<JsonObject> entries) =>
        entries.Where(e => Str(e["t"]) == "response" && e["message"] is JsonObject)
            .SelectMany(e => AiChatMessage.FromJson(e["message"]!).Content.Where(b => b.Type == "tool_use" && b.Id.Length > 0));

    private static string? ChangedPath(JsonObject result) =>
        result["image"] is { } image ? Str(image) : (result["changes"] as JsonArray)?.OfType<JsonObject>().Select(c => Str(c["after"]) is { Length: > 0 } a ? a : Str(c["before"])).FirstOrDefault();

    private static string Receipt(JsonObject result)
    {
        var parts = new List<string>();
        if (result["image"] is { } image) parts.Add("imagem " + Str(image));
        foreach (var c in (result["changes"] as JsonArray ?? []).OfType<JsonObject>())
            parts.Add((c["trashed"] is JsonValue t && t.GetValue<bool>() ? "lixeira " + Str(c["before"]) : Str(c["after"]) is { Length: > 0 } after
                ? after + " (revisão " + Short(Str(c["afterSha"])) + ")" : "removida " + Str(c["before"])));
        return parts.Count > 0 ? string.Join("; ", parts) : Str(result["content"]) is { Length: > 160 } s ? s[..160] + "…" : Str(result["content"]);
    }

    private static string Short(string sha) => sha.Length > 10 ? sha[..10] : sha;

    private static AiRunEffect Classify(JsonObject intent, DocumentStore documents)
    {
        var call = Str(intent["call"]);
        var tool = Str(intent["tool"]);
        var path = intent["path"] is null ? null : Str(intent["path"]);
        var target = intent["target"] is null ? path : Str(intent["target"]);
        var before = intent["before"] is null ? null : Str(intent["before"]);
        var after = intent["after"] is null ? null : Str(intent["after"]);
        if (tool == "create_folder")
            return new AiRunEffect(call, tool, path, AiRunEffect.NotDone, "criar uma pasta pode ser repetido sem efeito duplicado");
        if (tool == "generate_image")
            return new AiRunEffect(call, tool, path, AiRunEffect.Uncertain,
                "a imagem pode ter sido gerada e cobrada pelo provedor sem ser guardada; confira a pasta " + (path ?? AssistantImageTools.Folder) + " antes de gerar outra");
        if (path is null)
            return new AiRunEffect(call, tool, null, AiRunEffect.Uncertain, "o efeito desta ação fica fora do vault e não pode ser conferido daqui");
        string? Now(string? p) => p is null ? null : documents.Get(p) is { } d ? Sha(d.Content) : null;
        var atPath = Now(path);
        var atTarget = Now(target);
        bool expected = after == Absent ? atTarget is null : after is not null && atTarget == after;
        bool moved = target != path;
        if (expected && (!moved || atPath is null))
            return new AiRunEffect(call, tool, target, AiRunEffect.Done, "conferida no vault: " + (after == Absent ? path + " não existe mais" : target + " está na revisão esperada " + Short(after!)));
        bool untouched = atPath == before && (!moved || atTarget is null);
        if (untouched)
            return new AiRunEffect(call, tool, path, AiRunEffect.NotDone, "conferida no vault: " + path + " continua como estava");
        return new AiRunEffect(call, tool, path, AiRunEffect.Uncertain,
            path + " não está nem como estava nem como ficaria; confira a nota (e o histórico dela) antes de continuar");
    }

    /// <summary>
    /// The transcript of the run rebuilt from the journal after <paramref name="baseMessages"/> (the conversation as it
    /// was saved when the run started): each model step and each tool result as recorded; for an action the process
    /// cut, a result that says what was found in the vault, so the model never thinks it ran when it did not, or the
    /// other way round.
    /// </summary>
    public List<AiChatMessage> Rebuild(IReadOnlyList<AiChatMessage> baseMessages, DocumentStore documents)
    {
        var entries = Entries;
        var list = baseMessages.Take(BaseMessages).ToList();
        if (list.Count < BaseMessages && RequestMessage is { } request && !list.Any(m => m.Id == request.Id)) list.Add(request);
        var effects = Effects(documents).ToDictionary(e => e.CallId);
        var results = entries.Where(e => Str(e["t"]) == "result").GroupBy(e => Str(e["call"])).ToDictionary(g => g.Key, g => g.Last());
        foreach (var e in entries)
        {
            var type = Str(e["t"]);
            if (type == "resume" && e["message"] is JsonObject note) { list.Add(AiChatMessage.FromJson(note)); continue; }
            if (type != "response" || e["message"] is not JsonObject m) continue;
            var message = AiChatMessage.FromJson(m);
            if (message.Meta is not null) message.Meta.Streaming = false;
            foreach (var b in message.Content) { b.Pending = false; b.Partial = null; }
            list.Add(message);
            var calls = message.Content.Where(b => b.Type == "tool_use").ToList();
            if (calls.Count == 0) continue;
            var answer = new AiChatMessage { Id = AssistantAgent.NewId("m"), Role = "user", Meta = new AiChatMeta { ToolResults = true } };
            foreach (var call in calls)
            {
                if (results.TryGetValue(call.Id, out var r))
                {
                    answer.Content.Add(new AiChatBlock
                    {
                        Type = "tool_result", ToolUseId = call.Id, Content = Str(r["content"]), IsError = r["error"] is JsonValue err && err.GetValue<bool>(),
                        Status = r["status"] is null ? null : Str(r["status"]), Changes = (r["changes"] as JsonArray)?.Count ?? 0
                    });
                    continue;
                }
                var fx = effects.GetValueOrDefault(call.Id) ?? new AiRunEffect(call.Id, call.Name, null, AiRunEffect.NotDone, "não concluída");
                answer.Content.Add(fx.State switch
                {
                    AiRunEffect.Done => new AiChatBlock { Type = "tool_result", ToolUseId = call.Id, Status = "done",
                        Content = "Concluída antes de o app ser encerrado (" + fx.Detail + "). Não repita." },
                    AiRunEffect.NotDone => new AiChatBlock { Type = "tool_result", ToolUseId = call.Id, Status = "aborted", IsError = true,
                        Content = "Não executada: o app foi encerrado antes desta ação (" + fx.Detail + "). Nada foi alterado; refaça somente se ainda for necessária." },
                    _ => new AiChatBlock { Type = "tool_result", ToolUseId = call.Id, Status = "needs_you", IsError = true,
                        Content = "Precisa do usuário: o app foi encerrado durante esta ação e o resultado não pôde ser verificado (" + fx.Detail + "). Não repita sem confirmar com o usuário." }
                });
            }
            list.Add(answer);
        }
        return list;
    }

    /// <summary>The note a resumed run is given: what is done, what is not, what needs the person, and the goal.</summary>
    public AiChatMessage ResumeNote(DocumentStore documents)
    {
        var effects = Effects(documents);
        string Lines(string state) => string.Join("; ", effects.Where(e => e.State == state).Select(e => e.Tool + (e.Path is null ? "" : " " + e.Path)));
        var done = Lines(AiRunEffect.Done);
        var notDone = Lines(AiRunEffect.NotDone);
        var uncertain = Lines(AiRunEffect.Uncertain);
        var text = "[Retomada de trabalho interrompido] O app foi encerrado durante esta execução e o usuário pediu para retomá-la. Objetivo original: " + Goal + "." +
            (done.Length > 0 ? " Já feito e conferido (não repita): " + done + "." : "") +
            (notDone.Length > 0 ? " Não chegou a ser feito: " + notDone + "." : "") +
            (uncertain.Length > 0 ? " Não verificável (pergunte ao usuário antes de repetir): " + uncertain + "." : "") +
            " Releia as notas que for alterar, continue de onde parou e não repita gastos, imagens nem gravações já feitas.";
        return new AiChatMessage { Id = AssistantAgent.NewId("m"), Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock { Type = "text", Text = text, Hidden = true }] };
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>SHA-256 of a note's text (its revision across restarts; the store's counter is not durable).</summary>
    public static string? Sha(string? text) => text is null ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The hash of a tool call's input, with the keys in order so the same call hashes the same.</summary>
    public static string InputHash(JsonNode? input) => Sha(LegacyCustomize.Stringify(Canon(input))) ?? "";

    private static JsonNode? Canon(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Canon(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Canon).ToArray()),
        _ => n?.DeepClone()
    };

    private static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n?.ToString() ?? "";
    private static long Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<long>(out var l) ? l : n is JsonValue d && d.TryGetValue<double>(out var x) ? (long)x : 0;
}
