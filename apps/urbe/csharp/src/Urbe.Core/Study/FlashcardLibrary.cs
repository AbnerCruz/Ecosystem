using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core.Study;

/// <summary>A card of a deck: its stable id, front and back, and where it came from (a note or a source), when it did.</summary>
public sealed record DeckCard
{
    public string Id { get; init; } = "";
    public string Front { get; init; } = "";
    public string Back { get; init; } = "";
    public string? Source { get; init; }
}

/// <summary>A deck of flashcards made in the app (by the person or the Assistente): a study object, not a note.</summary>
public sealed record FlashcardDeck
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    /// <summary>"manual" or "assistente".</summary>
    public string Origin { get; init; } = "manual";
    /// <summary>The note or file the deck was made from, when there is one.</summary>
    public string? Source { get; init; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; init; }
    public List<DeckCard> Cards { get; init; } = [];

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>
/// One set of cards the library shows: a deck of the app, or the <c>pergunta::resposta</c> cards of a note — the same
/// list, without copying the note's cards anywhere (the note stays their only authority).
/// </summary>
/// <param name="Id">"deck:&lt;id&gt;" or "note:&lt;path&gt;".</param>
public sealed record StudySet(string Id, string Title, string Kind, string? NotePath, string? DeckId, IReadOnlyList<Flashcard> Cards, DateTime? Updated);

/// <summary>A set's progress today: how many cards, how many due (new or scheduled) and how many were ever reviewed.</summary>
public sealed record StudySetProgress(string SetId, int Cards, int Due, int Reviewed, DateOnly? Next);

/// <summary>
/// #1005 (first vertical slice): the native flashcard library. Decks the person or the Assistente makes live in
/// <c>.urbe/estudo/flashcards.json</c> — inside the vault, so they travel with it and its backups, and hidden from the
/// Explorer like every <c>.urbe</c> file, so studying never adds notes or plugins. The review progress stays where it
/// already was (the app-private study state, SM-2, per card id). One operation serves the library's buttons and the
/// Assistente's tools alike.
/// </summary>
public sealed class FlashcardLibrary
{
    public const string Path = ".urbe/estudo/flashcards.json";
    public const int Version = 1;
    /// <summary>Cards one call may add: far above any deck a person studies, it only keeps a runaway call bounded.</summary>
    public const int MaxCardsPerCall = 5000;
    public const int MaxText = 4000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public List<FlashcardDeck> Decks { get; } = [];

    private sealed record Document(int Version, List<FlashcardDeck> Decks);

    /// <summary>True for a library of a version this app knows (an absent file is an empty library).</summary>
    public static bool IsLibrary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("version", out var v) &&
                   v.TryGetInt32(out var n) && n >= 1 && n <= Version && doc.RootElement.TryGetProperty("decks", out var d) && d.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Reads the library; a file this app cannot read (newer, damaged) throws instead of being taken for empty.</summary>
    public static FlashcardLibrary Parse(string? json)
    {
        var library = new FlashcardLibrary();
        if (string.IsNullOrWhiteSpace(json)) return library;
        if (!IsLibrary(json)) throw new InvalidDataException("O arquivo de flashcards é de uma versão mais nova do Numa ou está danificado; ele não foi alterado.");
        var doc = JsonSerializer.Deserialize<Document>(json, Json);
        library.Decks.AddRange(doc?.Decks?.Where(d => d is not null && d.Id.Length > 0) ?? []);
        return library;
    }

    public string Serialize() => JsonSerializer.Serialize(new Document(Version, Decks), Json);

