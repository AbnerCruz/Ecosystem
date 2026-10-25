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
                var read = ReadSlice(document, Int(input, "start_line"), Int(input, "max_lines"), ReadBudget(host), exactRange: true);
                return Task.FromResult(new AiToolOutput("Versão da nota: " + document.Revision + "\n" + read.Text, Reads: [(document.Path, read.Partial)]));
            }
        };
    }
}
