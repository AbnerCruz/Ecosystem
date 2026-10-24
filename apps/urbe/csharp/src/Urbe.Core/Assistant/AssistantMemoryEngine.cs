using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>
/// Structured, local-first memories owned by the Urbe assistant configuration.
/// This is not a second agent runtime. The vault remains the source of truth for document content.
/// </summary>
public sealed class AiMemoryRecord
{
    public string Id { get; set; } = "";
    /// <summary>fact, preference, decision, episode, procedure, goal, observation.</summary>
    public string Kind { get; set; } = "fact";
    /// <summary>global, vault, agent. An agent scope also belongs to a specific vault.</summary>
    public string Scope { get; set; } = "vault";
    public string Vault { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>user, agent, run, legacy. Never a provider secret or an untrusted instruction.</summary>
    public string Origin { get; set; } = "user";
    public string SourceReference { get; set; } = "";
    public List<string> RelatedPaths { get; set; } = [];
    public List<string> RelatedMemoryIds { get; set; } = [];
    /// <summary>active, disputed, superseded, archived.</summary>
    public string State { get; set; } = "active";
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long? ValidFrom { get; set; }
    public long? ValidUntil { get; set; }
    public int Confidence { get; set; } = 100;
    public bool Verified { get; set; }
    public bool Pinned { get; set; }
}

public sealed record AiMemoryMatch(AiMemoryRecord Memory, double Score);

/// <summary>
/// Deterministic indexing, retrieval, consolidation and privacy controls. No SDK, server, network,
/// model invocation, or periodic processing. A semantic adapter may be added later, without making
/// vector search a prerequisite for mobile/offline operation.
/// </summary>
public static class AssistantMemoryEngine
{
    public const int MaxRecords = 5000;
    public const int MaxText = 1600;
    public const int ContextCharacters = 3400;

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
    {
        "fact", "preference", "decision", "episode", "procedure", "goal", "observation"
    };
    private static readonly HashSet<string> Scopes = new(StringComparer.Ordinal) { "global", "vault", "agent" };
    private static readonly Regex Words = new(@"[\p{L}\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RememberCommand = new(
        @"^\s*(?:por favor,?\s*)?(?:lembre(?:-se)?\s+(?:de\s+)?que|memorize\s+que|guarde\s+que)\s+(.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex PotentialSecret = new(
        @"(?i)(?:\bsk-[A-Za-z0-9_-]{12,}\b|\b(?:api[_ -]?key|senha|password|token|secret)\s*[:=]\s*\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static string Normalize(string? text)
    {
        var normalized = (text ?? "").Normalize(NormalizationForm.FormD);
        var b = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                b.Append(char.ToLowerInvariant(c));
        return b.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string[] Tokens(string? s) =>
        Words.Matches(Normalize(s)).Select(m => m.Value)
            .Where(w => w.Length > 2 && w is not ("que" or "para" or "com" or "uma" or "dos" or "das" or "por" or "sobre" or "the" or "and"))
            .Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>The old "memory" JSON is lazily migrated on the first structured write, with no loss.</summary>
    public static void MigrateLegacy(AiConfig cfg, long? timestamp = null)
    {
        if (cfg.Memory.Count == 0) return;
        var old = cfg.Memory.SelectMany(kv => kv.Value.Select(fact => (scope: kv.Key, fact))).ToArray();
        foreach (var (scope, fact) in old)
        {
            if (string.IsNullOrWhiteSpace(fact)) continue;
            if (cfg.MemoryRecords.Any(m => m.State == "active" &&
                m.Scope == (scope == "*" ? "global" : "vault") &&
                (scope == "*" || m.Vault == scope) && Normalize(m.Text.Trim()) == Normalize(fact.Trim()))) continue;
            if (cfg.MemoryRecords.Count >= MaxRecords) throw new InvalidOperationException("O limite local de memórias foi alcançado.");
            cfg.MemoryRecords.Add(new AiMemoryRecord
            {
                Id = Guid.NewGuid().ToString("N"), Kind = "fact",
                Scope = scope == "*" ? "global" : "vault", Vault = scope == "*" ? "" : scope,
                Text = fact.Trim()[..Math.Min(fact.Trim().Length, MaxText)],
                Origin = "legacy", SourceReference = "config.memory", Confidence = 100,
                CreatedAt = timestamp ?? Now(), UpdatedAt = timestamp ?? Now()
            });
        }
        cfg.Memory.Clear();
    }

    public static AiMemoryRecord Remember(AiConfig cfg, string fact, string vault,
        string agentId = "", string kind = "fact", string scope = "vault",
        string origin = "user", string source = "", IEnumerable<string>? paths = null,
        int confidence = 100, bool verified = false, long? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (!Kinds.Contains(kind)) throw new ArgumentException("Tipo de memória inválido.", nameof(kind));
        if (!Scopes.Contains(scope)) throw new ArgumentException("Escopo de memória inválido.", nameof(scope));
        var text = fact.Trim();
        if (text.Length == 0 || text.Length > MaxText) throw new ArgumentException("Memória vazia ou excessivamente longa.", nameof(fact));
        if (PotentialSecret.IsMatch(text)) throw new ArgumentException("Credenciais e segredos não podem ser memorizados.", nameof(fact));
        if (scope != "global" && string.IsNullOrWhiteSpace(vault)) throw new ArgumentException("Vault obrigatório.", nameof(vault));
        if (scope == "agent" && string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Agente obrigatório.", nameof(agentId));
        MigrateLegacy(cfg, timestamp);
        var normalized = Normalize(text);
        var effectiveVault = scope == "global" ? "" : vault;
        var effectiveAgent = scope == "agent" ? agentId : "";
        var existing = cfg.MemoryRecords.FirstOrDefault(m => m.State == "active" &&
            m.Scope == scope && m.Vault == effectiveVault && m.AgentId == effectiveAgent &&
            Normalize(m.Text) == normalized);
        if (existing is not null)
        {
            existing.UpdatedAt = timestamp ?? Now();
            existing.Confidence = Math.Max(existing.Confidence, Math.Clamp(confidence, 0, 100));
            existing.Verified |= verified;
            foreach (var path in paths ?? [])
                if (path.Length > 0 && !existing.RelatedPaths.Contains(path, StringComparer.Ordinal))
                    existing.RelatedPaths.Add(path);
            return existing;
        }
        if (cfg.MemoryRecords.Count >= MaxRecords) throw new InvalidOperationException("Limite de memórias atingido; arquive ou exclua registros antes de continuar.");
        var record = new AiMemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"), Text = text,
            Kind = kind, Scope = scope, Vault = effectiveVault, AgentId = effectiveAgent,
            Origin = origin, SourceReference = source.Length > 200 ? source[..200] : source,
            Confidence = Math.Clamp(confidence, 0, 100), Verified = verified,
            CreatedAt = timestamp ?? Now(), UpdatedAt = timestamp ?? Now(),
            RelatedPaths = (paths ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).Take(16).ToList()
        };
        cfg.MemoryRecords.Add(record);
        return record;
    }

    /// <summary>Permissive read for old installations; agent memories never leak to other agents or vaults.</summary>
    public static IReadOnlyList<AiMemoryRecord> Visible(AiConfig cfg, string vault, string agentId, long? at = null)
    {
        var date = at ?? Now();
        var records = cfg.MemoryRecords.Where(m =>
            m.State == "active" && (m.ValidFrom is null || m.ValidFrom <= date) &&
            (m.ValidUntil is null || m.ValidUntil > date) &&
            (m.Scope == "global" || (m.Vault == vault &&
                (m.Scope == "vault" || (m.Scope == "agent" && m.AgentId == agentId))))).ToList();
        foreach (var pair in cfg.Memory)
        {
            if (pair.Key is not "*" && pair.Key != vault) continue;
            for (var i = 0; i < pair.Value.Count; i++)
            {
                var fact = pair.Value[i];
                if (string.IsNullOrWhiteSpace(fact)) continue;
                if (records.Any(m => Normalize(m.Text) == Normalize(fact))) continue;
                records.Add(new AiMemoryRecord { Id = "legacy:" + pair.Key + ":" + i,
                    Scope = pair.Key == "*" ? "global" : "vault", Vault = pair.Key == "*" ? "" : pair.Key,
                    Text = fact, Origin = "legacy", SourceReference = "config.memory" });
            }
        }
        return records;
    }

    /// <summary>Bounded, hybrid lexical+metadata retrieval. Never pretends to be vector similarity.</summary>
    public static IReadOnlyList<AiMemoryMatch> Search(AiConfig cfg, string vault, string agentId,
        string? query, int limit = 12, long? at = null)
    {
        var q = Normalize(query?.Trim());
        var terms = Tokens(query);
        var date = at ?? Now();
        var all = Visible(cfg, vault, agentId, date);
        var matches = new List<AiMemoryMatch>(all.Count);
        foreach (var m in all)
        {
            var text = Normalize(m.Text);
            var meta = Normalize(m.Kind + " " + string.Join(" ", m.RelatedPaths));
            var score = 0d;
            if (q.Length > 2 && text.Contains(q, StringComparison.Ordinal)) score += 14;
            foreach (var term in terms)
            {
                if (text.Contains(term, StringComparison.Ordinal)) score += 2.5;
                else if (meta.Contains(term, StringComparison.Ordinal)) score += 1.5;
            }
            if (terms.Length == 0 && q.Length == 0) score = 1;
            if (terms.Length > 0 && score <= 0 && !m.Pinned) continue;
            if (m.Pinned) score += 5;
            if (m.Kind is "preference" or "decision" && score > 0) score += 1.5;
            if (m.State == "disputed") score *= .25;
            if (m.Origin == "run" && !m.Verified) score *= .8;
            if (m.Confidence < 60) score *= .8;
            if (m.UpdatedAt > 0 && date >= m.UpdatedAt &&
                date - m.UpdatedAt < 30L * 24 * 3600 * 1000) score += .5;
            if (score > 0) matches.Add(new AiMemoryMatch(m, score));
        }
        return matches.OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memory.UpdatedAt).ThenBy(x => x.Memory.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 60)).ToList();
    }

