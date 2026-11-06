using System.Globalization;
using System.Text;

namespace Urbe.Core.Assistant;

/// <summary>Why a fresh conversation may be more practical than going on with this one.</summary>
public enum ConversationHintReason
{
    /// <summary>The history is near what one request can carry: its beginning starts to be cut or condensed.</summary>
    Long,
    /// <summary>A long pause, and what is being typed shares no subject with anything said before.</summary>
    NewTopicAfterPause
}

/// <summary>A calm suggestion; <see cref="Fill"/> is the share of the model's history room the conversation already takes.</summary>
public sealed record ConversationHint(ConversationHintReason Reason, string Text, double Fill);

/// <summary>
/// CST-145: tells a conversation still in use from one where starting a new chat is more practical: when its history
/// nears what the model's window can carry (the same room <see cref="AssistantContextBudget.Build"/> gives it), or when,
/// after a long pause, the new request has nothing in common with what was talked about. It only suggests: the app
/// never switches by itself, and the old conversation stays in the history. The new chat starts from
/// <see cref="Seed"/>: a short summary taken from the conversation itself (never written by a model) and its id, so the
/// agent can read it in detail (read_conversation).
/// </summary>
public static class ConversationFreshness
{
    /// <summary>Share of the history room from which a conversation counts as long.</summary>
    public const double LongShare = 0.7;
    /// <summary>A pause this long, with a request on another subject, suggests a new chat.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromHours(6);
    /// <summary>A conversation with fewer visible messages is never worth leaving.</summary>
    public const int MinMessages = 6;
    public const string LongText = "Esta conversa está longa: começar uma nova com um resumo?";
    public const string NewTopicText = "Assunto novo depois de uma pausa: começar uma nova conversa com um resumo desta?";

    /// <summary>The characters of history a request can carry for a model with this window (the budget's estimate:
    /// the answer's reserve set apart, about 1.5 characters per token, never more than the agent's working history).</summary>
    public static long HistoryRoom(long? contextTokens, int maxTokens = 8192)
    {
        long window = contextTokens is > 0 ? contextTokens.Value : 128000;
        long reserved = Math.Min(Math.Max(1, maxTokens), Math.Max(1, window / 4));
        return Math.Min(AssistantAgent.WorkingChars, Math.Max(1, window - reserved) * 3 / 2);
    }

    /// <summary>What the conversation's messages take, as the request carries them (tool results and attachments included).</summary>
    public static long HistoryChars(AiConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        long chars = 0;
        foreach (var m in conversation.Messages) chars += m.ToJson(withId: false).ToJsonString().Length;
        return chars;
    }

    /// <summary>The hint to show now, or null when the conversation is still the right place.</summary>
    /// <param name="now">Unix milliseconds.</param>
    /// <param name="draft">What the person is typing (the subject of the next request).</param>
    /// <param name="historyChars">A cached <see cref="HistoryChars"/>, when the caller has it.</param>
    public static ConversationHint? Evaluate(AiConversation conversation, long? contextTokens, long now, string? draft = null, long? historyChars = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (conversation.OriginConversationId is not null) return null;   // a consultation between agents is not the person's chat
        if (VisibleCount(conversation) < MinMessages) return null;
        double fill = (double)(historyChars ?? HistoryChars(conversation)) / HistoryRoom(contextTokens);
        if (fill >= LongShare) return new(ConversationHintReason.Long, LongText, Math.Min(1, fill));
        if (conversation.Updated > 0 && now - conversation.Updated >= (long)Pause.TotalMilliseconds && NewSubject(conversation, draft))
            return new(ConversationHintReason.NewTopicAfterPause, NewTopicText, fill);
        return null;
    }

    /// <summary>The request has at least three words of substance and none of them was ever said in the conversation.</summary>
    public static bool NewSubject(AiConversation conversation, string? draft)
    {
        var asked = Stems(draft);
        if (asked.Count < 3) return false;
        var said = Stems(conversation.Title);
        foreach (var m in conversation.Messages) if (Visible(m) is { Length: > 0 } text) said.UnionWith(Stems(text));
        return !asked.Overlaps(said);
    }

