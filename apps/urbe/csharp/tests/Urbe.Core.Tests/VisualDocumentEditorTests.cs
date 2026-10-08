using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class VisualDocumentEditorTests
{
    [Fact]
    public void ParsesCommonBlocksAndRoundTripsCanonicalMarkdown()
    {
        const string markdown = "---\ntitle: Demo\n---\n\n# Título\n\nTexto simples.\n\n- um\n- dois\n\n1. primeiro\n2. segundo\n\n> uma citação\n\n```csharp\nvar x = 1;\n```\n\n---\n";

        var model = VisualDocumentEditor.Parse(markdown);

        Assert.Equal("---\ntitle: Demo\n---", model.Frontmatter);
        Assert.Collection(
            model.Blocks,
            block => Assert.Equal(VisualBlockKind.Heading, block.Kind),
            block => Assert.Equal(VisualBlockKind.Paragraph, block.Kind),
            block => Assert.Equal(VisualBlockKind.UnorderedList, block.Kind),
            block => Assert.Equal(VisualBlockKind.OrderedList, block.Kind),
            block => Assert.Equal(VisualBlockKind.Quote, block.Kind),
            block => Assert.Equal(VisualBlockKind.Code, block.Kind),
            block => Assert.Equal(VisualBlockKind.HorizontalRule, block.Kind));

        Assert.Equal(markdown, VisualDocumentEditor.ToMarkdown(model.Frontmatter, model.Blocks));
    }

    [Fact]
    public void AdvancedMarkdownIsExposedAsRawInsteadOfBeingSilentlyLost()
    {
        const string markdown = "# Cabeçalho\n\n| A | B |\n|---|---|\n| 1 | 2 |\n";

        var model = VisualDocumentEditor.Parse(markdown);

        Assert.Equal(VisualBlockKind.Heading, model.Blocks[0].Kind);
        Assert.Equal(VisualBlockKind.Raw, model.Blocks[1].Kind);
        Assert.Contains("| A | B |", model.Blocks[1].Text);

        Assert.Equal(markdown, VisualDocumentEditor.ToMarkdown(model.Frontmatter, model.Blocks));
    }

    [Fact]
    public void EditingAVisualBlockPreservesFrontmatterAndProducesMarkdown()
    {
        const string markdown = "---\nid: 42\n---\n\n# Antigo\n\nTexto";
        var model = VisualDocumentEditor.Parse(markdown);
        var blocks = model.Blocks.ToList();
        blocks[0] = blocks[0] with { Text = "Novo", Level = 2 };
        blocks[1] = blocks[1] with { Text = "Outro" };
        blocks.Add(new VisualBlock(VisualBlockKind.UnorderedList, "A\nB"));

        var result = VisualDocumentEditor.ToMarkdown(model.Frontmatter, blocks);

        Assert.StartsWith("---\nid: 42\n---\n\n", result, StringComparison.Ordinal);
        Assert.Contains("## Novo", result, StringComparison.Ordinal);
        Assert.Contains("Outro", result, StringComparison.Ordinal);
        Assert.Contains("- A\n- B", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeLanguageIsRetainedByVisualModel()
    {
        var model = VisualDocumentEditor.Parse("```csharp\nConsole.WriteLine(1);\n```");

        Assert.Single(model.Blocks);
        Assert.Equal(VisualBlockKind.Code, model.Blocks[0].Kind);
        Assert.Equal("csharp", model.Blocks[0].Language);
        Assert.Contains("Console.WriteLine(1);", model.Blocks[0].Text, StringComparison.Ordinal);
    }
}
