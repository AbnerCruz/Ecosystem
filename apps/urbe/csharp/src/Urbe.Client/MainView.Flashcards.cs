using Urbe.Core;
using Urbe.Core.Assistant;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>
/// #956: reviewing a note's flashcards (<c>pergunta::resposta</c>). The front, then the answer, then how well it was
/// remembered (Errei, Difícil, Bom, Fácil) sets when it comes back (SM-2). The student's state is app-private, per vault
/// and card, like the reading positions: apart from the notes, their checklists and the agents' memory.
/// </summary>
public sealed partial class MainView : IAssistantStudy
{
    /// <summary>Where the student's state is kept (tests point it elsewhere); null keeps it for the session only.</summary>
    public string? StudyStatePath { get; set; } = UrbeApp.LocalStatePath is { } p ? p + ".study.v1.json" : null;

    private Dictionary<string, CardState>? _study;

    private Dictionary<string, CardState> Study()
    {
        if (_study is not null) return _study;
        try { _study = Flashcards.Parse(StudyStatePath is { } path && File.Exists(path) ? File.ReadAllText(path) : null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _study = []; }
        return _study;
    }

    private void SaveStudy()
    {
        if (StudyStatePath is not { } path || _study is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, Flashcards.Serialize(_study));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { ShowLifeNotice("O progresso da revisão não pôde ser salvo: " + e.Message, Ui.ToastKind.Warning); }
    }

    private string CardKey(Flashcard card) => (_vault?.AtlasVaultSessionKey is { } v ? ReaderSessionCatalog.VaultKey(v) : "local") + "/" + card.Id;

    /// <summary>The note's cards due today (new or scheduled), for the menu's count.</summary>
    public IReadOnlyList<Flashcard> DueCards(CityNote note)
    {
        var id = _store?.Get(note.Path)?.Id ?? note.Path;
        var cards = Flashcards.Read(id, note.Content);
        var states = Study();
        var today = DateOnly.FromDateTime(DateTime.Now);
        return cards.Where(c => !states.TryGetValue(CardKey(c), out var s) || s.Due <= today).ToList();
    }

    public async Task ReviewCardsAsync(CityNote note)
    {
        var due = DueCards(note);
        if (due.Count == 0) { await Dialogs.AlertAsync("Revisar cartões", "Nenhum cartão para hoje nesta nota. Escreva um como pergunta::resposta numa linha."); return; }
        var today = DateOnly.FromDateTime(DateTime.Now);
        int done = 0;
        for (int i = 0; i < due.Count; i++)
        {
            var card = due[i];
            if (await Dialogs.ChooseAsync("Cartão " + (i + 1) + " de " + due.Count, [new DialogItem("Mostrar resposta")], card.Front) is not 0) break;
            var states = Study();
            var before = states.GetValueOrDefault(CardKey(card));
            string When(Recall r) { var d = Flashcards.Review(before, r, today).Due.DayNumber - today.DayNumber; return d <= 0 ? "Volta hoje" : d == 1 ? "Volta amanhã" : "Volta em " + d + " dias"; }
            var pick = await Dialogs.ChooseAsync(card.Front,
                [new DialogItem("Errei", Detail: When(Recall.Errei)), new DialogItem("Difícil", Detail: When(Recall.Dificil)),
                 new DialogItem("Bom", Detail: When(Recall.Bom)), new DialogItem("Fácil", Detail: When(Recall.Facil))], card.Back);
            if (pick is not int grade) break;
            states[CardKey(card)] = Flashcards.Review(before, (Recall)grade, today);
            SaveStudy();
            done++;
        }
        if (done > 0) ShowLifeNotice(done == 1 ? "1 cartão revisado." : done + " cartões revisados.");
    }

    // ---------------------------------------------------------------- #1005: the study library

    private FlashcardLibrary _flashcards = new();
    private bool _flashcardsReadOnly;

    IAssistantStudy? IAssistantApp.Study => this;

    private void LoadFlashcards(string? json)
    {
        try { _flashcards = FlashcardLibrary.Parse(json); _flashcardsReadOnly = false; }
        catch (InvalidDataException) { _flashcards = new(); _flashcardsReadOnly = true; }
    }

    private string StateKey(string cardId) => (_vault?.AtlasVaultSessionKey is { } v ? ReaderSessionCatalog.VaultKey(v) : "local") + "/" + cardId;

    private IReadOnlyList<StudySet> StudySets() =>
        _flashcards.Sets((_store?.List() ?? []).Where(d => ArtifactModel.IsNote(d.Path))
            .Select(d => (d.Path, d.Id, _contents.TryGetValue(d.Path, out var open) ? open : d.Content)));

    public IReadOnlyList<(StudySet Set, StudySetProgress Progress)> Sets()
    {
        var states = Study();
        var today = DateOnly.FromDateTime(DateTime.Now);
        return StudySets().Select(s => (s, FlashcardLibrary.Progress(s, id => states.GetValueOrDefault(StateKey(id)), today))).ToList();
    }

    private (int Sets, int Due)? FlashcardsSummary()
    {
        var sets = Sets();
        return sets.Count == 0 ? null : (sets.Count, sets.Sum(s => s.Progress.Due));
    }

    /// <summary>Changes the library on a copy, writes it (verified by the storage) and only then keeps it: a failed write changes nothing.</summary>
    private async Task<T> ChangeFlashcardsAsync<T>(Func<FlashcardLibrary, T> change)
    {
        if (_vault is null) throw new InvalidOperationException("Abra uma pasta de notas para guardar flashcards.");
        if (_flashcardsReadOnly) throw new InvalidOperationException("Os flashcards deste vault são de uma versão mais nova do Numa; atualize o app para mudá-los.");
        var copy = FlashcardLibrary.Parse(_flashcards.Serialize());
        var result = change(copy);
        var json = copy.Serialize();
        await _vault.SaveFlashcardsAsync(json);
        _flashcards = copy;
        return result;
    }

    public Task<FlashcardDeck> CreateDeckAsync(string title, IReadOnlyList<(string Front, string Back)> cards, string? source) =>
        ChangeFlashcardsAsync(l => l.Create(title, cards, "assistente", source, DateTime.UtcNow));

    public Task<FlashcardDeck> AddCardsAsync(string deckId, IReadOnlyList<(string Front, string Back)> cards) =>
        ChangeFlashcardsAsync(l => l.Add(deckId, cards, DateTime.UtcNow));

    void IAssistantStudy.OpenStudy(string? setId) => _ = ShowFlashcardsAsync(setId);

    private FlashcardsPanel? _flashcardsPanel;

    /// <summary>The library panel on the screen (tests and automation).</summary>
    public FlashcardsPanel? FlashcardsView => _flashcardsPanel is { IsVisible: true } p ? p : null;

    /// <summary>Opens the library panel over the app (at a set, when given).</summary>
    public Task ShowFlashcardsAsync(string? setId)
    {
        if (_flashcardsPanel is null)
        {
            _flashcardsPanel = new FlashcardsPanel(new FlashcardsHost(this)) { ZIndex = 30 };
            _stage.Children.Add(_flashcardsPanel);
        }
        _flashcardsPanel.IsVisible = true;
        _flashcardsPanel.Show(setId);
        _flashcardsPanel.Focus();
        return Task.CompletedTask;
    }

    public void CloseFlashcards() { if (_flashcardsPanel is not null) _flashcardsPanel.IsVisible = false; }

    private sealed class FlashcardsHost(MainView view) : FlashcardsPanel.IHost
    {
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
        public IReadOnlyList<(StudySet Set, StudySetProgress Progress)> Sets() => view.Sets();
        public IReadOnlyList<Flashcard> Due(StudySet set, bool all)
        {
            var states = view.Study();
            return all ? set.Cards : set.Cards.Where(c => !states.TryGetValue(view.StateKey(c.Id), out var s) || s.Due <= Today).ToList();
        }
        public IReadOnlyList<(string Label, string Detail)> When(Flashcard card)
        {
            var before = view.Study().GetValueOrDefault(view.StateKey(card.Id));
            return Enum.GetValues<Recall>().Select(r =>
            {
                var d = Flashcards.Review(before, r, Today).Due.DayNumber - Today.DayNumber;
                return (r.ToString(), d <= 0 ? "hoje" : d == 1 ? "amanhã" : d + " dias");
            }).ToList();
        }
        public void Grade(Flashcard card, Recall grade)
        {
            var key = view.StateKey(card.Id);
            view.Study()[key] = Flashcards.Review(view.Study().GetValueOrDefault(key), grade, Today);
            view.SaveStudy();
        }
        public string StateOf(Flashcard card) =>
            view.Study().GetValueOrDefault(view.StateKey(card.Id)) is not { } s ? "novo" : s.Due <= Today ? "para hoje" : "volta em " + s.Due.ToString("dd/MM");
        public async Task NewSetAsync() => await view.NewDeckAsync();
        public Task SetMenuAsync(StudySet set) => view.DeckMenuAsync(set);
        public Task CardMenuAsync(StudySet set, Flashcard card) => view.CardMenuAsync(set, card);
        public void OpenNote(string path) { view.CloseFlashcards(); view.OpenNoteByPath(path); }
        public void Close() => view.CloseFlashcards();
    }

    private async Task DeckMenuAsync(StudySet set)
    {
        if (set.DeckId is not { } deckId) return;
        var pick = await Dialogs.ChooseAsync(set.Title,
            [new DialogItem("Adicionar cartão", UrbeTheme.Icons.Plus), new DialogItem("Renomear", UrbeTheme.Icons.Pencil),
             new DialogItem("Excluir conjunto", UrbeTheme.Icons.Trash, "Os cartões e o progresso deles somem.", Danger: true)]);
        if (pick == 0) await AddCardManuallyAsync(deckId);
        else if (pick == 1 && await Dialogs.PromptAsync(new PromptOptions("Renomear conjunto", "Título", set.Title)) is { Length: > 0 } t)
            await Guard(() => ChangeFlashcardsAsync(l => l.Rename(deckId, t, DateTime.UtcNow)));
        else if (pick == 2 && await Dialogs.ConfirmAsync("Excluir \"" + set.Title + "\"?", set.Cards.Count + " cartões serão apagados.", "Excluir", danger: true))
            await Guard(() => ChangeFlashcardsAsync(l => l.Delete(deckId)));
    }

    private async Task CardMenuAsync(StudySet set, Flashcard card)
    {
        if (set.DeckId is not { } deckId) return;
        var choice = await Dialogs.ChooseAsync(card.Front, [new DialogItem("Editar", UrbeTheme.Icons.Pencil, "Mudar só a resposta mantém o progresso."), new DialogItem("Excluir cartão", UrbeTheme.Icons.Trash, Danger: true)], card.Back);
        if (choice == 0 && await AskCardAsync((card.Front, card.Back)) is { } edited)
            await Guard(() => ChangeFlashcardsAsync(l => l.EditCard(deckId, card.Id, edited.Front, edited.Back, DateTime.UtcNow)));
        else if (choice == 1 && await Dialogs.ConfirmAsync("Excluir o cartão?", card.Front, "Excluir", danger: true))
            await Guard(() => ChangeFlashcardsAsync(l => l.RemoveCard(deckId, card.Id, DateTime.UtcNow)));
    }

    private async Task Guard(Func<Task> act)
    {
        try { await act(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        { await Dialogs.AlertAsync("Flashcards", e.Message); }
    }

    private async Task<FlashcardDeck?> NewDeckAsync()
    {
        if (await Dialogs.PromptAsync(new PromptOptions("Novo conjunto", "Título", Placeholder: "Ex.: Animais")) is not { Length: > 0 } title) return null;
        if (await AskCardAsync(null) is not { } card) return null;
        FlashcardDeck? made = null;
        await Guard(async () => made = await ChangeFlashcardsAsync(l => l.Create(title, [card], "manual", null, DateTime.UtcNow)));
        if (made is not null && await Dialogs.ConfirmAsync("Conjunto criado", "Adicionar outro cartão?", "Adicionar outro", cancel: "Pronto")) await AddCardManuallyAsync(made.Id);
        return made;
    }

    private async Task AddCardManuallyAsync(string deckId)
    {
        while (await AskCardAsync(null) is { } card)
        {
            await Guard(() => ChangeFlashcardsAsync(l => l.Add(deckId, [card], DateTime.UtcNow)));
            if (!await Dialogs.ConfirmAsync("Cartão adicionado", "Adicionar outro?", "Adicionar outro", cancel: "Pronto")) return;
        }
    }

    private async Task<(string Front, string Back)?> AskCardAsync((string Front, string Back)? current)
    {
        if (await Dialogs.PromptAsync(new PromptOptions("Frente do cartão", "Pergunta ou termo", current?.Front ?? "", Confirm: "Próximo")) is not { Length: > 0 } front) return null;
        if (await Dialogs.PromptAsync(new PromptOptions("Verso do cartão", front, current?.Back ?? "", Confirm: "Salvar")) is not { Length: > 0 } back) return null;
        return (front, back);
    }
}
