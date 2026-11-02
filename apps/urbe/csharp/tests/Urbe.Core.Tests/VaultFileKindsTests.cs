using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>app.js classificarArquivo, rotuloClasse and formatBytes.</summary>
public sealed class VaultFileKindsTests
{
    [Theory]
    [InlineData("a/foto.JPG", "image", "Imagem")]
    [InlineData("som.flac", "audio", "Áudio")]
    [InlineData("filme.m4v", "video", "Vídeo")]
    [InlineData("livro.pdf", "pdf", "PDF")]
    [InlineData("site.xhtml", "html", "HTML")]
    [InlineData("manual.epub", "epub", "EPUB")]
    [InlineData("aula.PPTX", "slides", "Apresentação")]
    [InlineData("aula.odp", "slides", "Apresentação")]
    [InlineData("resumo.docx", "document", "Documento")]
    [InlineData("resumo.odt", "document", "Documento")]
    [InlineData("pacote.tar", "zip", "Arquivo compactado")]
    [InlineData("dados.bin", "other", "Arquivo")]
    public void ClassifiesLikeThe184(string path, string kind, string label)
    {
        Assert.Equal(kind, VaultFileKinds.Classify(path));
        Assert.Equal(label, VaultFileKinds.Label(kind));
    }

    [Theory]
    [InlineData(812, "812 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(10 * 1024, "10 KB")]
    [InlineData(5L * 1024 * 1024 + 300 * 1024, "5.3 MB")]
    [InlineData(123L * 1024 * 1024, "123 MB")]
    public void FormatsBytesLikeThe184(long n, string text) => Assert.Equal(text, VaultFileKinds.FormatBytes(n));
    
    [Theory]
    [InlineData("estudo.epub", "application/epub+zip")]
    [InlineData("slides.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("slides.odp", "application/vnd.oasis.opendocument.presentation")]
    [InlineData("texto.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("texto.odt", "application/vnd.oasis.opendocument.text")]
    public void KnownStudyFormatsHaveMimeForExplicitExternalFallback(string file, string mime) =>
        Assert.Equal(mime, VaultFileKinds.Mime(file));

}
