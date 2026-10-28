using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>CST-089: the run's agent's own earlier conversations in this vault (the current one left out), newest first.</summary>
public interface IAssistantConversations
{
    IReadOnlyList<AiConversation> Recent();
}

/// <summary>
/// CST-089: an agent looks up what it talked about before: <c>list_conversations</c> gives a summary of each recent
/// conversation (the request that opened it, the last answer, the files it changed, the date) and
/// <c>read_conversation</c> one of them in detail. Only the agent's own conversations, and only what the person saw:
/// hidden text (internal reviews, system context) and tool results stay out. Summaries are taken from the
/// conversation itself, never written by a model.
/// </summary>
public static class AssistantConversationTools
{
    private const int PageChars = 12000;

    public static AiTool List { get; } = new()
    {
        Name = "list_conversations", Access = "read",
        Description = "Lista as suas conversas anteriores com o usuário neste vault (só as suas, nunca as de outro agente), das mais recentes: título, data, o pedido que abriu, a última resposta e os arquivos alterados. Use quando o usuário se referir a algo conversado antes (\"aquilo que vimos ontem\") ou quando precisar retomar um trabalho. Com query, só as conversas sobre o assunto. Depois, read_conversation lê uma em detalhe.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"query":{"type":"string","description":"Assunto, palavras do pedido ou nome de nota"},"limit":{"type":"integer","minimum":1,"maximum":30}},"additionalProperties":false}""")!,
        Label = i => (string?)i["query"] is { Length: > 0 } q ? "Conversas sobre " + q : "Conversas recentes",
        Run = (i, host) =>
        {
            var all = History(host).Recent();
            var query = ((string?)i["query"] ?? "").Trim();
            int limit = Math.Clamp((int?)i["limit"] ?? 10, 1, 30);
            var terms = AssistantTools.Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3).ToList();
            var found = all.Where(c => terms.Count == 0 || terms.All(t => AssistantTools.Fold(c.Title + "\n" + Transcript(c)).Contains(t, StringComparison.Ordinal)))
                .Take(limit).ToList();
            if (found.Count == 0)
                return Task.FromResult(new AiToolOutput(query.Length > 0
                    ? "Nenhuma conversa anterior sua fala de \"" + query + "\"."
                    : "Você ainda não tem conversas anteriores neste vault."));
            var text = new StringBuilder(query.Length > 0 ? "Suas conversas sobre \"" + query + "\":\n" : "Suas conversas recentes:\n");
            foreach (var c in found)
            {
                text.Append("\n## ").Append(c.Title.Length > 0 ? c.Title : "(sem título)").Append(" — ").Append(Date(c.Updated)).Append(" · id: ").Append(c.Id).Append('\n');
                if (c.Messages.FirstOrDefault(m => m.Role == "user" && m.Meta?.ToolResults != true) is { } first)
                    text.Append("Pedido: ").Append(Clip(Visible(first), 220)).Append('\n');
                if (c.Messages.LastOrDefault(m => m.Role == "assistant" && Visible(m).Length > 0) is { } last)
                    text.Append("Resposta: ").Append(Clip(Visible(last), 320)).Append('\n');
                var files = c.Runs.SelectMany(r => r.Changes).Select(x => (x.After ?? x.Before)?.Path).OfType<string>().Distinct().ToList();
                if (files.Count > 0) text.Append("Arquivos alterados: ").Append(string.Join(", ", files.Take(12))).Append(files.Count > 12 ? " …" : "").Append('\n');
            }
            return Task.FromResult(new AiToolOutput(text.ToString().TrimEnd()));
        }
    };

    public static AiTool Read { get; } = new()
    {
        Name = "read_conversation", Access = "read",
        Description = "Lê em detalhe uma das suas conversas anteriores (id de list_conversations): as mensagens do usuário, as suas respostas e as ferramentas usadas. Conversas longas vêm em partes; continue com start.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"id":{"type":"string"},"start":{"type":"integer","minimum":1,"description":"Mensagem inicial, para continuar"}},"required":["id"],"additionalProperties":false}""")!,
        Label = i => "Lendo a conversa " + ((string?)i["id"] ?? ""),
        Run = (i, host) =>
        {
            var id = ((string?)i["id"] ?? "").Trim();
            var c = History(host).Recent().FirstOrDefault(x => x.Id == id)
                ?? throw new AiToolException("A conversa " + id + " não está entre as suas. Use list_conversations para ver as suas conversas.");
            var lines = Lines(c);
            int start = Math.Clamp((int?)i["start"] ?? 1, 1, Math.Max(1, lines.Count));
            var text = new StringBuilder("# " + (c.Title.Length > 0 ? c.Title : "(sem título)") + " — " + Date(c.Created) + " a " + Date(c.Updated) + "\n");
            int n = start - 1;
            for (; n < lines.Count && text.Length + lines[n].Length < PageChars; n++) text.Append('\n').Append(n + 1).Append(". ").Append(lines[n]).Append('\n');
            if (n == start - 1 && n < lines.Count) { text.Append('\n').Append(n + 1).Append(". ").Append(Clip(lines[n], PageChars)).Append('\n'); n++; }
            if (n < lines.Count) text.Append("\n⚠ CONTINUA: mensagens ").Append(n + 1).Append('–').Append(lines.Count).Append(". Chame read_conversation com start=").Append(n + 1).Append('.');
            return Task.FromResult(new AiToolOutput(text.ToString().TrimEnd()));
        }
    };

    private static IAssistantConversations History(IAssistantHost host) =>
        host.Conversations ?? throw new AiToolException("O histórico de conversas não está disponível aqui.");

    /// <summary>What the person saw, message by message: their words, the answers and the tools used (not their results).</summary>
    private static List<string> Lines(AiConversation c)
    {
        var lines = new List<string>();
        foreach (var m in c.Messages)
        {
            if (m.Role == "user")
            {
                if (m.Meta?.ToolResults == true) continue;
                if (Visible(m) is { Length: > 0 } said) lines.Add("Usuário: " + said);
                continue;
            }
            var parts = new List<string>();
            foreach (var b in m.Content)
            {
                if (b.Hidden) continue;
                if (b.Type == "text" && LegacyCustomize.JsTrim(b.Text ?? "") is { Length: > 0 } t) parts.Add(t);
                else if (b.Type == "tool_use") parts.Add("[ferramenta " + b.Name + ((b.Input as JsonObject)?["path"] is { } p ? ": " + p : "") + "]");
            }
            if (parts.Count > 0) lines.Add("Assistente: " + string.Join("\n", parts));
        }
        return lines;
    }

    private static string Transcript(AiConversation c) => string.Join("\n", Lines(c));

    private static string Visible(AiChatMessage m) =>
        LegacyCustomize.JsTrim(string.Join("\n", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text ?? "")));

    private static string Clip(string s, int n)
    {
        var one = s.Replace('\n', ' ');
        return one.Length > n ? one[..n] + "…" : one;
    }

    private static string Date(long ms) => ms <= 0 ? "?" :
        DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
