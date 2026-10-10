using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MarkdownVisualTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void MarkdownRendererMatchesEveryLegacyGolden()
    {
        using var json = JsonDocument.Parse(
            File.ReadAllBytes(
                Path.Combine(
                    Root,
                    "..",
                    "tests",
                    "fixtures",
                    "markdown-golden.json")));

        var cases = json.RootElement.EnumerateArray().ToArray();
        Assert.True(cases.Length >= 30);

        foreach (var item in cases)
        {
            var input = item.GetProperty("input").GetString() ?? string.Empty;
            var expected = item.GetProperty("html").GetString() ?? string.Empty;
            Assert.Equal(expected, MarkdownEngine.Render(input));
        }
    }

    [Fact]
    public void VisualSerializerMatchesEveryLegacyGolden()
    {
        using var json = JsonDocument.Parse(
            File.ReadAllBytes(
                Path.Combine(
                    Root,
                    "..",
                    "tests",
                    "fixtures",
                    "visual-golden.json")));

        var cases = json.RootElement.EnumerateArray().ToArray();
        Assert.True(cases.Length >= 100);

        foreach (var item in cases)
        {
            var html = item.GetProperty("html").GetString() ?? string.Empty;
            var editor = item.GetProperty("bodyEditor").GetString() ?? string.Empty;
            var expected = item.GetProperty("md").GetString() ?? string.Empty;

            Assert.Equal(
                expected,
                VisualMarkdown.FromHtmlUsingEditorSource(html, editor));
        }
    }

    [Theory]
    [InlineData("javascript:alert(1)", "#")]
    [InlineData("JaVaScRiPt:alert(1)", "#")]
    [InlineData("vbscript:x", "#")]
    [InlineData("file:///etc/passwd", "#")]
    [InlineData("data:text/html;base64,AAAA", "#")]
    [InlineData("data:image/png;base64,AAAA", "data:image/png;base64,AAAA")]
    [InlineData("https://x.y/a_b", "https://x.y/a_b")]
    public void SafeUrlMatchesLegacySanitizer(string value, string expected)
    {
        Assert.Equal(expected, MarkdownEngine.SafeUrl(value));
    }

    [Fact]
    public void InlineCodeProtectsMarkdownSyntax()
    {
        Assert.Equal(
            "<code>a [[x]] b</code>",
            MarkdownEngine.InlineMarkdown(
                new string('\x60', 1) + "a [[x]] b" + new string('\x60', 1)));

        Assert.Equal(
            "<span class=\"wikilink\" data-note-name=\"Nota\">Nota</span> e <code>[[Nota]]</code>",
            MarkdownEngine.InlineMarkdown(
                "[[Nota]] e " +
                new string('\x60', 1) +
                "[[Nota]]" +
                new string('\x60', 1)));
    }

    [Fact]
    public void VisualParserHandlesBrowserLikeFragmentsWithoutDom()
    {
        Assert.Equal(
            "| a | b |\n| :--- | ---: |\n| x \\| y |  |\n",
            VisualMarkdown.FromHtml(
                "<table data-align=\"left,right\"><thead><tr><th>a</th><th>b</th></tr></thead>" +
                "<tbody><tr><td>x | y</td><td><br></td></tr></tbody></table>"));

        Assert.Equal(
            "[[Alvo|rótulo]] e $x$\n",
            VisualMarkdown.FromHtml(
                "<p><span class=\"wikilink\" data-note-name=\"Alvo\">rótulo</span> e " +
                "<span class=\"umath\" data-tex=\"x\" data-open=\"$\" data-close=\"$\">" +
                "<span>qualquer render visual</span></span></p>"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Título\n")]
    public void RichHtmlEditRoundTripCanBeEnabledOnlyForIdenticalMarkdown(string source)
    {
        Assert.True(VisualMarkdown.IsLosslessEditorRoundTrip(source));
    }

    [Theory]
    [InlineData("Texto sem quebra final")]
    [InlineData("# Título\r\n")]
    [InlineData("Texto com espaço final.  \n")]
    public void RichHtmlEditRejectsMarkdownThatWouldBeNormalized(string source)
    {
        Assert.False(VisualMarkdown.IsLosslessEditorRoundTrip(source));
    }

    [Fact]
    public void FrontmatterIsExternalAuthorityInVisualMode()
    {
        const string source =
            "---\ntitle: X\ntags: [a]\n---\n\ncorpo antigo";

        Assert.Equal(
            "---\ntitle: X\ntags: [a]\n---\nnovo\n",
            VisualMarkdown.FromHtmlUsingEditorSource("<p>novo</p>", source));

        Assert.Equal(
            "---\ntitle: X\ntags: [a]\n---",
            VisualMarkdown.FromHtmlUsingEditorSource(string.Empty, source));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Urbe.Portable.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Execute os testes dentro do checkout do Urbe.");
    }
}
