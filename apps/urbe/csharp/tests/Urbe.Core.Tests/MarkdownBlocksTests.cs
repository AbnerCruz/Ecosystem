using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MarkdownBlocksTests
{
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        return root!.FullName;
    }

    private static string RenderedByBlocks(string md)
    {
        var text = md.Replace("\r\n", "\n");
        var html = new System.Text.StringBuilder();
        foreach (var b in MarkdownEngine.SplitBlocks(text))
            html.Append(MarkdownEngine.Render(text[b.Start..b.End]));
        return html.ToString();
    }

    [Fact]
    public void RenderingBlockByBlockEqualsRenderingTheNote()
    {
        var corpus = new List<string>();
        foreach (var dir in new[] { "tutorial", "docs", Path.Combine("csharp", "docs") })
        {
            var d = Path.Combine(Root(), dir);
            if (Directory.Exists(d)) corpus.AddRange(Directory.EnumerateFiles(d, "*.md", SearchOption.AllDirectories).Select(File.ReadAllText));
        }
        corpus.AddRange(
        [
            "---\ntags: [a]\n---\n\n# T\ntexto $x^2$ e\n\n$$\na\n\nb\n$$\nfim",
            "- a\n  - b\n1. c\n\n- [ ] d\n---\n- e",
            "| a | b |\n|-|-|\n| 1 | 2 |\ntexto\n> q\n> r\n> [!tip] t\n```\ncodigo\n\nmais\n```\n```js\nsem fim",
            "#\n# \n\n- \n* a | b\n|--|\n", "a\nb\n\n\nc", "", "\n\n"
        ]);
        Assert.True(corpus.Count > 40);
        foreach (var md in corpus)
        {
            var whole = MarkdownEngine.Render(md);
            var pieces = RenderedByBlocks(md);
            if (pieces.Length == 0) pieces = "<p><br></p>";
            Assert.Equal(whole, pieces);
        }
    }

    [Fact]
    public void BlocksCarryTheirKindAndRange()
    {
        const string md = "---\na: 1\n---\n# T\n\n- x\n- y\ntexto\n> q\n---\n";
        var blocks = MarkdownEngine.SplitBlocks(md);
        Assert.Equal(["frontmatter", "heading", "list", "paragraph", "quote", "rule"], blocks.Select(b => b.Kind));
        Assert.Equal("- x\n- y", md[blocks[2].Start..blocks[2].End]);
    }
}
