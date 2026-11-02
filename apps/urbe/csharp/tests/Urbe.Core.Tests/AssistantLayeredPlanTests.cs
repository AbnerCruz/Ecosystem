using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// Contract tests written before the layered planner implementation.
/// The planner validates claims and dependencies; it must never present model-reported
/// evidence as independently executed or device-validated tests.
/// </summary>
public sealed class AssistantLayeredPlanTests
{
    private static JsonObject Input(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void LegacyPlanRemainsCompatibleAndDoesNotPretendToBeLayered()
    {
        var plan = AssistantLayeredPlan.Build(Input("""
        {"steps":[{"step":"Ler","status":"done"},{"step":"Escrever","status":"pending"}]}
        """));
        Assert.False(plan.Layered);
        Assert.False(plan.Concluded);
        Assert.Equal(new[] { "Ler", "Escrever" }, plan.Steps.Select(x => x.Step));
        Assert.Equal(new[] { "done", "pending" }, plan.Steps.Select(x => x.Status));
    }

    [Fact]
    public void ValidGraphIncludesBaselineTargetChecksAndDependenciesInExistingPlanUi()
    {
        var plan = AssistantLayeredPlan.Build(Input("""
        {
          "goal":"Criar uma apostila", "current_state":"Notas existentes",
          "target_state":"PDF utilizável",
          "steps":[
            {"id":"N1","step":"Organizar fontes","status":"done",
             "acceptance":"Referências rastreáveis","tests":"Conferir IDs das notas",
             "test_evidence":"Teste definido e executado: fonte A", 
             "implementation_evidence":"Ferramenta de leitura: nota A",
             "verification_evidence":"Relatório: referência A conferida"},
            {"id":"N2","step":"Gerar publicação","status":"in_progress",
             "depends_on":["N1"],"acceptance":"PDF válido","tests":"Abrir o arquivo exportado"}
          ]
        }
        """));
        Assert.True(plan.Layered);
        Assert.False(plan.Concluded);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("done", plan.Steps[0].Status);
        Assert.Contains("N2", plan.Steps[1].Step);
        Assert.Contains("N1", plan.Steps[1].Details);
        Assert.Contains("PDF utilizável", plan.Steps[0].Details);
        Assert.Contains("Abrir o arquivo", plan.Steps[1].Details);
    }

    [Theory]
    [InlineData("""
        {"goal":"G","current_state":"A","target_state":"B","steps":[{"id":"a","step":"A","status":"pending"},{"id":"a","step":"B","status":"pending"}]}
        """)]
    [InlineData("""
        {"goal":"G","current_state":"A","target_state":"B","steps":[{"id":"a","step":"A","status":"pending","depends_on":["missing"]}]}
        """)]
    [InlineData("""
        {"goal":"G","current_state":"A","target_state":"B","steps":[{"id":"a","step":"A","status":"pending","depends_on":["b"]},{"id":"b","step":"B","status":"pending","depends_on":["a"]}]}
        """)]
    public void InvalidGraphCannotBePublished(string json)
    {
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(Input(json)));
    }

