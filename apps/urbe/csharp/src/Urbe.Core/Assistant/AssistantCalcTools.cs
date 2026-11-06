using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core.Calculus;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-206: the agents compute with the Urbe's calculator instead of guessing numbers — arithmetic, units, statistics,
/// matrices, derivatives, equations and integrals, exact where possible and marked “≈” where only numeric. Read-only:
/// it touches no note. The same lines in a <c>```calculo</c> block of a note show the same results there.
/// </summary>
public static class AssistantCalcTools
{
    public const string Name = "calculate";

    public static AiTool Calculate { get; } = new()
    {
        Name = Name, Access = "read",
        Description = "Calcula com o motor matemático do Urbe (exato e determinístico; nada é executado como código). Use SEMPRE que a resposta depender de conta, " +
                      "em vez de calcular de cabeça. Uma linha por pergunta; as linhas de cima valem para as de baixo. Aceita: valores (2,5*4; 20% * 150; 5!; media(3; 5; 8); " +
                      "desvio([..]); det([[1; 2]; [3; 4]])), definições (a = 3; f(x) = x^2 + 1), unidades com conversão (100 km/h em m/s; 37 °C em °F), " +
                      "'derivada de …', 'simplificar …', 'expandir …', 'resolver … = … [para t]', 'integral de …' e 'integral de … de a até b'. " +
                      "Vírgula decimal (1,5); separe argumentos com ';'. Resultados com ≈ são aproximados: diga isso ao usuário. " +
                      "Para deixar a conta viva na nota, escreva as mesmas linhas num bloco ```calculo (o Urbe mostra os resultados ao lado de cada linha).",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"lines":{"type":"string","description":"Uma ou mais linhas de cálculo, separadas por quebra de linha"}},"required":["lines"],"additionalProperties":false}""")!,
        Label = i => "Calculando" + ((string?)i["lines"] is { Length: > 0 } l ? ": " + AssistantTools.Clip(l.Split('\n')[0], 60) : ""),
        Run = (i, _) => Task.FromResult(new AiToolOutput(Answer((string?)i["lines"] ?? "")))
    };

    public static string Answer(string lines)
    {
        if (string.IsNullOrWhiteSpace(lines)) return "Nada para calcular: envie ao menos uma linha.";
        if (lines.Length > 20000) return "Linhas demais para uma chamada; divida em partes.";
        var sb = new StringBuilder();
        foreach (var line in Calculator.Run(lines))
        {
            if (line.Input.Trim().Length == 0 || line.Result is null && line.Error is null) continue;
            sb.Append(line.Input.Trim()).Append("  →  ");
            sb.Append(line.Ok ? line.Result + (line.Approximate ? "  [aproximado]" : "") : "ERRO: " + line.Error).Append('\n');
        }
        return sb.Length == 0 ? "Nenhuma linha de cálculo (só comentários)." : sb.ToString().TrimEnd();
    }
}
