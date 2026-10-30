using System.Text.Json.Nodes;
namespace Urbe.Core.Assistant;

/// <summary>Version-aware identity and exact evidence reuse. No network/memory/UI caches and no token quotas.</summary>
public static class AssistantExecutionEfficiency
{
    public static bool Cacheable(AiTool? tool) => tool?.Access == "read" && tool.Name is
        "page_schema" or "read_note" or "read_notes" or "list_notes" or "search_notes" or "grep_notes" or "get_links" or "get_tags" or "search_tags";
    public static string Signature(string name, JsonNode? input, long revision) => revision + ":" + name + ":" + Canonical(input)?.ToJsonString();
    private static JsonNode? Canonical(JsonNode? input) => input switch
    {
        JsonObject value => new JsonObject(value.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => KeyValuePair.Create(x.Key, Canonical(x.Value)))),
        JsonArray value => new JsonArray(value.Select(Canonical).ToArray()),
        _ => input?.DeepClone()
    };
}

/// <summary>
/// What one run did and where its time went (CST-094/095). Only counts and durations: never the content of notes,
/// prompts or answers. Kept in the conversation's own run record, so it lives and goes with the conversation.
/// Time not measured is never estimated: there is no "reasoning time", only waiting for the provider.
/// </summary>
public sealed class AiExecutionMetrics
{
    public int ReusedReads { get; set; }
    public int NoOpWrites { get; set; }
    public long ContextCharsSaved { get; set; }
    /// <summary>From the start of the run to its end, ms (0: a run recorded before these measures).</summary>
    public long TotalMs { get; set; }
    /// <summary>Until the first change was written, or the answer when nothing was changed.</summary>
    public long? FirstUsefulMs { get; set; }
    /// <summary>Waiting for the model's provider (network and generation), summed over its calls.</summary>
    public long ProviderMs { get; set; }
    /// <summary>The tools running, without the time spent waiting for the person's approval.</summary>
    public long ToolMs { get; set; }
    public long ApprovalMs { get; set; }
    public int ModelCalls { get; set; }
    public int ToolCalls { get; set; }
    public int ToolFailures { get; set; }
    public int Rejected { get; set; }
    /// <summary>Calls identical to one that had already failed.</summary>
    public int Repeats { get; set; }
    /// <summary>Tool calls that changed files.</summary>
    public int Edits { get; set; }
    /// <summary>Files written in the run that the agent read again afterwards (a check of what was saved).</summary>
    public int Verified { get; set; }
    /// <summary>The run ended by itself with an answer (not interrupted, cut, looping or failed).</summary>
    public bool Completed { get; set; }

    /// <summary>The line under a run: where the time went and what was done, as measured (null for an unmeasured run).</summary>
    public string? Describe()
    {
        if (TotalMs <= 0) return null;
        var parts = new List<string> { "Levou " + AiRunStats.Seconds(TotalMs) };
        if (ProviderMs > 0) parts.Add("modelo " + AiRunStats.Seconds(ProviderMs));
        if (ToolMs > 0) parts.Add("ferramentas " + AiRunStats.Seconds(ToolMs));
        if (ApprovalMs > 0) parts.Add("aguardando você " + AiRunStats.Seconds(ApprovalMs));
        if (ToolCalls > 0)
            parts.Add(ToolCalls + (ToolCalls == 1 ? " chamada" : " chamadas") +
                      (ToolFailures + Repeats > 0 ? ", " + (ToolFailures + Repeats) + (ToolFailures + Repeats == 1 ? " falhou" : " falharam") : "") +
                      (Rejected > 0 ? ", " + Rejected + (Rejected == 1 ? " recusada" : " recusadas") : ""));
        if (Edits > 0) parts.Add(Edits + (Edits == 1 ? " edição" : " edições") + ", " + Verified + (Verified == 1 ? " arquivo conferido" : " arquivos conferidos"));
        return string.Join(" · ", parts);
    }

    /// <summary>Without the measures (CST-094 turned off): only the counters kept since before them.</summary>
    public AiExecutionMetrics CountersOnly() => new() { ReusedReads = ReusedReads, NoOpWrites = NoOpWrites, ContextCharsSaved = ContextCharsSaved };
}

/// <summary>
/// CST-096: many runs together, as the owner asked: latency (median and p90) read with completion, rework and
/// checked work, never speed alone. Runs recorded before the measures are left out.
/// </summary>
public sealed record AiRunStats(int Runs, long MedianMs, long P90Ms, double Completion, double FailuresPerRun, double? VerifiedShare)
{
    public static AiRunStats Of(IEnumerable<AiRunRecord> runs)
    {
        var measured = runs.Where(r => r.Efficiency.TotalMs > 0).ToList();
        if (measured.Count == 0) return new(0, 0, 0, 0, 0, null);
        var times = measured.Select(r => r.Efficiency.TotalMs).OrderBy(t => t).ToList();
        long median = times.Count % 2 == 1 ? times[times.Count / 2] : (times[times.Count / 2 - 1] + times[times.Count / 2]) / 2;
        long p90 = times[Math.Clamp((int)Math.Ceiling(times.Count * 0.9) - 1, 0, times.Count - 1)];
        int edits = measured.Sum(r => r.Efficiency.Edits);
        return new(measured.Count, median, p90,
            measured.Count(r => r.Efficiency.Completed) / (double)measured.Count,
            measured.Sum(r => r.Efficiency.ToolFailures + r.Efficiency.Repeats) / (double)measured.Count,
            edits == 0 ? null : Math.Min(1, measured.Sum(r => r.Efficiency.Verified) / (double)edits));
    }

