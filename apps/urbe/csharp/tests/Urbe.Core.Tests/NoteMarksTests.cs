using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>DEC-0045 (UC-43): %%comments%% and #Tag: paragraphs, as plain Markdown marks over the 1.8.4 renderer.</summary>
public sealed class NoteMarksTests
{
    private static IEnumerable<RichNode> All(IEnumerable<RichNode> nodes) => nodes.SelectMany(n => new[] { n }.Concat(All(n.Children)));

    [Fact]
    public void Comments_BecomeMarkedSpans()
    {
        var tree = NoteMarks.Parse("Traduzir %%rever esta frase%% depois.");
        var comment = Assert.Single(All(tree), n => n.HasClass(NoteMarks.CommentClass));
        Assert.Equal("rever esta frase", comment.Children.Single().Text);
        var text = string.Concat(All(tree).Where(n => n.Tag == RichNode.TextTag).Select(n => n.Text));
        Assert.Equal("Traduzir rever esta frase depois.", text);
    }

    [Fact]
    public void TaggedParagraph_TakesTheTag()
    {
        var p = Assert.Single(NoteMarks.Parse("#Todo: comprar pão"));
        Assert.True(p.HasClass(NoteMarks.TagClass));
        Assert.Equal("Todo", p.Attr("data-tag"));
        Assert.Equal("comprar pão", p.Children.Single().Text);
        Assert.Equal("Dúvida", NoteMarks.TagOf("#Dúvida: x"));
    }

    [Theory]
    [InlineData("#Todo sem dois-pontos")]
    [InlineData("# Título")]
    [InlineData("texto #Todo: no meio")]
    [InlineData("#123: número")]
    public void NotATag(string md) => Assert.DoesNotContain(All(NoteMarks.Parse(md)), n => n.HasClass(NoteMarks.TagClass));

    [Fact]
    public void Code_KeepsItsPercentSigns()
    {
        Assert.DoesNotContain(All(NoteMarks.Parse("`a %%b%% c`")), n => n.HasClass(NoteMarks.CommentClass));
        Assert.DoesNotContain(All(NoteMarks.Parse("```\nx %%y%% z\n```")), n => n.HasClass(NoteMarks.CommentClass));
    }

    [Fact]
    public void StripComments_LeavesThemOutOfPagesButNotOfCode()
    {
        Assert.Equal("a  b", NoteMarks.StripComments("a %%nota%% b"));
        Assert.Equal("a\n\nb", NoteMarks.StripComments("a\n%%várias\nlinhas%%\nb").Replace("\n\n\n", "\n\n"));
        Assert.Equal("`x %%y%%` z ", NoteMarks.StripComments("`x %%y%%` z %%w%%"));
        Assert.Equal("sem nada", NoteMarks.StripComments("sem nada"));
        Assert.DoesNotContain("segredo", MarkdownEngine.Render(NoteMarks.StripComments("texto %%segredo%%")));
    }

