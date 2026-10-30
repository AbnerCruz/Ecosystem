using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Scoped agent-to-agent consultation: no shared transcript, private memory or direct history APIs.</summary>
public static class AssistantDelegateTools
{
    public static AiTool Consult { get; } = new()
    {
        Name = "consult_agent",
        Access = "ui",
        Description = "Consulte um dos outros agentes que o usuário configurou. Primeiro use sem agent_id para conhecer especialistas e capacidades públicas. Encaminhe somente a tarefa explícita necessária; você NÃO vê conversas, memórias nem instruções privadas dos outros agentes. O Urbe solicitará autorização se necessário, abrirá uma conversa separada para o especialista e trará a resposta. Para continuar a conversa com ESSE MESMO especialista, informe conversation_id devolvido pela consulta anterior, sem tentar acessar conversas alheias. Se não houver agente adequado, recomende que o usuário crie um. Não use para delegar sua própria tarefa sem necessidade.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"agent_id":{"type":"string","description":"ID público do especialista. Deixe vazio para listar agentes configurados."},"task":{"type":"string","description":"Pergunta ou trabalho específico que você quer enviar ao especialista, sem copiar o histórico privado da conversa."},"conversation_id":{"type":"string","description":"Opcional: ID devolvido por uma consulta anterior DESTA conversa para continuar o diálogo com o mesmo especialista."}},"additionalProperties":false}""")!,
        Label = i => string.IsNullOrWhiteSpace((string?)i["agent_id"]) ? "Procurando agentes disponíveis" : "Consultando agente " + ((string?)i["agent_id"] ?? ""),
        Run = async (i, host) =>
        {
            var gateway = host.Delegation ?? throw new AiToolException("Colaboração entre agentes indisponível neste ambiente.");
            var agentId = ((string?)i["agent_id"] ?? "").Trim();
            if (agentId.Length == 0)
            {
                var targets = gateway.Targets;
                if (targets.Count == 0)
                    return new AiToolOutput("Nenhum outro agente está configurado. Recomende ao usuário criar um especialista em Configurações → Agentes → Novo agente, escolhendo os modelos e permissões adequados.");
                return new AiToolOutput("Agentes configurados (somente perfis públicos, sem histórico ou memória):\n" +
                    string.Join("\n", targets.Select(a => "- " + a.Id + " · " + a.Name + " · " + a.Description +
                        (a.CanGenerateImages ? " · gera imagens" : " · sem modelo de imagem"))));
            }
            var task = ((string?)i["task"] ?? "").Trim();
            if (task.Length == 0) throw new AiToolException("Explique a tarefa que deseja enviar ao especialista.");
            return new AiToolOutput(await gateway.ConsultAsync(agentId, task, ((string?)i["conversation_id"] ?? "").Trim()));
        }
    };
    /// <summary>
    /// CST-092: what a consulted specialist changed in the vault, for the agent that asked (which must say what changed in
    /// its answer): created, changed, moved or sent to the trash, one line each, in the order they happened.
    /// </summary>
    public static IReadOnlyList<string> DescribeChanges(IEnumerable<AiChange> changes)
    {
        var lines = new List<string>();
        foreach (var c in changes)
        {
            var line = (c.Before, c.After, c.Trashed) switch
            {
                (_, _, true) => "moveu para a lixeira " + (c.Before ?? c.After)?.Path,
                (null, { } after, _) => "criou " + after.Path,
                ({ } before, { } after, _) when before.Path != after.Path => "moveu " + before.Path + " → " + after.Path,
                (_, { } after, _) => "alterou " + after.Path,
                ({ } before, null, _) => "apagou " + before.Path,
                _ => null
            };
            if (line is not null && !lines.Contains(line)) lines.Add(line);
        }
        return lines;
    }
}