    /// <summary>
    /// The first message of the new chat, for the person to read, change and send: where it came from (title, date, id),
    /// the request that opened it, the last answer and the notes it worked on, then what was being typed.
    /// </summary>
    public static string Seed(AiConversation old, string? draft = null)
    {
        ArgumentNullException.ThrowIfNull(old);
        var title = old.Title.Trim().Length > 0 ? old.Title.Trim() : "sem título";
        var text = new StringBuilder("Continuando a conversa “").Append(title).Append('”');
        if (old.Updated > 0)
            text.Append(" de ").Append(DateTimeOffset.FromUnixTimeMilliseconds(old.Updated).ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        text.Append(" (id: ").Append(old.Id).Append("; os detalhes estão nela).\n");
        var requests = old.Messages.Where(m => m.Role == "user" && m.Meta?.ToolResults != true && Visible(m).Length > 0).ToList();
        if (requests.Count > 0) text.Append("- Começou com: ").Append(Clip(Visible(requests[0]), 220)).Append('\n');
        if (requests.Count > 1) text.Append("- Último pedido: ").Append(Clip(Visible(requests[^1]), 220)).Append('\n');
        if (old.Messages.LastOrDefault(m => m.Role == "assistant" && Visible(m).Length > 0) is { } last)
            text.Append("- Última resposta: ").Append(Clip(Visible(last), 320)).Append('\n');
        var notes = old.Runs.SelectMany(r => r.Changes).Select(c => (c.After ?? c.Before)?.Path)
            .Concat(old.Messages.Select(m => m.Meta?.Context))
            .OfType<string>().Select(NotePath).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (notes.Count > 0)
            text.Append("- Notas: ").Append(string.Join(", ", notes.Take(12).Select(p => "[[" + p + "]]"))).Append(notes.Count > 12 ? " …" : "").Append('\n');
        text.Append('\n');
        if (!string.IsNullOrWhiteSpace(draft)) text.Append(draft.Trim());
        return text.ToString();
    }

    private static int VisibleCount(AiConversation c) => c.Messages.Count(m =>
        (m.Role == "user" && m.Meta?.ToolResults != true || m.Role == "assistant") && Visible(m).Length > 0);

    private static string Visible(AiChatMessage m) =>
        string.Join("\n", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text ?? "")).Trim();

    private static string Clip(string s, int n)
    {
        var one = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return one.Length > n ? one[..n].TrimEnd() + "…" : one;
    }

    // a note's path as a wiki link takes it; "" for something that is not a path (a context note like "Nota · seleção")
    private static string NotePath(string context)
    {
        var path = context.Trim();
        int dot = path.IndexOf(" · ", StringComparison.Ordinal);
        if (dot >= 0) path = path[..dot];
        if (path.Length == 0 || path.Contains('\n') || path.Contains("[[", StringComparison.Ordinal)) return "";
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path[..^3] : path;
    }

    // the words of substance, folded (no accents, lower case) and cut to five letters so "notas" meets "nota"
    private static HashSet<string> Stems(string? text)
    {
        var stems = new HashSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();
        foreach (var c in AssistantTools.Fold(text) + " ")
        {
            if (char.IsLetterOrDigit(c)) { word.Append(c); continue; }
            if (word.Length >= 4 && !Common.Contains(word.ToString())) stems.Add(word.Length > 5 ? word.ToString(0, 5) : word.ToString());
            word.Clear();
        }
        return stems;
    }

    // words of any request (Portuguese and English), folded: they say nothing about the subject
    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "quero", "queria", "gostaria", "fazer", "faca", "faz", "pode", "podes", "poderia", "preciso", "precisa", "sobre", "como",
        "para", "pela", "pelo", "pelas", "pelos", "essa", "esse", "esses", "essas", "isso", "este", "esta", "estes", "estas", "isto",
        "aquele", "aquela", "aquilo", "minha", "minhas", "meus", "nossa", "nosso", "nota", "notas", "agora", "tambem", "mais",
        "menos", "muito", "muita", "pouco", "depois", "antes", "ainda", "quando", "onde", "qual", "quais", "porque", "entao",
        "coisa", "coisas", "favor", "obrigado", "obrigada", "voce", "voces", "seria", "fosse", "estar", "estou", "tenho", "temos",
        "tem", "algo", "alguma", "algum", "todos", "todas", "tudo", "cada", "outra", "outro", "outras", "outros", "mesmo",
        "mesma", "assim", "aqui", "dela", "dele", "delas", "deles", "numa", "num", "uma", "umas", "uns", "ate", "desde", "entre",
        "sem", "com", "por", "que", "nao", "sim", "bem", "vamos", "vou", "vai", "fale", "diga", "explique", "mostre", "crie",
        "escreva", "ajude", "ajuda", "olhe", "veja", "lista", "liste", "please", "about", "what", "this", "that", "with", "from",
        "have", "make", "could", "would", "should", "your", "into", "some", "more", "also", "then", "there", "their", "they"
    };
}
