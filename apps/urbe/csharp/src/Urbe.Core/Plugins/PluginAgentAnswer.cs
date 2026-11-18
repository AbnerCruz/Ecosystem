using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Plugins;

/// <summary>
/// CST-146: what an agent hands back to the plugin that asked — the text, and, when the agent closed it with the
/// structured block, the suggested action, why, the steps, how to know it is done, the sources and what is uncertain.
/// Sources are checked: one outside the plugin's scope or missing from the vault becomes an uncertainty, never a fact.
/// </summary>
public sealed record PluginAgentAnswer(string Status, string Text, string Action, string Why, IReadOnlyList<string> Steps,
    string Done, IReadOnlyList<string> Sources, IReadOnlyList<string> Uncertain)
{
    /// <summary>What the agent is asked to close its answer with, so the plugin can use each part.</summary>
    public const string Instruction =
        "Termine a resposta com um bloco ```json com os campos: acao (a ação recomendada, curta), justificativa, passos (lista), " +
        "criterio (como saber que está concluída), fontes (caminhos das notas que sustentam, só dentro do escopo) e incertezas (lista do que não está confirmado). " +
        "Não apresente suposição como fato: o que não está nas notas vai em incertezas. Recomendar não é executar: não grave nada para cumprir a ação.";

    public static PluginAgentAnswer Declined { get; } = new("recusado", "", "", "", [], "", [], []);

    /// <param name="answer">The agent's final text, or null when the person declined or the run did not end.</param>
    /// <param name="inScope">Whether a path is inside the scope the plugin gave.</param>
    /// <param name="exists">Whether a path is a note or file of the vault.</param>
    public static PluginAgentAnswer Parse(string? answer, Func<string, bool> inScope, Func<string, bool> exists)
    {
        if (answer is null) return Declined;
        var blocks = Regex.Matches(answer, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        JsonObject? data = null;
        if (blocks.Count > 0)
            try { data = JsonNode.Parse(blocks[^1].Groups[1].Value) as JsonObject; }
            catch (System.Text.Json.JsonException) { }
        var text = blocks.Count > 0 ? answer.Remove(blocks[^1].Index, blocks[^1].Length).Trim() : answer.Trim();
        if (data is null) return new("sem estrutura", text, "", "", [], "", [], []);
        static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";
        static List<string> List(JsonNode? n) => n is JsonArray a ? a.Select(Str).Where(s => s.Length > 0).ToList() : Str(n) is { Length: > 0 } one ? [one] : [];
        var uncertain = List(data["incertezas"]);
        var sources = new List<string>();
        foreach (var source in List(data["fontes"]))
        {
            var path = source.Trim('[', ']');
            if (inScope(path) && exists(path)) sources.Add(path);
            else uncertain.Add("Fonte citada que não confere" + (inScope(path) ? " (não existe no vault)" : " (fora do escopo do plugin)") + ": " + path);
        }
        return new("concluido", text, Str(data["acao"]), Str(data["justificativa"]), List(data["passos"]), Str(data["criterio"]), sources, uncertain);
    }

    /// <summary>The answer as the plugin's variables: <c>name</c> is the text; <c>name_acao</c> and the others when declared.</summary>
    public IEnumerable<(string Name, object? Value)> Variables(string name)
    {
        yield return (name, Text);
        yield return (name + "_estado", Status);
        yield return (name + "_acao", Action);
        yield return (name + "_justificativa", Why);
        yield return (name + "_passos", Steps.Cast<object?>().ToList());
        yield return (name + "_criterio", Done);
        yield return (name + "_fontes", Sources.Cast<object?>().ToList());
        yield return (name + "_incertezas", Uncertain.Cast<object?>().ToList());
    }
}