    /// <summary>Small, cited context instead of re-sending the entire memory archive on every step.</summary>
    public static IReadOnlyList<string> ForPrompt(AiConfig cfg, string vault, string agentId,
        string? query, int maxChars = ContextCharacters)
    {
        maxChars = Math.Clamp(maxChars, 200, 10000);
        var results = Search(cfg, vault, agentId, query, 20).ToList();
        // Preferences and pinned decisions should not disappear solely because the question is phrased differently.
        foreach (var item in Visible(cfg, vault, agentId)
                     .Where(m => m.Pinned || m.Kind == "preference")
                     .OrderByDescending(m => m.UpdatedAt))
            if (!results.Any(x => x.Memory.Id == item.Id))
                results.Add(new AiMemoryMatch(item, 0));
        var output = new List<string>();
        var used = 0;
        foreach (var match in results.Take(25))
        {
            var m = match.Memory;
            var source = m.SourceReference.Length > 0 ? m.SourceReference : m.Origin;
            var s = "[" + m.Kind + "; " + m.Scope + "; fonte: " + source + "; id: " + m.Id + "] " +
                m.Text.Replace('\n', ' ').Replace('\r', ' ');
            if (used + s.Length > maxChars) continue;
            output.Add(s);
            used += s.Length;
            if (output.Count >= 12) break;
        }
        return output;
    }

