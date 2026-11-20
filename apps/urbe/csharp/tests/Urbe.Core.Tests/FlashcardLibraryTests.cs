using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;
using Urbe.Core.Study;

namespace Urbe.Core.Tests;

/// <summary>#1005, first vertical slice: the native flashcard library and the Assistente's tools over it.</summary>
public sealed class FlashcardLibraryTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ADeckIsMadeWholeAndSurvivesSavingAndReading()
    {
        var library = new FlashcardLibrary();
        var deck = library.Create("Animais", [("Maior mamífero?", "Baleia-azul"), ("Animal mais rápido?", "Guepardo")], "assistente", null, Now);
        Assert.Equal("animais", deck.Id);
        Assert.Equal(2, deck.Cards.Count);
        Assert.NotEqual(deck.Cards[0].Id, deck.Cards[1].Id);
        var back = FlashcardLibrary.Parse(library.Serialize());
        var again = Assert.Single(back.Decks);
        Assert.Equal(deck.Cards.Select(c => (c.Id, c.Front, c.Back)), again.Cards.Select(c => (c.Id, c.Front, c.Back)));
        Assert.Equal("assistente", again.Origin);
        // a second deck with the same title gets its own id
        Assert.Equal("animais-2", back.Create("Animais", [("a", "b")], "manual", null, Now).Id);
    }

    [Theory]
    [InlineData("", "a", "b")]
    [InlineData("T", "", "b")]
    [InlineData("T", "a", "")]
    public void ABadDeckIsRefusedAndNothingChanges(string title, string front, string back)
    {
        var library = new FlashcardLibrary();
        Assert.Throws<ArgumentException>(() => library.Create(title, [(front, back)], "manual", null, Now));
        Assert.Empty(library.Decks);
    }

    [Fact]
    public void ARepeatedFrontIsRefusedAndFixingTheAnswerKeepsTheCard()
    {
        var library = new FlashcardLibrary();
        Assert.Throws<ArgumentException>(() => library.Create("T", [("Gato", "felino"), ("gato", "outro")], "manual", null, Now));
        var deck = library.Create("T", [("Gato", "felino")], "manual", null, Now);
        Assert.Throws<ArgumentException>(() => library.Add(deck.Id, [("Gato", "de novo")], Now));
        var id = deck.Cards[0].Id;
        var fixedDeck = library.EditCard(deck.Id, id, "Gato", "Felis catus", Now);
        Assert.Equal(id, fixedDeck.Cards[0].Id);
        Assert.Equal("Felis catus", fixedDeck.Cards[0].Back);
        Assert.NotEqual(id, library.EditCard(deck.Id, id, "Cão", "Canis", Now).Cards[0].Id);
    }

    [Fact]
    public void TheLibraryListsDecksAndTheNotesCardsWithoutCopyingThem()
    {
        var library = new FlashcardLibrary();
        library.Create("Animais", [("a", "b")], "manual", null, Now);
        var sets = library.Sets([("Bio/Células.md", "doc_1", "# Células\n\nMitocôndria::usina da célula\n"), ("Diário.md", "doc_2", "Hoje choveu.")]);
        Assert.Equal(["deck:animais", "note:Bio/Células.md"], sets.Select(s => s.Id));
        Assert.Equal(Flashcards.Id("doc_1", "Mitocôndria"), sets[1].Cards[0].Id);
        Assert.Single(library.Decks);
    }

    [Fact]
    public void ProgressCountsDueAndReviewedCards()
    {
        var library = new FlashcardLibrary();
        var deck = library.Create("T", [("a", "1"), ("b", "2"), ("c", "3")], "manual", null, Now);
        var set = library.Sets([])[0];
        var today = new DateOnly(2026, 11, 20);
        var states = new Dictionary<string, CardState> { [deck.Cards[0].Id] = Flashcards.Review(null, Recall.Bom, today) };
        var p = FlashcardLibrary.Progress(set, id => states.GetValueOrDefault(id), today);
        Assert.Equal((3, 2, 1, today.AddDays(1)), (p.Cards, p.Due, p.Reviewed, p.Next!.Value));
    }

    [Fact]
    public void AFileOfANewerVersionIsNeverTakenForEmpty()
    {
        Assert.True(FlashcardLibrary.IsLibrary(null));
        Assert.False(FlashcardLibrary.IsLibrary("""{"version":99,"decks":[]}"""));
        Assert.False(FlashcardLibrary.IsLibrary("não é json"));
        Assert.Throws<InvalidDataException>(() => FlashcardLibrary.Parse("""{"version":99,"decks":[]}"""));
    }

    private sealed class Host : IAssistantHost, IAssistantStudy
    {
        public FlashcardLibrary Library = new();
        public int Writes;
        public string? Opened;
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() => Knowledge = new(Documents);
        public int ReadChars => 16000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => throw new InvalidOperationException("no note");
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new InvalidOperationException("no note");
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new InvalidOperationException("no folder");
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IAssistantStudy? Study => this;
        public IReadOnlyList<(StudySet Set, StudySetProgress Progress)> Sets() =>
            Library.Sets([]).Select(s => (s, FlashcardLibrary.Progress(s, _ => null, new DateOnly(2026, 11, 20)))).ToList();
        public Task<FlashcardDeck> CreateDeckAsync(string title, IReadOnlyList<(string Front, string Back)> cards, string? source) { Writes++; return Task.FromResult(Library.Create(title, cards, "assistente", source, Now)); }
        public Task<FlashcardDeck> AddCardsAsync(string deckId, IReadOnlyList<(string Front, string Back)> cards) { Writes++; return Task.FromResult(Library.Add(deckId, cards, Now)); }
        public void OpenStudy(string? setId) => Opened = setId ?? "";
    }

    private static JsonObject Input(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>The beta.60 prompt ("dois flashcards com a ferramenta nativa, tema animais") is one call of one tool.</summary>
    [Fact]
    public async Task TwoNativeFlashcardsAreOneCallWithNoNoteNoFolderNoPlugin()
    {
        var host = new Host();
        var output = await AssistantStudyTools.Create.Run(Input("""{"title":"Animais","cards":[{"front":"Maior mamífero?","back":"Baleia-azul"},{"front":"Ave que não voa e corre mais rápido?","back":"Avestruz"}]}"""), host);
        Assert.Equal(1, host.Writes);
        var deck = Assert.Single(host.Library.Decks);
        Assert.Equal(2, deck.Cards.Count);
        Assert.Contains("deck_id animais", output.Content);
        Assert.Contains("Nenhuma nota ou plugin", output.Content);
        Assert.Equal(("animais", "Animais", 2), AssistantStudyTools.Deck(output.Content));   // the chat shows it as a deck
        Assert.Null(AssistantStudyTools.Deck("Nota criada."));
        Assert.Empty(host.Documents.List());
        Assert.Null(output.Changes);
        // the tool's description sends flashcards here and the plugin tools away from them
        Assert.Contains("NÃO crie nota, pasta nem plugin", AssistantStudyTools.Create.Description);
        Assert.Contains("create_flashcards", AssistantPluginTools.All.Single(t => t.Name == "write_plugin").Description);

        var listed = (await AssistantStudyTools.List.Run(Input("{}"), host)).Content;
        Assert.Contains("deck:animais — Animais: 2 cartões, 2 para hoje", listed);
        Assert.Contains("Baleia-azul", (await AssistantStudyTools.List.Run(Input("""{"set_id":"animais"}"""), host)).Content);
        await AssistantStudyTools.Open.Run(Input("""{"set_id":"deck:animais"}"""), host);
        Assert.Equal("deck:animais", host.Opened);

        await AssistantStudyTools.Create.Run(Input("""{"deck_id":"animais","cards":[{"front":"Maior felino?","back":"Tigre"}]}"""), host);
        Assert.Equal(3, host.Library.Decks[0].Cards.Count);
    }

    [Fact]
    public async Task ABadCallSaysWhatToFixAndAnAppWithoutTheLibraryNeverFallsBackToAPlugin()
    {
        var host = new Host();
        var error = await Assert.ThrowsAsync<AiToolException>(() => AssistantStudyTools.Create.Run(Input("""{"title":"T","cards":[{"front":"a","back":""}]}"""), host));
        Assert.Contains("verso", error.Message);
        Assert.Empty(host.Library.Decks);
        var none = await Assert.ThrowsAsync<AiToolException>(() => AssistantStudyTools.Create.Run(Input("""{"title":"T","cards":[{"front":"a","back":"b"}]}"""), new NoStudyHost()));
        Assert.Contains("Não substitua por plugin", none.Message);
    }

    private sealed class NoStudyHost : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public NoStudyHost() => Knowledge = new(Documents);
        public int ReadChars => 16000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string text) => throw new NotSupportedException();
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }
}
