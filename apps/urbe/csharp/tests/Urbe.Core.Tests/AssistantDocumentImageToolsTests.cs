using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantDocumentImageToolsTests
{
    private static readonly byte[] Png =
    [
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
        0, 0, 0, 1, 0, 0, 0, 1
    ];

    private static readonly byte[] Jpeg =
    [
        255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0, 1,
        1, 0, 0, 1, 0, 1, 0, 0, 255, 217
    ];

    private static readonly byte[] Webp =
    [
        82, 73, 70, 70, 4, 0, 0, 0, 87, 69, 66, 80
    ];

    [Theory]
    [InlineData("pagina.png", "image/png")]
    [InlineData("foto.jpg", "image/jpeg")]
    [InlineData("formula.jpeg", "image/jpeg")]
    [InlineData("esquema.webp", "image/webp")]
    public void DetectsOnlySupportedSignedImages(string path, string mime)
    {
        var bytes = mime switch
        {
            "image/png" => Png,
            "image/jpeg" => Jpeg,
            _ => Webp
        };
        Assert.Equal(mime, AssistantDocumentImageTools.Mime(path, bytes));
        Assert.True(AssistantDocumentImageTools.AuthorizedReference(path));
    }

    [Theory]
    [InlineData("../passwords.png")]
    [InlineData("/tmp/secret.jpg")]
    [InlineData("C:\\photo.png")]
    [InlineData(".vault/formula.jpg")]
    [InlineData("vault//scan.png")]
    [InlineData("vault/.secret.png")]
    [InlineData("scan.exe")]
    public void RejectsEscapingOrUnsupportedPaths(string path)
    {
        Assert.False(AssistantDocumentImageTools.AuthorizedReference(path));
    }

    [Fact]
    public void FileExtensionAndContentMustAgree()
    {
        Assert.Null(AssistantDocumentImageTools.Mime("fake.jpg", Png));
        Assert.Null(AssistantDocumentImageTools.Mime("fake.pdf", Jpeg));
        Assert.Null(AssistantDocumentImageTools.Mime("formula.jpg", "not an image"u8.ToArray()));
        Assert.Null(AssistantDocumentImageTools.Mime("formula.png", new byte[AssistantDocumentImageTools.MaxImageBytes + 1]));
    }

    [Fact]
    public void HasSeparateReadOnlyInspectionAndExplicitlyNetworkedVisualAnalysis()
    {
        var inspect = Assert.Single(AssistantDocumentImageTools.All, t => t.Name == "inspect_document_image");
        Assert.Equal("read", inspect.Access);
        var analyze = Assert.Single(AssistantDocumentImageTools.All, t => t.Name == "analyze_document_image");
        Assert.Equal("network", analyze.Access);
    }
}
