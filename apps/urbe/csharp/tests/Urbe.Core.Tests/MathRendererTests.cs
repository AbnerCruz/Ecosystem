using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MathRendererTests
{
    [Theory]
    [InlineData(@"E=mc^2", false)]
    [InlineData(@"rac{-bpmsqrt{b^2-4ac}}{2a}", true)]
    [InlineData(@"int_0^1 x^2,dx=rac{1}{3}", true)]
    [InlineData(@"egin{pmatrix}a&b\c&dend{pmatrix}", true)]
    public void SupportedTexRendersToSelfContainedPathSvg(string tex, bool display)
    {
        var result = MathRenderer.RenderSvg(tex, display);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Null(result.Diagnostic);
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
    [InlineData(@"doesnotexist")]
    [InlineData(@"rac{")]
    [InlineData(@"oxed{x=4}")]
    public void UnsupportedOrMalformedTexIsDiagnosticAndNeverThrows(string tex)
    {
        var result = MathRenderer.RenderSvg(tex);

        Assert.False(result.Success);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Svg);
        Assert.Equal(tex, result.Tex);
    }

    [Fact]
    public void LimitsProtectHostsWithoutMutatingTex()
    {
        var huge = new string('x', MathRenderer.MaxTexLength + 1);
        var hugeResult = MathRenderer.RenderSvg(huge);
        Assert.False(hugeResult.Success);
        Assert.Equal(huge, hugeResult.Tex);
        Assert.Contains("limite", hugeResult.Diagnostic, StringComparison.OrdinalIgnoreCase);

        foreach (var size in new[] { float.NaN, float.PositiveInfinity, 5f, 257f })
        {
            var result = MathRenderer.RenderSvg("x", fontSize: size);
            Assert.False(result.Success);
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

        foreach (var item in corpus.EnumerateArray())
        {
            var tex = item.GetProperty("tex").GetString()!;
            var result = MathRenderer.RenderSvg(tex);
            Assert.Equal(tex, result.Tex);
            if (result.Success)
            {
                rendered++;
                Assert.Contains("<path ", result.Svg, StringComparison.Ordinal);
            }
            else
            {
                diagnostics++;
                Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
            }
        }

        // This is a floor, not a golden for known rejections: future compatibility
        // work may legitimately turn the remaining diagnostic into a rendering.
        Assert.True(rendered >= 30, "Rendered: " + rendered);
        Assert.True(diagnostics <= 1, "Diagnostics: " + diagnostics);
    }

    [Fact]
    public void RenderingIsDeterministicForTheSameInput()
    {
        const string tex = @"sum_{i=1}^{n}i=rac{n(n+1)}{2}";
        var first = MathRenderer.RenderSvg(tex);
        var second = MathRenderer.RenderSvg(tex);

        Assert.True(first.Success, first.Diagnostic);
        Assert.Equal(first.Svg, second.Svg);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
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
