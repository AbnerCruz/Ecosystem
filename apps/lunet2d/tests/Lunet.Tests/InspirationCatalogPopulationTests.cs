using System.Text.Json;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class InspirationCatalogPopulationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-catalog-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "inspiration.json");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ExpandedCatalog_IsOfflineUniqueAndHasUsableStudyMetadata()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        var curated = catalog.All.Where(r => r.Curated).ToArray();

        Assert.Equal(131, curated.Length);
        Assert.Equal(curated.Length, curated.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(curated.Length, curated.Select(r => r.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(catalog.Genres.Count >= 20);
        Assert.True(catalog.Mechanics.Count >= 70);
        Assert.All(curated, entry =>
        {
            Assert.StartsWith("ref.", entry.Id);
            Assert.InRange(entry.Title.Length, 1, 120);
            Assert.InRange(entry.Genre.Length, 1, 80);
            Assert.InRange(entry.Perspective.Length, 1, 80);
            Assert.InRange(entry.VisualStyle.Length, 1, 80);
            Assert.InRange(entry.StudyIdea.Length, 30, 1000);
            Assert.InRange(entry.Mechanics.Length, 1, 12);
            Assert.All(entry.Mechanics, mechanic => Assert.InRange(mechanic.Length, 1, 60));
            Assert.StartsWith("https://www.youtube.com/results?search_query=", entry.Url);
            Assert.True(Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps);
        });
        Assert.False(File.Exists(CatalogPath)); // Curated data is embedded; opening it performs no IO write.
    }

    [Fact]
    public void EveryCuratedMechanicHasAnOfflineExperimentAndSearchableFacet()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        foreach (var reference in catalog.All)
        {
            foreach (var mechanic in reference.Mechanics)
            {
                Assert.StartsWith(mechanic + " — ", InspirationLibrary.SuggestExperiment(reference, mechanic));
                Assert.NotEmpty(InspirationLibrary.GuideFor(mechanic));
                Assert.Contains(catalog.SearchFacets(new InspirationFilter(Mechanics: [mechanic])),
                    entry => entry.Id == reference.Id);
            }
        }
        Assert.Equal("entrada", InspirationLibrary.GuideFor("Dash"));
        Assert.True(InspirationLibrary.TextMatches("Construção", "construcao"));
        Assert.False(File.Exists(CatalogPath));
    }

    [Fact]
    public void Search_FindsCrossGenreMechanicsAndAerialMovement()
    {
        var catalog = new InspirationLibrary(CatalogPath);

        Assert.Contains(catalog.Search(genre: "RPG de ação", mechanic: "Mira", perspective: "Top-down",
            style: "Pixel art"), r => r.Title == "CrossCode");
        Assert.Contains(catalog.Search(query: "montarias voadoras", mechanic: "Voo"),
            r => r.Title == "Terraria");
        Assert.Contains(catalog.Search(query: "voo", mechanic: "Voo"),
            r => r.Title == "Owlboy");
        Assert.Contains(catalog.Search(query: "colonia", mechanic: "IA de agentes"),
            r => r.Title == "Oxygen Not Included");
        Assert.Contains(catalog.Search(query: "programacao", genre: "Puzzle"),
            r => r.Title == "Human Resource Machine");
    }

    [Fact]
    public void ExpandingBuiltInCatalog_DoesNotDiscardNotesFromOlderSmallerCatalog()
    {
        Directory.CreateDirectory(_root);
        var prior = new InspirationLibrary(CatalogPath).All.Single(r => r.Id == "ref.celeste");
        var previousState = new
        {
            Version = 1,
            Entries = new[] { prior with { Favorite = true, Notes = "Meu estudo anterior", ProjectName = "ProjetoAntigo" } }
        };
        File.WriteAllText(CatalogPath, JsonSerializer.Serialize(previousState));
        var reopened = new InspirationLibrary(CatalogPath);

        Assert.Equal(131, reopened.All.Count(r => r.Curated));
        var saved = reopened.All.Single(r => r.Id == "ref.celeste");
        Assert.True(saved.Favorite);
        Assert.Equal("Meu estudo anterior", saved.Notes);
        Assert.Equal("ProjetoAntigo", saved.ProjectName);
        Assert.Contains(reopened.All, r => r.Id == "ref.owlboy" && r.Curated);
    }

    [Fact]
    public void CatalogFiltering_DoesNotNeedNetworkOrMutateSavedFavorites()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        catalog.SetPersonalDetails("ref.terraria", true, "Comparar controles de voo", "");
        var before = File.ReadAllText(CatalogPath);

        Assert.Contains(catalog.Search(mechanic: "Voo", favoritesOnly: true),
            r => r.Id == "ref.terraria");
        Assert.DoesNotContain(catalog.Search(mechanic: "Voo", favoritesOnly: true),
            r => r.Id == "ref.owlboy");
        Assert.Equal(before, File.ReadAllText(CatalogPath));
    }
}
