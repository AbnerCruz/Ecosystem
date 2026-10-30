using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-56: quick notes, one note per month with a section per day (observation of 22/10/2026), each entry a checkbox (CST-053).</summary>
public sealed class QuickNotesTests
{
    private static readonly DateTime At = new(2026, 10, 16, 9, 5, 0);

    [Fact]
    public void ANewMonthStartsWithTheHeaderTheDayAndTheEntry()
    {
        var made = QuickNotes.Append(null, "  comprar pão  ", At)!.Value;
        Assert.Equal("Notas rápidas/2026-10.md", made.Path);
        Assert.Equal("# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- [ ] 09:05 — comprar pão\n", made.Content);
        Assert.Equal(made, QuickNotes.Append("  \n", "comprar pão", At));
    }

    [Fact]
    public void TheSameDayGetsTheLineAppended()
    {
        var first = "# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- [ ] 09:05 — comprar pão";
        var made = QuickNotes.Append(first, "ligar\npara   a Ana", At.AddHours(13).AddMinutes(30))!.Value;
        Assert.Equal(first + "\n- [ ] 22:35 — ligar para a Ana\n", made.Content);
        var again = QuickNotes.Append(made.Content, "ok", At)!.Value;
        Assert.Equal(made.Content + "- [ ] 09:05 — ok\n", again.Content);
    }

    [Fact]
    public void ANewDayGetsItsSectionAtTheEnd()
    {
        var month = QuickNotes.Append(null, "comprar pão", At)!.Value.Content;
        var made = QuickNotes.Append(month, "ideia nova", new DateTime(2026, 10, 22, 8, 45, 0))!.Value;
        Assert.Equal("Notas rápidas/2026-10.md", made.Path);
        Assert.Equal(month + "\n## 22/10 — quinta-feira\n\n- [ ] 08:45 — ideia nova\n", made.Content);
    }

    [Fact]
    public void ADayWhoseSectionIsNotTheLastGetsTheEntryInsideIt()
    {
        var note = "# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- [ ] 09:05 — comprar pão\n\n## Ideias soltas\n\n- texto meu\n";
        var made = QuickNotes.Append(note, "ok", At.AddHours(1))!.Value;
        Assert.Equal("# Notas rápidas — outubro de 2026\n\n## 16/10 — sexta-feira\n\n- [ ] 09:05 — comprar pão\n- [ ] 10:05 — ok\n\n## Ideias soltas\n\n- texto meu\n", made.Content);
    }

    [Theory]
    [InlineData("comprar pão", "- [ ] 09:05 — comprar pão")]
    [InlineData("[ ] comprar pão", "- [ ] 09:05 — comprar pão")]
    [InlineData("- [ ] comprar pão", "- [ ] 09:05 — comprar pão")]
    [InlineData("[x] já comprei", "- [x] 09:05 — já comprei")]
    [InlineData("* [X] já comprei", "- [x] 09:05 — já comprei")]
    [InlineData("- lembrar da reunião", "- [ ] 09:05 — lembrar da reunião")]
    public void EveryEntryIsOneCheckboxWhateverTheTextBrings(string text, string line) =>
        Assert.Equal(line, QuickNotes.Line(text, At));

    [Fact]
    public void EmptyTextIsNothingAndThePathFollowsTheLocalMonth()
    {
        Assert.Null(QuickNotes.Append("x", "  \n\t ", At));
        Assert.Null(QuickNotes.Line(null, At));
        Assert.Equal("Notas rápidas/2026-01.md", QuickNotes.PathFor(new DateTime(2026, 1, 31, 23, 59, 0)));
        Assert.Equal("Notas rápidas/2026-02.md", QuickNotes.PathFor(new DateTime(2026, 2, 1, 0, 0, 0)));
    }
    /// <summary>The shape of a real month note (entries before the checkbox, an entry with paragraphs and a seal).</summary>
    private static string RealMonth()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Urbe.Portable.slnx"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests", "fixtures", "quick-notes", "2026-10-estrutura-real.md")).Replace("\r\n", "\n");
    }

    [Fact]
    public void ARealMonthNoteKeepsEverythingItHadAndGetsTheNewEntriesInTheirDay()
    {
        // CST-054: the last day, then a new one; what was there stays byte for byte, old entries keep their form
        var month = RealMonth();
        var sameDay = QuickNotes.Append(month, "revisar a lista", new DateTime(2026, 10, 25, 21, 3, 0))!.Value;
        Assert.Equal("Notas rápidas/2026-10.md", sameDay.Path);
        Assert.StartsWith(month.TrimEnd('\n'), sameDay.Content);
        Assert.EndsWith("projetos\n- [ ] 21:03 — revisar a lista\n", sameDay.Content);

        var newDay = QuickNotes.Append(sameDay.Content, "comprar sementes", new DateTime(2026, 10, 30, 7, 15, 0))!.Value;
        Assert.StartsWith(sameDay.Content, newDay.Content);
        Assert.EndsWith("\n## 30/10 — sexta-feira\n\n- [ ] 07:15 — comprar sementes\n", newDay.Content);

        // a day in the middle of the month (with an entry of paragraphs) gets it after that entry, before the next day
        var middle = QuickNotes.Append(month, "regar", new DateTime(2026, 10, 19, 18, 0, 0))!.Value.Content;
        var at = middle.IndexOf("- [ ] 18:00 — regar", StringComparison.Ordinal);
        Assert.True(at > middle.IndexOf("{ia:\"modelo-x\"}", StringComparison.Ordinal) && at < middle.IndexOf("## 24/10", StringComparison.Ordinal));
        Assert.Equal(month.Length + "- [ ] 18:00 — regar\n".Length, middle.Length);
    }
}
