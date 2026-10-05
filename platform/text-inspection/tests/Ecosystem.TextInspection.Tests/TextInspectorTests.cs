using Ecosystem.TextInspection;

namespace Ecosystem.TextInspection.Tests;

public class TextInspectorTests
{
    [Theory]
    [InlineData("", 0, 0, 0)]
    [InlineData("Olá mundo\n🙂", 11, 3, 2)]
    [InlineData("  um\tdois\r\ntrês  ", 16, 3, 2)]
    [InlineData("🙂🙂", 2, 1, 1)]
    public void CountsContractSemantics(string text, int characters, int words, int lines)
    {
        var result = TextInspector.Inspect(text);
        Assert.Equal(characters, result.Characters);
        Assert.Equal(words, result.Words);
        Assert.Equal(lines, result.Lines);
    }

    [Fact]
    public void LimitIsMeasuredInUtf16Units()
    {
        Assert.Equal(TextInspector.MaximumLength,
            TextInspector.Inspect(new string('x', TextInspector.MaximumLength)).Characters);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TextInspector.Inspect(new string('x', TextInspector.MaximumLength + 1)));
    }

    [Fact]
    public void CancellationIsObservedBeforeAndDuringEnumeration()
    {
        Assert.Throws<OperationCanceledException>(() =>
            TextInspector.Inspect("texto", new CancellationToken(canceled: true)));

        using var source = new CancellationTokenSource();
        var text = new string('x', 10_000);
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => TextInspector.Inspect(text, source.Token));
    }
}
