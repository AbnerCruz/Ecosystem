using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// The native assistant's opt-in plan graph. Legacy AssistantTools.All stays unchanged
/// for 1.8.4 oracles; meaningful Urbe tasks can use objectives, dependencies and
/// declared evidence without introducing a second state store or an extra agent.
/// </summary>
public static class AssistantAdaptivePlanTools
{
    public static AiTool Update { get; } = new()
    {
        Name = "update_plan",
        Access = "plan",
        Description = "Plano vivo e proporcional. Para tarefa complexa informe goal (pedido original), current_state, target_state e steps como nós com IDs estáveis e depends_on. Cada nó iniciado precisa de acceptance e tests definidos ANTES de executar. Para done registre test_evidence, implementation_evidence e verification_evidence; depois confronte o resultado global com plano (final_plan_review) e pedido (final_user_review). Evidências textuais são relatos, NUNCA execução de teste ou aceite independente. Para tarefas simples mantenha steps legados. Replaneje com evidências sem etapas artificiais.",
        Parameters = (JsonObject)JsonNode.Parse("""
        {"type":"object","properties":{"goal":{"type":"string","description":"Pedido original; ativa planejamento em camadas."},"current_state":{"type":"string","description":"Estado inicial observado, com fontes reais."},"target_state":{"type":"string","description":"Resultado final verificável."},"final_plan_review":{"type":"string","description":"Confronto dos resultados com a estratégia inicial."},"final_user_review":{"type":"string","description":"Confronto dos resultados com o pedido original."},"steps":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string"},"step":{"type":"string"},"details":{"type":"string"},"acceptance":{"type":"string"},"status":{"type":"string","enum":["pending","in_progress","done"]},"depends_on":{"type":"array","items":{"type":"string"}},"tests":{"type":"string"},"test_evidence":{"type":"string"},"implementation_evidence":{"type":"string"},"verification_evidence":{"type":"string"}},"required":["step","status"],"additionalProperties":false}}},"required":["steps"],"additionalProperties":false}
        """)!,
        Label = input =>
        {
            var steps = (input["steps"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            var done = steps.Count(s => (string?)s["status"] == "done");
            return "Plano: " + done + "/" + steps.Count + " passos";
        },
        Run = (input, host) =>
        {
            var plan = AssistantLayeredPlan.Build(input);
            host.SetPlan(plan.Steps);
            if (plan.Layered)
                return Task.FromResult(new AiToolOutput(plan.Summary));
            var completed = plan.Steps.Count(s => s.Status == "done");
            var next = plan.Steps.FirstOrDefault(s => s.Status != "done");
            return Task.FromResult(new AiToolOutput("Plano atualizado: " + completed + "/" + plan.Steps.Count
                + " concluídos." + (next is null ? " Todas as etapas declaradas concluídas; revise o pedido antes de finalizar." : " Próximo: " + next.Step)));
        }
    };
}
