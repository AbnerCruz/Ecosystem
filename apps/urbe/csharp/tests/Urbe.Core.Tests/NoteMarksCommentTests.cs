using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class NoteMarksCommentTests
{
    [Fact]
    public void ACommentIsRewrittenOrRemovedAndTheRestStays()
    {
        const string text = "A palavra{\"um\"}{\"dois \\\"aspas\\\"\"} e `codigo{\"x\"}` fim.";
        var group = NoteMarks.CommentGroups(text).Single();
        Assert.Equal(["um", "dois \"aspas\""], group.Comments);
        Assert.Equal("A palavra{\"um\"}{\"novo\"} e `codigo{\"x\"}` fim.", NoteMarks.ChangeComment(text, group, 1, "novo"));
        Assert.Equal("A palavra{\"dois \\\"aspas\\\"\"} e `codigo{\"x\"}` fim.", NoteMarks.ChangeComment(text, group, 0, null));
        var none = NoteMarks.ChangeComment(NoteMarks.ChangeComment(text, group, 1, " "), NoteMarks.CommentGroups(NoteMarks.ChangeComment(text, group, 1, " ")).Single(), 0, null);
        Assert.Equal("A palavra e `codigo{\"x\"}` fim.", none);
        Assert.Equal(text, NoteMarks.ChangeComment(text, group, 5, "fora"));
    }
}
