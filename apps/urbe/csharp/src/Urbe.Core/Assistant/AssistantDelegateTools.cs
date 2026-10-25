using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Scoped agent-to-agent consultation: no shared transcript, private memory or direct history APIs.</summary>
public static class AssistantDelegateTools
{
    public static AiTool Consult { get; } = new()
    {
        Name = "consult_agent",
        Access = "ui",
        Description = "Consulte um dos outros agentes que o usuário configurou. Primeiro use sem agent_id para conhecer especialistas e capacidades públicas. Encaminhe somente a tarefa explícita necessária; você NÃO vê conversas, memórias nem instruções privadas dos outros agentes. O Urbe solicitará autorização se necessário, abrirá uma conversa separada para o especialista e trará a resposta. Se não houver agente adequado, recomende que o usuário crie um. Não use para delegar sua própria tarefa sem necessidade.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"agent_id":{"type":"string","description":"ID público do especialista. Deixe vazio para listar agentes configurados."},"task":{"type":"string","description":"Pergunta ou trabalho específico que você quer enviar ao especialista, sem copiar o histórico privado da conversa."}},"additionalProperties":false}""")!,
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
            if (task.Length > 12000) throw new AiToolException("Resuma o pedido ao especialista em até 12.000 caracteres; não envie a conversa inteira.");
            return new AiToolOutput(await gateway.ConsultAsync(agentId, task));
        }
    };
}
