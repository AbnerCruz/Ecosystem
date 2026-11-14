using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class EditHistoryTests
{
    private static void Type(EditHistory h, ref string text, int at, string letters, ref long now)
    {
        foreach (var c in letters) { text = text.Insert(at++, c.ToString()); h.Record(text, now += 100); }
    }

    [Fact]
    public void TypingIsUndoneAWordAtATime()
    {
        var h = new EditHistory();
        var text = "Um.";
        h.Reset(text);
        long now = 0;
        Type(h, ref text, 3, " dois três", ref now);
        Assert.Equal("Um. dois três", text);
        Assert.Equal(("Um. dois ", 9), h.Undo());
        Assert.Equal(("Um.", 3), h.Undo());
        Assert.Null(h.Undo());
        Assert.Equal(("Um. dois ", 9), h.Redo());
        Assert.Equal(("Um. dois três", 13), h.Redo());
        Assert.Null(h.Redo());
    }

    [Fact]
    public void APauseOrAnotherPlaceStartsANewStep()
    {
        var h = new EditHistory();
        var text = "ab";
        h.Reset(text);
        long now = 0;
        Type(h, ref text, 2, "cd", ref now);
        now += EditHistory.GroupPauseMs + 1;
        Type(h, ref text, 4, "ef", ref now);
        Type(h, ref text, 0, "X", ref now);
        Assert.Equal(("abcdef", 0), h.Undo());
        Assert.Equal(("abcd", 4), h.Undo());
        Assert.Equal(("ab", 2), h.Undo());
    }

    [Fact]
    public void AnEditAfterUndoDropsTheRedoAndBackspaceJoinsTheWord()
    {
        var h = new EditHistory();
        var text = "";
        h.Reset(text);
        long now = 0;
        Type(h, ref text, 0, "casa", ref now);
        text = "cas"; h.Record(text, now += 100);
        Type(h, ref text, 3, "o", ref now);
        Assert.Equal(("", 0), h.Undo());
        h.Record("novo", now += 100);
        Assert.False(h.CanRedo);
        Assert.Equal(("", 0), h.Undo());
    }

    [Fact]
    public void AReplacementIsOneStep()
    {
        var h = new EditHistory();
        h.Reset("Um texto longo.");
        h.Record("Um **texto** longo.", 10, group: false);
        Assert.Equal(("Um texto longo.", 8), h.Undo());
        Assert.Equal(("Um **texto** longo.", 12), h.Redo());
    }
}
