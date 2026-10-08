using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class EditorReferenceSessionTests
{
    [Fact]
    public void ReferencesAreSnapshotsAndNeverChangeTheDocumentOrItsRevision()
    {
        var documents = new DocumentStore();
        var source = documents.Upsert(new DocumentInput { Path = "Notas/A.md", Content = "Original\r\n" });
        var panel = new EditorReferenceSession();
        var reference = panel.PinDocument(source);
        Assert.Same(reference, panel.PinDocument(source));
        Assert.Single(panel.Entries);
        Assert.Equal(source, documents.Get(source.Id));

        var changed = documents.Upsert(new DocumentInput { Id = source.Id, Path = source.Path, Content = "Alterado" });
        Assert.Equal("Original\r\n", reference.Markdown);
        Assert.Equal("Alterado", changed.Content);
        Assert.Equal(source.Id, reference.DocumentId);
        Assert.Equal(source.Path, reference.Path);
        panel.PinDocument(changed);
        Assert.Equal(2, panel.Entries.Count);
    }

    [Fact]
    public void BlockReferenceCanBeTakenFromNonCanonicalMarkdownWithoutRewritingIt()
    {
        var documents = new DocumentStore();
        var source = documents.Upsert(new DocumentInput { Path = "A.md", Content = "# Título\r\n\r\nTexto **forte**." });
        var panel = new EditorReferenceSession();
        var reference = panel.PinBlock(source, 1);
        Assert.NotNull(reference);
        Assert.True(reference.IsExcerpt);
        Assert.Equal("Texto **forte**.\n", reference.Markdown);
        Assert.Equal(source, documents.Get(source.Id));
        Assert.Null(panel.PinBlock(source, -1));
        Assert.Null(panel.PinBlock(source, 2));
        Assert.Single(panel.Entries);
    }

    [Fact]
    public void RemovingOneReferenceKeepsOthersAndClearStartsANewSession()
    {
        var documents = new DocumentStore();
        var panel = new EditorReferenceSession();
        var a = panel.PinDocument(documents.Upsert(new DocumentInput { Path = "A.md", Content = "A" }));
        var b = panel.PinDocument(documents.Upsert(new DocumentInput { Path = "B.md", Content = "B" }));
        var snapshot = panel.Entries;
        Assert.True(panel.Remove(a.Id));
        Assert.False(panel.Remove(a.Id));
        Assert.Equal(b, Assert.Single(panel.Entries));
        Assert.Equal(2, snapshot.Count);
        panel.Clear();
        Assert.Empty(panel.Entries);
        Assert.NotNull(documents.Get(a.DocumentId));
    }
}
