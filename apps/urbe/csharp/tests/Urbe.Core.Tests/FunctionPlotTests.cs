using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-069: a ```grafico block draws the functions written in it. The expressions are read by a small, closed
/// evaluator (numbers, x, operators, functions): nothing in a note is ever run as code.
/// </summary>
public sealed class FunctionPlotTests
{
    [Theory]
    [InlineData("2 + 3*4", 0, 14)]
    [InlineData("2^3^2", 0, 512)]          // right-associative power
    [InlineData("-x^2", 3, -9)]            // the power binds tighter than the sign
    [InlineData("2x + 1", 4, 9)]           // implicit product
    [InlineData("3(x - 1)", 2, 3)]
    [InlineData("2sin(pi/2)", 0, 2)]
    [InlineData("sqrt(16) + abs(-2)", 0, 6)]
    [InlineData("ln(e) + log(100)", 0, 3)]
    [InlineData("x * cos(0)", 5, 5)]
    [InlineData("sen(0) + raiz(9)", 0, 3)]  // Portuguese names too
    [InlineData("1,5x", 2, 3)]             // decimal comma
    public void ExpressionsEvaluate(string expression, double x, double expected) =>
        Assert.Equal(expected, FunctionPlot.Expression.Parse(expression).Evaluate(x), 9);

    [Theory]
    [InlineData("2 +", "incompleta")]
    [InlineData("foo(x)", "foo")]
    [InlineData("x ** 2", "**")]
    [InlineData("y", "y")]
    public void AWrongExpressionSaysWhatIsWrong(string expression, string part)
    {
        var error = Assert.Throws<FormatException>(() => FunctionPlot.Expression.Parse(expression));
        Assert.Contains(part, error.Message);
    }

    [Fact]
    public void TheBlockGivesFunctionsRangesAndTitle()
    {
        var plot = FunctionPlot.Parse("título: Ondas\ny = sin(x)\nf(x) = x^2/4\nx: -6..6\ny: -2..4\n");
        Assert.Equal("Ondas", plot.Title);
        Assert.Equal(["sin(x)", "x^2/4"], plot.Functions.Select(f => f.Source).ToArray());
        Assert.Equal((-6, 6), (plot.XMin, plot.XMax));
        Assert.Equal((-2, 4), (plot.YMin!.Value, plot.YMax!.Value));
        Assert.Empty(plot.Errors);
    }

    [Fact]
    public void ALineWithAnErrorIsReportedAndTheOthersStillDraw()
    {
        var plot = FunctionPlot.Parse("y = sin(x)\ny = 2 +\n");
        Assert.Single(plot.Functions);
        Assert.Contains("Linha 2", Assert.Single(plot.Errors));
    }

    [Fact]
    public void SamplingBreaksAtAsymptotesAndWithoutYRangeItFitsTheCurves()
    {
        var plot = FunctionPlot.Parse("y = 1/x\nx: -2..2\ny: -10..10");
        var drawn = plot.Sample(400);
        var curve = Assert.Single(drawn.Curves);
        Assert.True(curve.Segments.Count >= 2, "segments " + curve.Segments.Count);   // no line across x = 0
        Assert.All(curve.Segments.SelectMany(s => s), p => Assert.True(double.IsFinite(p.Y)));
        var fitted = FunctionPlot.Parse("y = x^2\nx: -3..3").Sample(200);
        Assert.True(fitted.YMin <= 0 && fitted.YMax >= 9, fitted.YMin + ".." + fitted.YMax);
    }

    [Fact]
    public void TicksAreRoundNumbers()
    {
        Assert.Equal([-6, -4, -2, 0, 2, 4, 6], FunctionPlot.Ticks(-6, 6, 7));
        Assert.Equal([0, 0.5, 1], FunctionPlot.Ticks(0, 1, 3));
    }

    [Fact]
    public void ThePageVersionIsAnSvgWithTheCurves()
    {
        var svg = FunctionPlot.Parse("y = x\nx: -1..1").ToSvg(320, 200);
        Assert.StartsWith("<svg", svg);
        Assert.Contains("<path", svg);
        Assert.Contains("y = x", svg);   // the legend
        Assert.DoesNotContain("<script", svg);
    }

    [Fact]
    public void ASteepCurveStaysWholeAndTangentBreaksAtEachAsymptote()
    {
        Assert.Single(Assert.Single(FunctionPlot.Parse("y = exp(x)\nx: -2..5").Sample(300).Curves).Segments);
        var tan = Assert.Single(FunctionPlot.Parse("y = tan(x)\nx: -4..4\ny: -5..5").Sample(800).Curves);
        Assert.Equal(3, tan.Segments.Count);   // asymptotes at ±π/2 (±3π/2 ≈ ±4,71 is outside)
    }

    [Fact]
    public void APageShowsTheGraphNotItsCode()
    {
        var html = Urbe.Core.Pages.PageMarkdown.Markdown("Texto.\n\n```grafico\ny = x^2\n```\n");
        Assert.Contains("<svg", html);
        Assert.Contains("y = x^2", html);
        Assert.DoesNotContain("<pre", html);
    }

    [Fact]
    public void TheSlashMenuStartsAGraphWithAnExample()
    {
        var edit = LegacyVisualEdit.Plot("/graf", 5);
        Assert.Equal("```grafico\ny = sin(x)\nx: -6..6\n```", edit.Text);
        Assert.Equal("sin(x)", edit.Text[edit.SelectionStart..edit.SelectionEnd]);
        Assert.Contains(LegacyEditorTools.FilterSlashWithAddons("graf"), i => i.Act == "plot");
    }
}
