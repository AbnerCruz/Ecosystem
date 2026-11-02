using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PublicationFragmentTests
{
    [Fact]
    public void StableBlockReferenceSurvivesMovesAndNeverFallsBackToTheWholeNote()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "A.md", Content = "# Título\n\nTrecho escolhido ^trecho\n\nFORA DO TRECHO" });
        using var catalog = new PublicationSourceCatalog(store);
        var source = new PublicationSourceReference(note.Id, "^trecho");
        Assert.Contains("Trecho escolhido", catalog.Read(source)!.Markdown);
        Assert.DoesNotContain("FORA DO TRECHO", catalog.Read(source)!.Markdown);
        store.Upsert(new DocumentInput { Id = note.Id, Path = "B.md", Content = "Outra posição\n\nTrecho revisado ^trecho" });
        Assert.Contains("Trecho revisado", catalog.Read(source)!.Markdown);
        store.Upsert(new DocumentInput { Id = note.Id, Path = "B.md", Content = "Sem marcador" });
        Assert.Null(catalog.Read(source));
    }

    [Fact]
    public void IndependentPlacementsReorderAndRemoveWithoutLosingSharedNoteIdentity()
    {
        var project = PublicationProject.Create("Livro").AddSource(new PublicationSourceReference("doc_a", "^um")).AddSource(new PublicationSourceReference("doc_a", "^dois"));
        Assert.Equal(2, project.Sources.Count);
        var second = project.Sections[1].Id;
        project = project.MovePlacement(second, 0);
        Assert.Equal("^dois", project.Sections[0].Source.Fragment);
        project = project.RemovePlacement(second);
        Assert.Equal("^um", Assert.Single(project.Sources).Fragment);
        Assert.Equal("^um", Assert.Single(PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project)).Sections).Source.Fragment);
    }
}
