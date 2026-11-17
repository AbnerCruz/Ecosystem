using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>
/// #956: reviewing a note's flashcards (<c>pergunta::resposta</c>). The front, then the answer, then how well it was
/// remembered (Errei, Difícil, Bom, Fácil) sets when it comes back (SM-2). The student's state is app-private, per vault
/// and card, like the reading positions: apart from the notes, their checklists and the agents' memory.
/// </summary>
public sealed partial class MainView
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
}
