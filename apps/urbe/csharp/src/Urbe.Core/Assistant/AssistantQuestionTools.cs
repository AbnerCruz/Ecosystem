using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Asks exactly one decision-changing question, then yields execution to the user.
/// The agent may re-evaluate answered/unanswered questions when the user replies.</summary>
public static class AssistantQuestionTools
{
    public static AiTool Ask { get; } = new()
    {
        Name = "ask_user", Access = "ui",
        Description = "Pausa a execução e mostra UMA pergunta relevante por vez no chat, com progresso de dúvidas resolvidas. Use somente quando uma informação material faltar. Após a resposta, reavalie quais dúvidas continuam relevantes; uma resposta pode resolver várias, e novas dúvidas podem surgir. Nunca repita perguntas já respondidas. Envie apenas uma chamada ask_user por resposta.",
        Parameters = (JsonObject)JsonNode.Parse("""
        {"type":"object","properties":{
          "question":{"type":"string","description":"Pergunta específica, uma por vez"},
          "resolved":{"type":"integer","minimum":0,"maximum":100},
          "total":{"type":"integer","minimum":1,"maximum":100},
          "reason":{"type":"string","description":"Por que a resposta altera o plano"}
        },"required":["question","resolved","total"],"additionalProperties":false}
        """)!,
        Label = i => "Pergunta: " + ((string?)i["question"] ?? ""),
        Run = (i, _) =>
        {
            var question = ((string?)i["question"] ?? "").Trim();
            if (question.Length == 0 || question.Length > 700) throw new AiToolException("Faça uma pergunta curta e específica.");
            int resolved = Math.Clamp((int?)i["resolved"] ?? 0, 0, 100);
            int total = Math.Clamp((int?)i["total"] ?? 1, 1, 100);
            if (resolved > total) resolved = total;
            return Task.FromResult(new AiToolOutput("Pergunta exibida ao usuário. " + resolved + "/" + total +
                " dúvidas resolvidas. Aguarde a resposta antes de continuar a execução."));
        }
    };
}
