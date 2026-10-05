using System;
using System.Threading;
using Ecosystem.TextInspection;

namespace Ecosystem.TextInspection.Tests;

public class TextInspectorTests
{
    [Theory]
    [InlineData("", 0, 0, 0)]
    [InlineData("abc def", 7, 2, 1)]
    [InlineData("Olá", 3, 1, 1)]
    [InlineData("🙂🙂", 2, 1, 1)]
    [InlineData("  um\tdois  ", 11, 2, 1)]
    [InlineData("a\nb\n", 4, 2, 3)]
    public void CountsContractSemantics(string text, int characters, int words, int lines)
    {
        var result = TextInspector.Inspect(text);
        Assert.Equal(characters, result.Characters);
        Assert.Equal(words, result.Words);
        Assert.Equal(lines, result.Lines);
    }

    [Fact]
    public void MaximumLengthIsAcceptedAndMeasuredInUtf16Units()
    {
        var text = new string('x', TextInspector.MaximumLength);
        var result = TextInspector.Inspect(text);
        Assert.Equal(TextInspector.MaximumLength, result.Characters);
    }

    [Fact]
    public void AboveMaximumLengthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TextInspector.Inspect(new string('x', TextInspector.MaximumLength + 1)));
    }

    [Fact]
    public void CancellationIsObserved()
    {
        Assert.Throws<OperationCanceledException>(() =>
            TextInspector.Inspect("texto", new CancellationToken(canceled: true)));
    }
}
