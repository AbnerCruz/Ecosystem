namespace Urbe.Core.Tests;

/// <summary>UC-61: Personalização/editor.json — the editor's bar and "/" menu, as the person orders them.</summary>
public sealed class EditorLayoutTests
{
    private static readonly string[] Bar = ["bold", "italic", "h1", "math"];
    private static readonly string[] Menu = ["h1", "h2", "list", "task", "box"];

    [Fact]
    public void TheFileOrdersAndCutsUnknownIdsLeaveAndSeparatorsAreTidied()
    {
        var (bar, menu) = EditorLayout.Parse("""{"versao":1,"barra":["|","h1","|","|","nada","bold","bold","|"],"menu":["task","h1","task","nada"]}""", Bar, Menu);
        Assert.Equal(["h1", "|", "bold"], bar);
        Assert.Equal(["task", "h1"], menu);
        Assert.Equal((null, null), EditorLayout.Parse("{ não é json", Bar, Menu));
        var (onlyBar, noMenu) = EditorLayout.Parse("""{"barra":["math"]}""", Bar, Menu);
        Assert.Equal(["math"], onlyBar);
        Assert.Null(noMenu);
    }

    [Fact]
    public void TheMenuFollowsTheOrderAndTheTemplateReadsBackAsTheDefault()
    {
        var items = LegacyEditorTools.FilterSlashWithAddons("");
        Assert.Equal(["task", "h1"], EditorLayout.Arrange(items, ["task", "h1"]).Select(x => x.Act));
        Assert.Equal(items, EditorLayout.Arrange(items, null));
        var template = EditorLayout.Template(["bold", "|", "h1"], ["h1", "task"]);
        var (b, m) = EditorLayout.Parse(template, Bar, Menu);
        Assert.Equal(["bold", "|", "h1"], b);
        Assert.Equal(["h1", "task"], m);
    }
    [Fact]
    public void TheBarIsReplacedAndEverythingElseInTheFileStays()
    {
        const string file = "{\n  \"versao\": 1,\n  \"menu\": [\"h1\"],\n  \"meu\": true\n}\n";
        var next = EditorLayout.WithBar(file, ["undo", "redo", "|", "italic", "bold"]);
        var (bar, menu) = EditorLayout.Parse(next, ["undo", "redo", "bold", "italic"], ["h1"]);
        Assert.Equal(["undo", "redo", "|", "italic", "bold"], bar);
        Assert.Equal(["h1"], menu);
        Assert.Contains("\"meu\": true", next);
        // back to the default: "barra" leaves, the rest stays
        var back = EditorLayout.WithBar(next, null);
        Assert.Null(EditorLayout.Parse(back, ["bold"], ["h1"]).Bar);
        Assert.Equal(["h1"], EditorLayout.Parse(back, ["bold"], ["h1"]).Menu);
        // a broken file is started again rather than refused
        Assert.Equal(["bold"], EditorLayout.Parse(EditorLayout.WithBar("{ quebrado", ["bold"]), ["bold"], []).Bar);
    }
}