    [Fact]
    public void ActiveNodeCannotBypassUnfinishedDependency()
    {
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(Input("""
        {"goal":"G","current_state":"A","target_state":"B","steps":[
          {"id":"a","step":"A","status":"pending"},
          {"id":"b","step":"B","status":"in_progress","depends_on":["a"],"tests":"Teste de B"}
        ]}
        """)));
    }

    [Fact]
    public void DoneRequiresTestsAcceptanceAndThreeSeparateEvidenceStages()
    {
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(Input("""
        {"goal":"G","current_state":"A","target_state":"B","steps":[
          {"id":"a","step":"A","status":"done","acceptance":"A pronto",
           "tests":"Verificar A","test_evidence":"Primeiro teste","implementation_evidence":"A foi feito"}
        ]}
        """)));
    }

    [Fact]
    public void TestDefinitionMustPrecedeExecutionForActiveNode()
    {
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(Input("""
        {"goal":"G","current_state":"A","target_state":"B","steps":[
          {"id":"a","step":"A","status":"in_progress","acceptance":"A pronto"}
        ]}
        """)));
    }

    [Fact]
    public void FinishingAllNodesDoesNotCompleteGoalBeforeBothFinalConfrontations()
    {
        const string node = """
        {"id":"a","step":"A","status":"done","acceptance":"A pronto",
         "tests":"Verificar A","test_evidence":"Teste registrado",
         "implementation_evidence":"Execução registrada",
         "verification_evidence":"Confronto registrado"}
        """;
        var basic = Input("""{"goal":"G","current_state":"A","target_state":"B","steps":[""" + node + "]}");
        Assert.False(AssistantLayeredPlan.Build(basic).Concluded);
        var final = Input("""{"goal":"G","current_state":"A","target_state":"B","final_plan_review":"Plano confrontado","final_user_review":"Pedido confrontado","steps":[""" + node + "]}");
        var plan = AssistantLayeredPlan.Build(final);
        Assert.True(plan.Concluded);
        Assert.Contains("declarad", plan.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DoesNotAllowFinalReviewsWhileAnyNodeRemainsPending()
    {
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(Input("""
        {"goal":"G","current_state":"A","target_state":"B",
         "final_plan_review":"Tudo feito", "final_user_review":"Tudo entregue",
         "steps":[{"id":"a","step":"A","status":"pending"}]}
        """)));
    }
    [Fact]
    public void GraphUpdateWithoutGoalMustNeverSilentlyDowngradeToLegacy()
    {
        var missing = Input("""
        {"steps":[{"id":"read","step":"Ler","status":"done","tests":"Ler fonte",
            "acceptance":"Fonte lida","test_evidence":"Lido","implementation_evidence":"Ação",
            "verification_evidence":"Conferido"}]}
        """);
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(missing));
    }

    [Fact]
    public void FollowingLayeredUpdatesInSameRunInheritOriginalContextAndStillValidate()
    {
        var initial = Input("""
        {"goal":"Criar notas","current_state":"Vault vazio","target_state":"Três notas",
         "steps":[{"id":"folder","step":"Criar pasta","status":"in_progress",
             "tests":"Listar","acceptance":"Pasta presente"}]}
        """);
        var followUp = Input("""
        {"steps":[{"id":"folder","step":"Criar pasta","status":"done",
          "tests":"Listar","acceptance":"Pasta presente",
          "test_evidence":"list_notes retornou a pasta",
          "implementation_evidence":"create_folder retornou sucesso",
          "verification_evidence":"Listagem confirmada"}]}
        """);
        var normalized = AssistantLayeredPlan.NormalizeUpdate(followUp, initial);
        var checkedPlan = AssistantLayeredPlan.Build(normalized);
        Assert.True(checkedPlan.Layered);
        Assert.False(checkedPlan.Concluded);
        Assert.Contains("Criar notas", checkedPlan.Steps[0].Details);
        Assert.Equal("Criar notas", (string?)normalized["goal"]);
        Assert.Null(followUp["goal"]);
    }

    [Fact]
    public void InheritedGraphCannotAcceptPrematureFinalReview()
    {
        var initial = Input("""
        {"goal":"G","current_state":"A","target_state":"B",
         "steps":[{"id":"n1","step":"A","status":"pending"}]}
        """);
        var incoming = Input("""
        {"final_plan_review":"Feito","final_user_review":"Feito",
         "steps":[{"id":"n1","step":"A","status":"pending"}]}
        """);
        Assert.Throws<AiToolException>(() =>
            AssistantLayeredPlan.Build(AssistantLayeredPlan.NormalizeUpdate(incoming, initial)));
    }

    [Theory]
    [InlineData("Ainda pendente: reler o arquivo")]
    [InlineData("Pendente: confirmar criação")]
    [InlineData("A conferir no final")]
    public void PendingTestEvidenceIsNotProofOfCompletedNode(string evidence)
    {
        var plan = Input("""
        {"goal":"G","current_state":"A","target_state":"B",
         "steps":[{"id":"n1","step":"Nota","status":"done","tests":"Reabrir",
           "acceptance":"Nota correta","test_evidence":"OK",
           "implementation_evidence":"create_note retornou sucesso",
           "verification_evidence":"OK"}]}
        """);
        ((JsonObject)((JsonArray)plan["steps"]!)[0]!)["test_evidence"] = evidence;
        Assert.Throws<AiToolException>(() => AssistantLayeredPlan.Build(plan));
    }

}
