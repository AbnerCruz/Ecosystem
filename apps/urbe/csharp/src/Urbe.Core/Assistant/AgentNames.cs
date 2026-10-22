using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// UC-81 (owner's idea, 22/10/2026: "o usuário pode gostar de dar nomes aos seus agentes"): each agent may have a call
/// name, kept in the Assistente's configuration on this device (like the memory: never in the vault). The name is how
/// the agent introduces itself, what the chip shows and what <c>&amp;Nome&amp;:</c> calls in a note (UC-80); the agent's
/// id never changes (NN-019). Names are unique among the agents, ignoring case and accents.
/// </summary>
public static class AgentNames
{
    public const int MaxLength = 24;

    /// <summary>Every agent: the built-in ones, then the person's own.</summary>
    public static IReadOnlyList<AiAgentDef> All(AiConfig config) => [.. AssistantAgent.Agents, .. config.CustomAgents];

    /// <summary>The agent's call name: the one given on this device, else its own name.</summary>
    public static string CallName(AiAgentDef agent, AiConfig config) =>
        config.AgentNames.TryGetValue(agent.Id, out var name) && name.Length > 0 ? name : agent.Name;

    /// <summary>The agent as it introduces itself (its call name in the place of its name).</summary>
    public static AiAgentDef Named(AiAgentDef agent, AiConfig config) => agent with { Name = CallName(agent, config) };

    /// <summary>The agent called <paramref name="name"/> (its call name or, when free, its own name), or null.</summary>
    public static AiAgentDef? Find(string? name, AiConfig config)
    {
        var key = Key(name);
        if (key.Length == 0) return null;
        var agents = All(config);
        return agents.FirstOrDefault(a => Key(CallName(a, config)) == key)
            ?? agents.FirstOrDefault(a => Key(a.Name) == key && !agents.Any(o => o.Id != a.Id && Key(CallName(o, config)) == key));
    }

    /// <summary>Why <paramref name="name"/> cannot be the call name of <paramref name="agentId"/>, or null when it can.</summary>
    public static string? Problem(string? name, string agentId, AiConfig config)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) return "Escreva um nome.";
        if (n.Length > MaxLength) return "Use até " + MaxLength + " letras.";
        if (!char.IsLetter(n[0]) || n.Any(c => !(char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.'))) return "Use letras, números, espaço, hífen ou ponto, começando por uma letra.";
        var key = Key(n);
        if (All(config).FirstOrDefault(a => a.Id != agentId && (Key(CallName(a, config)) == key || Key(a.Name) == key)) is { } other)
            return "O agente " + CallName(other, config) + " já usa esse nome.";
        return null;
    }

    /// <summary>Gives <paramref name="agentId"/> the call name (its own name, or empty, takes the given one away); the problem, or null when done.</summary>
    public static string? Set(AiConfig config, string agentId, string? name)
    {
        var agent = All(config).FirstOrDefault(a => a.Id == agentId);
        if (agent is null) return "Agente desconhecido.";
        var n = (name ?? "").Trim();
        if (n.Length == 0 || n == agent.Name) { config.AgentNames.Remove(agentId); return null; }
        if (Problem(n, agentId, config) is { } problem) return problem;
        config.AgentNames[agentId] = n;
        return null;
    }

    /// <summary>
    /// The system prompt's lines about the name: what the agent is called and, with <see cref="Tool"/>, to ask once
    /// before taking a new name the person calls it by.
    /// </summary>
    public static string Naming(AiAgentDef agent, AiConfig config)
    {
        var others = All(config).Where(a => a.Id != agent.Id).Select(a => CallName(a, config)).ToList();
        return "Seu nome é " + CallName(agent, config) + ". Os outros agentes do usuário são: " + string.Join(", ", others) + ".\n" +
               "Se o usuário te chamar por outro nome (por exemplo, “Jarvis, resuma esta nota”), atenda o pedido normalmente e, no fim da resposta, pergunte uma vez: “Quer que eu passe a me chamar Jarvis?”. " +
               "Só com o sim dele use set_my_name. Não pergunte de novo por um nome que ele recusou nesta conversa, nem por um nome de outro agente.";
    }

    /// <summary>set_my_name: the agent takes the call name the person agreed to (on this device, never in the vault).</summary>
    public static AiTool Tool { get; } = new()
    {
        Name = "set_my_name", Access = "memory",
        Description = "Passa a usar um novo nome de chamada para você, guardado na configuração do Assistente deste aparelho. Use só depois de perguntar e de o usuário dizer que sim.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"name":{"type":"string","description":"O novo nome, ex.: Jarvis"}},"required":["name"],"additionalProperties":false}""")!,
        Label = i => "Passar a se chamar " + ((string?)i["name"] ?? "").Trim(),
        Run = (i, host) =>
        {
            var name = ((string?)i["name"] ?? "").Trim();
            if (host.Names is not { } names) throw new AiToolException("Nomes de agentes indisponíveis aqui.");
            if (names.Rename(name) is { } problem) throw new AiToolException(problem);
            return Task.FromResult(new AiToolOutput("Pronto: agora você se chama " + name + "."));
        }
    };

    // case and accents do not tell two names apart
    private static string Key(string? name)
    {
        var d = (name ?? "").Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}

/// <summary>UC-81: renames the agent of the run (its call name on this device); the problem, or null when done.</summary>
public interface IAssistantNames
{
    string? Rename(string name);
}
