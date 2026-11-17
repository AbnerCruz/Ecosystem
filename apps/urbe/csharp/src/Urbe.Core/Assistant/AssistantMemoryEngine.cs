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
    // Narrow lexical aliases improve high-precision offline recall without an embedding download.
    // These are not a semantic search engine; keep associations conservative and tested.
    private static readonly string[][] RecallAliases =
    [
        ["pet", "cachorro", "cachorra", "cao", "cadela"],
        ["objetivo", "meta"],
        ["preferencia", "preferencias"],
        ["automovel", "carro"]
    ];

    private static IEnumerable<string> AliasesFor(string term) =>
        RecallAliases.Where(group => group.Contains(term, StringComparer.Ordinal))
            .SelectMany(group => group).Where(alias => alias != term);

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

    /// <summary>The ids of the memories in prompt lines made by <see cref="ForPrompt"/> and its kin ("…; id: X]").</summary>
    public static IReadOnlyList<string> IdsIn(IEnumerable<string> lines) =>
        lines.Select(l => System.Text.RegularExpressions.Regex.Match(l, @"\bid: ([^\]\s;]+)\]")).Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct().ToList();

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

    private static readonly HashSet<string> Negations = new(StringComparer.Ordinal) { "nao", "não", "nunca", "jamais", "nem", "never", "not" };

    private static string[] Tokens(string? s) =>
        Words.Matches(Normalize(s)).Select(m => m.Value)
            .Where(w => w.Length > 2 && w is not ("que" or "para" or "com" or "uma" or "dos" or "das" or "por" or "sobre" or "the" or "and" or "qual" or "quais" or "meu" or "minha" or "como" or "quem" or "onde" or "quando"))
            .Select(Singular).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// A light Portuguese plural reduction on accent-folded words, the same on both sides, so "estrelas" finds "Estrela"
    /// and "lição" finds "lições". Words of four letters or fewer are left as they are ("mais" is not "mal").
    /// </summary>
    internal static string Singular(string w)
    {
        if (w.Length <= 4) return w;
        if (w.EndsWith("oes", StringComparison.Ordinal) || w.EndsWith("aes", StringComparison.Ordinal)) return w[..^3] + "ao";
        if (w.EndsWith("ais", StringComparison.Ordinal)) return w[..^3] + "al";
        if (w.EndsWith("eis", StringComparison.Ordinal)) return w[..^3] + "el";
        if (w.EndsWith("ns", StringComparison.Ordinal)) return w[..^2] + "m";
        if (w.EndsWith("res", StringComparison.Ordinal) || w.EndsWith("zes", StringComparison.Ordinal) || w.EndsWith("ses", StringComparison.Ordinal)) return w[..^2];
        if (w[^1] == 's' && w[^2] is not ('s' or 'u' or 'i')) return w[..^1];
        return w;
    }

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
            RelatedPaths = (paths ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).ToList()
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
        // Expand once per query, never allocate synonym enumerators for every stored record.
        var expandedTerms = terms.Select(term => (Term: term, Aliases: AliasesFor(term).ToArray())).ToArray();
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
            foreach (var (term, aliases) in expandedTerms)
            {
                if (index.TextTerms.Contains(term)) score += 2.5;
                else if (index.MetaTerms.Contains(term)) score += 1.5;
                else if (aliases.Any(index.TextTerms.Contains)) score += 1.25;
                else if (aliases.Any(index.MetaTerms.Contains)) score += .75;
            }
            if (terms.Length == 0 && q.Length == 0) score = 1;
            // Short greetings have no useful query tokens. Recency alone must not
            // make every unrelated record relevant to a greeting or acknowledgment.
            if (terms.Length == 0 && q.Length > 0 && score <= 0 &&
                !m.Pinned && m.Kind != "preference") continue;
            if (terms.Length > 0 && score <= 0 && !m.Pinned) continue;
            if (m.Pinned) score += 5;
            if (m.Kind is "preference" or "decision" or "procedure" && score > 0) score += 1.5;
            // a log of what a run did is evidence, not learned knowledge: it never outranks what the person said
            if (m.Kind == "episode") score *= .6;
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
        // What the person told: the matches that are not run logs first; then the standing preferences, procedures
        // ("sempre que eu pedir…") and pinned memories, useful even without the question's words; the run logs last.
        // Before, many run logs matching the question filled the room and the person's own preferences never arrived
        // (owner's audit, beta.54: "only episodic memories in context").
        var results = matches.Where(x => x.Memory.Kind != "episode").ToList();
        foreach (var item in Visible(cfg, vault, agentId)
                     .Where(m => (m.Pinned || m.Kind is "preference" or "procedure") && !PotentialSecret.IsMatch(m.Text))
                     .OrderByDescending(m => m.UpdatedAt))
            if (!results.Any(x => x.Memory.Id == item.Id))
                results.Add(new AiMemoryMatch(item, 0));
        results.AddRange(matches.Where(x => x.Memory.Kind == "episode"));
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

    /// <summary>Resolve a short follow-up against the nearest substantive user request, without a classifier or LLM call.</summary>
    public static string ContinuityQuery(IReadOnlyList<AiChatMessage> conversation)
    {
        var requests = conversation.Where(m => m.Role == "user" && m.Meta?.ToolResults != true)
            .Select(m => string.Join(" ", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text)))
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (requests.Length == 0) return "";
        var last = requests[^1].Trim();
        var plain = Normalize(last).Trim(' ', '.', '!', '?');
        bool followUp = System.Text.RegularExpressions.Regex.IsMatch(plain,
            @"^(?:continua|continue|prossegue|prossiga|faz|faca|pode seguir|pode continuar|segue|mantem isso|mantenha isso|pode fazer|vai|isso mesmo)(?:\s+(?:por favor|com isso|assim|ai|agora))?$",
            RegexOptions.CultureInvariant);
        if (!followUp) return last;
        for (int i = requests.Length - 2; i >= 0 && i >= requests.Length - 8; i--)
        {
            var previous = requests[i].Trim();
            if (previous.Length > 12 && !Regex.IsMatch(Normalize(previous),
                @"^(?:continua|faz|pode seguir|mantem isso|obrigado|ok|beleza)\s*[.!?]?$"))
                return previous + " " + last;
        }
        return last;
    }

    /// <summary>Explicit transport budget from the final request planner; does not reserve a second window.</summary>
    public static IReadOnlyList<string> ForPromptWithin(AiConfig cfg, string vault, string agentId, string? query, int budgetChars) =>
        PackPrompt(cfg, vault, agentId, Search(cfg, vault, agentId, query, int.MaxValue), budgetChars);

    /// <summary>
    /// Capture only unambiguous first-person preferences, goals, decisions and procedures from a successful
    /// user turn, returning what was learned or reinforced. No extra model calls, no invisible capture of arbitrary dialogue or attachments.
    /// Inferred memories are agent-local; explicitly shared memories still use "lembre que".
    /// </summary>
    public static IReadOnlyList<AiMemoryRecord> CaptureUserStatements(
        AiConfig cfg, string vault, string agentId, string? userMessage, string source)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || userMessage.Length > 4000 ||
            string.IsNullOrWhiteSpace(agentId) || userMessage.Contains("```", StringComparison.Ordinal) ||
            ExplicitRememberRequest(userMessage) is not null)
            return [];

        var learned = new List<AiMemoryRecord>();
        var sentences = Regex.Split(userMessage, @"(?<=[.!?])\s+|\r?\n+");
        foreach (var raw in sentences.Take(12))
        {
            var sentence = raw.Trim();
            if (sentence.Length is < 12 or > 260 || sentence.EndsWith('?') ||
                PotentialSecret.IsMatch(sentence) || sentence.StartsWith('>') ||
                sentence.StartsWith('-') || sentence.StartsWith('"')) continue;

            var normalized = Normalize(sentence);
            var kind = StatementKind(normalized);
            if (kind is null) continue;

            // Said again (in this or another conversation): the same memory, reinforced, with the new source kept.
            if (cfg.MemoryRecords.FirstOrDefault(m => m.State == "active" && m.Scope == "agent" &&
                m.Vault == vault && m.AgentId == agentId && Normalize(m.Text) == normalized) is { } known)
            {
                if (known.SourceReference.Contains(source, StringComparison.Ordinal)) continue; // the same turn again
                known.Confidence = Math.Min(100, known.Confidence + 5);
                known.UpdatedAt = Now();
                var sources = known.SourceReference + "; " + source;
                known.SourceReference = sources.Length > 200 ? sources[^200..] : sources;
                learned.Add(known);
                continue;
            }
            var record = Remember(cfg, sentence, vault, agentId, kind, "agent", "user", source, confidence: 85, verified: true);
            // Learning, not only logging: the person's newer word replaces what it contradicts (kept, dated, linked),
            // and a more specific statement is linked to the general one it refines, both still true.
            foreach (var other in cfg.MemoryRecords.Where(m => m.State == "active" && m.Id != record.Id && m.Scope == "agent" &&
                m.Vault == vault && m.AgentId == agentId && m.Kind is not ("episode" or "observation")).ToList())
                switch (Relate(other.Text, record.Text))
                {
                    case MemoryRelation.Contradicts: Supersede(cfg, other.Id, record.Id); break;
                    case MemoryRelation.Refines: Link(cfg, record.Id, other.Id); break;
                }
            learned.Add(record);
        }
        return learned;
    }

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
        // owner's audit (beta.54): episodes kept a cut request and cut paths; they keep them whole now (DEC-0059)
        var names = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).ToArray();
        if (names.Length == 0 || string.IsNullOrWhiteSpace(runId)) return null;
        // If the user's request contains credentials, do not retain it even as an excerpt.
        var description = PotentialSecret.IsMatch(request ?? "") ? "[pedido privado]" :
            System.Text.RegularExpressions.Regex.Replace((request ?? "").Trim(), @"\s*[\r\n]+\s*", " ");
        return Remember(cfg, "Execução alterou " + string.Join(", ", names) +
            (description.Length > 0 ? ". Pedido: " + description : "") +
            ". Resultado não verificado independentemente.", vault, agentId, "episode", "agent",
            "run", "run:" + runId, names, 50, false, timestamp);
    }

    /// <summary>
    /// CST-088: what a memory is worth, apart: what the app recorded happening (a task that changed files), what an
    /// agent declared, what the person said, and what was checked. Shown to the person and to the agent that recalls it.
    /// </summary>
    public static string Standing(AiMemoryRecord m) =>
        m.Verified ? "verificada"
        : m.Origin switch
        {
            "run" => "registrada pelo app; o resultado não foi conferido",
            "agent" => "declarada pelo agente; não conferida",
            "user" or "legacy" => "dita pelo usuário",
            _ => "não conferida"
        };

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

    /// <summary>
    /// CST-134: notes renamed or moved (each file, as the vault's relink lists them): every memory that came from one
    /// points to it at its new path, so the memory graph and the sources stay on the real note. Returns how many changed.
    /// </summary>
    public static int RetargetPaths(AiConfig cfg, IReadOnlyCollection<(string From, string To)> files)
    {
        MigrateLegacy(cfg);
        var moved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (from, to) in files) moved[from] = to;
        int changed = 0;
        foreach (var m in cfg.MemoryRecords)
        {
            if (!m.RelatedPaths.Any(moved.ContainsKey)) continue;
            m.RelatedPaths = m.RelatedPaths.Select(p => moved.GetValueOrDefault(p, p)).Distinct(StringComparer.Ordinal).ToList();
            changed++;
        }
        return changed;
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
            if (Relate(a.Text, b.Text) == MemoryRelation.Contradicts) candidates.Add((a, b));
            if (candidates.Count >= 25) return candidates;
        }
        return candidates;
    }

    public enum MemoryRelation { None, Refines, Contradicts }

    /// <summary>
    /// How two statements relate. The subject is read apart from negation ("não gosta de X" talks about what
    /// "gosta de X" does); one saying more about the same subject refines it (strogonoff → strogonoff de frango).
    /// </summary>
    // How people open a statement: "Eu…", "Na verdade, …", "Mudei de ideia: …" (owner's audit, beta.54).
    private static readonly Regex Lead = new(@"^(?:(?:eu|ah|ok|olha|bom|entao|sinceramente|na verdade|pensando bem|agora|hoje em dia|mudei de ideia|corrigindo)\s*[,:;—-]?\s+)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Liking = new(
        @"^(?<neg>(?:nao|nunca|ja nao|nem)\s+)?(?<verb>voltei a gostar|passei a gostar|deixei de gostar|parei de gostar|gosto|curto|adoro|amo|odeio|detesto|suporto|aguento|gostei|curti|adorei|amei|odiei|detestei)(?:\s+mais)?\s+(?:(?:de|do|da|dos|das|que|quando|o|a|os|as)\s+)?(?<obj>\S.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Disliking = new(StringComparer.Ordinal) { "odeio", "detesto", "odiei", "detestei", "deixei de gostar", "parei de gostar" };
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal) { "mais", "muito", "muita", "bastante", "demais", "tambem", "ainda", "agora", "ontem", "hoje", "sim", "realmente" };

    /// <summary>
    /// A first-person liking or disliking: whether it is for or against, and what it is about (the words after the verb,
    /// up to a "mas" that adds something else). "Odeio X" and "gosto de X" are opposite even without a "não".
    /// </summary>
    internal static (bool Positive, HashSet<string> About)? PreferenceOf(string sentence)
    {
        var m = Liking.Match(Lead.Replace(Normalize(sentence).Trim(), ""));
        if (!m.Success) return null;
        var about = Regex.Split(m.Groups["obj"].Value, @",?\s+mas\s+|;|,\s+porem\s+")[0];
        var words = Tokens(about).Where(w => !Fillers.Contains(w) && !Negations.Contains(w)).ToHashSet(StringComparer.Ordinal);
        if (words.Count == 0) return null;
        return (Disliking.Contains(m.Groups["verb"].Value) == m.Groups["neg"].Success, words);
    }

    public static MemoryRelation Relate(string a, string b)
    {
        if (Normalize(a) == Normalize(b)) return MemoryRelation.None;
        if (PreferenceOf(a) is { } pa && PreferenceOf(b) is { } pb)
        {
            if (pa.About.SetEquals(pb.About)) return pa.Positive != pb.Positive ? MemoryRelation.Contradicts : MemoryRelation.None;
            if (pa.Positive == pb.Positive && (pa.About.IsProperSubsetOf(pb.About) || pb.About.IsProperSubsetOf(pa.About))) return MemoryRelation.Refines;
            return MemoryRelation.None;   // a different thing, or a specific case of the opposite: neither replaces the other
        }
        var ta = Tokens(a); var tb = Tokens(b);
        var ca = ta.Where(w => !Negations.Contains(w)).ToHashSet(StringComparer.Ordinal);
        var cb = tb.Where(w => !Negations.Contains(w)).ToHashSet(StringComparer.Ordinal);
        if (ca.Count < 2 || cb.Count < 2) return MemoryRelation.None;
        if (ca.IsProperSubsetOf(cb) || cb.IsProperSubsetOf(ca)) return MemoryRelation.Refines;
        bool opposite = ta.Any(Negations.Contains) != tb.Any(Negations.Contains);
        var overlap = ca.Intersect(cb).Count();
        return ca.SetEquals(cb) && opposite || overlap >= 3 && overlap >= Math.Min(ca.Count, cb.Count) * .65
            ? MemoryRelation.Contradicts : MemoryRelation.None;
    }

    /// <summary>The kind of a first-person statement worth learning, or null.</summary>
    private static string? StatementKind(string normalized) => StatementKindOf(Lead.Replace(normalized.Trim(), ""));

    private static string? StatementKindOf(string normalized) =>
        Regex.IsMatch(normalized, @"^(?:prefiro|minha preferencia e)\s*\S", RegexOptions.CultureInvariant) || PreferenceOf(normalized) is not null ? "preference"
        : Regex.IsMatch(normalized, @"^(?:meu objetivo e|minha meta e|quero aprender)\s+\S", RegexOptions.CultureInvariant) ? "goal"
        : Regex.IsMatch(normalized, @"^(?:decidi|decidimos|a decisao e|vou usar|vamos usar)\s+\S", RegexOptions.CultureInvariant) ? "decision"
        : Regex.IsMatch(normalized, @"^(?:sempre que|toda vez que|quando eu pedir)\s+\S", RegexOptions.CultureInvariant) ? "procedure"
        : null;

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
