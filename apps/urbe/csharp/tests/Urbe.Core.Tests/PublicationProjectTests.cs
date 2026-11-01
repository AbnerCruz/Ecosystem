using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Publicações CST-150/151/153/156/162: no copied author text in project files.</summary>
public sealed class PublicationProjectTests
{
    [Fact]
    public void ProjectRoundtripRetainsStableIdsOrderAndUnknownMetadata()
    {
        var note = new DocumentStore().Make(new DocumentInput { Path = "Ideias.md", Content = "texto confidencial" });
        var first = PublicationProject.Create("Revista", note.Id);
        var second = first.AddSource("doc_outro").MoveSource("doc_outro", 0);
        var json = PublicationProjectCodec.Serialize(second);
        var parsed = PublicationProjectCodec.Parse(json);
        Assert.Equal(second.PublicationId, parsed.PublicationId);
        Assert.Equal(new[] { "doc_outro", note.Id }, parsed.Sections.Select(s => s.Source.DocumentId));
        Assert.Equal(new[] { "doc_outro", note.Id }, parsed.Sources.Select(s => s.DocumentId));
        Assert.DoesNotContain("texto confidencial", json);
        Assert.Equal("html", Assert.Single(parsed.Formats));

        using var raw = JsonDocument.Parse(json);
        Assert.True(raw.RootElement.TryGetProperty("publicationId", out _));
        Assert.True(raw.RootElement.TryGetProperty("sections", out _));

        var extended = json.Replace("\"audience\":", "\"extraFuture\":{\"option\":true},\"audience\":", StringComparison.Ordinal);
        Assert.Contains("\"extraFuture\"", PublicationProjectCodec.Serialize(PublicationProjectCodec.Parse(extended)));
    }

    [Fact]
    public void ProjectRejectsUnsupportedVersionsAndBrokenReferencesWithoutTouchingVault()
    {
        var project = PublicationProject.Create("A");
        var json = PublicationProjectCodec.Serialize(project);
        Assert.Throws<NotSupportedException>(() => PublicationProjectCodec.Parse(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999")));
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Parse(json.Replace("\"title\": \"A\"", "\"title\": \"\"")));
        Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Parse(json.Replace("\"sources\": []", "\"sources\": [{\"documentId\":\"\"}]")));
        Assert.Throws<ArgumentException>(() => project.AddSource(""));
        Assert.Throws<InvalidOperationException>(() => project.MoveSource("missing", 0));
    }

    [Fact]
    public void HtmlResolvesCurrentNoteContentAfterEditRenameAndReportsDeletedSources()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "A.md", Content = "# Primeira\n\nTexto um" });
        var project = PublicationProject.Create("Livro <vivo>", note.Id);
        using var catalog = new PublicationSourceCatalog(store);
        var first = PublicationHtml.Export(project, catalog);
        Assert.Contains("Texto um", first.Html);
        Assert.Contains("Livro &lt;vivo&gt;", first.Html);
        Assert.Empty(first.MissingDocumentIds);

        store.Upsert(new DocumentInput { Id = note.Id, Path = "B.md", Content = "# Segunda\n\nTexto dois" });
        var newVersion = PublicationHtml.Export(project, catalog);
        Assert.Contains("Texto dois", newVersion.Html);
        Assert.DoesNotContain("Texto um", newVersion.Html);
        Assert.Empty(newVersion.MissingDocumentIds);
        Assert.DoesNotContain("file://", newVersion.Html, StringComparison.OrdinalIgnoreCase);

        store.Remove(note.Id);
        var missing = PublicationHtml.Export(project, catalog);
        Assert.Equal(new[] { note.Id }, missing.MissingDocumentIds);
        Assert.DoesNotContain("Texto dois", missing.Html);
        Assert.False(missing.IsComplete);
    }

    [Fact]
    public void HtmlEscapesAuthoredContentAndIsOfflineConstrained()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "A.md", Content = "Texto <script>alert(1)</script>" });
        using var catalog = new PublicationSourceCatalog(store);
        var html = PublicationHtml.Export(PublicationProject.Create("Teste", note.Id), catalog);
        Assert.DoesNotContain("<script>", html.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html.Html);
        Assert.Contains("default-src 'none'", html.Html);
        Assert.Contains("lang=\"pt-BR\"", html.Html);
    }
}
