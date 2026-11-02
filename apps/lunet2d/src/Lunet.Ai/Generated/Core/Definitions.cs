using System.Text.Json;

namespace AgentRuntime;

public sealed record AgentDefinition(AgentIdentity Identity, ModelProfile Model, string SystemPrompt, int MaxSteps, ToolGrant Grant);

/// <summary>Limites que impedem laços sem fim (plano §8.4): falhas equivalentes, repetição sem progresso e tentativas de verificação.</summary>
public sealed record RunPolicy(int MaxEquivalentFailures = 3, int NoProgressRepeats = 3, int MaxVerificationAttempts = 3)
{
    public static RunPolicy Default { get; } = new();
}

/// <summary>Tudo que define UM run: tarefa, agente, Context e os três níveis de permissão (organização, projeto, agente) mais o orçamento.</summary>
public sealed record RunRequest(
    string RunId,
    TaskSpec Task,
    AgentDefinition Agent,
    ContextPath Context,
    ToolGrant OrganizationGrant,
    ToolGrant ProjectGrant,
    IReadOnlyList<BudgetScope> BudgetScopes,
    RunPolicy Policy);

public sealed record ApprovalRequest(string RunId, AgentIdentity Agent, ToolDescriptor Tool, string ArgumentsJson);

public sealed record ApprovalDecision(bool Approved, string ApprovedBy)
{
    public static ApprovalDecision Deny { get; } = new(false, "none");
}

/// <summary>
/// Quem aprova uma operação destrutiva. Sem aprovador, o runtime ESCALA em vez de agir: o proprietário só é chamado para o que a
/// política não sabe julgar (ADD-0012), nunca para operações dentro da concessão.
/// </summary>
public interface IApprover
{
    ValueTask<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken);
}

public enum RunStatus { Created, Running, Succeeded, Failed, Cancelled, Blocked }

public enum BlockReason { Budget, Escalation, Provider, NoProgress, StepLimit }

/// <summary>O que um run devolve: o estado reconstruído do log (fonte de verdade), a verificação e os artefatos.</summary>
public sealed record RunResult(RunState State, VerificationResult? Verification, IReadOnlyList<ArtifactRef> Artifacts);

/// <summary>Serializa blocos de conteúdo para o log (só o que é permitido registrar) e os reconstrói na retomada.</summary>
public static class ContentSerializer
{
    public static string ToJson(IReadOnlyList<ContentBlock> blocks)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartArray();
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case TextBlock t:
                        w.WriteStartObject(); w.WriteString("type", "text"); w.WriteString("text", t.Text); w.WriteEndObject();
                        break;
                    case ToolUseBlock u:
                        w.WriteStartObject(); w.WriteString("type", "tool_use"); w.WriteString("id", u.Id); w.WriteString("name", u.Name);
                        w.WritePropertyName("input"); u.Input.WriteTo(w); w.WriteEndObject();
                        break;
                    case ToolResultBlock r:
                        w.WriteStartObject(); w.WriteString("type", "tool_result"); w.WriteString("tool_use_id", r.ToolUseId);
                        w.WriteString("content", r.Content); w.WriteBoolean("is_error", r.IsError); w.WriteEndObject();
                        break;
                    case ReasoningBlock:
                        throw new InvalidOperationException("Raciocínio privado nunca é serializado nem registrado.");
                    default:
                        throw new InvalidOperationException($"Bloco desconhecido: {block.GetType().Name}.");
                }
            }
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static IReadOnlyList<ContentBlock> FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<ContentBlock>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var type = e.GetProperty("type").GetString();
            switch (type)
            {
                case "text": list.Add(new TextBlock(e.GetProperty("text").GetString() ?? "")); break;
                case "tool_use": list.Add(new ToolUseBlock(e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("input").Clone())); break;
                case "tool_result": list.Add(new ToolResultBlock(e.GetProperty("tool_use_id").GetString()!, e.GetProperty("content").GetString() ?? "", e.GetProperty("is_error").GetBoolean())); break;
                default: throw new JsonException($"Tipo de bloco desconhecido: '{type}'.");
            }
        }
        return list;
    }
}
