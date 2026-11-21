using Urbe.Core.Study;

namespace Urbe.Core.Tests;

public sealed class FlashcardsTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    [Fact]
    public void CardsAreTheLinesWithTwoColonsOutsideCode()
    {
        const string note = "# Bio\n\n- Fotossíntese::Luz vira energia química\nClorofila :: pigmento verde\n\n```\na::b\n```\nTexto sem cartão\n`x`::y";
        var cards = Flashcards.Read("n1", note);
        Assert.Equal(["Fotossíntese", "Clorofila"], cards.Select(c => c.Front));
        Assert.Equal("pigmento verde", cards[1].Back);
        Assert.Equal(3, cards[1].Line);
        // fixing the answer keeps the card's id; another note or another front is another card
        Assert.Equal(cards[0].Id, Flashcards.Read("n1", "Fotossíntese::outra resposta")[0].Id);
        Assert.NotEqual(cards[0].Id, Flashcards.Read("n2", "Fotossíntese::x")[0].Id);
    }

    [Fact]
    public void ReviewsSpaceOutWhatIsKnownAndBringBackWhatWasMissed()
    {
        var s = Flashcards.Review(null, Recall.Bom, Today);
        Assert.Equal((Today.AddDays(1), 1d), (s.Due, s.Interval));
        s = Flashcards.Review(s, Recall.Bom, Today.AddDays(1));
        Assert.Equal(Today.AddDays(1 + 3), s.Due);   // 1 × 2.5 → 3 days
        var easy = Flashcards.Review(null, Recall.Facil, Today);
        Assert.Equal(Today.AddDays(4), easy.Due);
        var missed = Flashcards.Review(s, Recall.Errei, Today.AddDays(4));
        Assert.Equal((Today.AddDays(4), 0d, 1, 2.3), (missed.Due, missed.Interval, missed.Lapses, Math.Round(missed.Ease, 2)));
        var floor = new CardState(Today, 10, 1.3);
        Assert.Equal(1.3, Flashcards.Review(floor, Recall.Errei, Today).Ease);
    }

    [Fact]
    public void DueCardsAndTheStudentsFileRoundTrip()
    {
        var cards = Flashcards.Read("n", "a::1\nb::2\nc::3");
        var states = new Dictionary<string, CardState> { [cards[0].Id] = new(Today.AddDays(2), 2), [cards[1].Id] = new(Today, 1) };
        Assert.Equal(["b", "c"], Flashcards.Due(cards, states, Today).Select(c => c.Front));
        var back = Flashcards.Parse(Flashcards.Serialize(states));
        Assert.Equal(states[cards[0].Id], back[cards[0].Id]);
        Assert.Empty(Flashcards.Parse("{lixo"));
    }
}
