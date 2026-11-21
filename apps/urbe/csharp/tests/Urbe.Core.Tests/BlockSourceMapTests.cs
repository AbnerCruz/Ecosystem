namespace Urbe.Core.Tests;

/// <summary>CST-156: a block knows the note lines it comes from, and an edit to one block goes back only there.</summary>
public sealed class BlockSourceMapTests
{
    private const string Note = "---\ntags: [a]\n---\n# Título\n\nPrimeiro parágrafo\ncontinua aqui.\n\n- um\n- dois\n\n```cs\nx = 1;\n\ny = 2;\n```\n\n> citação\n";

    [Fact]
    public void EachBlockPointsAtItsLines()
    {
        var located = VisualDocumentEditor.Locate(Note);
        var lines = Note.Split('\n');
        string At(int i) => string.Join("\n", lines.Skip(located[i].FirstLine - 1).Take(located[i].LineCount));
        Assert.Equal(5, located.Count);
        Assert.Equal("# Título", At(0));
        Assert.Equal("Primeiro parágrafo\ncontinua aqui.", At(1));
        Assert.Equal("- um\n- dois", At(2));
        Assert.Equal("```cs\nx = 1;\n\ny = 2;\n```", At(3));   // a blank line inside code stays in the block
        Assert.Equal("> citação", At(4));
    }

    [Fact]
    public void ReplacingOneBlockLeavesEverythingElseByteForByte()
    {
        var edited = VisualDocumentEditor.ReplaceBlock(Note, 1, "Parágrafo novo.");
        Assert.Equal(Note.Replace("Primeiro parágrafo\ncontinua aqui.", "Parágrafo novo."), edited);
        var crlf = Note.Replace("\n", "\r\n");
        Assert.Equal(crlf.Replace("> citação", "> outra"), VisualDocumentEditor.ReplaceBlock(crlf, 4, "> outra"));
        Assert.Equal(VisualDocumentEditor.Parse(Note).Blocks, VisualDocumentEditor.Locate(Note).Select(l => l.Block));
    }

    /// <summary>CST-156: the text the preview drew (no marks) finds its block's first line; a text in two blocks or none asks.</summary>
    [Fact]
    public void TheShownTextFindsItsBlock()
    {
        const string Note = "---\ntags: [a]\n---\n# Ciclo da água\n\nA água **evapora** e vira [nuvem](https://x.org).\n\n- primeiro item\n- segundo item\n\nRepete.\n\nRepete.\n";
        Assert.Equal(4, VisualDocumentEditor.LineShowing(Note, "Ciclo da água"));
        Assert.Equal(6, VisualDocumentEditor.LineShowing(Note, "A água evapora e vira nuvem."));
        Assert.Equal(8, VisualDocumentEditor.LineShowing(Note, "segundo item"));
        Assert.Null(VisualDocumentEditor.LineShowing(Note, "Repete."));   // two blocks say it: ask
        Assert.Null(VisualDocumentEditor.LineShowing(Note, "não está aqui"));
    }
}
