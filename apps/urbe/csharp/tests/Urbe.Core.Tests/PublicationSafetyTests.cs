using System.Text.RegularExpressions;

namespace Urbe.Core.Tests;

/// <summary>CST-182: hostile HTML written in a note never becomes something that runs in an exported publication.</summary>
public sealed class PublicationSafetyTests
{
    private const string Hostile = "# Nota\n\n<script>alert('nota')</script>\n\n<img src=x onerror=\"alert(1)\">\n\n[clique](javascript:alert(2))\n\n<iframe src=\"https://mal.example\"></iframe>\n";

    [Theory]
    [InlineData("book")]
    [InlineData("slides")]
    [InlineData("course")]
    public void HostileHtmlInANoteIsInert(string layout)
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Nota.md", Content = Hostile });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("P", note.Id);
        project = project with { Design = project.Design with { Layout = layout } };
        var html = PublicationHtml.Export(project, catalog).Html;
        Assert.DoesNotContain("<script>alert('nota')", html);
        Assert.DoesNotContain("javascript:alert", html);
        Assert.DoesNotMatch(new Regex(@"<img[^>]*\sonerror\s*=", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatch(new Regex(@"<iframe", RegexOptions.IgnoreCase), html);
        // and whatever the page's own scripts are, its policy keeps it from reaching anything outside the file
        Assert.Contains("default-src 'none'", html);
    }
}