    public static string? ExplicitRememberRequest(string? userMessage)
    {
        var m = RememberCommand.Match(userMessage ?? "");
        if (!m.Success) return null;
        var fact = m.Groups[1].Value.Trim();
        return fact.Length is > 0 and <= MaxText && !PotentialSecret.IsMatch(fact) ? fact : null;
    }

    /// <summary>
    /// Record an observable action, never claim its result was independently verified. No note body
    /// is captured and no cloud model is invoked. Called only after a successfully ended writing run.
    /// </summary>
    public static AiMemoryRecord? ObserveFinishedRun(AiConfig cfg, string vault, string agentId,
        string runId, IReadOnlyList<string> paths, string? request, long? timestamp = null)
    {
        var names = paths.Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Length > 100 ? p[..100] + "…" : p)
            .Distinct(StringComparer.Ordinal).Take(8).ToArray();
        if (names.Length == 0 || string.IsNullOrWhiteSpace(runId)) return null;
        // If the user's request contains credentials, do not retain it even as an excerpt.
        var description = PotentialSecret.IsMatch(request ?? "") ? "[pedido privado]" :
            (request ?? "").Trim().Replace('\n', ' ').Replace('\r', ' ');
        if (description.Length > 180) description = description[..180] + "…";
        return Remember(cfg, "Execução alterou " + string.Join(", ", names) +
            (description.Length > 0 ? ". Pedido: " + description : "") +
            ". Resultado não verificado independentemente.", vault, agentId, "episode", "agent",
            "run", "run:" + runId, names, 50, false, timestamp);
    }

    /// <summary>Resolve an old UI memory identity before migrating, then use canonical IDs.</summary>
    public static AiMemoryRecord? ResolveForEdit(AiConfig cfg, string id)
    {
        if (!id.StartsWith("legacy:", StringComparison.Ordinal))
        {
            MigrateLegacy(cfg);
            return cfg.MemoryRecords.FirstOrDefault(m => m.Id == id);
        }
        var parts = id.Split(':', 3);
        if (parts.Length != 3 || !int.TryParse(parts[2], out var index) ||
            !cfg.Memory.TryGetValue(parts[1], out var values) || index < 0 || index >= values.Count)
            return null;
        var content = values[index];
        var scope = parts[1] == "*" ? "global" : "vault";
        var vault = parts[1] == "*" ? "" : parts[1];
        MigrateLegacy(cfg);
        return cfg.MemoryRecords.FirstOrDefault(m => m.Origin == "legacy" &&
            m.Scope == scope && m.Vault == vault && m.Text == content);
    }

