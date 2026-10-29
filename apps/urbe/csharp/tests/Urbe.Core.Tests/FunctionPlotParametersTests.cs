using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-069, second part (FeaturesCheck: "fenômenos de física definidos por equações… previews em tempo real
/// configurados pelo usuário"): a graph's parameters (<c>a = 2 em 0..5</c>, a slider in the editor, as Desmos does)
/// and parametric curves (<c>(cos(t), sin(t))</c>). Still text, still a closed evaluator.
/// </summary>
public sealed class FunctionPlotParametersTests
{
    private static double At(FunctionPlot plot, int curve, double x) =>
        plot.Sample(400).Curves[curve].Segments.SelectMany(s => s).MinBy(p => Math.Abs(p.X - x))!.Y;

    [Fact]
    public void AParameterIsANumberWithARangeAndCurvesFollowIt()
    {
        var plot = FunctionPlot.Parse("y = a*x\na = 2 em 0..5\n");
        Assert.Empty(plot.Errors);
        var a = Assert.Single(plot.Parameters);
        Assert.Equal(("a", 2.0, 0.0, 5.0), (a.Name, a.Value, a.Min, a.Max));
        Assert.Single(plot.Functions);   // the parameter is not a curve
        Assert.Equal(2, At(plot, 0, 1), 6);
        a.Value = 3.5;                   // the slider: no new reading of the text
        Assert.Equal(3.5, At(plot, 0, 1), 6);
    }

    [Theory]
    [InlineData("k = 0", -10, 10)]
    [InlineData("k = 4", -10, 10)]
    [InlineData("k = 40", -80, 80)]
    public void WithoutARangeTheSliderGoesAroundTheValue(string line, double min, double max)
    {
        var k = Assert.Single(FunctionPlot.Parse(line + "\ny = k").Parameters);
        Assert.Equal((min, max), (k.Min, k.Max));
    }

    [Theory]
    [InlineData("y = ax", 3, 6)]            // a·x, as Desmos reads it
    [InlineData("y = 2ax + b", 3, 13)]
    [InlineData("y = a sin(x) + b", 0, 1)]
    [InlineData("y = ab x", 1, 2)]
    public void ParametersMultiplyWithoutAStar(string function, double x, double expected)
    {
        var plot = FunctionPlot.Parse(function + "\na = 2\nb = 1\n");
        Assert.Empty(plot.Errors);
        Assert.Equal(expected, At(plot, 0, x), 6);
    }

    [Theory]
    [InlineData("a = x", "número")]
    [InlineData("a = 2 em 5..1", "intervalo")]
    [InlineData("a = 9 em 0..5", "fora")]
    public void AWrongParameterSaysWhy(string line, string part)
    {
        var plot = FunctionPlot.Parse("y = x\n" + line);
        Assert.Contains(plot.Errors, e => e.Contains(part, StringComparison.Ordinal));
    }

    [Fact]
    public void AParametricCurveGoesOverT()
    {
        var plot = FunctionPlot.Parse("(cos(t), sin(t))\n");
        Assert.Empty(plot.Errors);
        var curve = Assert.Single(plot.Sample(200).Curves);
        var points = curve.Segments.SelectMany(s => s).ToList();
        Assert.All(points, p => Assert.Equal(1, Math.Sqrt(p.X * p.X + p.Y * p.Y), 6));   // a circle
        Assert.Equal("(cos(t), sin(t))", curve.Label);
        var drawing = plot.Sample(200);
        Assert.InRange(drawing.XMin, -1.3, -1);   // without x: the view fits the curve
        Assert.InRange(drawing.XMax, 1, 1.3);
    }

    [Fact]
    public void AThrowIsAPhysicsCurveWithItsParameters()
    {
        // a projectile: speed and angle as sliders, gravity fixed, t to the landing
        var plot = FunctionPlot.Parse("título: Lançamento\nv = 10 em 0..30\nθ = pi/4\ng = 9,8\n(v cos(θ) t, v sin(θ) t - g t^2/2)\nt: 0..1,443\ny: 0..4\n");
        Assert.Empty(plot.Errors);
        Assert.Equal(["v", "θ", "g"], plot.Parameters.Select(p => p.Name));
        var points = plot.Sample(300).Curves[0].Segments.SelectMany(s => s).ToList();
        Assert.Equal(0, points[0].X, 9);
        Assert.InRange(points[^1].Y, -0.05, 0.05);                 // back on the ground
        Assert.InRange(points[^1].X, 10.15, 10.25);                // v²/g for 45°
        Assert.InRange(points.Max(p => p.Y), 2.5, 2.6);            // v²/4g
    }

    [Theory]
    [InlineData("y = a*x\na = 2 em 0..5\n", "a", 3.5, "y = a*x\na = 3.5 em 0..5\n")]
    [InlineData("k = 0,5\ny = k*x", "k", 0.75, "k = 0,75\ny = k*x")]   // the note's decimal comma is kept
    [InlineData("  a=1  \ny = a", "a", 2, "  a=2  \ny = a")]            // and its spacing
    public void TheSliderWritesItsValueBackIntoTheText(string text, string name, double value, string expected) =>
        Assert.Equal(expected, FunctionPlot.WithValue(text, name, value));

    [Fact]
    public void ParametersMayComeBeforeOrAfterTheirUse()
    {
        Assert.Empty(FunctionPlot.Parse("a = 1\ny = a x").Errors);
        Assert.Empty(FunctionPlot.Parse("y = a x\na = 1").Errors);
    }
}
