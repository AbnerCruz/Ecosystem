namespace Urbe.Core.Tests;

public sealed class PublicationThemeTests
{
    [Fact]
    public void TokensComeFromTheThemeTheOwnAccentAndTheLayout()
    {
        Assert.Equal("#075985", PublicationTheme.Of(new PublicationDesign { Theme = "ocean" }).Accent);
        Assert.Equal("#0b6e4f", PublicationTheme.Of(new PublicationDesign { Theme = "ocean", Accent = "#0B6E4F" }).Accent);
        Assert.Equal("#6b4f2a", PublicationTheme.Of(new PublicationDesign { Accent = "verde" }).Accent);   // invalid: the theme's
        Assert.Equal("serif", PublicationTheme.Of(new PublicationDesign { Layout = "book" }).Font);
        Assert.Equal("sans", PublicationTheme.Of(new PublicationDesign { Layout = "book", Font = "sans" }).Font);
        Assert.Contains("monospace", PublicationTheme.Of(new PublicationDesign { Font = "mono" }).CssFont);
    }

    [Fact]
    public void EveryHtmlOutputReadsTheSameTokens()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "N.md", Content = "# N\n\nTexto.\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("P", note.Id) with { Design = new PublicationDesign { Accent = "#0b6e4f", Font = "serif" } };
        var article = PublicationHtml.Export(project, catalog).Html;
        Assert.Contains("#0b6e4f", article);
        Assert.Contains("Georgia", article);
        Assert.Contains("#0b6e4f", PublicationHtml.Export(project with { Design = project.Design with { Layout = "slides" } }, catalog).Html);
        Assert.Contains("#0b6e4f", PublicationHtml.Export(project with { Design = project.Design with { Layout = "course" } }, catalog).Html);
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project));
        Assert.Equal(("#0b6e4f", "serif"), (back.Design.Accent, back.Design.Font));
    }
}
