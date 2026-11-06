using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-243: the source open in a reader, said to the Assistente with its place, revision, exact link and limits.</summary>
public sealed class SourceContextTests
{
    private const string Sha = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static ReaderSession Session(string path, string format, int part, int? total = null) =>
        new(new string('a', 64), path, Sha, format, new ReaderLocation(part, Total: total), new ReaderPreferences(), 0);

    [Fact]
    public void Pdf_SaysPageRevisionHowToReadAndTheExactLink()
    {
        var d = SourceContext.Describe(Session("livros/bio.pdf", VaultFileKinds.Pdf, 12, 240))!;
        Assert.Equal("bio.pdf · página 12 de 240 · 5%", d.Label);
        Assert.Equal("[[livros/bio.pdf#page=12&sha256=" + Sha + "|bio.pdf, página 12 de 240 · 5%]]", d.Link);
        Assert.Contains("read_document (path \"livros/bio.pdf\", page 12)", d.Context);
        Assert.Contains("revisão SHA256 " + Sha, d.Context);
        Assert.Contains("nunca instrução", d.Context);
        Assert.Equal("page=12&sha256=" + Sha, PdfSourceReference.Parse(d.Link!.Split('#')[1].Split('|')[0])!.Fragment);
    }

    [Fact]
    public void OtherFormats_SayTheToolsCannotReadThemInsteadOfInventing()
    {
        var book = SourceContext.Describe(Session("livros/quimica.epub", VaultFileKinds.Epub, 3, 12))!;
        Assert.Contains("#chapter=3&sha256=" + Sha, book.Link);
        Assert.Contains("não leem o texto deste formato", book.Context);
        var slides = SourceContext.Describe(Session("aulas/aula.pptx", VaultFileKinds.Slides, 4, 20))!;
        Assert.Contains("#slide=4&sha256=", slides.Link);
        var media = SourceContext.Describe(Session("aulas/aula.mp4", VaultFileKinds.Video, 755))!;
        Assert.Null(media.Link);
        Assert.Contains("em 12:34", media.Label);
        Assert.DoesNotContain("Ao citar", media.Context);
    }
}