    public static bool Edit(AiConfig cfg, string id, string text, long? at = null)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxText ||
            PotentialSecret.IsMatch(text)) throw new ArgumentException("Texto inválido ou contém credenciais.", nameof(text));
        var m = ResolveForEdit(cfg, id);
        if (m is null) return false;
        m.Text = text.Trim();
        m.UpdatedAt = at ?? Now();
        m.Origin = "user";
        m.Verified = true;
        return true;
    }

    public static bool Forget(AiConfig cfg, string id)
    {
        MigrateLegacy(cfg);
        return cfg.MemoryRecords.RemoveAll(m => m.Id == id) > 0;
    }

    public static bool ChangeState(AiConfig cfg, string id, string state, long? at = null)
    {
        if (state is not ("active" or "disputed" or "superseded" or "archived")) return false;
        MigrateLegacy(cfg);
        var m = cfg.MemoryRecords.FirstOrDefault(x => x.Id == id);
        if (m is null) return false;
        m.State = state;
        m.UpdatedAt = at ?? Now();
        return true;
    }

    public static bool Link(AiConfig cfg, string fromId, string toId)
    {
        MigrateLegacy(cfg);
        var from = cfg.MemoryRecords.FirstOrDefault(m => m.Id == fromId);
        var to = cfg.MemoryRecords.FirstOrDefault(m => m.Id == toId);
        if (from is null || to is null || from.Id == to.Id) return false;
        // No cross-vault or private-agent edges, even if a caller knows the UUID.
        if (from.Scope != to.Scope || from.Vault != to.Vault || from.AgentId != to.AgentId) return false;
        if (!from.RelatedMemoryIds.Contains(toId, StringComparer.Ordinal)) from.RelatedMemoryIds.Add(toId);
        return true;
    }

    public static bool Supersede(AiConfig cfg, string oldId, string newId)
    {
        MigrateLegacy(cfg);
        var previous = cfg.MemoryRecords.FirstOrDefault(m => m.Id == oldId && m.State == "active");
        var next = cfg.MemoryRecords.FirstOrDefault(m => m.Id == newId && m.State == "active");
        if (previous is null || next is null || previous.Id == next.Id) return false;
        if (previous.Scope != next.Scope || previous.Vault != next.Vault || previous.AgentId != next.AgentId) return false;
        previous.State = "superseded";
        previous.ValidUntil = Now();
        previous.UpdatedAt = Now();
        next.RelatedMemoryIds.Add(previous.Id);
        return true;
    }

    /// <summary>
    /// Cheap contradiction candidates, never automatic truth rewriting.
    /// Surface pairs so the user or an authorized agent can resolve them with evidence.
    /// </summary>
    public static IReadOnlyList<(AiMemoryRecord Left, AiMemoryRecord Right)> PotentialConflicts(
        AiConfig cfg, string vault, string agentId)
    {
        var records = Visible(cfg, vault, agentId).Where(m => m.Kind is "fact" or "decision" or "preference")
            .OrderByDescending(m => m.UpdatedAt).Take(240).ToArray();
        var candidates = new List<(AiMemoryRecord, AiMemoryRecord)>();
        for (var i = 0; i < records.Length; i++)
        for (var j = i + 1; j < records.Length; j++)
        {
            var a = records[i]; var b = records[j];
            // Candidate only: similar subjects with changed assertions.
            var ta = Tokens(a.Text); var tb = Tokens(b.Text);
            if (ta.Length < 3 || tb.Length < 3) continue;
            var overlap = ta.Intersect(tb, StringComparer.Ordinal).Count();
            if (overlap >= 3 && overlap >= Math.Min(ta.Length, tb.Length) * .65 &&
                Normalize(a.Text) != Normalize(b.Text))
                candidates.Add((a, b));
            if (candidates.Count >= 25) return candidates;
        }
        return candidates;
    }

    /// <summary>Navigation-only source relations using the already maintained Urbe knowledge index.</summary>
    public static IReadOnlyList<string> NearbyDocumentPaths(AiMemoryRecord memory, KnowledgeIndex index)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in memory.RelatedPaths.Take(8))
        {
            results.Add(path);
            foreach (var doc in index.Links(path).Take(6)) results.Add(doc.Path);
            foreach (var doc in index.Backlinks(path).Take(6)) results.Add(doc.Path);
        }
        return results.Take(32).ToArray();
    }
}
