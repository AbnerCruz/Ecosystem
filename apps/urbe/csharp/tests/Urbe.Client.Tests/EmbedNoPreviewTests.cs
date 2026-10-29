using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-041: an embed the Urbe cannot show says which file it is and that the link still works.</summary>
public sealed class EmbedNoPreviewTests
{
    [Fact]
    public void TheMessageNamesTheKindAndKeepsTheLink()
    {
        var text = MainView.NoPreview("Documentos/Contrato.docx");
        Assert.Contains(".docx", text);
        Assert.Contains("o link continua valendo", text);
        Assert.Contains("este arquivo", MainView.NoPreview("LEIAME"));
    }
}
