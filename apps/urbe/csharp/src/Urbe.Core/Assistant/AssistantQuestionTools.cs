using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Asks exactly one decision-changing question, then yields execution to the user.
/// The agent may re-evaluate answered/unanswered questions when the user replies.</summary>
public static class AssistantQuestionTools
{
    public static AiTool Ask { get; } = new()
    {
        Name = "ask_user", Access = "ui",
        Description = "Investigação de requisitos: faça UMA pergunta concreta e decisiva por vez sempre que uma resposta alterar objetivos, público, escopo, restrições, alternativas, prioridades, aceitação ou riscos. Avalie contexto já dado antes de perguntar, busque no vault o que puder, e faça perguntas na profundidade que a tarefa realmente exige, sem mínimos nem máximos predeterminados. Após a resposta revise dúvidas pendentes e plano, retire as resolvidas e inclua novas descobertas. Não repita perguntas respondidas nem incomode por decisões irrelevantes. Envie apenas uma chamada ask_user por resposta. Informe em pending as dúvidas que continuam abertas e em understanding quanto você entende do pedido: o usuário vê o que falta e o que mudou.",
        Parameters = (JsonObject)JsonNode.Parse("""
        {"type":"object","properties":{
          "question":{"type":"string","description":"Pergunta específica, uma por vez"},
          "resolved":{"type":"integer","minimum":0},
          "total":{"type":"integer","minimum":1},
          "reason":{"type":"string","description":"Por que a resposta altera o plano"},
          "understanding":{"type":"integer","minimum":0,"maximum":100,"description":"Quanto você declara entender do pedido agora, de 0 a 100; seja honesto, é uma estimativa"},
          "pending":{"type":"array","maxItems":12,"items":{"type":"string"},"description":"As dúvidas ainda em aberto, cada uma em poucas palavras (as mesmas palavras de uma pergunta para a outra)"}
        },"required":["question","resolved","total"],"additionalProperties":false}
        """)!,
        Label = i => "Pergunta: " + ((string?)i["question"] ?? ""),
        Run = (i, _) =>
        {
            var question = ((string?)i["question"] ?? "").Trim();
            if (question.Length == 0 || question.Length > 700) throw new AiToolException("Faça uma pergunta curta e específica.");
            int resolved = Math.Max(0, (int)LegacyCustomize.JsToNumber(i["resolved"]));
            int total = Math.Max(1, (int)LegacyCustomize.JsToNumber(i["total"]));
            if (resolved > total) resolved = total;
            return Task.FromResult(new AiToolOutput("Pergunta exibida ao usuário. " + resolved + "/" + total +
                " dúvidas resolvidas. Aguarde a resposta antes de continuar a execução."));
        }
    };
}
