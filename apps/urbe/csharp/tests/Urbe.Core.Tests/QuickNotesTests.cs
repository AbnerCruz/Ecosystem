using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-56: quick notes, one note per month with a section per day (observation of 22/10/2026).</summary>
public sealed class QuickNotesTests
{
    private static readonly DateTime At = new(2026, 10, 16, 9, 5, 0);

    [Fact]
    public void ANewMonthStartsWithTheHeaderTheDayAndTheEntry()
    {
        var made = QuickNotes.Append(null, "  comprar pão  ", At)!.Value;
        Assert.Equal("Notas rápidas/2026-10.md", made.Path);
        Assert.Equal("# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- 09:05 — comprar pão\n", made.Content);
        Assert.Equal(made, QuickNotes.Append("  \n", "comprar pão", At));
    }

    [Fact]
    public void TheSameDayGetsTheLineAppended()
    {
        var first = "# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- 09:05 — comprar pão";
        var made = QuickNotes.Append(first, "ligar\npara   a Ana", At.AddHours(13).AddMinutes(30))!.Value;
        Assert.Equal(first + "\n- 22:35 — ligar para a Ana\n", made.Content);
        var again = QuickNotes.Append(made.Content, "ok", At)!.Value;
        Assert.Equal(made.Content + "- 09:05 — ok\n", again.Content);
    }

    [Fact]
    public void ANewDayGetsItsSectionAtTheEnd()
    {
        var month = QuickNotes.Append(null, "comprar pão", At)!.Value.Content;
        var made = QuickNotes.Append(month, "ideia nova", new DateTime(2026, 10, 22, 8, 45, 0))!.Value;
        Assert.Equal("Notas rápidas/2026-10.md", made.Path);
        Assert.Equal(month + "\n## 22/10 — quinta-feira\n\n- 08:45 — ideia nova\n", made.Content);
    }

    [Fact]
    public void ADayWhoseSectionIsNotTheLastGetsTheEntryInsideIt()
    {
        var note = "# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- 09:05 — comprar pão\n\n## Ideias soltas\n\n- texto meu\n";
        var made = QuickNotes.Append(note, "ok", At.AddHours(1))!.Value;
        Assert.Equal("# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- 09:05 — comprar pão\n- 10:05 — ok\n\n## Ideias soltas\n\n- texto meu\n", made.Content);
    }

    [Fact]
    public void EmptyTextIsNothingAndThePathFollowsTheLocalMonth()
    {
        Assert.Null(QuickNotes.Append("x", "  \n\t ", At));
        Assert.Null(QuickNotes.Line(null, At));
        Assert.Equal("Notas rápidas/2026-01.md", QuickNotes.PathFor(new DateTime(2026, 1, 31, 23, 59, 0)));
        Assert.Equal("Notas rápidas/2026-02.md", QuickNotes.PathFor(new DateTime(2026, 2, 1, 0, 0, 0)));
    }
}
