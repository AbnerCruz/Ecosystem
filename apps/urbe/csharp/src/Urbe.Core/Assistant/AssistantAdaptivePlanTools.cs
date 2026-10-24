using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// Urbe's opt-in dynamic plan contract. The legacy AssistantTools.All remains byte-for-byte
/// compatible with the 1.8.4 oracle, while the active Urbe chat gets unlimited plan depth.
/// </summary>
public static class AssistantAdaptivePlanTools
{
    public static AiTool Update { get; } = new()
    {
        Name = "update_plan",
        Access = "plan",
        Description = "Plano vivo com extensão e detalhamento proporcionais à tarefa. Use details para método e dependências; acceptance para critério verificável. Investigue decisões, riscos e informação relacionada. Replaneje com base em evidências, sem inventar etapas.",
        Parameters = (JsonObject)JsonNode.Parse("""
        {"type":"object","properties":{"steps":{"type":"array","items":{"type":"object","properties":{"step":{"type":"string"},"details":{"type":"string"},"acceptance":{"type":"string"},"status":{"type":"string","enum":["pending","in_progress","done"]}},"required":["step","status"]}}},"required":["steps"],"additionalProperties":false}
        """)!,
        Label = input =>
        {
            var steps = (input["steps"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            var done = steps.Count(s => (string?)s["status"] == "done");
            return "Plano: " + done + "/" + steps.Count + " passos";
        },
        Run = (input, host) =>
        {
            var steps = (input["steps"] as JsonArray ?? []).OfType<JsonObject>()
                .Where(s => !string.IsNullOrWhiteSpace((string?)s["step"]))
                .Select(s =>
                {
                    var step = ((string?)s["step"] ?? "").Trim();
                    var status = (string?)s["status"] ?? "pending";
                    var details = ((string?)s["details"])?.Trim();
                    var acceptance = ((string?)s["acceptance"])?.Trim();
                    return new AiPlanStep(step, status is "pending" or "in_progress" or "done" ? status : "pending",
                        string.IsNullOrWhiteSpace(details) ? null : details,
                        string.IsNullOrWhiteSpace(acceptance) ? null : acceptance);
                }).ToList();
            if (steps.Count == 0) throw new AiToolException("Envie ao menos um passo.");
            host.SetPlan(steps);
            var completed = steps.Count(s => s.Status == "done");
            var next = steps.FirstOrDefault(s => s.Status != "done");
            return Task.FromResult(new AiToolOutput("Plano atualizado: " + completed + "/" + steps.Count
                + " concluídos." + (next is null ? " Todas as etapas concluídas." : " Próximo: " + next.Step)));
        }
    };
}
