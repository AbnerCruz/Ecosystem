using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MathCompatibilityTests
{
    [Fact]
    public void EmptyInputHasNoDiagnostics()
    {
        Assert.Empty(MathCompatibility.Analyze(null));
        Assert.Empty(MathCompatibility.Analyze(string.Empty));
        Assert.Empty(MathCompatibility.Analyze(@"x + \\frac{1}{2}"));
    }

    [Fact]
    public void ReportsObservedUnsupportedCommandsWithUtf16Positions()
    {
        const string tex = "😀 x + \\boxed{y} + \\dfrac{1}{2} + \\cancel{z}";

        var diagnostics = MathCompatibility.Analyze(tex);

        Assert.Equal(3, diagnostics.Count);
        Assert.Equal(new[] { "\\boxed", "\\dfrac", "\\cancel" },
            diagnostics.Select(item => item.Command));
        Assert.All(diagnostics, item =>
        {
            Assert.Equal(MathCompatibilityCodes.UnsupportedCommand, item.Code);
            Assert.True(item.BlocksRendering);
            Assert.Equal(item.Command,
                tex.Substring(item.Start, item.Length));
        });
        Assert.Equal(tex.IndexOf(@"\boxed", StringComparison.Ordinal), diagnostics[0].Start);
        Assert.NotNull(diagnostics[1].Suggestion);
        Assert.Null(diagnostics[0].Suggestion);
    }

    [Fact]
    public void ReportsMacroDeclarationsWithoutExpandingOrGuessingInvocations()
    {
        const string tex = @"\newcommand{\foo}{x^2} + \def\bar#1{#1+1} + \foo + \bar{2}";

        var diagnostics = MathCompatibility.Analyze(tex);

        Assert.Equal(2, diagnostics.Count);
        Assert.Equal(new[] { "\\newcommand", "\\def" },
            diagnostics.Select(item => item.Command));
        Assert.All(diagnostics, item =>
            Assert.Equal(MathCompatibilityCodes.MacroDeclaration, item.Code));
        Assert.DoesNotContain(diagnostics, item => item.Command is "\\foo" or "\\bar");
        Assert.Equal(@"\newcommand{\foo}{x^2} + \def\bar#1{#1+1} + \foo + \bar{2}", tex);
    }

    [Fact]
    public void CommentsAndLinebreakControlSymbolsDoNotCreateFalsePositives()
    {
        const string tex = "x % \\boxed{ignored}\n" +
                           @"y \\boxed literal + \\cancel{real}";

        var diagnostics = MathCompatibility.Analyze(tex);

        var item = Assert.Single(diagnostics);
        Assert.Equal("\\cancel", item.Command);
    }

    [Fact]
    public void CatalogMatchesRendererDiscoveryWithoutSilentAliases()
    {
        var expected = new[]
        {
            "\\dfrac",
            "\\boldsymbol",
            "\\lVert",
            "\\rVert",
            "\\overset",
            "\\underset",
            "\\boxed",
            "\\cancel"
        };

        Assert.Equal(expected.Order(), MathCompatibility.KnownUnsupportedCommands.Keys.Order());
        Assert.DoesNotContain(MathCompatibility.KnownUnsupportedCommands.Values,
            suggestion => suggestion?.Contains("automatic", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void EveryOccurrenceIsReportedInSourceOrder()
    {
        const string tex = @"\boxed{a} + \boxed{b} + \overset{x}{y}";

        var diagnostics = MathCompatibility.Analyze(tex);

        Assert.Equal(3, diagnostics.Count);
        Assert.True(diagnostics[0].Start < diagnostics[1].Start);
        Assert.True(diagnostics[1].Start < diagnostics[2].Start);
        Assert.Equal(new[] { "\\boxed", "\\boxed", "\\overset" },
            diagnostics.Select(item => item.Command));
    }
}
