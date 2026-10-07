using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MathDomainAcceptanceTests
{
    [Fact]
    public void EditingCompatibilityMacrosAndRendererComposeWithoutUi()
    {
        const string markdown =
            "Antes $x^2$ depois\n\n$$\\frac{a}{b}$$";

        var formulas = MathEditing.Scan(markdown);
        Assert.Equal(2, formulas.Count);
        Assert.Equal("x^2", formulas[0].Tex);
        Assert.Equal(@"\frac{a}{b}", formulas[1].Tex);
        Assert.Contains(
            MathEditing.Complete(@"\sq", 8),
            command => command.Cmd == @"\sqrt");

        var compatibility = MathCompatibility.Analyze(@"x + \boxed{4}");
        var unsupported = Assert.Single(compatibility);
        Assert.Equal(MathCompatibilityCodes.UnsupportedCommand, unsupported.Code);
        Assert.Equal(@"\boxed", unsupported.Command);
        Assert.True(unsupported.BlocksRendering);

        const string source = @"\RR + \sq{x+1}";
        var macros = new Dictionary<string, string>
        {
            [@"\RR"] = @"\mathbb{R}",
            [@"\sq"] = @"\left(#1\right)^2"
        };
        var expansion = MathMacros.Expand(source, macros);
        Assert.True(expansion.Success, expansion.Diagnostic);
        Assert.Equal(source, expansion.Source);
        Assert.Equal(
            @"\mathbb{R} + \left(x+1\right)^2",
            expansion.Expanded);

        var rendered = MathRenderer.RenderSvg(source, macros: macros);
        Assert.True(rendered.Success, rendered.Diagnostic);
        Assert.Equal(source, rendered.Tex);
        Assert.Empty(rendered.CompatibilityDiagnostics);
        AssertSafeGeometry(rendered);
    }

    [Fact]
    public void RealTutorialCorpusMeetsDomainAcceptanceFloorWithStableGeometry()
    {
        using var observations = LoadObservations();
        var corpus = observations.RootElement.GetProperty("realCorpus");
        var expected = observations.RootElement.GetProperty("realCorpusCounts");
        var expectedRendered = expected.GetProperty("rendered").GetInt32();
        var expectedDiagnostics = expected.GetProperty("diagnostic").GetInt32();

        var rendered = 0;
        var diagnostics = 0;
        var compatibilityDiagnostics = 0;

        foreach (var item in corpus.EnumerateArray())
        {
            var tex = item.GetProperty("tex").GetString()!;
            var first = MathRenderer.RenderSvg(tex);
            var second = MathRenderer.RenderSvg(tex);

            Assert.Equal(tex, first.Tex);
            Assert.Equal(first.Success, second.Success);
            Assert.Equal(first.Diagnostic, second.Diagnostic);
            Assert.Equal(first.Width, second.Width);
            Assert.Equal(first.Height, second.Height);
            Assert.Equal(first.Svg, second.Svg);
            Assert.Equal(
                first.CompatibilityDiagnostics,
                second.CompatibilityDiagnostics);

            if (first.Success)
            {
                rendered++;
                Assert.Empty(first.CompatibilityDiagnostics);
                AssertSafeGeometry(first);
            }
            else
            {
                diagnostics++;
                compatibilityDiagnostics += first.CompatibilityDiagnostics.Count;
                Assert.False(string.IsNullOrWhiteSpace(first.Diagnostic));
                Assert.Empty(first.Svg);
            }
        }

        Assert.Equal(expectedRendered, rendered);
        Assert.Equal(expectedDiagnostics, diagnostics);
        Assert.Equal(30, rendered);
        Assert.Equal(1, diagnostics);
        Assert.Equal(1, compatibilityDiagnostics);
    }

    [Fact]
    public void DomainAcceptanceRejectsDynamicMacrosAndUnsafeSvgFeatures()
    {
        const string dynamicMacro = @"\newcommand{\foo}{x^2}\foo";
        var dynamicResult = MathRenderer.RenderSvg(dynamicMacro);

        Assert.False(dynamicResult.Success);
        var diagnostic = Assert.Single(dynamicResult.CompatibilityDiagnostics);
        Assert.Equal(MathCompatibilityCodes.MacroDeclaration, diagnostic.Code);
        Assert.Equal(dynamicMacro, dynamicResult.Tex);

        var safe = MathRenderer.RenderSvg(
            @"\sum_{i=1}^{n} i = \frac{n(n+1)}{2}");
        Assert.True(safe.Success, safe.Diagnostic);
        AssertSafeGeometry(safe);
        Assert.DoesNotContain("<script", safe.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<text", safe.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", safe.Svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "http://",
            safe.Svg.Replace(
                "http://www.w3.org/2000/svg",
                string.Empty,
                StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSafeGeometry(MathRenderResult result)
    {
        Assert.True(float.IsFinite(result.Width) && result.Width > 0);
        Assert.True(float.IsFinite(result.Height) && result.Height > 0);
        Assert.Contains("<path ", result.Svg, StringComparison.Ordinal);

        var match = Regex.Match(
            result.Svg,
            @"viewBox='0 0 ([0-9.]+) ([0-9.]+)'",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, result.Svg[..Math.Min(result.Svg.Length, 180)]);

        var svgWidth = float.Parse(
            match.Groups[1].Value,
            CultureInfo.InvariantCulture);
        var svgHeight = float.Parse(
            match.Groups[2].Value,
            CultureInfo.InvariantCulture);

        Assert.InRange(Math.Abs(svgWidth - result.Width), 0, 0.02f);
        Assert.InRange(Math.Abs(svgHeight - result.Height), 0, 0.02f);
    }

    private static JsonDocument LoadObservations()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "Urbe.Portable.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return JsonDocument.Parse(
            File.ReadAllBytes(
                Path.Combine(
                    dir!.FullName,
                    "..",
                    "docs",
                    "csharp",
                    "math-renderer-observations.json")));
    }
}
