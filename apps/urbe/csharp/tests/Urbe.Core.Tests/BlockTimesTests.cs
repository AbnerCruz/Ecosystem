using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-025: when each block got its current text, read from the note's own history ("Versões anteriores"), like a
/// blame: no new file, nothing to fall out of step. A revision holds the text before a burst of edits and the time
/// the burst began.
/// </summary>
public sealed class BlockTimesTests
{
    private static LegacyHistory.Revision R(long t, string content) => new(t, "N.md", content, false);

    [Fact]
    public void EachBlockTakesTheTimeOfTheEditThatGaveItItsText()
    {
        const string now = "# Título\n\nPrimeiro parágrafo.\n\nSegundo, reescrito.\n\nTerceiro novo.\n";
        // newest first: before the 300 burst the second was old and the third did not exist; before 100 there was only the title
        var history = new[]
        {
            R(300, "# Título\n\nPrimeiro parágrafo.\n\nSegundo.\n"),
            R(200, "# Título\n\nPrimeiro parágrafo.\n"),
            R(100, "# Título\n"),
        };
        var times = BlockTimes.Compute(now, history, createdMs: 50);
        Assert.Equal([50L, 100L, 300L, 300L], times.Select(t => t.Time!.Value).ToArray());
        Assert.Equal(now.IndexOf("Segundo", StringComparison.Ordinal), times[2].Start);
    }

    [Fact]
    public void AMovedBlockKeepsItsTimeAndAnUndoTakesTheTimeOfTheUndo()
    {
        var history = new[]
        {
            R(400, "B\n\nA2\n"),      // A was changed to A2 at 200..; at 400 the change was undone back to A
            R(200, "A\n\nB\n"),
        };
        var times = BlockTimes.Compute("B\n\nA\n", history, createdMs: 10);
        Assert.Equal(10L, times[0].Time);       // B only moved: its text is from the start
        Assert.Equal(400L, times[1].Time);      // A came back with the undo at 400
    }

    [Fact]
    public void SealsDoNotChangeABlocksTimeAndAFullHistoryLeavesOldBlocksUnknown()
    {
        var history = new[] { R(500, "Texto.\n") };
        var sealedNow = BlockTimes.Compute("Texto. {ia:\"m\"}\n", history, createdMs: 1);
        Assert.Equal(1L, sealedNow[0].Time);
        // with as many revisions as the history keeps, an unchanged block may be older than all of them
        var full = Enumerable.Range(0, LegacyHistory.Limit).Select(i => R(1000 - i, "Velho.\n\nv" + i + "\n")).ToArray();
        var times = BlockTimes.Compute("Velho.\n\nnovo\n", full, createdMs: 1);
        Assert.Null(times[0].Time);
        Assert.Equal(1000L, times[1].Time);
    }

    [Fact]
    public void ABlockAsOldAsTheNoteShowsOnlyItsCreationDay()
    {
        var index = new BlockTimes.Index([new LegacyHistory.Revision(900, "N.md", "Velho.\n", false)], createdMs: 100, createdDayOnly: true);
        Assert.Equal(new BlockTimes.Stamp(100, true), index.Next("Velho."));
        Assert.Equal(new BlockTimes.Stamp(900, false), index.Next("Novo."));
        var now = new DateTime(2026, 10, 28, 18, 0, 0);
        Assert.Equal("25/10", BlockTimes.Label(new DateTime(2026, 10, 25), now, dayOnly: true));
        Assert.Equal("hoje", BlockTimes.Label(new DateTime(2026, 10, 28), now, dayOnly: true));
    }

    [Theory]
    [InlineData(0, 0, "14:32")]
    [InlineData(1, 0, "ontem 14:32")]
    [InlineData(3, 0, "25/10 14:32")]
    [InlineData(400, 0, "23/09/2025")]
    public void TheLabelIsShortAndRelative(int daysAgo, int _, string expected)
    {
        var now = new DateTime(2026, 10, 28, 18, 0, 0);
        var at = new DateTime(2026, 10, 28, 14, 32, 0).AddDays(-daysAgo);
        Assert.Equal(expected, BlockTimes.Label(at, now));
    }
}
