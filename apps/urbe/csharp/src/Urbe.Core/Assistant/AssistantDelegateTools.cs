using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Scoped agent-to-agent consultation: no shared transcript, private memory or direct history APIs.</summary>
public static class AssistantDelegateTools
{
    public static AiTool Consult { get; } = new()
    {
        Name = "consult_agent",
        Access = "ui",
        Description = "Consulte um dos outros agentes que o usuário configurou. Primeiro use sem agent_id para conhecer especialistas e capacidades públicas. Encaminhe somente a tarefa explícita necessária; você NÃO vê conversas, memórias nem instruções privadas dos outros agentes. O Urbe solicitará autorização se necessário, abrirá uma conversa separada para o especialista e trará a resposta. Para continuar a conversa com ESSE MESMO especialista, informe conversation_id devolvido pela consulta anterior, sem tentar acessar conversas alheias. Se não houver agente adequado, recomende que o usuário crie um. Não use para delegar sua própria tarefa sem necessidade.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"agent_id":{"type":"string","description":"ID público do especialista. Deixe vazio para listar agentes configurados."},"task":{"type":"string","description":"Pergunta ou trabalho específico que você quer enviar ao especialista, sem copiar o histórico privado da conversa."},"conversation_id":{"type":"string","description":"Opcional: ID devolvido por uma consulta anterior DESTA conversa para continuar o diálogo com o mesmo especialista."}},"additionalProperties":false}""")!,
        Label = i => string.IsNullOrWhiteSpace((string?)i["agent_id"]) ? "Procurando agentes disponíveis" : "Consultando agente " + ((string?)i["agent_id"] ?? ""),
        Run = async (i, host) =>
        {
            var gateway = host.Delegation ?? throw new AiToolException("Colaboração entre agentes indisponível neste ambiente.");
            var agentId = ((string?)i["agent_id"] ?? "").Trim();
            if (agentId.Length == 0)
            {
                var targets = gateway.Targets;
                if (targets.Count == 0)
                    return new AiToolOutput("Nenhum outro agente está configurado. Recomende ao usuário criar um especialista em Configurações → Agentes → Novo agente, escolhendo os modelos e permissões adequados.");
                return new AiToolOutput("Agentes configurados (somente perfis públicos, sem histórico ou memória):\n" +
                    string.Join("\n", targets.Select(a => "- " + a.Id + " · " + a.Name + " · " + a.Description +
                        (a.CanGenerateImages ? " · gera imagens" : " · sem modelo de imagem"))));
            }
            var task = ((string?)i["task"] ?? "").Trim();
            if (task.Length == 0) throw new AiToolException("Explique a tarefa que deseja enviar ao especialista.");
            return new AiToolOutput(await gateway.ConsultAsync(agentId, task, ((string?)i["conversation_id"] ?? "").Trim()));
        }
    };
    public const string DelegateName = "delegate_to_agent";

    /// <summary>
    /// CST-217: work delegated to one of the person's agents with a scoped brief, returning a verifiable artifact. The
    /// specialist works in its own conversation, with its own memory and permissions (approvals reach the person here),
    /// writes only inside the scope, and its artifact is read back from the vault here, not taken from its words. With
    /// reviewer_id, another agent inspects the real file (read-only); a review that did not read it is not accepted.
    /// </summary>
    public static AiTool DelegateTo { get; } = new()
    {
        Name = DelegateName,
        Access = "ui",
        Description = "Delegue um trabalho com escopo a um dos agentes que o usuário configurou (use consult_agent sem agent_id para conhecê-los). Use só quando um especialista fizer melhor uma parte separável do trabalho; tarefas simples você faz sozinho, sem delegar. Informe a tarefa, as fontes que ele deve ler, onde ele pode gravar (scope) e o artefato esperado (uma nota em artifact_path, ou texto). O especialista NÃO vê esta conversa nem suas memórias; trabalha com as próprias permissões e as aprovações chegam ao usuário. O Urbe lê o artefato real no vault e devolve caminho, revisão, trecho e a justificativa do especialista. Com reviewer_id, outro agente revisa o arquivo real, sem alterar nada.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"agent_id":{"type":"string","description":"ID do especialista configurado."},"task":{"type":"string","description":"O trabalho, com critérios de pronto; sem copiar o histórico privado da conversa."},"expected":{"type":"string","enum":["note","text"],"description":"note: uma nota gravada em artifact_path; text: resultado só em texto."},"artifact_path":{"type":"string","description":"Caminho da nota esperada (obrigatório com expected=note)."},"scope":{"type":"array","items":{"type":"string"},"description":"Notas ou pastas onde ele pode gravar. Padrão: só artifact_path."},"sources":{"type":"array","items":{"type":"string"},"description":"Notas que ele deve ler como fonte."},"max_steps":{"type":"integer","description":"Limite de etapas do especialista (padrão 40, máximo 80)."},"reviewer_id":{"type":"string","description":"Opcional: outro agente que revisa o artefato real antes de você aceitá-lo."}},"required":["agent_id","task","expected"],"additionalProperties":false}""")!,
        Label = i => "Delegando a " + ((string?)i["agent_id"] ?? "") + ": " + (((string?)i["task"] ?? "") is { Length: > 60 } t ? t[..60] + "…" : (string?)i["task"] ?? ""),
        Run = async (i, host) =>
        {
            var gateway = host.Delegation ?? throw new AiToolException("Delegação indisponível neste trabalho (um especialista não delega de novo).");
            var targets = gateway.Targets;
            var agentId = ((string?)i["agent_id"] ?? "").Trim();
            if (!targets.Any(t => t.Id == agentId))
                throw new AiToolException("Agente não encontrado: " + agentId + ". Configurados: " + (targets.Count > 0 ? string.Join(", ", targets.Select(t => t.Id)) : "nenhum") + ".");
            var task = ((string?)i["task"] ?? "").Trim();
            if (task.Length == 0) throw new AiToolException("Explique o trabalho em task.");
            var expected = (string?)i["expected"] == "note" ? "note" : "text";
            string? artifact = null;
            if (expected == "note")
            {
                if (string.IsNullOrWhiteSpace((string?)i["artifact_path"])) throw new AiToolException("Com expected=note, informe artifact_path.");
                artifact = AssistantTools.ValidPath((string?)i["artifact_path"]);
            }
            static List<string> List(JsonNode? n) => (n as JsonArray ?? []).Select(x => AssistantTools.Norm(x?.ToString())).Where(x => x.Length > 0).Distinct().ToList();
            var scope = List(i["scope"]);
            if (scope.Count == 0 && artifact is not null) scope.Add(artifact);
            var steps = i["max_steps"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : i["max_steps"] is JsonValue d && d.TryGetValue<double>(out var x) ? (int)x : AiDelegationBrief.DefaultSteps;
            var brief = new AiDelegationBrief(task, scope, List(i["sources"]), expected, artifact, Math.Clamp(steps, 1, AiDelegationBrief.MostSteps));
            if (artifact is not null && !brief.Allows(artifact)) throw new AiToolException("artifact_path " + artifact + " está fora do scope informado.");
            var reviewerId = ((string?)i["reviewer_id"] ?? "").Trim();
            if (reviewerId.Length > 0 && (reviewerId == agentId || !targets.Any(t => t.Id == reviewerId)))
                throw new AiToolException("reviewer_id deve ser outro agente configurado, diferente de quem faz o trabalho.");

            var outcome = await gateway.DelegateAsync(agentId, brief);
            var lines = new List<string> { "Trabalho delegado a " + outcome.AgentName + " (conversa separada " + outcome.ConversationId + ", terminou: " + (outcome.Stopped ?? "?") + ")." };
            var outside = outcome.Changes.Select(c => (c.After ?? c.Before)?.Path).OfType<string>().Where(p => !brief.Allows(p)).Distinct().ToList();
            if (outside.Count > 0) lines.Add("ATENÇÃO: mudanças fora do escopo: " + string.Join(", ", outside) + ".");
            if (DescribeChanges(outcome.Changes) is { Count: > 0 } changed) lines.Add("No vault, o especialista " + string.Join("; ", changed) + ". Diga isso na sua resposta.");
            UrbeDocument? doc = null;
            string? sha = null;
            if (artifact is not null)
            {
                doc = host.Documents.Get(artifact);
                if (doc is null)
                    lines.Add("ARTEFATO NÃO ENTREGUE: " + artifact + " não existe no vault. Não diga que foi feito; veja a justificativa e decida com o usuário.");
                else
                {
                    sha = AssistantRunJournal.Sha(doc.Content)!;
                    var text = host.Readable(doc.Content);
                    bool touched = outcome.Changes.Any(c => string.Equals((c.After ?? c.Before)?.Path, doc.Path, StringComparison.OrdinalIgnoreCase));
                    lines.Add("ARTEFATO (lido agora do vault): " + doc.Path + " · revisão " + sha[..12] + " · " + text.Length + " caracteres" +
                        (touched ? "" : " · ATENÇÃO: o especialista não alterou esta nota neste trabalho") + ".");
                    lines.Add("Trecho real:\n" + (text.Length > 1500 ? text[..1500] + "\n…[continua; leia a nota para ver tudo]" : text));
                }
            }
            lines.Add("Justificativa e resposta do especialista (relato dele, não prova):\n" + (outcome.Answer.Length > 0 ? outcome.Answer : "(sem resposta textual)"));
            if (reviewerId.Length > 0)
            {
                if (doc is null || sha is null) lines.Add("Revisão independente não feita: não há artefato para revisar.");
                else
                {
                    var review = await gateway.DelegateAsync(reviewerId, new AiDelegationBrief("Revise " + doc.Path + " para este trabalho: " + task, [], [doc.Path], "text", doc.Path,
                        Math.Min(brief.MaxSteps, 24), AiDelegationBrief.Reviewer, sha));
                    bool read = Inspected(review.Transcript, doc.Path);
                    bool same = AssistantRunJournal.Sha(host.Documents.Get(doc.Path)?.Content) == sha;
                    lines.Add(!read
                        ? "REVISÃO NÃO ACEITA: " + review.AgentName + " não leu o arquivo real; o parecer dele não vale como revisão independente."
                        : "REVISÃO INDEPENDENTE de " + review.AgentName + " (leu " + doc.Path + (same ? ", mesma revisão" : ", mas a nota mudou depois") + "):\n" + review.Answer);
                }
            }
            return new AiToolOutput(string.Join("\n", lines));
        }
    };

    /// <summary>Whether a run read <paramref name="path"/> itself (a successful read_note/read_notes of it), not a summary.</summary>
    public static bool Inspected(IReadOnlyList<AiChatMessage> transcript, string path)
    {
        static string Bare(string? s) { var p = AssistantTools.Norm(s); return p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? p[..^3] : p; }
        var target = Bare(path);
        var blocks = transcript.SelectMany(m => m.Content).ToList();
        foreach (var call in blocks.Where(b => b.Type == "tool_use" && b.Name is "read_note" or "read_notes" && b.Input is JsonObject))
        {
            var input = (JsonObject)call.Input!;
            var paths = new List<string?> { input["path"]?.ToString() };
            if (input["paths"] is JsonArray many) paths.AddRange(many.Select(p => p?.ToString()));
            if (!paths.Any(p => string.Equals(Bare(p), target, StringComparison.OrdinalIgnoreCase))) continue;
            if (blocks.Any(b => b.Type == "tool_result" && b.ToolUseId == call.Id && !b.IsError)) return true;
        }
        return false;
    }
    /// <summary>
    /// CST-092: what a consulted specialist changed in the vault, for the agent that asked (which must say what changed in
    /// its answer): created, changed, moved or sent to the trash, one line each, in the order they happened.
    /// </summary>
    public static IReadOnlyList<string> DescribeChanges(IEnumerable<AiChange> changes)
    {
        var lines = new List<string>();
        foreach (var c in changes)
        {
            var line = (c.Before, c.After, c.Trashed) switch
            {
                (_, _, true) => "moveu para a lixeira " + (c.Before ?? c.After)?.Path,
                (null, { } after, _) => "criou " + after.Path,
                ({ } before, { } after, _) when before.Path != after.Path => "moveu " + before.Path + " → " + after.Path,
                (_, { } after, _) => "alterou " + after.Path,
                ({ } before, null, _) => "apagou " + before.Path,
                _ => null
            };
            if (line is not null && !lines.Contains(line)) lines.Add(line);
        }
        return lines;
    }
}
