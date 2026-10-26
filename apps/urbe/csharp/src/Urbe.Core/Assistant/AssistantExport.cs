using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// Explicit, portable snapshots of local assistant data. Neither export reads nor serializes AiConfig.Providers:
/// credentials live there, outside the exported conversation and agent memories.
/// Full mode preserves every persisted block, including hidden context, tool arguments/results and per-run metrics.
/// </summary>
public static class AssistantExport
{
    private const string Schema = "urbe.assistant.export/1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string Conversation(AiConversation conversation, bool complete, string? appVersion = null, long? at = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (!complete) return ConversationMarkdown(conversation, appVersion);
        var envelope = new JsonObject
        {
            ["schema"] = Schema, ["kind"] = "conversation", ["mode"] = "complete",
            ["exportedAt"] = at ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["appVersion"] = appVersion,
            ["conversation"] = AssistantStore.ConversationToJson(conversation)
        };
        return envelope.ToJsonString(Json);
    }

    public static string Memory(AiConfig config, string agentId, bool complete, string? appVersion = null, long? at = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("Agente obrigatório.", nameof(agentId));
        // Do not include global/vault memories owned by other contexts or legacy unscoped entries.
        // All states are retained: disputed, superseded and archived facts matter for forensic exports.
        var memories = config.MemoryRecords.Where(m => m.Scope == "agent" && m.AgentId == agentId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
        if (!complete)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Memória do agente " + agentId);
            sb.AppendLine();
            sb.AppendLine("Formato: " + Schema + " · modo leve · versão do Urbe: " + (appVersion ?? "desconhecida"));
            sb.AppendLine();
            foreach (var m in memories)
            {
                sb.AppendLine("## " + m.Kind + " · " + m.Id);
                sb.AppendLine();
                sb.AppendLine(m.Text);
                sb.AppendLine();
                sb.AppendLine("Estado: " + m.State + " · origem: " + m.Origin + " · vault: " + m.Vault);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        var envelope = new JsonObject
        {
            ["schema"] = Schema, ["kind"] = "agent-memory", ["mode"] = "complete",
            ["exportedAt"] = at ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["appVersion"] = appVersion, ["agentId"] = agentId,
            ["memories"] = JsonSerializer.SerializeToNode(memories, Json)
        };
        return envelope.ToJsonString(Json);
    }

    private static string ConversationMarkdown(AiConversation conversation, string? version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# " + (string.IsNullOrWhiteSpace(conversation.Title) ? "Conversa" : conversation.Title));
        sb.AppendLine();
        sb.AppendLine("Formato: " + Schema + " · modo leve · versão do Urbe: " + (version ?? "desconhecida"));
        sb.AppendLine("Conversa: " + conversation.Id + " · agente: " + conversation.AgentId + " · modelo: " + conversation.Model);
        sb.AppendLine();
        // The lightweight view remains chronological and displays tools without dropping them silently.
        foreach (var message in conversation.Messages.Concat(conversation.QueuedMessages))
        {
            sb.AppendLine("## " + message.Role + " · " + message.Id);
            sb.AppendLine();
            foreach (var block in message.Content)
            {
                switch (block.Type)
                {
                    case "text":
                        if (!string.IsNullOrWhiteSpace(block.Text) && !block.Hidden) sb.AppendLine(block.Text);
                        break;
                    case "tool_use":
                        sb.AppendLine("- Ferramenta: " + block.Name + " (" + block.Id + ")"
                            + (block.Pending ? " · pendente" : ""));
                        break;
                    case "tool_result":
                        sb.AppendLine("- Resultado da ferramenta " + block.ToolUseId +
                            (block.IsError ? " · erro" : " · concluído") + ": " + Preview(block.Content));
                        break;
                    case "attachment":
                        sb.AppendLine("- Anexo: " + block.FileName + " (" + block.MimeType + ")");
                        break;
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine("Execuções: " + conversation.Runs.Count + " · tokens de entrada: " +
            conversation.Usage.Input + " · tokens de saída: " + conversation.Usage.Output);
        return sb.ToString();
    }

    private static string Preview(string s)
    {
        var oneLine = s.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length > 600 ? oneLine[..600] + "…" : oneLine;
    }
}