    [Fact]
    public void WithoutMarks_TheTreeIsTheOriginal()
    {
        const string md = "# Título\n\nUm **texto** com [[link]] e `código`.\n\n- item\n";
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(MarkdownTree.Parse(md)), System.Text.Json.JsonSerializer.Serialize(NoteMarks.Parse(md)));
    }

    [Fact]
    public void CommentTool_WrapsOrOpensAComment()
    {
        var empty = LegacyVisualEdit.Comment("a  b", 2, 2);
        Assert.Equal("a %%  %% b", empty.Text);
        Assert.Equal(5, empty.SelectionStart);
        Assert.Equal("a %%x%% b", LegacyVisualEdit.Comment("a x b", 2, 3).Text);
        Assert.Equal("a x b", LegacyVisualEdit.Comment("a %%x%% b", 4, 5).Text);
    }

    [Fact]
    public void WordComments_AreFoundReadAddedAndLeftOutOfPages()
    {
        const string md = "Uma palavra{\"primeiro\"}{\"diz \\\"oi\\\"\"} no texto. `codigo{\"não\"}` fim{\"só\"}\n";
        var groups = NoteMarks.CommentGroups(md);
        Assert.Equal(2, groups.Count);
        Assert.Equal(["primeiro", "diz \"oi\""], groups[0].Comments);
        Assert.Equal(["só"], groups[1].Comments);
        Assert.Equal("palavra", md[(groups[0].Start - 7)..groups[0].Start]);

        // a new one goes at the end of the run, quoted
        var added = NoteMarks.AddComment(md, groups[1], "novo \"x\"");
        Assert.Contains("fim{\"só\"}{\"novo \\\"x\\\"\"}", added);
        Assert.Equal(["só", "novo \"x\""], NoteMarks.CommentGroups(added)[1].Comments);

        // rendered: the run becomes one marked span after the word, with its comments
        var spans = All(NoteMarks.Parse(md)).Where(n => n.HasClass(NoteMarks.NotesClass)).ToList();
        Assert.Equal(2, spans.Count);
        Assert.Contains("primeiro", spans[0].Attr("data-comments"));
        Assert.DoesNotContain(All(NoteMarks.Parse(md)), n => n.Tag == RichNode.TextTag && n.Text.Contains("{\"primeiro"));

        // pages and exported HTML: no comments, code untouched
        Assert.Equal("Uma palavra no texto. `codigo{\"não\"}` fim\n", NoteMarks.StripComments(md));
    }

    [Fact]
    public void AiMark_IsAddedOnceAndRenderedAsAModelMark()
    {
        Assert.Equal("Texto. {ia:\"claude-x\"}\n", NoteMarks.WithAiMark("Texto.\n", "anthropic/claude-x"));
        Assert.Equal("Texto. {ia:\"claude-x\"}\n", NoteMarks.WithAiMark("Texto. {ia:\"claude-x\"}\n", "anthropic/claude-x"));
        var span = All(NoteMarks.Parse("Texto. {ia:\"claude-x\"}\n")).Single(n => n.HasClass(NoteMarks.AiClass));
        Assert.Equal("claude-x", span.Attr("data-model"));
        Assert.Equal("Texto. · IA: claude-x\n", NoteMarks.StripComments("Texto. {ia:\"claude-x\"}\n"));
        Assert.DoesNotContain(All(NoteMarks.Parse("Texto. {ia:\"claude-x\"}\n")), n => n.Tag == RichNode.TextTag && n.Text.Contains("{ia:"));
    }

    [Fact]
    public void AgentRequests_AreChipsInTheEditorFoundInTheSourceAndLeftOutOfPages()
    {
        // UC-80: &Nome&: at the start of a line asks that agent; &&: the default one
        const string text = "# Herói\n\n&Jarvis&: Dê uma sugestão de nome\n\n&&: resuma\n\nTexto & mais &x&: não.\n\n```\n&Jarvis&: em código\n```\n";
        var calls = NoteMarks.AgentCalls(text);
        Assert.Equal([("Jarvis", "Dê uma sugestão de nome"), ("", "resuma")], calls.Select(c => (c.Agent, c.Request)));
        Assert.Equal("&Jarvis&: Dê uma sugestão de nome", text.Substring(calls[0].Start, calls[0].Length));
        var chips = All(NoteMarks.Parse(text)).Where(n => n.HasClass(NoteMarks.AgentClass)).ToList();
        Assert.Equal(["Jarvis", ""], chips.Select(c => c.Attr("data-agent")));
        Assert.Equal("Dê uma sugestão de nome", chips[0].Children[0].Text);
        var page = NoteMarks.StripComments(text);
        Assert.DoesNotContain("Dê uma sugestão", page);
        Assert.DoesNotContain("resuma", page);
        Assert.Contains("Texto & mais &x&: não.", page);
        Assert.Contains("&Jarvis&: em código", page);
    }
}
