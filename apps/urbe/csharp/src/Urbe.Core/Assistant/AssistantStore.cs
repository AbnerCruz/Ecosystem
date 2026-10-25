using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Where the assistant keeps its files on this device (never in the vault): small text files by name.</summary>
public interface IAssistantStorage
{
    string? Read(string name);
    void Write(string name, string text);
    void Delete(string name);
    IEnumerable<string> List();
}

/// <summary>In memory (tests; and what the original does without IndexedDB).</summary>
public sealed class MemoryAssistantStorage : IAssistantStorage
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    public string? Read(string name) => _files.GetValueOrDefault(name);
    public void Write(string name, string text) => _files[name] = text;
    public void Delete(string name) => _files.Remove(name);
    public IEnumerable<string> List() => _files.Keys.ToList();
}

/// <summary>A folder of the app's own data (Android: the app's private files); a failure costs only persistence.</summary>
public sealed class FolderAssistantStorage(string folder) : IAssistantStorage
{
    private string PathOf(string name) => Path.Combine(folder, name);

    public string? Read(string name)
    {
        try { return File.Exists(PathOf(name)) ? File.ReadAllText(PathOf(name)) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Write(string name, string text)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var tmp = PathOf(name + ".tmp");
            File.WriteAllText(tmp, text);
            File.Move(tmp, PathOf(name), overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public void Delete(string name)
    {
        try { File.Delete(PathOf(name)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public IEnumerable<string> List()
    {
        try { return Directory.Exists(folder) ? Directory.GetFiles(folder).Select(Path.GetFileName).OfType<string>().ToList() : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }
}

/// <summary>A run of the agent as the conversation remembers it: what it changed (to undo) and how it ended.</summary>
public sealed class AiRunRecord
{
    public string Id { get; set; } = "";
    public long Started { get; set; }
    public List<AiChange> Changes { get; set; } = [];
    public string? Stopped { get; set; }
    public string? Error { get; set; }
    public bool Reverted { get; set; }
}

/// <summary>A conversation: its messages, its agent, model and permissions, and its last runs.</summary>
public sealed class AiConversation
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public long Created { get; set; }
    public long Updated { get; set; }
    public string AgentId { get; set; } = "geral";
    public string? ProviderId { get; set; }
    public string Model { get; set; } = "";
    public string Policy { get; set; } = "readonly";
    public string Vault { get; set; } = "";
    public List<AiChatMessage> Messages { get; set; } = [];
    public List<AiRunRecord> Runs { get; set; } = [];
    public AiUsage Usage { get; set; } = new(0, 0, null);
    public List<AiWebSource> WebSources { get; set; } = [];
}

/// <summary>The models a provider listed, and when.</summary>
public sealed record AiModelCache(long At, IReadOnlyList<AiModelInfo> List);

/// <summary>A model and its configured provider; credentials remain in Providers.</summary>
public sealed record AiModelChoice(string ProviderId, string Model);

/// <summary>One titled instruction block for an individual agent.</summary>
public sealed record AiInstructionBlock(string Title, string Content);

/// <summary>Assistant configuration, with backward-compatible independent image and agent preferences.</summary>
public sealed class AiConfig
{
    public int Version { get; set; } = 1;
    public int AgentWorkspaceVersion { get; set; }
    public Dictionary<string, string> AgentPolicies { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, AiModelChoice> AgentImages { get; set; } = new(StringComparer.Ordinal);
    public string PolicyFor(string agentId) => AgentPolicies.GetValueOrDefault(agentId) is "ask" or "auto" ? AgentPolicies[agentId] : "readonly";
    public List<AiProviderConfig> Providers { get; set; } = [];
    public string? ProviderId { get; set; }
    public string Model { get; set; } = "";
    /// <summary>Independent image provider and model; null/empty use existing automatic defaults.</summary>
    public string? ImageProviderId { get; set; }
    public string ImageModel { get; set; } = "";
    /// <summary>Agent-specific overrides; no entry means inherit the conversation/default model.</summary>
    public Dictionary<string, AiModelChoice> AgentModels { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<AiInstructionBlock>> AgentInstructionBlocks { get; set; } = new(StringComparer.Ordinal);
    public string Policy { get; set; } = "readonly";
    public string AgentId { get; set; } = "geral";
    public string Instructions { get; set; } = "";
    /// <summary>Facts to remember, by vault name ('*' for every vault).</summary>
    public Dictionary<string, List<string>> Memory { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Versioned structured memories. Old entries in Memory are migrated on first write.</summary>
    public List<AiMemoryRecord> MemoryRecords { get; set; } = [];
    public bool AutoCaptureEpisodes { get; set; } = true;
    public List<AiAgentDef> CustomAgents { get; set; } = [];
    /// <summary>UC-81: the call names the person gave the agents on this device, by agent id.</summary>
    public Dictionary<string, string> AgentNames { get; set; } = new(StringComparer.Ordinal);
    public int MaxSteps { get; set; } = 30;
    /// <summary>One private configuration authority for network consent, credentials and budgets.</summary>
    public AiWebConfig Web { get; set; } = new();
    public Dictionary<string, AiModelCache> ModelCache { get; set; } = new(StringComparer.Ordinal);

    public AiModelChoice ModelFor(AiConversation conversation)
    {
        if (AgentModels.TryGetValue(conversation.AgentId, out var chosen) &&
            !string.IsNullOrWhiteSpace(chosen.Model))
            return chosen;
        if (AgentWorkspaceVersion == 1 && conversation.AgentId != "geral" && conversation.Model.Length == 0 &&
            AgentModels.TryGetValue("geral", out var assistant)) return assistant;
        var id = conversation.ProviderId ?? ProviderId ?? Providers.FirstOrDefault()?.Id ?? "";
        return new AiModelChoice(id, conversation.Model.Length > 0 ? conversation.Model : Model);
    }

    public AiAgentDef AgentWithInstructions(AiAgentDef agent)
    {
        if (!AgentInstructionBlocks.TryGetValue(agent.Id, out var blocks)) return agent;
        var additional = string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b.Content))
            .Select(b => "### " + (string.IsNullOrWhiteSpace(b.Title) ? "Instrução" : b.Title.Trim()) + "\n" + b.Content.Trim()));
        return additional.Length == 0 ? agent : agent with { Prompt = agent.Prompt.Length > 0 ? agent.Prompt + "\n\n" + additional : additional };
    }
}

/// <summary>
/// The assistant's conversations and configuration, only on this device (store.js): API keys never go to the vault
/// nor to the notes' repository. The files are JSON in the shape of the original's (config, one file per conversation).
/// </summary>
public sealed class AssistantStore(IAssistantStorage storage)
{
    private const string ConfigFile = "config.json";
    private const string Prefix = "conversa-";

    public AiConfig GetConfig()
    {
        var text = storage.Read(ConfigFile);
        if (text is null) return new AiConfig();
        try { return ConfigFromJson(JsonNode.Parse(text) as JsonObject ?? []); }
        catch (JsonException) { return new AiConfig(); }
    }

    /// <summary>Initialize agent workspaces once. Never rewrites conversations or infers write grants.</summary>
    public AiConfig InitializeAgentWorkspaces(ILocalTextRuntime? runtime)
    {
        var original = storage.Read(ConfigFile);
        if (original is not null)
        {
            try
            {
                var parsed = JsonNode.Parse(original) as JsonObject ?? throw new JsonException();
                if (Num(parsed["version"]) > 1 || Num(parsed["agentWorkspaceVersion"]) > 1)
                    throw new IOException("Configuração de IA de uma versão mais nova; nenhum dado foi alterado.");
            }
            catch (JsonException e) { throw new IOException("Configuração de IA ilegível; nenhum dado foi alterado.", e); }
        }
        var config = GetConfig();
        if (config.AgentWorkspaceVersion == 1) return config;
        var conversations = ListConversations();
        var retained = conversations.Select(c => c.AgentId)
            .Concat(config.AgentNames.Keys).Concat(config.AgentModels.Keys).Concat(config.AgentInstructionBlocks.Keys)
            .Concat(config.CustomAgents.Select(a => a.Id)).ToHashSet(StringComparer.Ordinal);
        if (original is not null && config.AgentId != "geral") retained.Add(config.AgentId);
        foreach (var id in retained.Where(id => id != "geral"))
        {
            if (config.CustomAgents.Any(a => a.Id == id)) continue;
            var template = AssistantAgent.Agents.FirstOrDefault(a => a.Id == id);
            config.CustomAgents.Add(template ?? new AiAgentDef(id, "Agente preservado " + id, "sparkle", "", "read", false, ""));
        }
        // Preserve explicit choices, including the model in an older conversation. An unconfigured
        // Assistant gets the local default even when another preference already created config.json.
        if (runtime is not null && !config.AgentModels.ContainsKey("geral") && config.Model.Length == 0 &&
            !conversations.Any(c => c.AgentId == "geral" && c.Model.Length > 0))
        {
            var provider = config.Providers.FirstOrDefault(p => p.Preset == "device") ??
                new AiProviderConfig(config.Providers.Any(p => p.Id == "device") ? AssistantAgent.NewId("p") : "device", "device", "IA no aparelho", "", "");
            if (!config.Providers.Any(p => p.Id == provider.Id)) config.Providers.Add(provider);
            config.AgentModels["geral"] = new AiModelChoice(provider.Id, runtime.Recommended.Id);
        }
        if (original is not null && !string.IsNullOrWhiteSpace(config.ImageProviderId) && config.ImageModel.Length > 0)
            foreach (var id in retained.Append("geral"))
                config.AgentImages[id] = new AiModelChoice(config.ImageProviderId, config.ImageModel);
        if (original is not null)
        {
            const string backup = "config-before-agent-workspaces.json";
            if (storage.Read(backup) is null) storage.Write(backup, original);
            if (storage.Read(backup) != original) throw new IOException("Não foi possível preservar a configuração anterior.");
        }
        config.AgentWorkspaceVersion = 1;
        SetConfigVerified(config);
        return config;
    }

    public List<AiConversation> ConversationsFor(string agentId, string vault) =>
        ListConversations().Where(c => c.AgentId == agentId && c.Vault == vault).ToList();

    public void SetConfig(AiConfig c) => storage.Write(ConfigFile, ConfigToJson(c).ToJsonString());

    /// <summary>
    /// Required for changes to persistent AI memory. The folder adapter historically swallows IO
    /// failures: fail closed unless the saved bytes can be read back verbatim.
    /// </summary>
    public void SetConfigVerified(AiConfig c)
    {
        var payload = ConfigToJson(c).ToJsonString();
        storage.Write(ConfigFile, payload);
        if (!string.Equals(storage.Read(ConfigFile), payload, StringComparison.Ordinal))
            throw new IOException("Não foi possível confirmar a gravação da configuração do assistente.");
    }

    /// <summary>The conversations, the most recent first.</summary>
    public List<AiConversation> ListConversations() =>
        storage.List().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => Load(n)).OfType<AiConversation>().OrderByDescending(c => c.Updated).ToList();

    public AiConversation? GetConversation(string id) => Load(FileOf(id));

    public void SaveConversation(AiConversation c, long? now = null)
    {
        c.Updated = now ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        storage.Write(FileOf(c.Id), ConversationToJson(c).ToJsonString());
    }

    public void DeleteConversation(string id) => storage.Delete(FileOf(id));

    private static string FileOf(string id) => Prefix + string.Concat(id.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_')) + ".json";

    private AiConversation? Load(string name)
    {
        var text = storage.Read(name);
        if (text is null) return null;
        try { return JsonNode.Parse(text) is JsonObject o ? ConversationFromJson(o) : null; }
        catch (JsonException) { return null; }
    }

    // ---------------------------------------------------------------- JSON (the original's shape)

    private static string Str(JsonNode? n, string fallback = "") => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;
    private static long Num(JsonNode? n) => n is null ? 0 : (long)LegacyCustomize.JsToNumber(n);

    public static JsonObject ProviderToJson(AiProviderConfig p)
    {
        var o = new JsonObject { ["id"] = p.Id, ["preset"] = p.Preset, ["name"] = p.Name ?? "", ["apiKey"] = p.ApiKey ?? "", ["baseUrl"] = p.BaseUrl ?? "" };
        if (p.Kind is not null) o["kind"] = p.Kind;
        if (p.StreamUsage is not null) o["streamUsage"] = p.StreamUsage;
        return o;
    }

    public static AiProviderConfig ProviderFromJson(JsonNode n) =>
        new(Str(n["id"]), Str(n["preset"], "custom"), Str(n["name"]), Str(n["apiKey"]), Str(n["baseUrl"]), n["kind"] is null ? null : Str(n["kind"]), (bool?)n["streamUsage"]);

    private static JsonObject AgentToJson(AiAgentDef a) => new()
    {
        ["id"] = a.Id, ["name"] = a.Name, ["icon"] = a.Icon, ["description"] = a.Description, ["tools"] = a.Tools, ["delegate"] = a.Delegate, ["prompt"] = a.Prompt
    };

    private static AiAgentDef AgentFromJson(JsonNode n) =>
        new(Str(n["id"]), Str(n["name"]), Str(n["icon"], "sparkle"), Str(n["description"]), Str(n["tools"], "all"), (bool?)n["delegate"] ?? false, Str(n["prompt"]));

    private static JsonObject ModelToJson(AiModelInfo m)
    {
        var o = new JsonObject { ["id"] = m.Id, ["name"] = m.Name, ["context"] = m.Context, ["tools"] = m.Tools };
        o["price"] = m.Price is { } p ? new JsonObject { ["input"] = p.Input, ["output"] = p.Output } : null;
        return o;
    }

    private static AiModelInfo ModelFromJson(JsonNode n) => new(Str(n["id"]), Str(n["name"]), n["context"] is null ? null : Num(n["context"]),
        n["price"] is JsonObject p ? (LegacyCustomize.JsToNumber(p["input"]), LegacyCustomize.JsToNumber(p["output"])) : null, (bool?)n["tools"]);

    public static JsonObject ConfigToJson(AiConfig c)
    {
        var policies = new JsonObject();
        foreach (var (id, policy) in c.AgentPolicies) policies[id] = policy;
        var images = new JsonObject();
        foreach (var (id, image) in c.AgentImages) images[id] = new JsonObject { ["providerId"] = image.ProviderId, ["model"] = image.Model };
        var memory = new JsonObject();
        foreach (var (k, v) in c.Memory) memory[k] = new JsonArray(v.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        var names = new JsonObject();
        foreach (var (k, v) in c.AgentNames.OrderBy(p => p.Key, StringComparer.Ordinal)) names[k] = v;
        var agentModels = new JsonObject();
        foreach (var (k, v) in c.AgentModels.OrderBy(p => p.Key, StringComparer.Ordinal))
            agentModels[k] = new JsonObject { ["providerId"] = v.ProviderId, ["model"] = v.Model };
        var instructionBlocks = new JsonObject();
        foreach (var (k, v) in c.AgentInstructionBlocks.OrderBy(p => p.Key, StringComparer.Ordinal))
            instructionBlocks[k] = new JsonArray(v.Select(b => (JsonNode)new JsonObject { ["title"] = b.Title, ["content"] = b.Content }).ToArray());
        var cache = new JsonObject();
        foreach (var (k, v) in c.ModelCache) cache[k] = new JsonObject { ["at"] = v.At, ["list"] = new JsonArray(v.List.Select(m => (JsonNode)ModelToJson(m)).ToArray()) };
        var webAgents = new JsonObject();
        foreach (var (id, allowed) in c.Web.AgentAccess.OrderBy(x => x.Key, StringComparer.Ordinal)) webAgents[id] = allowed;
        var web = new JsonObject
        {
            ["enabled"] = c.Web.Enabled, ["provider"] = c.Web.Provider, ["apiKey"] = c.Web.ApiKey,
            ["maxSearchCalls"] = c.Web.MaxSearchCalls, ["maxFetchCalls"] = c.Web.MaxFetchCalls,
            ["timeoutSeconds"] = c.Web.TimeoutSeconds, ["maxDownloadBytes"] = c.Web.MaxDownloadBytes,
            ["agentAccess"] = webAgents
        };
        return new JsonObject
        {
            ["agentWorkspaceVersion"] = c.AgentWorkspaceVersion, ["agentPolicies"] = policies, ["agentImages"] = images,
            ["web"] = web, ["version"] = c.Version, ["providers"] = new JsonArray(c.Providers.Select(p => (JsonNode)ProviderToJson(p)).ToArray()), ["providerId"] = c.ProviderId,
            ["model"] = c.Model, ["imageProviderId"] = c.ImageProviderId, ["imageModel"] = c.ImageModel,
            ["agentModels"] = agentModels, ["agentInstructionBlocks"] = instructionBlocks,
            ["policy"] = c.Policy, ["agentId"] = c.AgentId, ["instructions"] = c.Instructions, ["memory"] = memory,
            ["memorySchemaVersion"] = 2,
            ["memoryRecords"] = JsonSerializer.SerializeToNode(c.MemoryRecords, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            ["autoCaptureEpisodes"] = c.AutoCaptureEpisodes,
            ["customAgents"] = new JsonArray(c.CustomAgents.Select(a => (JsonNode)AgentToJson(a)).ToArray()), ["maxSteps"] = c.MaxSteps, ["modelCache"] = cache, ["agentNames"] = names
        };
    }

    public static AiConfig ConfigFromJson(JsonObject o)
    {
        var c = new AiConfig
        {
            AgentWorkspaceVersion = (int)Num(o["agentWorkspaceVersion"]),
            Version = o["version"] is null ? 1 : (int)Num(o["version"]),
            Providers = (o["providers"] as JsonArray ?? []).OfType<JsonObject>().Select(ProviderFromJson).ToList(),
            ProviderId = o["providerId"] is JsonValue pid && pid.TryGetValue<string>(out var ps) ? ps : null,
            Model = Str(o["model"]),
            ImageProviderId = o["imageProviderId"] is JsonValue iv && iv.TryGetValue<string>(out var imageId) ? imageId : null,
            ImageModel = Str(o["imageModel"]), Policy = Str(o["policy"], "ask"), AgentId = Str(o["agentId"], "geral"), Instructions = Str(o["instructions"]),
            CustomAgents = (o["customAgents"] as JsonArray ?? []).OfType<JsonObject>().Select(AgentFromJson).ToList(),
            MaxSteps = o["maxSteps"] is null ? 30 : (int)Num(o["maxSteps"]),
            AutoCaptureEpisodes = o["autoCaptureEpisodes"] is null || (bool?)o["autoCaptureEpisodes"] == true,
            MemoryRecords = o["memoryRecords"] is JsonArray records
                ? JsonSerializer.Deserialize<List<AiMemoryRecord>>(records.ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [] : []
        };
        if (o["agentPolicies"] is JsonObject policies)
            foreach (var (id, policy) in policies) c.AgentPolicies[id] = Str(policy, "readonly");
        if (o["agentImages"] is JsonObject images)
            foreach (var (id, value) in images)
                if (value is JsonObject image) c.AgentImages[id] = new AiModelChoice(Str(image["providerId"]), Str(image["model"]));
        if (o["web"] is JsonObject w)
        {
            // Preserve an explicit old offline preference instead of silently restoring network access.
            c.Web.Enabled = (bool?)w["enabled"] ?? true;
            c.Web.Provider = Str(w["provider"], "bing-rss");
            c.Web.ApiKey = Str(w["apiKey"]);
            // Old builds wrote Brave as the default even without a key. Migrate that unusable
            // search setting to keyless search; users with an actual Brave credential keep it.
            if (c.Web.Provider == "brave" && string.IsNullOrWhiteSpace(c.Web.ApiKey))
                c.Web.Provider = "bing-rss";
            c.Web.MaxSearchCalls = (int)Math.Clamp(w["maxSearchCalls"] is null ? 12 : Num(w["maxSearchCalls"]), 1, 100);
            c.Web.MaxFetchCalls = (int)Math.Clamp(w["maxFetchCalls"] is null ? 20 : Num(w["maxFetchCalls"]), 1, 100);
            c.Web.TimeoutSeconds = (int)Math.Clamp(w["timeoutSeconds"] is null ? 18 : Num(w["timeoutSeconds"]), 3, 60);
            c.Web.MaxDownloadBytes = (int)Math.Clamp(w["maxDownloadBytes"] is null ? 1_000_000 : Num(w["maxDownloadBytes"]), 10_000, 5_000_000);
            if (w["agentAccess"] is JsonObject grants)
                foreach (var (id, grant) in grants) c.Web.AgentAccess[id] = (bool?)grant ?? false;
        }
        if (o["agentModels"] is JsonObject models)
            foreach (var (id, value) in models)
                if (value is JsonObject choice && Str(choice["providerId"]).Length > 0 && Str(choice["model"]).Length > 0)
                    c.AgentModels[id] = new AiModelChoice(Str(choice["providerId"]), Str(choice["model"]));
        if (o["agentInstructionBlocks"] is JsonObject blocks)
            foreach (var (id, value) in blocks)
                c.AgentInstructionBlocks[id] = (value as JsonArray ?? []).OfType<JsonObject>()
                    .Select(b => new AiInstructionBlock(Str(b["title"]), Str(b["content"]))).ToList();
        if (o["memory"] is JsonObject mem)
            foreach (var (k, v) in mem) c.Memory[k] = (v as JsonArray ?? []).Select(x => Str(x)).Where(x => x.Length > 0).ToList();
        if (o["agentNames"] is JsonObject an)
            foreach (var (k, v) in an) if (Str(v).Trim() is { Length: > 0 } name) c.AgentNames[k] = name;
        if (o["modelCache"] is JsonObject mc)
            foreach (var (k, v) in mc)
                if (v is JsonObject e) c.ModelCache[k] = new AiModelCache(Num(e["at"]), (e["list"] as JsonArray ?? []).OfType<JsonObject>().Select(ModelFromJson).ToList());
        return c;
    }

    private static JsonNode? SnapToJson(AiNoteSnap? s) => s is null ? null : new JsonObject { ["path"] = s.Path, ["content"] = s.Content };
    private static AiNoteSnap? SnapFromJson(JsonNode? n) => n is JsonObject o ? new AiNoteSnap(Str(o["path"]), Str(o["content"])) : null;

    public static JsonObject ConversationToJson(AiConversation c) => new()
    {
        ["id"] = c.Id, ["title"] = c.Title, ["created"] = c.Created, ["updated"] = c.Updated, ["agentId"] = c.AgentId, ["providerId"] = c.ProviderId,
        ["model"] = c.Model, ["policy"] = c.Policy, ["vault"] = c.Vault,
        ["messages"] = new JsonArray(c.Messages.Select(m => (JsonNode)m.ToJson()).ToArray()),
        ["runs"] = new JsonArray(c.Runs.Select(r => (JsonNode)new JsonObject
        {
            ["id"] = r.Id, ["started"] = r.Started, ["stopped"] = r.Stopped, ["error"] = r.Error, ["reverted"] = r.Reverted,
            ["changes"] = new JsonArray(r.Changes.Select(x =>
            {
                var o = new JsonObject { ["id"] = x.Id, ["before"] = SnapToJson(x.Before), ["after"] = SnapToJson(x.After) };
                if (x.Trashed) o["trashed"] = true;
                return (JsonNode)o;
            }).ToArray())
        }).ToArray()),
        ["usage"] = AssistantAgent.UsageJson(c.Usage),
        ["webSources"] = new JsonArray(c.WebSources.Select(s => (JsonNode)new JsonObject
        {
            ["id"] = s.Id, ["title"] = s.Title, ["originalUrl"] = s.OriginalUrl, ["finalUrl"] = s.FinalUrl,
            ["domain"] = s.Domain, ["published"] = s.Published, ["accessed"] = s.Accessed, ["status"] = s.Status
        }).ToArray())
    };

    public static AiConversation ConversationFromJson(JsonObject o) => new()
    {
        Id = Str(o["id"]), Title = Str(o["title"]), Created = Num(o["created"]), Updated = Num(o["updated"]), AgentId = Str(o["agentId"], "geral"),
        ProviderId = o["providerId"] is JsonValue pv && pv.TryGetValue<string>(out var ps) ? ps : null, Model = Str(o["model"]), Policy = Str(o["policy"], "ask"), Vault = Str(o["vault"]),
        Messages = (o["messages"] as JsonArray ?? []).OfType<JsonObject>().Select(m => AiChatMessage.FromJson(m)).ToList(),
        Runs = (o["runs"] as JsonArray ?? []).OfType<JsonObject>().Select(r => new AiRunRecord
        {
            Id = Str(r["id"]), Started = Num(r["started"]), Stopped = r["stopped"] is null ? null : Str(r["stopped"]), Error = r["error"] is null ? null : Str(r["error"]),
            Reverted = (bool?)r["reverted"] ?? false,
            Changes = (r["changes"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new AiChange(Str(x["id"]), SnapFromJson(x["before"]), SnapFromJson(x["after"]), (bool?)x["trashed"] ?? false)).ToList()
        }).ToList(),
        Usage = o["usage"] is JsonObject u ? new AiUsage(Num(u["input"]), Num(u["output"]), u["cost"] is null ? null : LegacyCustomize.JsToNumber(u["cost"])) : new AiUsage(0, 0, null),
        WebSources = (o["webSources"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(s => new AiWebSource(Str(s["id"]), Str(s["title"]), Str(s["originalUrl"]), Str(s["finalUrl"]),
                Str(s["domain"]), s["published"] is null ? null : Str(s["published"]), Str(s["accessed"]), Str(s["status"]))).ToList()
    };
}

/// <summary>What undoing a run needs from the app: the trash and writing a note back (the vault first).</summary>
public interface IAssistantUndoHost
{
    DocumentStore Documents { get; }
    /// <summary>Restores a trashed document by id; false when it cannot.</summary>
    Task<bool> RestoreTrashedAsync(string id);
    Task TrashAsync(UrbeDocument doc);
    /// <summary>The document back at <paramref name="path"/> with <paramref name="content"/> (moved back when the path differs).</summary>
    Task WriteBackAsync(UrbeDocument current, string path, string content);
}

/// <summary>tools.js revert(): the run's changes undone newest first; notes the user edited afterwards are kept.</summary>
public static class AssistantUndo
{
    public sealed record Report(int Restored, IReadOnlyList<string> Skipped);

    public static async Task<Report> RevertAsync(IAssistantUndoHost host, IReadOnlyList<AiChange> changes)
    {
        int restored = 0;
        var skipped = new List<string>();
        foreach (var c in changes.Reverse())
        {
            var cur = host.Documents.Get(c.Id);
            if (c.After is not null && cur is not null && cur.Content != c.After.Content) { skipped.Add(cur.Path); continue; }   // the user changed it afterwards
            if (c.Trashed)
            {
                try { if (await host.RestoreTrashedAsync(c.Id)) restored++; else skipped.Add(c.Before?.Path ?? ""); }
                catch (Exception) { skipped.Add(c.Before?.Path ?? ""); }
                continue;
            }
            if (c.Before is null)
            {
                if (cur is not null) { await host.TrashAsync(cur); restored++; }
                continue;
            }
            if (cur is not null) { await host.WriteBackAsync(cur, c.Before.Path, c.Before.Content); restored++; }
        }
        return new Report(restored, skipped);
    }
}
