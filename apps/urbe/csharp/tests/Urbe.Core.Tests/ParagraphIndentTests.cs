using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Owner's screenshot (15/11/2026): paragraphs indented by hand showed pushed to the right. As in CommonMark and
/// the Obsidian, the spaces and tabs before a paragraph's text are not part of it; the file keeps them as written.</summary>
public sealed class ParagraphIndentTests
{
    [Theory]
    [InlineData("  A ideia surgiu", "<p>A ideia surgiu</p>")]
    [InlineData("\tA ideia", "<p>A ideia</p>")]
    [InlineData("   **Negrito** no início", "<p><strong>Negrito</strong> no início</p>")]
    [InlineData("Sem recuo", "<p>Sem recuo</p>")]
    public void LeadingSpacesOfAParagraphAreNotShown(string md, string html) => Assert.Equal(html, MarkdownEngine.Render(md));

    [Fact]
    public void ListsAndCodeKeepTheirIndentation()
    {
        Assert.Contains("<li>", MarkdownEngine.Render("  - item recuado"));
        Assert.Contains("    dentro", MarkdownEngine.Render("```\n    dentro\n```"));
    }
}