    public FlashcardDeck? Find(string id) => Decks.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));

    /// <summary>A card's id: from its deck and its front, so fixing the answer keeps its history (as the notes' cards).</summary>
    public static string CardId(string deckId, string front) => Flashcards.Id("deck:" + deckId, front);

    /// <summary>Makes a deck (checked whole before anything changes) and returns it.</summary>
    public FlashcardDeck Create(string title, IReadOnlyList<(string Front, string Back)> cards, string origin, string? source, DateTime now)
    {
        title = (title ?? "").Trim();
        if (title.Length == 0) throw new ArgumentException("Dê um título ao conjunto de cartões.");
        if (title.Length > 200) throw new ArgumentException("O título tem " + title.Length + " caracteres; use até 200.");
        if (origin is not ("manual" or "assistente")) throw new ArgumentException("origin é manual ou assistente.");
        var id = NewId(title);
        var deck = new FlashcardDeck { Id = id, Title = title, Origin = origin, Source = Blank(source), Created = now, Updated = now, Cards = Checked(id, cards, []) };
        Decks.Add(deck);
        return deck;
    }

    /// <summary>Adds cards to a deck; a front the deck already has is refused (it would be the same card).</summary>
    public FlashcardDeck Add(string deckId, IReadOnlyList<(string Front, string Back)> cards, DateTime now)
    {
        var deck = Find(deckId) ?? throw new ArgumentException("Não há conjunto " + deckId + ".");
        var added = Checked(deck.Id, cards, deck.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal));
        var changed = deck with { Cards = [.. deck.Cards, .. added], Updated = now };
        Decks[Decks.IndexOf(deck)] = changed;
        return changed;
    }

    /// <summary>Changes one card's text; its id (and so its review history) stays when the front stays.</summary>
    public FlashcardDeck EditCard(string deckId, string cardId, string front, string back, DateTime now)
    {
        var deck = Find(deckId) ?? throw new ArgumentException("Não há conjunto " + deckId + ".");
        var at = deck.Cards.FindIndex(c => c.Id == cardId);
        if (at < 0) throw new ArgumentException("O conjunto não tem o cartão " + cardId + ".");
        var others = deck.Cards.Where((_, i) => i != at).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var card = Checked(deck.Id, [(front, back)], others)[0] with { Source = deck.Cards[at].Source };
        var list = deck.Cards.ToList();
        list[at] = card;
        var changed = deck with { Cards = list, Updated = now };
        Decks[Decks.IndexOf(deck)] = changed;
        return changed;
    }

    public FlashcardDeck RemoveCard(string deckId, string cardId, DateTime now)
    {
        var deck = Find(deckId) ?? throw new ArgumentException("Não há conjunto " + deckId + ".");
        if (deck.Cards.RemoveAll(c => c.Id == cardId) == 0) throw new ArgumentException("O conjunto não tem o cartão " + cardId + ".");
        var changed = deck with { Updated = now };
        Decks[Decks.IndexOf(deck)] = changed;
        return changed;
    }

    public FlashcardDeck Rename(string deckId, string title, DateTime now)
    {
        var deck = Find(deckId) ?? throw new ArgumentException("Não há conjunto " + deckId + ".");
        title = (title ?? "").Trim();
        if (title.Length is 0 or > 200) throw new ArgumentException("O título tem de 1 a 200 caracteres.");
        var changed = deck with { Title = title, Updated = now };
        Decks[Decks.IndexOf(deck)] = changed;
        return changed;
    }

    public bool Delete(string deckId) => Decks.RemoveAll(d => d.Id == deckId) > 0;

    /// <summary>Every set the library shows: the decks, then each note with <c>pergunta::resposta</c> cards.</summary>
    public IReadOnlyList<StudySet> Sets(IEnumerable<(string Path, string Id, string Content)> notes)
    {
        var sets = new List<StudySet>();
        foreach (var d in Decks)
            sets.Add(new StudySet("deck:" + d.Id, d.Title, "deck", null, d.Id, d.Cards.Select((c, i) => new Flashcard(c.Id, c.Front, c.Back, i)).ToList(), d.Updated));
        foreach (var (path, id, content) in notes)
        {
            if (!content.Contains("::", StringComparison.Ordinal)) continue;
            var cards = Flashcards.Read(id, content);
            if (cards.Count == 0) continue;
            sets.Add(new StudySet("note:" + path, System.IO.Path.GetFileNameWithoutExtension(path), "note", path, null, cards, null));
        }
        return sets;
    }

    /// <summary>A set's progress on <paramref name="today"/>; <paramref name="state"/> is the review state by card id.</summary>
    public static StudySetProgress Progress(StudySet set, Func<string, CardState?> state, DateOnly today)
    {
        int due = 0, reviewed = 0;
        DateOnly? next = null;
        foreach (var c in set.Cards)
        {
            var s = state(c.Id);
            if (s is null || s.Due <= today) due++;
            if (s is { Reviews: > 0 }) reviewed++;
            if (s is not null && s.Due > today && (next is null || s.Due < next)) next = s.Due;
        }
        return new StudySetProgress(set.Id, set.Cards.Count, due, reviewed, next);
    }

    private static List<DeckCard> Checked(string deckId, IReadOnlyList<(string Front, string Back)> cards, HashSet<string> taken)
    {
        if (cards is null || cards.Count == 0) throw new ArgumentException("Mande ao menos um cartão (frente e verso).");
        if (cards.Count > MaxCardsPerCall) throw new ArgumentException(cards.Count + " cartões numa chamada; mande até " + MaxCardsPerCall + " e o resto em outra.");
        var list = new List<DeckCard>();
        for (int i = 0; i < cards.Count; i++)
        {
            var front = (cards[i].Front ?? "").Trim();
            var back = (cards[i].Back ?? "").Trim();
            if (front.Length == 0 || back.Length == 0) throw new ArgumentException("O cartão " + (i + 1) + " está sem " + (front.Length == 0 ? "frente" : "verso") + ".");
            if (front.Length > MaxText || back.Length > MaxText) throw new ArgumentException("O cartão " + (i + 1) + " passa de " + MaxText + " caracteres num lado.");
            var id = CardId(deckId, front);
            if (!taken.Add(id)) throw new ArgumentException("O cartão " + (i + 1) + " repete a frente \"" + front + "\" de outro cartão do conjunto.");
            list.Add(new DeckCard { Id = id, Front = front, Back = back });
        }
        return list;
    }

    private string NewId(string title)
    {
        var slug = new string(title.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        if (slug.Length == 0) slug = "conjunto";
        var id = slug;
        for (int n = 2; Find(id) is not null; n++) id = slug + "-" + n;
        return id;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
