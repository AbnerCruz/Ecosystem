using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-56: the day's quick note.</summary>
public sealed class QuickNotesTests
{
    private static readonly DateTime At = new(2026, 10, 16, 9, 5, 0);

    [Fact]
    public void ANewDayStartsWithTheHeaderAndTheEntry()
    {
        var made = QuickNotes.Append(null, "  comprar pão  ", At)!.Value;
        Assert.Equal("Notas rápidas/2026-10-16.md", made.Path);
        Assert.Equal("# Notas rápidas — 16/10/2026\n\n- 09:05 — comprar pão\n", made.Content);
        Assert.Equal(made, QuickNotes.Append("  \n", "comprar pão", At));
    }

    [Fact]
    public void AnExistingNoteGetsTheLineAppended()
    {
        var first = "# Notas rápidas — 16/10/2026\n\n- 09:05 — comprar pão";
        var made = QuickNotes.Append(first, "ligar\npara   a Ana", At.AddHours(13).AddMinutes(30))!.Value;
        Assert.Equal(first + "\n- 22:35 — ligar para a Ana\n", made.Content);
        var again = QuickNotes.Append(made.Content, "ok", At)!.Value;
        Assert.Equal(made.Content + "- 09:05 — ok\n", again.Content);
    }

    [Fact]
    public void EmptyTextIsNothingAndThePathFollowsTheLocalDay()
    {
        Assert.Null(QuickNotes.Append("x", "  \n\t ", At));
        Assert.Null(QuickNotes.Line(null, At));
        Assert.Equal("Notas rápidas/2026-01-02.md", QuickNotes.PathFor(new DateTime(2026, 1, 2, 23, 59, 0)));
    }
}
