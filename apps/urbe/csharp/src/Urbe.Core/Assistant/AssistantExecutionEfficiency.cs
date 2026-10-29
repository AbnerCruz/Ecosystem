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

public sealed class AiExecutionMetrics
{
    public int ReusedReads { get; set; }
    public int NoOpWrites { get; set; }
    public long ContextCharsSaved { get; set; }
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
                var read = ReadSlice(Readable(host, document), Int(input, "start_line"), Int(input, "max_lines"), ReadBudget(host), exactRange: true);
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
