namespace Urbe.Core.Tests;

/// <summary>UC-80: the lines an edit changed, for the suggestion in the editor.</summary>
public sealed class LineChangeTests
{
    [Theory]
    [InlineData("a\n&J&: nome\nc\n", "a\nAurora\nc\n", 2, 12, "Aurora\n")]
    [InlineData("a\nb\nc", "a\nb\nc\nd", 4, 5, "c\nd")]
    [InlineData("um dois\n", "um três\n", 0, 8, "um três\n")]
    [InlineData("a\nb\n", "a\nx\ny\nb\n", 2, 2, "x\ny\n")]
    [InlineData("a\nb\nc\n", "a\nc\n", 2, 4, "")]
    public void TheChangeCoversWholeLines(string before, string after, int start, int end, string replacement)
    {
        var c = LineChange.Of(before, after)!;
        Assert.Equal((start, end, replacement), (c.Start, c.End, c.Replacement));
        // putting the replacement in place gives the text after
        Assert.Equal(after, before[..c.Start] + c.Replacement + before[c.End..]);
    }

    [Fact]
    public void NoChangeIsNull() => Assert.Null(LineChange.Of("a\n", "a\n"));
}
