namespace Urbe.Core.Tests;

public sealed class AnchorBlockTests
{
    [Fact]
    public void TheParagraphGetsAMarkerAndExtractFindsIt()
    {
        var md = "# T\n\nPrimeira linha\ncontinua aqui.\n\nOutro parágrafo.";
        var (text, id) = EmbedFragments.AnchorBlock(md, md.IndexOf("Primeira", StringComparison.Ordinal) + 3, () => "pub1")!.Value;
        Assert.Equal("pub1", id);
        Assert.Contains("continua aqui. ^pub1\n", text);
        Assert.Equal(["pub1"], EmbedFragments.BlockIds(text));
    }

    [Fact]
    public void AnExistingMarkerIsKeptAndSealsStayLast()
    {
        var md = "Texto ^velho";
        Assert.Equal(("Texto ^velho", "velho"), EmbedFragments.AnchorBlock(md, 2, () => "novo"));
        var sealedMd = "Texto da IA {ia:\"m\"}";
        Assert.Equal("Texto da IA ^n {ia:\"m\"}", EmbedFragments.AnchorBlock(sealedMd, 1, () => "n")!.Value.Markdown);
    }

    [Fact]
    public void HeadingsBlankLinesAndCodeHaveNoBlock()
    {
        var md = "# T\n\n```\ncode\n```\n";
        Assert.Null(EmbedFragments.AnchorBlock(md, 1, () => "x"));
        Assert.Null(EmbedFragments.AnchorBlock(md, md.IndexOf('\n') + 1, () => "x"));
        Assert.Null(EmbedFragments.AnchorBlock(md, md.IndexOf("code", StringComparison.Ordinal), () => "x"));
    }
}
