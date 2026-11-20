using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core.Study;

namespace Urbe.Core.Assistant;

/// <summary>
/// #1005: the native study library, as the app shows it — decks of flashcards and the cards of the notes — and the one
/// operation that makes and changes decks. The library's buttons and the Assistente's tools call the same methods.
/// </summary>
public interface IAssistantStudy
{
    /// <summary>Every set (decks and notes with cards) with its progress today.</summary>
    IReadOnlyList<(StudySet Set, StudySetProgress Progress)> Sets();
    /// <summary>Makes a deck, written and read back before it returns.</summary>
    Task<FlashcardDeck> CreateDeckAsync(string title, IReadOnlyList<(string Front, string Back)> cards, string? source);
    /// <summary>Adds cards to a deck, written and read back before it returns.</summary>
    Task<FlashcardDeck> AddCardsAsync(string deckId, IReadOnlyList<(string Front, string Back)> cards);
    /// <summary>Opens the library (at a set, when given) for the person to review.</summary>
    void OpenStudy(string? setId);
}

/// <summary>
/// #1005 / beta.60 regression: "two flashcards with the native tool" became a plugin after 13 model calls. Flashcards
/// are a native study object: create_flashcards makes the deck in one call; list_flashcards and open_flashcards show it.
/// No note, folder or plugin is made for them.
/// </summary>
public static class AssistantStudyTools
{
    private const string CardsSchema = """{"type":"array","minItems":1,"items":{"type":"object","properties":{"front":{"type":"string","description":"Pergunta ou termo"},"back":{"type":"string","description":"Resposta"}},"required":["front","back"],"additionalProperties":false}}""";

