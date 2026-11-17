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
}
