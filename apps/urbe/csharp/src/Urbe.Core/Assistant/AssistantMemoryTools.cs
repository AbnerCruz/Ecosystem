using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Typed memory tools. Changes to historical facts require the same approval as destructive tools.</summary>
public static class AssistantMemoryTools
{
    private static JsonObject Schema(string raw) => JsonNode.Parse(raw)!.AsObject();
    private static string Get(JsonObject o, string key, string fallback = "") => (string?)o[key] ?? fallback;

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new()
        {
            Name = "recall_memory", Access = "read",
            Description = "Procura lembranças por tema e mostra proveniência, confiabilidade e notas relacionadas. Não invente fatos ausentes e não trate memória de execução como verificação do documento.",
            Parameters = Schema("""{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":12}},"required":["query"],"additionalProperties":false}"""),
            Label = i => "Buscar na memória: " + Get(i, "query")[..Math.Min(60, Get(i, "query").Length)],
            Run = (i, host) =>
            {
                var found = host.SearchMemory(Get(i, "query"), Math.Clamp((int?)i["limit"] ?? 6, 1, 12));
                if (found.Count == 0) return Task.FromResult(new AiToolOutput("Não encontrei lembranças correspondentes. Consulte as notas como fonte de verdade."));
                var lines = found.Select(match =>
                {
                    var m = match.Memory;
                    var paths = AssistantMemoryEngine.NearbyDocumentPaths(m, host.Knowledge);
                    return "- " + m.Id + " (" + m.Kind + "; confiança " + m.Confidence +
                        "%; origem " + m.Origin + "; referência " + m.SourceReference + "): " +
                        m.Text.Replace('\n', ' ') +
                        (paths.Count > 0 ? "\n  Fontes e vizinhos: " + string.Join(", ", paths) : "");
                });
                return Task.FromResult(new AiToolOutput(string.Join("\n", lines)));
            }
        },
        new()
        {
            Name = "learn_memory", Access = "memory",
            Description = "Registra um fato, preferência, decisão, objetivo ou PROCEDIMENTO aprendido. Registre procedimentos apenas com evidência de que funcionaram. Evite segredos, conjecturas e conteúdo extenso de notas. Escopo vault ou agent.",
            Parameters = Schema("""{"type":"object","properties":{"fact":{"type":"string"},"kind":{"type":"string","enum":["fact","preference","decision","goal","procedure","observation"]},"scope":{"type":"string","enum":["vault","agent"]},"source_note":{"type":"string"}},"required":["fact","kind"],"additionalProperties":false}"""),
            Label = i => "Aprender " + Get(i, "kind", "fact") + ": " + Get(i, "fact")[..Math.Min(50, Get(i, "fact").Length)],
            Run = (i, host) =>
            {
                var path = Get(i, "source_note");
                if (path.Length > 0 && host.Documents.Get(path) is null)
                    throw new AiToolException("A fonte citada não existe no vault.");
                var memory = host.StoreMemory(Get(i, "fact"), Get(i, "kind"), Get(i, "scope", "vault"), path);
                return Task.FromResult(new AiToolOutput("Memória salva com ID " + memory.Id + ". Fonte: " +
                    (path.Length > 0 ? path : "conversa") + "."));
            }
        },
        new()
        {
            Name = "review_memory", Access = "read",
            Description = "Encontra POSSÍVEIS contradições entre lembranças. São candidatos heurísticos, não confirmação de erro; compare as notas e consulte o usuário antes de decidir.",
            Parameters = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
            Label = _ => "Revisar consistência da memória",
            Run = (_, host) =>
            {
                var conflicts = host.MemoryConflicts();
                return Task.FromResult(new AiToolOutput(conflicts.Count == 0 ?
                    "Nenhum candidato heurístico encontrado. Isso não prova ausência de contradições." :
                    string.Join("\n", conflicts.Take(10).Select(x => x.Left.Id + " (" + x.Left.Text +
                        ") ⇄ " + x.Right.Id + " (" + x.Right.Text + ")"))));
            }
        },
        new()
        {
            Name = "supersede_memory", Access = "destructive",
            Description = "Marca uma lembrança antiga como substituída por outra ativa, mantendo o histórico. Exige confirmação do usuário e fontes adequadas.",
            Parameters = Schema("""{"type":"object","properties":{"old_id":{"type":"string"},"new_id":{"type":"string"}},"required":["old_id","new_id"],"additionalProperties":false}"""),
            Label = i => "Substituir memória " + Get(i, "old_id"),
            Run = (i, host) => Task.FromResult(new AiToolOutput(
                host.SupersedeMemory(Get(i, "old_id"), Get(i, "new_id")) ?
                "A versão antiga foi preservada como substituída." : "IDs incompatíveis ou inexistentes."))
        },
        new()
        {
            Name = "forget_memory", Access = "destructive",
            Description = "Exclui definitivamente uma lembrança privada pelo ID depois de confirmação. Nunca confunda exclusão da memória com exclusão de notas.",
            Parameters = Schema("""{"type":"object","properties":{"id":{"type":"string"}},"required":["id"],"additionalProperties":false}"""),
            Label = i => "Esquecer memória " + Get(i, "id"),
            Run = (i, host) => Task.FromResult(new AiToolOutput(host.ForgetMemory(Get(i, "id")) ?
                "Lembrança apagada do armazenamento local." : "Lembrança indisponível ou fora do escopo atual."))
        }
    ];
}
