using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class WebClipTests
{
    [Fact]
    public void APageKeepsItsArticleTitleSourceAndAbsoluteLinks()
    {
        const string html = "<html><head><title>Fotossíntese – Site</title><script>track()</script></head><body><nav>menu</nav>" +
            "<article><h1>Fotossíntese</h1><p>As plantas usam <a href=\"/luz\">luz</a> para produzir açúcar.</p><ul><li>água</li><li>CO₂</li></ul></article>" +
            "<footer>rodapé</footer></body></html>";
        var page = WebClip.Read("https://example.org/bio/f", html);
        Assert.Equal("Fotossíntese – Site", page.Title);
        Assert.Contains("# Fotossíntese", page.Markdown);
        Assert.Contains("[luz](https://example.org/luz)", page.Markdown);
        Assert.Contains("- água", page.Markdown);
        Assert.DoesNotContain("menu", page.Markdown);
        Assert.DoesNotContain("track", page.Markdown);
        Assert.DoesNotContain("rodapé", page.Markdown);
        var note = WebClip.Note("https://example.org/bio/f", page, new DateTime(2026, 11, 15));
        Assert.StartsWith("---\nsource: https://example.org/bio/f\ncaptured: 2026-11-15\n---\n\n", note);
        Assert.Equal("https://example.org/bio/f", NoteProperties.Parse(note).Items.First(p => p.Key == "source").Value);
    }

    [Fact]
    public void APageWithoutTitleIsNamedByItsSite() =>
        Assert.Equal("example.org", WebClip.Read("https://example.org/x", "<p>oi</p>").Title);
}
