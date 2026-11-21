using System.Diagnostics;

namespace Urbe.Core.Tests;

/// <summary>CST-181: a formula already drawn is not typeset again; what draws differently never comes from another's entry.</summary>
public sealed class MathRendererCacheTests
{
    [Fact]
    public void TheSameFormulaComesBackWithoutBeingTypesetAgain()
    {
        var tex = @"\frac{a_" + Guid.NewGuid().ToString("N")[..6] + "}{b}";
        var first = MathRenderer.RenderSvg(tex, true, 18);
        Assert.True(first.Success);
        Assert.Same(first, MathRenderer.RenderSvg(tex, true, 18));
        Assert.NotSame(first, MathRenderer.RenderSvg(tex, false, 18));
        Assert.NotEqual(first.Width, MathRenderer.RenderSvg(tex, true, 36).Width);
        var macro = MathRenderer.RenderSvg(@"\R", true, 18, new Dictionary<string, string> { [@"\R"] = @"\mathbb{R}" });
        var other = MathRenderer.RenderSvg(@"\R", true, 18, new Dictionary<string, string> { [@"\R"] = "x" });
        Assert.NotEqual(macro.Svg, other.Svg);
        Assert.True(MathRenderer.Cache.Bytes <= 16L << 20);
    }

    [Fact]
    public void ABookOfFormulasDrawnAgainIsManyTimesFaster()
    {
        var formulas = Enumerable.Range(0, 40).Select(i => @"\sum_{k=1}^{" + i + @"} \frac{k^2}{\sqrt{k+" + Guid.NewGuid().ToString("N")[..4] + "}}").ToList();
        var clock = Stopwatch.StartNew();
        foreach (var f in formulas) MathRenderer.RenderSvg(f, true, 18);
        var cold = clock.Elapsed;
        clock.Restart();
        foreach (var f in formulas) MathRenderer.RenderSvg(f, true, 18);
        Assert.True(clock.Elapsed * 10 < cold, "frio " + cold.TotalMilliseconds + " ms, de novo " + clock.Elapsed.TotalMilliseconds + " ms");
    }
}
