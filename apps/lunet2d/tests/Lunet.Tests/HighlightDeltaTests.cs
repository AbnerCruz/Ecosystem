using Lunet.Editor;

namespace Lunet.Tests;

public sealed class HighlightDeltaTests
{
    [Fact]
    public void TypingInTheMiddle_KeepsTokensOnBothSides()
    {
        var before = "int a = 1;\nint b = 2;\nint c = 3;\n";
        var after = "int a = 1;\nint bb = 2;\nint c = 3;\n";
        var old = SyntaxHighlighter.Classify(before);
        var now = SyntaxHighlighter.Classify(after);
        int edit = before.IndexOf("b =", StringComparison.Ordinal) + 1;
        var (prefix, suffix) = HighlightDelta.Compute(old, now, edit, after.Length - (edit + 1), after.Length, after.Length - before.Length);
        Assert.True(prefix >= 3, $"prefix {prefix}");
        Assert.True(suffix >= 3, $"suffix {suffix}");
        Assert.True(prefix + suffix <= Math.Min(old.Count, now.Count));
        for (int i = 0; i < prefix; i++) Assert.Equal(old[i], now[i]);
        for (int i = 0; i < suffix; i++)
        {
            var o = old[old.Count - 1 - i]; var n = now[now.Count - 1 - i];
            Assert.Equal(o.Start + 1, n.Start);
            Assert.Equal(o.Kind, n.Kind);
        }
    }

    [Fact]
    public void OpeningAComment_ReSpansEverythingAfterTheEdit()
    {
        var before = "int a = 1;\nint b = 2;\n";
        var after = "/*int a = 1;\nint b = 2;\n";
        var old = SyntaxHighlighter.Classify(before);
        var now = SyntaxHighlighter.Classify(after);
        var (prefix, suffix) = HighlightDelta.Compute(old, now, 0, after.Length - 2, after.Length, 2);
        Assert.Equal(0, prefix);
        Assert.Equal(0, suffix);
    }

    [Fact]
    public void LargeFile_OneCharEdit_TouchesFewTokens()
    {
        var lines = Enumerable.Range(0, 5000).Select(i => $"var x{i} = {i};");
        var before = string.Join("\n", lines);
        var at = before.IndexOf("x2500 ", StringComparison.Ordinal) + 5;
        var after = before.Insert(at, "9");
        var old = SyntaxHighlighter.Classify(before);
        var now = SyntaxHighlighter.Classify(after);
        var (prefix, suffix) = HighlightDelta.Compute(old, now, at, after.Length - (at + 1), after.Length, 1);
        Assert.True(now.Count - prefix - suffix <= 4, $"middle {now.Count - prefix - suffix}");
    }
}
