using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-151/153: a Publication reads the real document on demand.
/// The source catalog must not become a second editable content store.
/// </summary>
public sealed class PublicationSourceCatalogTests
{
    [Fact]
    public void LiveReferenceReadsTheSameNoteAfterEditAndRename()
    {
        var store = new DocumentStore();
        var original = store.Upsert(new DocumentInput { Path = "Estudos/Tectônica.md", Content = "Placas convergentes." });
        using var catalog = new PublicationSourceCatalog(store);
        var source = new PublicationSourceReference(original.Id);

        var first = catalog.Read(source)!;
        Assert.Equal(original.Id, first.DocumentId);
        Assert.Equal("Estudos/Tectônica.md", first.Path);
        Assert.Equal("Placas convergentes.", first.Markdown);

        var changes = new List<PublicationSourceChange>();
        catalog.SourceChanged += (_, e) => changes.Add(e);

        var revised = store.Upsert(new DocumentInput
        {
            Id = original.Id,
            Path = "Pesquisa/Tectônica.md",
            Content = "Placas convergentes e divergentes."
        });
        var current = catalog.Read(source)!;
        Assert.Equal(revised.Id, current.DocumentId);
        Assert.Equal("Pesquisa/Tectônica.md", current.Path);
        Assert.Equal("Placas convergentes e divergentes.", current.Markdown);
        Assert.True(current.Revision > first.Revision);
        Assert.Single(changes);
        Assert.Equal(original.Id, changes[0].DocumentId);
        Assert.Equal(PublicationSourceChangeKind.Updated, changes[0].Kind);
    }

    [Fact]
    public void RemovingSourceSignalsMissingInsteadOfRecreatingOrCopyingText()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Origem.md", Content = "conteúdo autoral" });
        using var catalog = new PublicationSourceCatalog(store);
        var source = new PublicationSourceReference(note.Id);
        var changes = new List<PublicationSourceChange>();
        catalog.SourceChanged += (_, e) => changes.Add(e);

        Assert.True(store.Remove(note.Id));
        Assert.Null(catalog.Read(source));
        Assert.Equal(PublicationSourceChangeKind.Removed, Assert.Single(changes).Kind);
        Assert.Empty(store.List());
    }

    [Fact]
    public void BlockSelectorsFailExplicitlyUntilStableBlockIdentityIsImplemented()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Origem.md", Content = "# Capítulo\nTexto" });
        using var catalog = new PublicationSourceCatalog(store);
        Assert.Throws<NotSupportedException>(() =>
            catalog.Read(new PublicationSourceReference(note.Id, "#^bloco-1")));
    }

    [Fact]
    public void ResetInvalidatesAllConsumersAndDisposeDetachesNotifications()
    {
        var store = new DocumentStore();
        var catalog = new PublicationSourceCatalog(store);
        var changes = new List<PublicationSourceChange>();
        catalog.SourceChanged += (_, e) => changes.Add(e);

        store.Upsert(new DocumentInput { Path = "A.md", Content = "um" });
        store.Clear();
        Assert.Equal(PublicationSourceChangeKind.Created, changes[0].Kind);
        Assert.Equal(PublicationSourceChangeKind.Reset, changes[1].Kind);
        Assert.Null(changes[1].DocumentId);

        catalog.Dispose();
        store.Upsert(new DocumentInput { Path = "B.md", Content = "dois" });
        Assert.Equal(2, changes.Count);
    }
}
