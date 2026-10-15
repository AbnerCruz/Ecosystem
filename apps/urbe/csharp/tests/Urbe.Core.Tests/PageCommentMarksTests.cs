using System.Text.Json.Nodes;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>UC-49 in pages: word comments as a 💬 in the studio preview, left out of the exported HTML, untouched when off (the oracles).</summary>
public sealed class PageCommentMarksTests
{
    private sealed class Notes(params PageNote[] all) : IPageNotes
    {
        public PageNote? Get(string path) => all.FirstOrDefault(n => n.Path == path);
        public IReadOnlyList<PageNote> List() => all;
    }

    private static JsonObject Page(string markdown, string title = "Título{\"no título\"}") => new()
    {
        ["meta"] = new JsonObject { ["title"] = title },
        ["sections"] = new JsonArray(
            new JsonObject { ["id"] = "s_a", ["type"] = "text", ["props"] = new JsonObject { ["markdown"] = markdown } },
            new JsonObject { ["id"] = "s_b", ["type"] = "note", ["props"] = new JsonObject { ["source"] = "note", ["path"] = "Nota.md" } },
            new JsonObject { ["id"] = "s_c", ["type"] = "code", ["props"] = new JsonObject { ["code"] = "x = {\"literal\"}" } })
    };

    private static readonly Notes Vault = new(new PageNote("Nota.md", "Nota", "# Nota\n\nCasa{\"da nota\"} amarela."));

    [Fact]
    public void Preview_ShowsTheMarkerAfterTheWord_WithTheCountAndTheComments()
    {
        var html = new PageEngine(2026) { UrbeMarks = true }.Render(Page("Uma palavra{\"primeiro\"}{\"segundo \\\"aspas\\\"\"} e `cod{\"x\"}`. %%oculto%%"), Vault, preview: true);
        Assert.Contains("palavra<sup class=\"urbe-cm\"", html);
        Assert.Contains("💬2</sup>", html);
        Assert.Contains("title=\"primeiro\nsegundo &quot;aspas&quot;\"", html);
        Assert.Contains("data-comments=\"", html);
        // a note shown in the page has its comments too
        Assert.Contains("Casa<sup class=\"urbe-cm\"", html);
        Assert.Contains("title=\"da nota\"", html);
        // code keeps its text; %%…%% stays out
        Assert.Contains("cod{&quot;x&quot;}", html);
        Assert.Contains("{&quot;literal&quot;}", html);
        Assert.DoesNotContain("oculto", html);
        // inside tags and the <title> the run just goes away
        Assert.Contains("<title>Título", html);
        Assert.DoesNotContain("no título", html[..html.IndexOf("<body", StringComparison.Ordinal)]);
        Assert.DoesNotContain('', html);
        Assert.Contains(".urbe-cm-pop", html);
    }

    [Fact]
    public void Export_LeavesTheCommentsOut()
    {
        var html = new PageEngine(2026) { UrbeMarks = true }.Render(Page("Uma palavra{\"primeiro\"} aqui. Texto{ia:\"modelo\"}"), Vault);
        Assert.DoesNotContain("urbe-cm", html);
        Assert.DoesNotContain("primeiro", html);
        Assert.DoesNotContain("da nota", html);
        Assert.Contains("Uma palavra aqui.", html);
        Assert.Contains("· IA: modelo", html);
        Assert.Contains("{&quot;literal&quot;}", html);
    }

    [Fact]
    public void Off_RendersTheTextsAsThe184Does()
    {
        var page = Page("Uma palavra{\"primeiro\"} aqui.");
        var plain = new PageEngine(2026).Render(page, Vault, preview: true);
        Assert.Contains("palavra{&quot;primeiro&quot;}", plain);
        Assert.DoesNotContain("urbe-cm", plain);
        // without any mark, on and off write the same page
        var none = Page("Nada a marcar.", "Título");
        var clean = new Notes(new PageNote("Nota.md", "Nota", "# Nota\n\nCasa amarela."));
        Assert.Equal(new PageEngine(2026).Render(none, clean, preview: true), new PageEngine(2026) { UrbeMarks = true }.Render(none, clean, preview: true));
    }
}
