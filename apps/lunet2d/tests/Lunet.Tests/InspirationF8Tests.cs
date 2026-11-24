using Lunet.Core;

namespace Lunet.Tests;

public sealed class InspirationF8Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-f8-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "inspiration.json");

    [Fact]
    public void MultifacetSearchCombinesAlternativesAndRequiresEverySelectedMechanic()
    {
        var library = new InspirationLibrary(Catalog);
        var matches = library.SearchFacets(new InspirationFilter("exploracao",
            ["Sandbox", "Aventura"], ["Voo", "Montarias"], ["Lateral", "Top-down"], ["Pixel art"]));
        Assert.Contains(matches, e => e.Id == "ref.terraria");
        Assert.All(matches, e =>
        {
            Assert.Contains("Voo", e.Mechanics);
            Assert.Contains("Montarias", e.Mechanics);
        });
        Assert.Equal(131, library.SearchFacets(new InspirationFilter()).Count);
        Assert.False(File.Exists(Catalog));
    }

    [Fact]
    public void StableLinkSurvivesRenameDisappearanceAndReopenWithoutGuessingLegacyName()
    {
        var store = new ProjectStore(Path.Combine(_root, "projects"));
        var project = store.Create("GameA");
        var id = project.Manifest.GameId;
        var library = new InspirationLibrary(Catalog);
        library.SetPersonalDetails("ref.celeste", true, "Comparar dash", "GameA", id);
        library.SetPersonalDetails("ref.terraria", true, "Nota legada", "GameA");
        Directory.Move(project.Directory, Path.Combine(store.RootDirectory, "Moved"));
        var reopened = new InspirationLibrary(Catalog);
        var entry = reopened.All.Single(e => e.Id == "ref.celeste");
        Assert.Equal("Moved", InspirationLibrary.ResolveProject(entry, LauncherProjectCatalog.Scan(store)));
        Assert.Null(InspirationLibrary.ResolveProject(reopened.All.Single(e => e.Id == "ref.terraria"), LauncherProjectCatalog.Scan(store)));
        Directory.Delete(Path.Combine(store.RootDirectory, "Moved"), true);
        Assert.Null(InspirationLibrary.ResolveProject(entry, LauncherProjectCatalog.Scan(store)));
        Assert.Equal(id, new InspirationLibrary(Catalog).All.Single(e => e.Id == entry.Id).ProjectId);
        Assert.Equal("Comparar dash", entry.Notes);
    }

    [Fact]
    public void ComparisonAndExperimentAreLocalAndSeparateFromPersonalNotes()
    {
        var library = new InspirationLibrary(Catalog);
        var comparison = library.Compare("ref.celeste", "ref.hyperlight");
        Assert.Contains("Dash", comparison.SharedMechanics);
        Assert.Contains("Respawn", comparison.FirstOnly);
        Assert.Contains("Combate", comparison.SecondOnly);
        var first = library.All.Single(e => e.Id == "ref.celeste");
        Assert.Contains("Dash", InspirationLibrary.SuggestExperiment(first, "Dash"));
        Assert.Empty(first.Notes);
        Assert.False(File.Exists(Catalog));
    }

    [Fact]
    public void PersonalAttributionAndArchivedLinkedReferenceRemainRecoverable()
    {
        var library = new InspirationLibrary(Catalog);
        var entry = library.Add("Minha ideia", "Puzzle", "Top-down", "Pixel art", ["Lógica"],
            "Testar regras.", author: "Autor original", license: "CC BY 4.0");
        var id = Guid.NewGuid().ToString("D");
        library.SetPersonalDetails(entry.Id, true, "Minha nota", "GameA", id);
        library.Delete(entry.Id);
        var reopened = new InspirationLibrary(Catalog);
        Assert.DoesNotContain(reopened.All, e => e.Id == entry.Id);
        var archived = Assert.Single(reopened.Archived);
        Assert.Equal("Autor original", archived.Author);
        Assert.Equal("CC BY 4.0", archived.License);
        Assert.Equal(id, archived.ProjectId);
        reopened.Restore(entry.Id);
        Assert.Equal("Minha nota", new InspirationLibrary(Catalog).All.Single(e => e.Id == entry.Id).Notes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
