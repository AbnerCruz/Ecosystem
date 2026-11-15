using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>The formatting buttons give Markdown that is read as they promise (editor review, 14/11/2026).</summary>
public sealed class EditorFormattingTests
{
    private static string Apply(string text, int a, int b, string id) => LegacyVisualEdit.ToggleMark(text, a, b, id).Text;

    [Fact]
    public void TheSpacesASelectionTakesStayOutsideTheMarks()
    {
        const string t = "uma palavra aqui";
        // a double tap that took the space after the word
        Assert.Equal("uma **palavra** aqui", Apply(t, 4, 12, "bold"));
        Assert.Equal("uma _palavra_ aqui", Apply(t, 3, 12, "italic"));
        Assert.Equal("uma `palavra` aqui", Apply(t, 4, 12, "code"));
    }

    [Fact]
    public void ASelectionOverSeveralLinesMarksEachLine()
    {
        const string t = "- primeiro item\n- segundo item\n\nfim";
        var all = t.IndexOf("\n\nfim", StringComparison.Ordinal);
        var bold = Apply(t, 0, all, "bold");
        Assert.Equal("- **primeiro item**\n- **segundo item**\n\nfim", bold);
        // and again takes them away
        var back = LegacyVisualEdit.ToggleMark(bold, 0, bold.IndexOf("\n\nfim", StringComparison.Ordinal), "bold");
        Assert.Equal(t, back.Text);
    }

    [Fact]
    public void ItalicInsideAWordUsesTheStar()
    {
        Assert.Equal("pala*vra*s", Apply("palavras", 4, 7, "italic"));
        Assert.Equal("_palavras_", Apply("palavras", 0, 8, "italic"));
    }

    [Fact]
    public void ItalicWrittenWithAStarIsSeenAndTakenOff()
    {
        const string t = "um *leve* e **forte**";
        var inLeve = t.IndexOf("leve", StringComparison.Ordinal) + 1;
        Assert.Contains("italic", LegacyEditorTools.ActiveTools(t, inLeve, inLeve));
        Assert.Equal("um leve e **forte**", Apply(t, inLeve, inLeve, "italic"));
        // the stars of bold are not italic
        var inForte = t.IndexOf("forte", StringComparison.Ordinal) + 1;
        Assert.DoesNotContain("italic", LegacyEditorTools.ActiveTools(t, inForte, inForte));
        Assert.Contains("bold", LegacyEditorTools.ActiveTools("um __forte__", 6, 6));
    }
}
