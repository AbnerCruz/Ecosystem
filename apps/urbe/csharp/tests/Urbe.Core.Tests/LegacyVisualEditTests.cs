using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyVisualEditTests
{
    [Theory]
    [InlineData("texto", 2, "## ", "## texto")]
    [InlineData("## texto", 5, "## ", "texto")]
    [InlineData("# texto", 3, "## ", "## texto")]
    [InlineData("- item", 4, "## ", "## item")]
    [InlineData("> citado", 3, "# ", "# citado")]
    [InlineData("texto", 0, "- ", "- texto")]
    [InlineData("- texto", 0, "- ", "texto")]
    [InlineData("- [x] texto", 0, "- [ ] ", "texto")]
    [InlineData("1. texto", 0, "- [ ] ", "texto")]
    [InlineData("# titulo", 0, "- [ ] ", "- [ ] titulo")]
    [InlineData("texto", 0, "> ", "> texto")]
    [InlineData("> a\n> b", 5, "> ", "a\nb")]
    [InlineData("", 0, "# ", "# ")]
    public void BlockToolsConvertThePreservingTheContent(string text, int caret, string prefix, string expected)
    {
        var edit = LegacyVisualEdit.ToggleBlock(text, caret, prefix);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(edit.Text.Length, edit.SelectionStart);
    }

    [Fact]
    public void BlockToolsOnlyTouchTheCaretLineExceptQuotes()
    {
        Assert.Equal("a\n## b\nc", LegacyVisualEdit.ToggleBlock("a\nb\nc", 2, "## ").Text);
        Assert.Equal("- a\nb", LegacyVisualEdit.ToggleBlock("- a\n- b", 5, "- ").Text);
        Assert.Equal("x\n> a\n> b", LegacyVisualEdit.ToggleBlock("x\na\n> b", 3, "> ").Text);
    }

    [Fact]
    public void WrapTogglesAndKeepsTheSelection()
    {
        var on = LegacyVisualEdit.ToggleWrap("um dois tres", 3, 7, "**");
        Assert.Equal("um **dois** tres", on.Text);
        Assert.Equal((5, 9), (on.SelectionStart, on.SelectionEnd));
        var off = LegacyVisualEdit.ToggleWrap(on.Text, on.SelectionStart, on.SelectionEnd, "**");
        Assert.Equal("um dois tres", off.Text);
        Assert.Equal("um dois tres", LegacyVisualEdit.ToggleWrap("um _dois_ tres", 4, 8, "_").Text);
        Assert.Equal("a b c", LegacyVisualEdit.ToggleWrap("a **b** c", 2, 7, "**").Text);
        Assert.Equal("_x_", LegacyVisualEdit.ToggleWrap("x", 0, 1, "_").Text);
    }

    [Theory]
    [InlineData("/", 1, "```\n\n```", 4)]
    [InlineData("/cod", 4, "```\n\n```", 4)]
    [InlineData("texto", 5, "texto\n```\n\n```", 10)]
    [InlineData("a\n/\nb", 3, "a\n```\n\n```\nb", 6)]
    public void CodeBlock_StartsEmptyWithTheCaretInside(string text, int caret, string expected, int at)
    {
        var edit = LegacyVisualEdit.CodeBlock(text, caret);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(at, edit.SelectionStart);
        Assert.Equal('\n', edit.Text[at - 1]);
    }

    [Fact]
    public void CodeWikiAndLinkTools()
    {
        // DEC-0045: no "código" placeholder; the caret goes between the backticks
        var empty = LegacyVisualEdit.InlineCode("a  b", 2, 2);
        Assert.Equal("a `` b", empty.Text);
        Assert.Equal(3, empty.SelectionStart);
        Assert.Equal("a `x` b", LegacyVisualEdit.InlineCode("a x b", 2, 3).Text);
        Assert.Equal("a x b", LegacyVisualEdit.InlineCode("a `x` b", 3, 4).Text);   // code goes back to text
        Assert.Equal("a [[Nota]] b", LegacyVisualEdit.WikiOfSelection("a  Nota  b", 2, 8)!.Text);
        Assert.Null(LegacyVisualEdit.WikiOfSelection("a  b", 2, 3));
        Assert.Equal("[texto](https://x.org)", LegacyVisualEdit.Link("texto", 0, 5, " https://x.org ")!.Text);
        Assert.Equal("[mailto:a@b.c](mailto:a@b.c)", LegacyVisualEdit.Link("", 0, 0, "mailto:a@b.c")!.Text);
        Assert.Null(LegacyVisualEdit.Link("x", 0, 1, "ftp://x"));
        Assert.Null(LegacyVisualEdit.Link("x", 0, 1, "https://"));
    }

    [Fact]
    public void SlashChoicesClearTheBlockThenApplyTheTool()
    {
        Assert.Equal("# ", LegacyVisualEdit.Slash("h1")!.Text);
        Assert.Equal("- [ ] ", LegacyVisualEdit.Slash("task")!.Text);
        Assert.Equal("[[", LegacyVisualEdit.Slash("noteLink")!.Text);
        Assert.Null(LegacyVisualEdit.Slash("rule"));
    }
}
