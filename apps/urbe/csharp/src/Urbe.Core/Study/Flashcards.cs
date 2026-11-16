using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Urbe.Core.Study;

/// <summary>A card of a note: <c>pergunta::resposta</c> on one line (the Obsidian Spaced Repetition syntax).</summary>
public sealed record Flashcard(string Id, string Front, string Back, int Line);

/// <summary>Where a card is in its review: next date, interval (days), ease, reviews and lapses.</summary>
public sealed record CardState(DateOnly Due, double Interval = 0, double Ease = 2.5, int Reviews = 0, int Lapses = 0);

/// <summary>The answer the person gives after seeing the back.</summary>
public enum Recall { Errei, Dificil, Bom, Facil }

/// <summary>
/// #956: flashcards in the notes, reviewed with spaced repetition (SM-2, the scheduler behind Anki). The cards are the
/// note's text; the student's state lives apart (an app-private file, per vault and card), never in the agents' memory nor in the
/// notes' checklists. A card's id comes from its note and its front: fixing the answer keeps its history.
/// </summary>
public static class Flashcards
{
    public static IReadOnlyList<Flashcard> Read(string noteId, string markdown)
    {
        var cards = new List<Flashcard>();
        var lines = markdown.Replace("\r", "").Split('\n');
        char fence = '\0';
        for (int i = 0; i < lines.Length; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal))
            {
                if (fence == '\0') fence = t[0]; else if (t[0] == fence) fence = '\0';
                continue;
            }
            if (fence != '\0') continue;
            var at = lines[i].IndexOf("::", StringComparison.Ordinal);
            if (at <= 0) continue;
            var front = lines[i][..at].Trim().TrimStart('-', '*', ' ');
            var back = lines[i][(at + 2)..].Trim();
            if (front.Length == 0 || back.Length == 0 || front.Contains('`')) continue;
            cards.Add(new(Id(noteId, front), front, back, i));
        }
        return cards;
    }

    public static string Id(string noteId, string front) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(noteId + "\n" + front.Trim().ToLowerInvariant())))[..16].ToLowerInvariant();

    /// <summary>SM-2: the next state after a review on <paramref name="today"/>.</summary>
    public static CardState Review(CardState? state, Recall answer, DateOnly today)
    {
        var s = state ?? new CardState(today);
        if (answer == Recall.Errei)
            return s with { Due = today, Interval = 0, Ease = Math.Max(1.3, s.Ease - .2), Reviews = s.Reviews + 1, Lapses = s.Lapses + 1 };
        double ease = Math.Max(1.3, s.Ease + answer switch { Recall.Dificil => -.15, Recall.Facil => .15, _ => 0 });
        double interval = s.Interval <= 0
            ? answer switch { Recall.Dificil => 1, Recall.Bom => 1, _ => 4 }
            : s.Interval * answer switch { Recall.Dificil => 1.2, Recall.Bom => ease, _ => ease * 1.3 };
        interval = Math.Max(1, Math.Round(interval, MidpointRounding.AwayFromZero));
        return s with { Due = today.AddDays((int)interval), Interval = interval, Ease = ease, Reviews = s.Reviews + 1 };
    }

    /// <summary>The cards to review on <paramref name="today"/>: new ones and those due, in the note's order.</summary>
    public static IReadOnlyList<Flashcard> Due(IReadOnlyList<Flashcard> cards, IReadOnlyDictionary<string, CardState> states, DateOnly today) =>
        cards.Where(c => !states.TryGetValue(c.Id, out var s) || s.Due <= today).ToList();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string Serialize(IReadOnlyDictionary<string, CardState> states) =>
        JsonSerializer.Serialize(new { version = 1, cards = states }, Json);

    public static Dictionary<string, CardState> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("cards", out var cards)
                ? cards.Deserialize<Dictionary<string, CardState>>(Json) ?? [] : [];
        }
        catch (JsonException) { return []; }
    }
}