    public static AiTool Create { get; } = new()
    {
        Name = "create_flashcards", Access = "write",
        Description = "Cria um conjunto de flashcards (cartões de memorização) NATIVO na biblioteca de Estudo do Numa, com revisão espaçada. " +
                      "Use sempre que pedirem flashcards, cartões, cards de estudo ou perguntas para revisar — inclusive a partir de uma nota ou fonte (leia-a antes e passe source). " +
                      "Uma chamada cria todos os cartões. NÃO crie nota, pasta nem plugin para isso, e não procure no guia ou nas notas como fazer: esta é a ferramenta. " +
                      "Para pôr cartões num conjunto existente use deck_id (de list_flashcards). Fatos comuns dispensam busca.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"title":{"type":"string","description":"Título do conjunto (sem deck_id)"},"deck_id":{"type":"string","description":"Conjunto existente para acrescentar"},"source":{"type":"string","description":"Caminho da nota ou arquivo de onde os cartões vieram"},"cards":""" + CardsSchema + """},"required":["cards"],"additionalProperties":false}""")!,
        Label = i => { var n = (i["cards"] as JsonArray)?.Count ?? 0; return (n == 1 ? "1 flashcard" : n + " flashcards") + ((string?)i["title"] is { Length: > 0 } t ? ": " + t : ""); },
        Preview = (i, _) =>
        {
            var cards = Cards(i);
            var after = new StringBuilder();
            after.Append("# ").Append((string?)i["title"] ?? (string?)i["deck_id"] ?? "Flashcards").Append('\n');
            foreach (var (f, b) in cards) after.Append("- ").Append(f).Append(" → ").Append(b).Append('\n');
            return new AiPreview("Estudo › Flashcards", null, after.ToString(), "Conjunto da biblioteca de Estudo (não é nota)");
        },
        Run = async (i, host) =>
        {
            var study = host.Study ?? throw new AiToolException("A biblioteca de Estudo não está disponível neste app; nada foi criado. Não substitua por plugin ou nota sem a pessoa pedir.");
            var cards = Cards(i);
            FlashcardDeck deck;
            try
            {
                deck = (string?)i["deck_id"] is { Length: > 0 } id
                    ? await study.AddCardsAsync(id, cards)
                    : await study.CreateDeckAsync((string?)i["title"] ?? throw new AiToolException("Dê title (conjunto novo) ou deck_id (existente)."), cards, (string?)i["source"]);
            }
            catch (ArgumentException e) { throw new AiToolException(e.Message); }
            return new AiToolOutput("Conjunto \"" + deck.Title + "\" (deck_id " + deck.Id + ") salvo na biblioteca de Estudo com " + deck.Cards.Count +
                                    (deck.Cards.Count == 1 ? " cartão" : " cartões") + "; verificado ao reler. A pessoa revisa em Estudo › Flashcards (open_flashcards abre). Nenhuma nota ou plugin foi criado.");
        }
    };

    public static AiTool List { get; } = new()
    {
        Name = "list_flashcards", Access = "read",
        Description = "Lista os conjuntos de flashcards da biblioteca de Estudo (os nativos e os das notas pergunta::resposta) com cartões, pendentes hoje e revisados; com set_id, mostra os cartões do conjunto.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"set_id":{"type":"string"}},"additionalProperties":false}""")!,
        Label = _ => "Flashcards da biblioteca",
        Run = (i, host) =>
        {
            var study = host.Study ?? throw new AiToolException("A biblioteca de Estudo não está disponível neste app.");
            var sets = study.Sets();
            if ((string?)i["set_id"] is { Length: > 0 } wanted)
            {
                var (set, p) = sets.FirstOrDefault(s => s.Set.Id == wanted || s.Set.DeckId == wanted);
                if (set is null) throw new AiToolException("Não há conjunto " + wanted + ". Conjuntos: " + string.Join(", ", sets.Select(s => s.Set.Id)));
                var sb = new StringBuilder(set.Title + " (" + set.Id + "): " + p.Cards + " cartões, " + p.Due + " para hoje\n");
                foreach (var c in set.Cards) sb.Append("- ").Append(c.Front).Append(" → ").Append(c.Back).Append('\n');
                return Task.FromResult(new AiToolOutput(sb.ToString()));
            }
            if (sets.Count == 0) return Task.FromResult(new AiToolOutput("A biblioteca de Estudo não tem flashcards ainda."));
            return Task.FromResult(new AiToolOutput(string.Join("\n", sets.Select(s =>
                "- " + s.Set.Id + " — " + s.Set.Title + (s.Set.Kind == "note" ? " (da nota)" : "") + ": " + s.Progress.Cards + " cartões, " + s.Progress.Due + " para hoje, " + s.Progress.Reviewed + " já revisados"))));
        }
    };

    public static AiTool Open { get; } = new()
    {
        Name = "open_flashcards", Access = "ui",
        Description = "Abre a biblioteca de Estudo › Flashcards para a pessoa revisar (num conjunto, com set_id). Você não revisa pela pessoa.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"set_id":{"type":"string"}},"additionalProperties":false}""")!,
        Label = _ => "Abrir os flashcards",
        Run = (i, host) =>
        {
            var study = host.Study ?? throw new AiToolException("A biblioteca de Estudo não está disponível neste app.");
            study.OpenStudy((string?)i["set_id"]);
            return Task.FromResult(new AiToolOutput("Biblioteca de flashcards aberta."));
        }
    };

    public static IReadOnlyList<AiTool> All { get; } = [Create, List, Open];

    private static readonly System.Text.RegularExpressions.Regex Made = new(@"^Conjunto ""(?<title>.*)"" \(deck_id (?<id>[^)\s]+)\) salvo na biblioteca de Estudo com (?<n>\d+) cart", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>The deck a create_flashcards result names (id, title, cards), for the chat to show it as a study object.</summary>
    public static (string Id, string Title, int Cards)? Deck(string? result) =>
        result is not null && Made.Match(result) is { Success: true } m ? (m.Groups["id"].Value, m.Groups["title"].Value, int.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture)) : null;

    private static List<(string Front, string Back)> Cards(JsonObject input) =>
        (input["cards"] as JsonArray ?? throw new AiToolException("Mande cards: [{front, back}]."))
            .Select((n, k) => n is JsonObject o ? ((string?)o["front"] ?? "", (string?)o["back"] ?? "") : throw new AiToolException("O cartão " + (k + 1) + " não é {front, back}."))
            .ToList();
}
