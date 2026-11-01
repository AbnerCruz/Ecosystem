using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// Opt-in, read-only normalization of a layered task graph into the existing Urbe plan UI.
/// The source of truth for artifacts, tests and approvals remains the corresponding host;
/// evidence strings here are agent declarations, NEVER independently verified receipts.
/// This type has no I/O, LLM calls or stored duplicate plan authority.
/// </summary>
public static class AssistantLayeredPlan
{
    public sealed record Result(IReadOnlyList<AiPlanStep> Steps, bool Layered, bool Concluded, string Summary);

    private sealed record Node(
        string Id, string Title, string Status, string? Details, string? Acceptance,
        string Tests, string TestEvidence, string ImplementationEvidence,
        string VerificationEvidence, IReadOnlyList<string> Dependencies);

    private static string Get(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text.Trim() : "";

    public static Result Build(JsonObject input)
    {
        if (input["steps"] is not JsonArray steps)
            throw new AiToolException("O plano exige uma lista de objetivos.");
        if (!input.ContainsKey("goal"))
        {
            // Previous update_plan contract is preserved, including step labels, status
            // normalization and lack of artificial depth/step limits.
            var legacy = steps.OfType<JsonObject>()
                .Where(s => !string.IsNullOrWhiteSpace(Get(s, "step")))
                .Select(s =>
                {
                    var status = Get(s, "status");
                    var details = Get(s, "details");
                    var acceptance = Get(s, "acceptance");
                    return new AiPlanStep(Get(s, "step"),
                        status is "pending" or "in_progress" or "done" ? status : "pending",
                        details.Length == 0 ? null : details,
                        acceptance.Length == 0 ? null : acceptance);
                }).ToArray();
            if (legacy.Length == 0) throw new AiToolException("Envie ao menos um passo.");
            return new Result(legacy, false, false, "Plano simples atualizado (sem verificação independente).");
        }

        var goal = Get(input, "goal");
        var initial = Get(input, "current_state");
        var target = Get(input, "target_state");
        if (goal.Length == 0 || initial.Length == 0 || target.Length == 0)
            throw new AiToolException("Plano em camadas exige pedido original, ponto atual e resultado final.");

        if (steps.Count == 0 || steps.Any(s => s is not JsonObject))
            throw new AiToolException("O grafo exige pelo menos um nó válido.");

        var nodes = new List<Node>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps.OfType<JsonObject>())
        {
            var id = Get(step, "id");
            var title = Get(step, "step");
            var status = Get(step, "status");
            if (id.Length == 0 || title.Length == 0 || !ids.Add(id))
                throw new AiToolException("Nó sem ID/título ou ID repetido: " + id);
            if (status is not ("pending" or "in_progress" or "done"))
                throw new AiToolException("Estado inválido para " + id + ": " + status);

            var dependencies = new List<string>();
            if (step.ContainsKey("depends_on"))
            {
                if (step["depends_on"] is not JsonArray array)
                    throw new AiToolException("Dependências inválidas de " + id);
                foreach (var item in array)
                {
                    if (item is not JsonValue value || !value.TryGetValue<string>(out var dependency)
                        || string.IsNullOrWhiteSpace(dependency))
                        throw new AiToolException("ID de dependência inválido em " + id);
                    if (!dependencies.Contains(dependency.Trim(), StringComparer.OrdinalIgnoreCase))
                        dependencies.Add(dependency.Trim());
                }
            }

            var tests = Get(step, "tests");
            var testEvidence = Get(step, "test_evidence");
            var implementationEvidence = Get(step, "implementation_evidence");
            var verificationEvidence = Get(step, "verification_evidence");
            var acceptance = Get(step, "acceptance");
            if (status is "in_progress" or "done" && (tests.Length == 0 || acceptance.Length == 0))
                throw new AiToolException(id + ": defina primeiro os testes e o critério de aceite.");
            if (status == "done" && (testEvidence.Length == 0 || implementationEvidence.Length == 0
                || verificationEvidence.Length == 0))
                throw new AiToolException(id + ": conclusão exige evidências distintas para testes, execução e confronto.");
            nodes.Add(new Node(id, title, status, Get(step, "details"), acceptance.Length == 0 ? null : acceptance,
                tests, testEvidence, implementationEvidence, verificationEvidence, dependencies));
        }

        var byId = nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!byId.TryGetValue(id, out var node))
                throw new AiToolException("Dependência inexistente: " + id);
            if (!visiting.Add(id))
                throw new AiToolException("O grafo contém dependência circular em " + id);
            foreach (var dep in node.Dependencies) Visit(dep);
            visiting.Remove(id);
            visited.Add(id);
        }
        foreach (var node in nodes) Visit(node.Id);

        foreach (var node in nodes.Where(n => n.Status != "pending"))
            foreach (var dep in node.Dependencies)
                if (byId[dep].Status != "done")
                    throw new AiToolException(node.Id + " não pode avançar: " + dep + " ainda não foi concluído.");

        var allDone = nodes.All(n => n.Status == "done");
        var planReview = Get(input, "final_plan_review");
        var userReview = Get(input, "final_user_review");
        if (!allDone && (planReview.Length > 0 || userReview.Length > 0))
            throw new AiToolException("Confronto final não pode fechar um plano com nós pendentes.");
        var concluded = allDone && planReview.Length > 0 && userReview.Length > 0;

        var output = nodes.Select((n, index) =>
        {
            var details = new List<string>();
            if (index == 0)
            {
                details.Add("Pedido original: " + goal);
                details.Add("Ponto atual: " + initial);
                details.Add("Ponto final: " + target);
            }
            if (!string.IsNullOrWhiteSpace(n.Details)) details.Add(n.Details);
            if (n.Dependencies.Count > 0) details.Add("Depende de: " + string.Join(", ", n.Dependencies));
            if (n.Tests.Length > 0) details.Add("Testes previstos: " + n.Tests);
            if (n.TestEvidence.Length > 0) details.Add("Testes (relato): " + n.TestEvidence);
            if (n.ImplementationEvidence.Length > 0) details.Add("Execução (relato): " + n.ImplementationEvidence);
            if (n.VerificationEvidence.Length > 0) details.Add("Confronto (relato): " + n.VerificationEvidence);
            if (index == nodes.Count - 1 && concluded)
            {
                details.Add("Confronto com o plano (relato): " + planReview);
                details.Add("Confronto com o pedido (relato): " + userReview);
            }
            return new AiPlanStep(n.Id + " · " + n.Title, n.Status, string.Join("\n", details), n.Acceptance);
        }).ToArray();

        var completed = nodes.Count(n => n.Status == "done");
        var next = nodes.FirstOrDefault(n => n.Status != "done");
        var summary = "Plano em camadas: " + completed + "/" + nodes.Count
            + " nós com estados declarados pelo agente. ";
        if (concluded)
            summary += "Confrontos finais declarados. Evidências declaradas NÃO são validação independente: conferir artefatos e recibos reais antes de anunciar entrega.";
        else if (allDone)
            summary += "Todos os nós foram declarados concluídos, mas faltam o confronto com o plano e/ou com o pedido original.";
        else
            summary += "Próximo nó: " + next!.Id + " — " + next.Title + ". A conclusão exige testes, execução e confronto registrados.";
        return new Result(output, true, concluded, summary);
    }
}
