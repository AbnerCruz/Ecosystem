using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>DEC-0045 (UC-37): the toolbar's on/off state is read from the Markdown under the selection, never kept apart.</summary>
public sealed class EditorActiveToolsTests
{
    [Theory]
    [InlineData("a **negrito** b", 6, 6, "bold")]
    [InlineData("a **negrito** b", 4, 11, "bold")]
    [InlineData("a _itálico_ b", 5, 5, "italic")]
    [InlineData("a `x = 1` b", 4, 4, "code")]
    [InlineData("# Título", 4, 4, "h1")]
    [InlineData("## Seção", 0, 0, "h2")]
    [InlineData("### Sub", 7, 7, "h3")]
    [InlineData("- item", 3, 3, "list")]
    [InlineData("- [ ] fazer", 8, 8, "task")]
    [InlineData("- [x] feito", 8, 8, "task")]
    [InlineData("> citação", 4, 4, "quote")]
    [InlineData("- **forte** item", 5, 5, "list,bold")]
    [InlineData("texto\n# Título", 9, 9, "h1")]
    public void ReadsTheStateFromTheText(string text, int a, int b, string expected)
    {
        Assert.Equal(expected.Split(',').OrderBy(x => x), LegacyEditorTools.ActiveTools(text, a, b).OrderBy(x => x));
    }

    [Theory]
    [InlineData("a **negrito** b", 0, 0)]       // outside the pair
    [InlineData("a **negrito** b", 0, 6)]       // the selection leaves the pair
    [InlineData("snake_case_name", 7, 7)]       // underscores inside a word are not italic
    [InlineData("a `**x**` b", 6, 6)]           // marks inside code are code, not bold
    [InlineData("**a\nb**", 1, 6)]              // across lines
    [InlineData("#hashtag", 2, 2)]              // a tag is not a heading
    [InlineData("", 0, 0)]
    public void OffWhenTheTextDoesNotSaySo(string text, int a, int b)
    {
        var on = LegacyEditorTools.ActiveTools(text, a, b);
        Assert.DoesNotContain("bold", on);
        Assert.DoesNotContain("italic", on);
        Assert.DoesNotContain("h1", on);
    }

    [Fact]
    public void TogglingTwiceLeavesTheButtonOffAgain()
    {
        // the 1.8.4 bug: a second tap did not turn the button off; here the state follows the text both ways
        var on = LegacyVisualEdit.ToggleWrap("a x b", 2, 3, "**");
        Assert.Contains("bold", LegacyEditorTools.ActiveTools(on.Text, on.SelectionStart, on.SelectionEnd));
        var off = LegacyVisualEdit.ToggleWrap(on.Text, on.SelectionStart, on.SelectionEnd, "**");
        Assert.Equal("a x b", off.Text);
        Assert.DoesNotContain("bold", LegacyEditorTools.ActiveTools(off.Text, off.SelectionStart, off.SelectionEnd));
    }
}
