using System.Globalization;
using System.Runtime.CompilerServices;
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
    // Stored knowledge is not bounded by a fixed memory-count or record-size quota.
    // Model context is budgeted separately from durable storage.

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

    // Exactly one memoized projection per live record (no revision history); weak ownership avoids leaks.
    private sealed class LexicalProjection
    {
        public string Text = "", Kind = "";
        public string[] Paths = [];
        public string NormalizedText = "", NormalizedMeta = "";
        public HashSet<string> TextTerms = new(StringComparer.Ordinal), MetaTerms = new(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<AiMemoryRecord, LexicalProjection> Index = new();
    private static LexicalProjection Projection(AiMemoryRecord m)
    {
        var p = Index.GetValue(m, _ => new LexicalProjection());
        lock (p)
        {
            if (p.Text != m.Text || p.Kind != m.Kind || !p.Paths.SequenceEqual(m.RelatedPaths))
            {
                p.Text = m.Text; p.Kind = m.Kind; p.Paths = m.RelatedPaths.ToArray();
                p.NormalizedText = Normalize(m.Text);
                p.NormalizedMeta = Normalize(m.Kind + " " + string.Join(" ", m.RelatedPaths));
                p.TextTerms = Tokens(m.Text).ToHashSet(StringComparer.Ordinal);
                p.MetaTerms = Tokens(m.Kind + " " + string.Join(" ", m.RelatedPaths)).ToHashSet(StringComparer.Ordinal);
            }
        }
        return p;
    }
    private static bool Phrase(string haystack, string needle)
    {
        if (needle.Length == 0) return false;
        for (int start = 0; (start = haystack.IndexOf(needle, start, StringComparison.Ordinal)) >= 0; start++)
        {
            int end = start + needle.Length;
            if ((start == 0 || !char.IsLetterOrDigit(haystack[start - 1])) &&
                (end == haystack.Length || !char.IsLetterOrDigit(haystack[end]))) return true;
        }
        return false;
    }

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
        var prepared = new List<AiMemoryRecord>();
        var seen = new HashSet<string>(cfg.MemoryRecords.Where(m => m.State == "active")
            .Select(m => m.Scope + "\u001f" + m.Vault + "\u001f" + Normalize(m.Text.Trim())),
            StringComparer.Ordinal);
        foreach (var (key, facts) in cfg.Memory)
        foreach (var fact in facts)
        {
            // Preserve historical text exactly: old installations never had a 1600-char limit.
            if (string.IsNullOrWhiteSpace(fact)) continue;
            var scope = key == "*" ? "global" : "vault";
            var vault = key == "*" ? "" : key;
            if (!seen.Add(scope + "\u001f" + vault + "\u001f" + Normalize(fact.Trim()))) continue;
            prepared.Add(new AiMemoryRecord
            {
                Id = Guid.NewGuid().ToString("N"), Kind = "fact", Scope = scope, Vault = vault,
                Text = fact, Origin = "legacy", SourceReference = "config.memory",
                Confidence = 100, CreatedAt = timestamp ?? Now(), UpdatedAt = timestamp ?? Now()
            });
        }
        // No arbitrary 5000-record quota: old facts are migrated without loss.
        cfg.MemoryRecords.AddRange(prepared);
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
        if (text.Length == 0) throw new ArgumentException("Memória vazia.", nameof(fact));
        if (PotentialSecret.IsMatch(text)) throw new ArgumentException("Credenciais e segredos não podem ser memorizados.", nameof(fact));
        if (scope != "global" && string.IsNullOrWhiteSpace(vault)) throw new ArgumentException("Vault obrigatório.", nameof(vault));
        if (scope == "agent" && string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Agente obrigatório.", nameof(agentId));
        MigrateLegacy(cfg, timestamp);
        var normalized = Normalize(text);
        // A model claiming to have learned something is not equivalent to verified evidence.
        // Human-entered facts retain their specified confidence; agent observations are capped.
        var effectiveConfidence = verified || origin is "user" or "legacy" ? Math.Clamp(confidence, 0, 100)
            : Math.Min(Math.Clamp(confidence, 0, 100), origin == "run" ? 50 : 60);
        var effectiveVault = scope == "global" ? "" : vault;
        var effectiveAgent = scope == "agent" ? agentId : "";
        var existing = cfg.MemoryRecords.FirstOrDefault(m => m.State == "active" &&
            m.Scope == scope && m.Vault == effectiveVault && m.AgentId == effectiveAgent &&
            Normalize(m.Text) == normalized);
        if (existing is not null)
        {
            existing.UpdatedAt = timestamp ?? Now();
            existing.Confidence = Math.Max(existing.Confidence, effectiveConfidence);
            existing.Verified |= verified;
            foreach (var path in paths ?? [])
                if (path.Length > 0 && !existing.RelatedPaths.Contains(path, StringComparer.Ordinal))
                    existing.RelatedPaths.Add(path);
            return existing;
        }
        var record = new AiMemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"), Text = text,
            Kind = kind, Scope = scope, Vault = effectiveVault, AgentId = effectiveAgent,
            Origin = origin, SourceReference = source.Length > 200 ? source[..200] : source,
            Confidence = effectiveConfidence, Verified = verified,
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
            // Old user configurations may contain secrets that predate input validation.
            // Keep them editable locally, but never send them to a model as recalled context.
            if (PotentialSecret.IsMatch(m.Text)) continue;
            var index = Projection(m);
            var score = 0d;
            if (q.Length > 2 && Phrase(index.NormalizedText, q)) score += 14;
            foreach (var term in terms)
            {
                if (index.TextTerms.Contains(term)) score += 2.5;
                else if (index.MetaTerms.Contains(term)) score += 1.5;
            }
            if (terms.Length == 0 && q.Length == 0) score = 1;
            // Short greetings have no useful query tokens. Recency alone must not
            // make every unrelated record relevant to a greeting or acknowledgment.
            if (terms.Length == 0 && q.Length > 0 && score <= 0 &&
                !m.Pinned && m.Kind != "preference") continue;
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
            .Take(Math.Max(1, limit)).ToList();
    }

    /// <summary>
    /// Compatibility entry point for a caller-supplied budget. The budget is context transport,
    /// not a limit on knowledge; all memories remain locally persisted.
    /// </summary>
    public static IReadOnlyList<string> ForPrompt(AiConfig cfg, string vault, string agentId,
        string? query, int maxChars = 3400) =>
        PackPrompt(cfg, vault, agentId, Search(cfg, vault, agentId, query, int.MaxValue), maxChars);

    private static IReadOnlyList<string> PackPrompt(AiConfig cfg, string vault, string agentId,
        IReadOnlyList<AiMemoryMatch> matches, int maxChars)
    {
        maxChars = Math.Max(0, maxChars);
        var results = matches.ToList();
        // Stable preferences and pinned decisions remain useful even without exact query terms.
        foreach (var item in Visible(cfg, vault, agentId)
                     .Where(m => (m.Pinned || m.Kind == "preference") && !PotentialSecret.IsMatch(m.Text))
                     .OrderByDescending(m => m.UpdatedAt))
            if (!results.Any(x => x.Memory.Id == item.Id))
                results.Add(new AiMemoryMatch(item, 0));
        var output = new List<string>();
        var used = 0;
        var seenText = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in results)
        {
            var m = match.Memory;
            var normalized = Normalize(m.Text.Trim());
            // A duplicate global/vault/agent fact costs tokens only once in the system context.
            if (seenText.Contains(normalized)) continue;
            var source = m.SourceReference.Length > 0 ? m.SourceReference : m.Origin;
            var formatted = "[" + m.Kind + "; " + m.Scope + "; fonte: " + source + "; id: " + m.Id + "] " +
                m.Text.Replace('\n', ' ').Replace('\r', ' ');
            if (formatted.Length > maxChars - used)
            {
                // Retain provenance and the beginning of a large result, never silently drop a relevant decision.
                var available = maxChars - used;
                var suffix = " …[parcial; recuperar original com recall_memory, id: " + m.Id + "]";
                var prefix = "[" + m.Kind + "; fonte: " + source + "; id: " + m.Id + "] ";
                if (available < prefix.Length + suffix.Length + 16) continue;
                formatted = prefix + m.Text.Replace('\n', ' ').Replace('\r', ' ')[..Math.Min(m.Text.Length, available - prefix.Length - suffix.Length)] + suffix;
            }
            output.Add(formatted);
            used += formatted.Length;
            seenText.Add(normalized);
        }
        return output;
    }

    /// <summary>
    /// Adaptive first-pass recall. Cheap lexical ranking identifies relevant material; diminishing
    /// returns allocate proportionally less prompt space as memory grows. The model can retrieve
    /// all remaining evidence, without a fixed cap, through paginated recall_memory calls.
    /// </summary>
    public static IReadOnlyList<string> ForPromptAdaptive(AiConfig cfg, string vault, string agentId,
        string? query, long contextTokens, int systemChars = 0, int conversationChars = 0,
        int reservedOutputTokens = 8192)
    {
        // Conservative multilingual token approximation; the provider remains authoritative.
        long window = Math.Max(0, contextTokens) * 3L;
        long output = Math.Min(Math.Max(1, reservedOutputTokens), Math.Max(1, contextTokens / 4L)) * 3L;
        long overhead = Math.Max(4096, contextTokens / 12L * 3L);
        long free = window - output - overhead - Math.Max(0, systemChars) -
            Math.Max(0, conversationChars);
        if (free <= 0) return [];
        var matches = string.IsNullOrWhiteSpace(query) ? Array.Empty<AiMemoryMatch>() :
            Search(cfg, vault, agentId, query, int.MaxValue);
        // When the question itself has no matching subject, only durable preferences
        // and explicitly pinned memories may enter the first context pass.
        IReadOnlyList<AiMemoryMatch> candidates = matches.Count > 0 ? matches :
            Visible(cfg, vault, agentId)
                .Where(m => (m.Kind == "preference" || m.Pinned) &&
                    !PotentialSecret.IsMatch(m.Text))
                .Select(m => new AiMemoryMatch(m, 0)).ToArray();
        if (candidates.Count == 0) return [];

        // Soft initial budget: sqrt(N), not N. For 10,000 matches the automatic recall
        // selects representatives; the agent requests subsequent pages only as required.
        // Deeper requests may consume more, but cannot exceed the selected model's space.
        var avg = candidates.Average(x => Math.Min(x.Memory.Text.Length + 100, 20000));
        var complexity = 1d + Math.Log2(1 + Tokens(query).Length);
        if (Regex.IsMatch(Normalize(query), @"\b(todos|todas|completo|completa|detalhado|profundo|exaustivo|historia|historico)\b"))
            complexity *= 2;
        var desired = Math.Ceiling(avg * Math.Sqrt(candidates.Count) * complexity);
        var allowed = (int)Math.Min(int.MaxValue, Math.Min(free, Math.Max(avg, desired)));
        return PackPrompt(cfg, vault, agentId, candidates, allowed);
    }

    /// <summary>Explicit transport budget from the final request planner; does not reserve a second window.</summary>
    public static IReadOnlyList<string> ForPromptWithin(AiConfig cfg, string vault, string agentId, string? query, int budgetChars) =>
        PackPrompt(cfg, vault, agentId, Search(cfg, vault, agentId, query, int.MaxValue), budgetChars);

    public static string? ExplicitRememberRequest(string? userMessage)
    {
        var m = RememberCommand.Match(userMessage ?? "");
        if (!m.Success) return null;
        var fact = m.Groups[1].Value.Trim();
        return fact.Length > 0 && !PotentialSecret.IsMatch(fact) ? fact : null;
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
        if (string.IsNullOrWhiteSpace(text) || PotentialSecret.IsMatch(text)) throw new ArgumentException("Texto inválido ou contém credenciais.", nameof(text));
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