    public string Describe() => Runs == 0 ? "Sem execuções medidas ainda." :
        Runs + (Runs == 1 ? " execução" : " execuções") + " · mediana " + Seconds(MedianMs) + " · p90 " + Seconds(P90Ms) +
        " · " + Percent(Completion) + " concluídas · " + FailuresPerRun.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " falhas por execução" +
        (VerifiedShare is { } v ? " · " + Percent(v) + " das edições conferidas" : "");

    public static string Seconds(long ms) => ms < 60_000
        ? (ms / 1000.0).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " s"
        : ms / 60_000 + " min " + ms % 60_000 / 1000 + " s";

    private static string Percent(double share) => Math.Round(share * 100) + "%";
}

public static partial class AssistantTools
{
    /// <summary>The native range contract respects max_lines; the frozen legacy oracle retains its old contract.</summary>
    public static AiTool EfficientRead(AiTool tool)
    {
        if (tool.Name != "read_note") return tool;
        var parameters = (JsonObject)tool.Parameters.DeepClone();
        parameters["properties"]!["max_lines"]!["description"] = "Quantidade máxima de linhas do trecho solicitado; não solicita leitura da nota inteira.";
        return new AiTool
        {
            Name = tool.Name, Access = tool.Access, Parameters = parameters, Label = tool.Label,
            Description = "Lê a nota ou um trecho com linhas e versão. Use start_line/max_lines para consultar só a seção necessária. Leitura já recebida continua válida enquanto a versão não mudar. Não releia após cada edição sem necessidade.",
            Run = (input, host) =>
            {
                var document = ResolveDoc(host, Str(input, "path"))!;
                // the agent reads what it may write: the note without the seals the Urbe manages (UC-51)
                var read = ReadSlice(Readable(host, document), Int(input, "start_line"), Int(input, "max_lines"), ReadBudget(host), exactRange: true, inherited: host.Knowledge.Inherited(document.Id));
                return Task.FromResult(new AiToolOutput("Versão da nota: " + document.Revision + "\n" + read.Text, Reads: [(document.Path, read.Partial)], Seen: [read.Seen]));
            }
        };
    }
}

/// <summary>
/// What one agent run has read of each note: the seal-free lines, at a revision. A whole rewrite (write_note) of a
/// note it read must start from the current revision, read in full; otherwise it would put back an old copy or drop
/// the lines it never saw (owner's report on long dictionary revisions). Its own writes keep what it knows current.
/// </summary>
public sealed class AiReadCoverage
{
    private sealed class Entry
    {
        public int Revision, Total;
        public bool Whole;
        public readonly List<(int From, int To)> Ranges = [];
    }

    private readonly Dictionary<string, Entry> _notes = new(StringComparer.OrdinalIgnoreCase);

    public void Read(AiSeen seen)
    {
        if (!_notes.TryGetValue(seen.Path, out var entry) || entry.Revision != seen.Revision)
            _notes[seen.Path] = entry = new Entry { Revision = seen.Revision };
        entry.Total = seen.Total;
        entry.Ranges.Add((seen.From, seen.To));
        entry.Whole = entry.Whole || Covered(entry.Ranges) >= seen.Total;
    }

    /// <summary>The run wrote the note: <paramref name="full"/> when it wrote all of its text (create/write).</summary>
    public void Wrote(string? beforePath, string path, int revisionBefore, int revisionAfter, bool full)
    {
        Entry? prior = null;
        if (beforePath is not null && _notes.Remove(beforePath, out var old)) prior = old;
        else if (_notes.TryGetValue(path, out var same)) prior = same;
        _notes[path] = new Entry { Revision = revisionAfter, Whole = full || prior is { Whole: true } && prior.Revision == revisionBefore };
    }

    /// <summary>Why a whole rewrite of <paramref name="current"/> must not go ahead now, or null.</summary>
    public string? RewriteBlocker(UrbeDocument current)
    {
        if (!_notes.TryGetValue(current.Path, out var entry)) return null;   // never read: a deliberate replacement
        if (entry.Revision != current.Revision)
            return "A nota " + current.Path + " mudou desde a sua última leitura (versão " + entry.Revision + " → " + current.Revision +
                   "; o usuário ou outra ação a alterou). write_note trocaria a nota inteira pela cópia antiga. Nada foi gravado. " +
                   "Leia a versão atual (read_note) e refaça a alteração sobre ela; para mudanças parciais, use edit_note.";
        if (!entry.Whole)
            return "Você leu só parte de " + current.Path + (entry.Ranges.Count > 0
                       ? " (" + string.Join(", ", Merge(entry.Ranges).Select(r => r.From == r.To ? r.From.ToString() : r.From + "–" + r.To)) + " de " + entry.Total + " linhas)"
                       : " (os trechos das suas edições)") +
                   ". write_note substituiria também o que você não leu. Nada foi gravado. Leia a nota inteira (read_note) ou use edit_note nas partes que conhece.";
        return null;
    }

    private static int Covered(List<(int From, int To)> ranges) => Merge(ranges).Sum(r => r.To - r.From + 1);

    private static List<(int From, int To)> Merge(List<(int From, int To)> ranges)
    {
        var merged = new List<(int From, int To)>();
        foreach (var r in ranges.OrderBy(r => r.From))
        {
            if (merged.Count > 0 && r.From <= merged[^1].To + 1) merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, r.To));
            else merged.Add(r);
        }
        return merged;
    }
}
