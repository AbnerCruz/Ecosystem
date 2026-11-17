namespace Urbe.Core.Tests;

public sealed class PublicationStyleTests
{
    [Fact]
    public void ANewPublicationTakesTheStyleNeverTheContent()
    {
        var model = PublicationProject.Create("Livro A", "d1").AddSource("d2") with
        {
            Design = new PublicationDesign { Layout = "book", Theme = "ocean", Accent = "#0b6e4f", Font = "serif" },
            Audience = "Ensino médio", Objective = "Revisão"
        };
        model = model with { LastExport = PublicationExportRecord.Of(model, "html", _ => "x", DateTimeOffset.UnixEpoch) };
        var styled = model.StyleFor("Livro B");
        Assert.Equal("Livro B", styled.Title);
        Assert.Equal(model.Design, styled.Design);
        Assert.Equal(("Ensino médio", "Revisão"), (styled.Audience, styled.Objective));
        Assert.Empty(styled.Sources);
        Assert.Empty(styled.Sections);
        Assert.Null(styled.LastExport);
        Assert.NotEqual(model.PublicationId, styled.PublicationId);
        PublicationProjectCodec.Validate(styled);
    }
}
