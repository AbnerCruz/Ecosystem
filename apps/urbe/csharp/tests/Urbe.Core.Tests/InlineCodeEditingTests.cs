using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class InlineCodeEditingTests
{
    [Theory]
    [InlineData("", 0, "``", 1)]
    [InlineData("texto", 0, "``texto", 1)]
    [InlineData("a b", 2, "a ``b", 3)]
    public void NoSelectionInsertsEmptyPairWithCaretInside(
        string source, int caret, string expected, int expectedCaret)
    {
        var edit = InlineCodeEditing.Toggle(source, caret, caret);
        Assert.NotNull(edit);
        Assert.Equal(expected, edit.Markdown);
        Assert.Equal(expectedCaret, edit.SelectionStart);
        Assert.Equal(expectedCaret, edit.SelectionEnd);
    }

    [Fact]
    public void SelectingTextWrapsOnlySelectionEvenInMiddleOfParagraph()
    {
        var edit = InlineCodeEditing.Toggle("a código b", 2, 8);
        Assert.NotNull(edit);
        Assert.Equal("a `código` b", edit.Markdown);
        Assert.Equal(3, edit.SelectionStart);
        Assert.Equal(9, edit.SelectionEnd);
    }

    [Fact]
    public void RepeatedClickTogglesExistingWrappedSelectionWithoutRewritingNeighbors()
    {
        const string source = "início código fim";
        var wrapped = InlineCodeEditing.Toggle(source, 7, 13);
        Assert.NotNull(wrapped);
        var unwrapped = InlineCodeEditing.Toggle(
            wrapped.Markdown, wrapped.SelectionStart, wrapped.SelectionEnd);
        Assert.NotNull(unwrapped);
        Assert.Equal(source, unwrapped.Markdown);
        Assert.Equal(7, unwrapped.SelectionStart);
        Assert.Equal(13, unwrapped.SelectionEnd);
    }

    [Fact]
    public void BackticksInSelectionChooseSafeDelimiterAndToggleBack()
    {
        const string source = "um `a` depois";
        var wrapped = InlineCodeEditing.Toggle(source, 3, 6);
        Assert.NotNull(wrapped);
        Assert.Equal("um `` `a` `` depois", wrapped.Markdown);

        var unwrapped = InlineCodeEditing.Toggle(
            wrapped.Markdown, wrapped.SelectionStart, wrapped.SelectionEnd);
        Assert.NotNull(unwrapped);
        Assert.Equal(source, unwrapped.Markdown);
    }

    [Fact]
    public void EmptyPairTogglesOffWithoutPlaceholderText()
    {
        var created = InlineCodeEditing.Toggle("ab", 1, 1);
        Assert.NotNull(created);
        var undone = InlineCodeEditing.Toggle(created.Markdown, 2, 2);
        Assert.NotNull(undone);
        Assert.Equal("ab", undone.Markdown);
        Assert.Equal(1, undone.SelectionStart);
    }

    [Fact]
    public void Utf16OffsetsKeepSurrogatePairsIntact()
    {
        const string source = "a😀b";
        var edit = InlineCodeEditing.Toggle(source, 1, 3);
        Assert.NotNull(edit);
        Assert.Equal("a`😀`b", edit.Markdown);
        Assert.Equal(2, edit.SelectionStart);
        Assert.Equal(4, edit.SelectionEnd);
        Assert.Null(InlineCodeEditing.Toggle(source, 2, 2));
    }

    [Theory]
    [InlineData("a\nb", 0, 3)]
    [InlineData("a\r\nb", 0, 4)]
    public void MultilineSelectionDoesNotBecomeInlineCode(string source, int from, int to)
    {
        Assert.Null(InlineCodeEditing.Toggle(source, from, to));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    [InlineData(0, 6)]
    public void InvalidSelectionDoesNotChangeSource(int start, int end)
    {
        Assert.Null(InlineCodeEditing.Toggle("a", start, end));
    }

    [Fact]
    public void AdjacentTextAndWhitespaceRemainByteForByteUntouched()
    {
        const string source = "x  A \r\nB";
        var edit = InlineCodeEditing.Toggle(source, 3, 4);
        Assert.NotNull(edit);
        Assert.Equal("x  `A` \r\nB", edit.Markdown);
    }
}
