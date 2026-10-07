using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MathRendererTests
{
    [Theory]
    [InlineData(@"E=mc^2", false)]
    [InlineData(@"x = \frac{-b \pm \sqrt{b^2-4ac}}{2a}", true)]
    [InlineData(@"\int_0^1 x^2\,dx=\frac{1}{3}", true)]
    [InlineData(@"\begin{pmatrix}a&b\\c&d\end{pmatrix}", true)]
    public void SupportedTexRendersToSelfContainedPathSvg(string tex, bool display)
    {
        var result = MathRenderer.RenderSvg(tex, display);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Null(result.Diagnostic);
        Assert.Empty(result.CompatibilityDiagnostics);
        Assert.StartsWith("<svg ", result.Svg, StringComparison.Ordinal);
        Assert.Contains("<path ", result.Svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<text", result.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", result.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", result.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", result.Svg.Replace(
            "http://www.w3.org/2000/svg",
            string.Empty,
            StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Theory]
    [InlineData(@"\doesnotexist")]
    [InlineData(@"\frac{")]
    [InlineData(@"\boxed{x=4}")]
    public void UnsupportedOrMalformedTexIsDiagnosticAndNeverThrows(string tex)
    {
        var result = MathRenderer.RenderSvg(tex);

        Assert.False(result.Success);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Svg);
        Assert.Equal(tex, result.Tex);
    }

    [Fact]
    public void KnownUnsupportedCommandReturnsStructuredCompatibilityDiagnostic()
    {
        const string tex = @"x + \boxed{4}";

        var result = MathRenderer.RenderSvg(tex);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.CompatibilityDiagnostics);
        Assert.Equal(MathCompatibilityCodes.UnsupportedCommand, diagnostic.Code);
        Assert.Equal("\\boxed", diagnostic.Command);
        Assert.Equal(tex.IndexOf(@"\boxed", StringComparison.Ordinal), diagnostic.Start);
        Assert.Equal(diagnostic.Command.Length, diagnostic.Length);
        Assert.True(diagnostic.BlocksRendering);
        Assert.Contains("\\boxed", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(tex, result.Tex);
    }

    [Fact]
    public void MacroDeclarationIsBlockedBeforeRendererWithoutMutatingTex()
    {
        const string tex = @"\newcommand{\foo}{x^2}\foo";

        var result = MathRenderer.RenderSvg(tex);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.CompatibilityDiagnostics);
        Assert.Equal(MathCompatibilityCodes.MacroDeclaration, diagnostic.Code);
        Assert.Equal("\\newcommand", diagnostic.Command);
        Assert.Equal(0, diagnostic.Start);
        Assert.True(diagnostic.BlocksRendering);
        Assert.Contains("macro", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(tex, result.Tex);
    }

    [Fact]
    public void ParserOnlyFailureDoesNotInventCompatibilityDiagnostic()
    {
        const string tex = @"\doesnotexist";

        var result = MathRenderer.RenderSvg(tex);

        Assert.False(result.Success);
        Assert.Empty(result.CompatibilityDiagnostics);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(tex, result.Tex);
    }

    [Fact]
    public void ConfiguredStringMacrosRenderWithoutMutatingSourceTex()
    {
        const string tex = @"\RR + \sq{x+1}";
        var macros = new Dictionary<string, string>
        {
            [@"\RR"] = @"\mathbb{R}",
            [@"\sq"] = @"\left(#1\right)^2"
        };

        var result = MathRenderer.RenderSvg(tex, macros: macros);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(tex, result.Tex);
        Assert.Empty(result.CompatibilityDiagnostics);
        Assert.Contains("<path ", result.Svg, StringComparison.Ordinal);
        Assert.Contains(
            "aria-label='" + tex + "'",
            result.Svg,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MacroExpansionFailureIsDiagnosticAndPreservesSource()
    {
        const string tex = @"\a";
        var macros = new Dictionary<string, string>
        {
            [@"\a"] = @"\b",
            [@"\b"] = @"\a"
        };

        var result = MathRenderer.RenderSvg(tex, macros: macros);

        Assert.False(result.Success);
        Assert.Equal(tex, result.Tex);
        Assert.Empty(result.Svg);
        Assert.Contains("macros", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ciclo", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MacroExpandingToKnownUnsupportedCommandFailsExplicitly()
    {
        const string tex = @"\answer{4}";
        var macros = new Dictionary<string, string>
        {
            [@"\answer"] = @"\boxed{#1}"
        };

        var result = MathRenderer.RenderSvg(tex, macros: macros);

        Assert.False(result.Success);
        Assert.Equal(tex, result.Tex);
        Assert.Empty(result.Svg);
        Assert.Contains("Após expandir macros", result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("\\boxed", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void LimitsProtectHostsWithoutMutatingTex()
    {
        var huge = new string('x', MathRenderer.MaxTexLength + 1);
        var hugeResult = MathRenderer.RenderSvg(huge);
        Assert.False(hugeResult.Success);
        Assert.Equal(huge, hugeResult.Tex);
        Assert.Empty(hugeResult.CompatibilityDiagnostics);
        Assert.Contains("limite", hugeResult.Diagnostic, StringComparison.OrdinalIgnoreCase);

        foreach (var size in new[] { float.NaN, float.PositiveInfinity, 5f, 257f })
        {
            var result = MathRenderer.RenderSvg("x", fontSize: size);
            Assert.False(result.Success);
            Assert.Empty(result.CompatibilityDiagnostics);
            Assert.Contains("fonte", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RealTutorialCorpusHasNoExceptionsAndKeepsCurrentThirtyOfThirtyOneFloor()
    {
        using var observations = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CSharpRoot(), "..", "docs", "csharp",
                "math-renderer-observations.json")));

        var corpus = observations.RootElement.GetProperty("realCorpus");
        var rendered = 0;
        var diagnostics = 0;
        var compatibilityDiagnostics = 0;

        foreach (var item in corpus.EnumerateArray())
        {
            var tex = item.GetProperty("tex").GetString()!;
            var result = MathRenderer.RenderSvg(tex);
            Assert.Equal(tex, result.Tex);
            if (result.Success)
            {
                rendered++;
                Assert.Empty(result.CompatibilityDiagnostics);
                Assert.Contains("<path ", result.Svg, StringComparison.Ordinal);
            }
            else
            {
                diagnostics++;
                compatibilityDiagnostics += result.CompatibilityDiagnostics.Count;
                Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
            }
        }

        Assert.True(rendered >= 30, "Rendered: " + rendered);
        Assert.True(diagnostics <= 1, "Diagnostics: " + diagnostics);
        Assert.Equal(1, compatibilityDiagnostics);
    }

    [Fact]
    public void RenderingIsDeterministicForTheSameInput()
    {
        const string tex = @"\sum_{i=1}^{n}i=\frac{n(n+1)}{2}";
        var first = MathRenderer.RenderSvg(tex);
        var second = MathRenderer.RenderSvg(tex);

        Assert.True(first.Success, first.Diagnostic);
        Assert.Equal(first.Svg, second.Svg);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
        Assert.Equal(first.CompatibilityDiagnostics, second.CompatibilityDiagnostics);
    }

    private static string CSharpRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "Urbe.Portable.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
