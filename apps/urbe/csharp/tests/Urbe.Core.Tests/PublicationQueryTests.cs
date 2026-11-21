namespace Urbe.Core.Tests;

public sealed class PublicationQueryTests
{
    private static UrbeDocument Doc(string id, string path, params string[] tags) =>
        new(id, path, path, "", new Dictionary<string, string>(), tags, [], null, null, 1);

    [Fact]
    public void NewNotesWithTheTagAreOfferedButNotUsedOrDeclinedOnes()
    {
        var project = PublicationProject.Create("P", "a") with { Query = new PublicationQuery("tag", "receita") { Declined = ["c"] } };
        var notes = new[] { Doc("a", "A.md", "receita"), Doc("b", "B.md", "#Receita"), Doc("c", "C.md", "receita"), Doc("d", "D.md", "outra") };
        Assert.Equal(["b"], project.Query!.NewMatches(project, notes, _ => []).Select(n => n.Id));
    }

    [Fact]
    public void FolderAndSearchQueriesAndTheQuerySurvivesARoundTrip()
    {
        var notes = new[] { Doc("a", "Livro/A.md"), Doc("b", "Livro/Sub/B.md"), Doc("c", "Outro/C.md") };
        var folder = new PublicationQuery("folder", "Livro");
        Assert.Equal(["a", "b"], folder.NewMatches(PublicationProject.Create("P"), notes, _ => []).Select(n => n.Id));
        var search = new PublicationQuery("search", "x");
        Assert.Equal(["c"], search.NewMatches(PublicationProject.Create("P"), notes, _ => [notes[2]]).Select(n => n.Id));
        var project = PublicationProject.Create("P", "a") with { Query = folder with { Declined = ["b"] } };
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project));
        Assert.Equal("folder", back.Query!.Kind);
        Assert.Equal(["b"], back.Query.Declined);
    }

    /// <summary>CST-160: a note that stops matching (tag taken off, moved out of the folder) is told for review, never removed by itself.</summary>
    [Fact]
    public void NotesThatStopMatchingAreToldForReview()
    {
        var notes = new Dictionary<string, UrbeDocument> { ["a"] = Doc("a", "A.md", "receita"), ["b"] = Doc("b", "B.md") };
        var project = PublicationProject.Create("P", "a").AddSource("b") with { Query = new PublicationQuery("tag", "receita") };
        Assert.Equal(["b"], project.Query!.NoLongerMatching(project, notes.GetValueOrDefault, _ => []).Select(n => n.Id));
        var search = project with { Query = new PublicationQuery("search", "x") };
        Assert.Equal(["a"], search.Query!.NoLongerMatching(search, notes.GetValueOrDefault, _ => [notes["b"]]).Select(n => n.Id));
        Assert.Equal(2, project.Sections.Count);   // nothing was taken out
        var kept = project with { Query = project.Query with { Kept = ["b"] } };
        Assert.Empty(kept.Query!.NoLongerMatching(kept, notes.GetValueOrDefault, _ => []));   // kept on purpose: not asked again
        Assert.Equal(["b"], PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(kept)).Query!.Kept);
    }

    /// <summary>CST-160: order by title, by date or grouped by folder; the cover stays first and the notes are untouched.</summary>
    [Fact]
    public void SectionsAreOrderedByTitleDateOrFolderWithTheCoverFirst()
    {
        static UrbeDocument D(string id, string path, string modified) =>
            new(id, path, Path.GetFileNameWithoutExtension(path), "", new Dictionary<string, string>(), [], [], null, modified, 1);
        var notes = new Dictionary<string, UrbeDocument>
        {
            ["capa"] = D("capa", "Capa.md", "2026-01-01"), ["z"] = D("z", "B/Zebra.md", "2026-03-01"),
            ["a"] = D("a", "B/Água.md", "2026-05-01"), ["m"] = D("m", "A/Maçã.md", "2026-02-01")
        };
        var project = PublicationProject.Create("P", "capa").AddSource("z").AddSource("a").AddSource("m");
        project = project.WithCover(project.Sections[0].Id);
        string Order(PublicationProject p) => string.Join(",", p.Sections.Select(s => s.Source.DocumentId));
        Assert.Equal("capa,a,m,z", Order(project.SortedBy("titulo", notes.GetValueOrDefault)));   // Água before Maçã, accents read
        Assert.Equal("capa,m,z,a", Order(project.SortedBy("data", notes.GetValueOrDefault)));
        Assert.Equal("capa,m,a,z", Order(project.SortedBy("pasta", notes.GetValueOrDefault)));   // A/ then B/, by name inside
        Assert.Equal(Order(project.SortedBy("pasta", notes.GetValueOrDefault)), string.Join(",", project.SortedBy("pasta", notes.GetValueOrDefault).Sources.Select(s => s.DocumentId)));
        Assert.Throws<ArgumentException>(() => project.SortedBy("tamanho", notes.GetValueOrDefault));
    }
}
