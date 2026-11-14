using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AgentRuntime;

namespace Lunet.Ai;

public sealed record LunetAiDocHit(string Id, string Title, string Snippet, double Score);

/// <summary>
/// What the AI knows about Lunet itself (LUNET-488): the Framework API reference, the offline guides and the
/// capabilities document (what exists and what does not). Offline, deterministic keyword search; no embeddings.
/// </summary>
public sealed class LunetAiKnowledge
{
    private const int MaxRead = 16_000;
    private sealed record Entry(string Id, string Title, string Name, string Text, string Normalized);
    private readonly List<Entry> _entries;

    private LunetAiKnowledge(List<Entry> entries) => _entries = entries;

    public int Count => _entries.Count;

    /// <summary>Builds the knowledge base from the API JSON (docs/api/lunet-framework.json) and the guides (name → markdown).</summary>
    public static LunetAiKnowledge Create(string? apiJson, IReadOnlyDictionary<string, string> guides)
    {
        var entries = new List<Entry>();
        var capabilities = ReadEmbedded("capacidades.md");
        entries.Add(Make("lunet:capacidades", "Capacidades do Lunet: o que existe e o que ainda não existe", "capacidades", capabilities));
        if (!string.IsNullOrWhiteSpace(apiJson))
        {
            using var document = JsonDocument.Parse(apiJson);
            foreach (var type in document.RootElement.GetProperty("types").EnumerateArray())
            {
                string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                var name = Str(type, "name");
                var ns = Str(type, "namespace");
                var text = new StringBuilder();
                text.AppendLine($"{Str(type, "signature")}  (namespace {ns})");
                text.AppendLine(Str(type, "summary"));
                if (Str(type, "remarks") is { Length: > 0 } remarks) text.AppendLine(remarks);
                if (type.TryGetProperty("examples", out var examples))
                    foreach (var example in examples.EnumerateArray()) text.AppendLine(example.GetString());
                if (type.TryGetProperty("members", out var members))
                {
                    text.AppendLine("Membros:");
                    foreach (var member in members.EnumerateArray())
                        text.AppendLine($"- {Str(member, "signature")} — {Str(member, "summary")}");
                }
                entries.Add(Make($"api:{ns}.{name}", $"{ns}.{name} ({Str(type, "kind")})", name, text.ToString()));
            }
        }
        foreach (var (name, markdown) in guides.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var title = markdown.Split('\n').FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? name;
            entries.Add(Make("guide:" + name, "Guia: " + title, name, markdown));
        }
        return new LunetAiKnowledge(entries);
    }

    public IReadOnlyList<LunetAiDocHit> Search(string query, int max = 8)
    {
        var terms = Tokens(query).Distinct().ToArray();
        if (terms.Length == 0) return [];
        var hits = new List<LunetAiDocHit>();
        foreach (var entry in _entries)
        {
            double score = 0;
            var title = Normalize(entry.Title);
            var name = Normalize(entry.Name);
            foreach (var term in terms)
            {
                if (name == term) score += 40;
                else if (name.Contains(term, StringComparison.Ordinal)) score += 12;
                if (title.Contains(term, StringComparison.Ordinal)) score += 6;
                int count = Occurrences(entry.Normalized, term);
                if (count > 0) score += 2 + Math.Min(count, 12) * 0.6;
            }
            // Documents matching every term rank first.
            if (terms.All(t => entry.Normalized.Contains(t, StringComparison.Ordinal))) score *= 1.5;
            if (entry.Id == "lunet:capacidades") score *= 1.3;
            if (score > 0) hits.Add(new LunetAiDocHit(entry.Id, entry.Title, Snippet(entry, terms), score));
        }
        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Id, StringComparer.Ordinal).Take(Math.Clamp(max, 1, 20)).ToArray();
    }

    public string? Read(string id)
    {
        var entry = _entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        return entry.Text.Length <= MaxRead ? entry.Text : entry.Text[..MaxRead] + "\n…(truncado)";
    }

    private static Entry Make(string id, string title, string name, string text) => new(id, title, name, text, Normalize(text));

    private static string Snippet(Entry entry, string[] terms)
    {
        int at = -1;
        foreach (var term in terms)
        {
            at = entry.Normalized.IndexOf(term, StringComparison.Ordinal);
            if (at >= 0) break;
        }
        int start = Math.Max(0, at - 120);
        var text = entry.Text.Length > start ? entry.Text[start..] : entry.Text;
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        return text.Length <= 380 ? text : text[..380] + "…";
    }

    private static int Occurrences(string text, string term)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0 && count < 50) { count++; index += term.Length; }
        return count;
    }

    private static IEnumerable<string> Tokens(string text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 2);

    /// <summary>Lowercase, no accents, non-alphanumerics become spaces (same length mapping is not required).</summary>
    private static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return builder.ToString();
    }

    internal static string ReadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lunet.Ai.Knowledge." + name)
                           ?? throw new InvalidOperationException("Recurso ausente: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary><c>lunet.docs.search</c> / <c>lunet.docs.read</c>: the AI studies Lunet instead of guessing APIs.</summary>
public sealed class LunetAiDocsTool : ITool
{
    public const string SearchName = "lunet.docs.search";
    public const string ReadName = "lunet.docs.read";
    private readonly LunetAiKnowledge _knowledge;
    private readonly string _name;
    public ToolDescriptor Descriptor { get; }

    public LunetAiDocsTool(LunetAiKnowledge knowledge, string name)
    {
        if (name is not (SearchName or ReadName)) throw new ArgumentException("Capability desconhecida.", nameof(name));
        _knowledge = knowledge;
        _name = name;
        Descriptor = new ToolDescriptor(name, "1.0.0",
            name == SearchName
                ? "Search Lunet's own documentation: Framework API types/members (ids 'api:…'), offline guides ('guide:…') and the capabilities list of what exists and what does not ('lunet:capacidades'). Use before writing code with an API you have not confirmed."
                : "Read one documentation entry by id returned from lunet.docs.search (full API members with signatures, or a whole guide).",
            name == SearchName
                ? """{"type":"object","properties":{"query":{"type":"string"},"max_results":{"type":"integer","minimum":1,"maximum":20}},"required":["query"],"additionalProperties":false}"""
                : """{"type":"object","properties":{"id":{"type":"string"}},"required":["id"],"additionalProperties":false}""",
            new HashSet<string> { Permissions.AgentAct }, RiskClass.Read,
            ContextPath.Create(new ContextStep(ContextLevel.Ecosystem, "ecosystem"), new ContextStep(ContextLevel.Product, "lunet2d")), true);
    }

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string Arg(string key) => invocation.Arguments.ValueKind == JsonValueKind.Object &&
            invocation.Arguments.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        if (_name == SearchName)
        {
            int max = invocation.Arguments.ValueKind == JsonValueKind.Object &&
                      invocation.Arguments.TryGetProperty("max_results", out var m) && m.TryGetInt32(out var parsed) ? parsed : 8;
            var hits = _knowledge.Search(Arg("query"), max);
            return ValueTask.FromResult(hits.Count == 0
                ? ToolOutcome.Ok("[] (nada encontrado; tente outros termos ou leia lunet:capacidades)")
                : ToolOutcome.Ok(JsonSerializer.Serialize(hits.Select(h => new { id = h.Id, title = h.Title, snippet = h.Snippet }), LunetAiSpriteTool.ReadableJson)));
        }
        var text = _knowledge.Read(Arg("id"));
        return ValueTask.FromResult(text is null ? ToolOutcome.Error("Documento não encontrado: use um id retornado por lunet.docs.search.") : ToolOutcome.Ok(text));
    }
}
