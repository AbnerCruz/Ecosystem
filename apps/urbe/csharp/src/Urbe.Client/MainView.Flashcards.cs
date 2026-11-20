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

    /// <summary>The library: every set with its cards due today; choosing one opens what can be done with it.</summary>
    public async Task ShowFlashcardsAsync(string? setId)
    {
        while (true)
        {
            var sets = Sets();
            var chosen = setId is null ? null : sets.FirstOrDefault(s => s.Set.Id == setId || s.Set.DeckId == setId).Set;
            setId = null;
            if (chosen is null)
            {
                var items = sets.Select(s => new DialogItem(s.Set.Title, s.Set.Kind == "note" ? UrbeTheme.Icons.File : UrbeTheme.Icons.Book,
                    s.Progress.Cards + (s.Progress.Cards == 1 ? " cartão" : " cartões") + " · " + (s.Progress.Due == 0 ? "nada para hoje" : s.Progress.Due + " para hoje") +
                    (s.Set.Kind == "note" ? " · da nota" : ""))).ToList();
                items.Add(new DialogItem("Novo conjunto", UrbeTheme.Icons.Plus, "Escreva os cartões aqui; nenhuma nota é criada."));
                var pick = await Dialogs.ChooseAsync("Flashcards", items, sets.Count == 0 ? "Nenhum conjunto ainda. Crie um, peça ao Assistente, ou escreva pergunta::resposta numa nota." : null);
                if (pick is not int i) return;
                if (i == sets.Count) { if (await NewDeckAsync() is { } made) setId = "deck:" + made.Id; continue; }
                chosen = sets[i].Set;
            }
            if (!await SetActionsAsync(chosen)) return;
        }
    }

    /// <summary>What can be done with a set; false when the person left the library.</summary>
    private async Task<bool> SetActionsAsync(StudySet set)
    {
        while (true)
        {
            var (current, progress) = Sets().FirstOrDefault(s => s.Set.Id == set.Id);
            if (current is null) return true;
            var actions = new List<(DialogItem Item, Func<Task<bool>> Run)>
            {
                (new DialogItem(progress.Due == 0 ? "Revisar (nada para hoje)" : "Revisar " + progress.Due + (progress.Due == 1 ? " cartão" : " cartões"), UrbeTheme.Icons.Book,
                    progress.Next is { } next && progress.Due == 0 ? "O próximo volta em " + next.ToString("dd/MM") : "Pergunta, resposta e o quanto você lembrou."),
                    async () => { await ReviewSetAsync(current, all: false); return true; }),
                (new DialogItem("Ver os cartões", UrbeTheme.Icons.Notes, progress.Cards + " no conjunto, " + progress.Reviewed + " já revisados"), async () => { await BrowseCardsAsync(current); return true; }),
            };
            if (current.Kind == "deck" && current.DeckId is { } deckId)
            {
                actions.Add((new DialogItem("Adicionar cartão", UrbeTheme.Icons.Plus), async () => { await AddCardManuallyAsync(deckId); return true; }));
                actions.Add((new DialogItem("Renomear", UrbeTheme.Icons.Pencil), async () =>
                {
                    if (await Dialogs.PromptAsync(new PromptOptions("Renomear conjunto", "Título", current.Title)) is { Length: > 0 } t)
                        await Guard(() => ChangeFlashcardsAsync(l => l.Rename(deckId, t, DateTime.UtcNow)));
                    return true;
                }));
                actions.Add((new DialogItem("Excluir conjunto", UrbeTheme.Icons.Trash, "Os cartões e o progresso deles somem.", Danger: true), async () =>
                {
                    if (!await Dialogs.ConfirmAsync("Excluir \"" + current.Title + "\"?", progress.Cards + " cartões serão apagados.", "Excluir", danger: true)) return true;
                    await Guard(() => ChangeFlashcardsAsync(l => l.Delete(deckId)));
                    return false;
                }));
            }
            else if (current.NotePath is { } path)
                actions.Add((new DialogItem("Abrir a nota", UrbeTheme.Icons.File, "Os cartões são as linhas pergunta::resposta dela."), () => { OpenNoteByPath(path); return Task.FromResult(false); }));
            var pick = await Dialogs.ChooseAsync(current.Title, actions.Select(a => a.Item).ToList(), current.Kind == "note" ? "Da nota " + current.NotePath : null);
            if (pick is not int i) return true;
            if (!await actions[i].Run()) return i == actions.Count - 1 && current.Kind == "deck";
        }
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

    private async Task BrowseCardsAsync(StudySet set)
    {
        var states = Study();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = set.Cards.Select(c =>
        {
            var s = states.GetValueOrDefault(StateKey(c.Id));
            return new DialogItem(c.Front, Detail: c.Back + " · " + (s is null ? "novo" : s.Due <= today ? "para hoje" : "volta em " + s.Due.ToString("dd/MM")));
        }).ToList();
        var pick = await Dialogs.ChooseAsync(set.Title, items);
        if (pick is not int i || set.DeckId is not { } deckId) return;
        var card = set.Cards[i];
        var choice = await Dialogs.ChooseAsync(card.Front, [new DialogItem("Editar", UrbeTheme.Icons.Pencil, "Mudar só a resposta mantém o progresso."), new DialogItem("Excluir cartão", UrbeTheme.Icons.Trash, Danger: true)], card.Back);
        if (choice == 0 && await AskCardAsync((card.Front, card.Back)) is { } edited)
            await Guard(() => ChangeFlashcardsAsync(l => l.EditCard(deckId, card.Id, edited.Front, edited.Back, DateTime.UtcNow)));
        else if (choice == 1 && await Dialogs.ConfirmAsync("Excluir o cartão?", card.Front, "Excluir", danger: true))
            await Guard(() => ChangeFlashcardsAsync(l => l.RemoveCard(deckId, card.Id, DateTime.UtcNow)));
    }

    /// <summary>The review of a set: front, then back, then how well it was remembered (SM-2), saved after each card.</summary>
    private async Task ReviewSetAsync(StudySet set, bool all)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var states = Study();
        var due = all ? set.Cards : set.Cards.Where(c => !states.TryGetValue(StateKey(c.Id), out var s) || s.Due <= today).ToList();
        if (due.Count == 0) { await Dialogs.AlertAsync(set.Title, "Nada para revisar hoje neste conjunto."); return; }
        int done = 0, right = 0;
        for (int i = 0; i < due.Count; i++)
        {
            var card = due[i];
            if (await Dialogs.ChooseAsync("Cartão " + (i + 1) + " de " + due.Count, [new DialogItem("Mostrar resposta")], card.Front) is not 0) break;
            var before = Study().GetValueOrDefault(StateKey(card.Id));
            string When(Recall r) { var d = Flashcards.Review(before, r, today).Due.DayNumber - today.DayNumber; return d <= 0 ? "Volta hoje" : d == 1 ? "Volta amanhã" : "Volta em " + d + " dias"; }
            var pick = await Dialogs.ChooseAsync(card.Front,
                [new DialogItem("Errei", Detail: When(Recall.Errei)), new DialogItem("Difícil", Detail: When(Recall.Dificil)),
                 new DialogItem("Bom", Detail: When(Recall.Bom)), new DialogItem("Fácil", Detail: When(Recall.Facil))], card.Back);
            if (pick is not int grade) break;
            Study()[StateKey(card.Id)] = Flashcards.Review(before, (Recall)grade, today);
            SaveStudy();
            done++;
            if (grade > 0) right++;
        }
        if (done > 0) await Dialogs.AlertAsync(set.Title, done + (done == 1 ? " cartão revisado" : " cartões revisados") + ", " + right + " lembrados.");
    }
}
