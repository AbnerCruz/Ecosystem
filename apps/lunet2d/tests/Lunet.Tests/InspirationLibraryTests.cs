using Lunet.Core;

namespace Lunet.Tests;

public sealed class InspirationLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-explore-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "inspiration.json");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Catalog_SeedsUsableReferencesWithoutNetworkOrProject()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        Assert.True(catalog.All.Count >= 10);
        Assert.Contains(catalog.All, r => r.Id == "ref.celeste" && r.Curated);
        Assert.Contains(catalog.Genres, g => g == "Puzzle");
        Assert.Contains(catalog.Mechanics, m => m == "Construção");
        Assert.False(File.Exists(CatalogPath)); // Reading the built-in catalog never writes or calls a network.
    }

    [Fact]
    public void Search_CombinesFacetsAndRemovesDiacriticsInQuery()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        var matches = catalog.Search(query: "simulacao", genre: "Simulação", mechanic: "Construção",
            perspective: "Top-down");
        Assert.Contains(matches, r => r.Title == "RimWorld");
        Assert.DoesNotContain(matches, r => r.Title == "Factorio");
        Assert.Empty(catalog.Search(genre: "Puzzle", mechanic: "Autoataque"));
        Assert.Contains(catalog.Search(query: "otimizacao"), r => r.Title == "Mini Metro");
    }

    [Fact]
    public void PersonalReferencesAndCuratedNotes_RoundTripAcrossRestart()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        var custom = catalog.Add("Asas mecânicas", "Aventura", "Top-down", "Pixel art",
            ["Montarias", "Voo", "Exploração"], "Testar pouso e altitude.", "https://example.org/referencia");
        catalog.SetPersonalDetails(custom.Id, true, "Controle da câmera no voo.", "MeuJogo");
        catalog.SetPersonalDetails("ref.celeste", true, "Estudar a curva do dash.", "");
        var reopened = new InspirationLibrary(CatalogPath);
        var reference = Assert.Single(reopened.Search(query: "asas", favoritesOnly: true));
        Assert.Equal(custom.Id, reference.Id);
        Assert.Equal("MeuJogo", reference.ProjectName);
        Assert.Contains("câmera", reference.Notes);
        Assert.Contains(reopened.Search(query: "curva dash", favoritesOnly: true), e => e.Id == "ref.celeste");

        var modified = reopened.Update(reference with { StudyIdea = "Rever controle e gravidade." });
        Assert.Equal("Rever controle e gravidade.", modified.StudyIdea);
        Assert.Equal(modified.StudyIdea, new InspirationLibrary(CatalogPath)
            .All.Single(r => r.Id == custom.Id).StudyIdea);
        reopened.Delete(custom.Id);
        Assert.DoesNotContain(new InspirationLibrary(CatalogPath).All, e => e.Id == custom.Id);
        Assert.Contains(new InspirationLibrary(CatalogPath).All, e => e.Id == "ref.celeste" && e.Favorite);
    }

    [Fact]
    public void CuratedReferences_AllowPersonalNotesButNotDeletionOrMetadataOverwrite()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        var sample = catalog.All.Single(r => r.Id == "ref.celeste");
        Assert.Throws<InvalidOperationException>(() => catalog.Delete(sample.Id));
        Assert.Throws<InvalidOperationException>(() => catalog.Update(sample with { Title = "Fraudulento" }));
        catalog.SetPersonalDetails(sample.Id, true, "Minha anotação", "");
        Assert.Equal("Celeste", new InspirationLibrary(CatalogPath).All.Single(r => r.Id == sample.Id).Title);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///data/private")]
    public void UnsafeLinks_AreRejectedWithoutChangingDisk(string url)
    {
        var catalog = new InspirationLibrary(CatalogPath);
        Assert.Throws<ArgumentException>(() => catalog.Add("Unsafe", "Puzzle", "Top-down",
            "Pixel art", ["Lógica"], "Nada", url));
        Assert.False(File.Exists(CatalogPath));
    }

    [Theory]
    [InlineData("Notes", 6001)]
    [InlineData("ProjectName", 81)]
    public void InvalidSavedFields_AreReportedWithoutOverwritingPersonalData(string field, int length)
    {
        Directory.CreateDirectory(_root);
        var catalog = new InspirationLibrary(CatalogPath);
        catalog.SetPersonalDetails("ref.celeste", true, "Minha nota", "MeuJogo");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(CatalogPath))!;
        var entry = document["Entries"]!.AsArray()
            .Single(e => e!["Id"]!.GetValue<string>() == "ref.celeste")!;
        entry[field] = new string('x', length);
        var original = document.ToJsonString();
        File.WriteAllText(CatalogPath, original);

        var error = Assert.Throws<InvalidDataException>(() => new InspirationLibrary(CatalogPath));
        Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Equal(original, File.ReadAllText(CatalogPath));
    }

    [Fact]
    public void InvalidJson_DoesNotGetReplacedOrSilentlyReinitialized()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(CatalogPath, "{broken-json");
        Assert.Throws<InvalidDataException>(() => new InspirationLibrary(CatalogPath));
        Assert.Equal("{broken-json", File.ReadAllText(CatalogPath));
    }

    [Fact]
    public void RetiredCuratedReference_PreservesPersonalNotesAcrossCatalogUpgradeAndSave()
    {
        // Simulate a reference removed from a newer built-in catalog.
        var retired = new InspirationReference("ref.retired", "Retired game", "Puzzle",
            "Top-down", "Pixel art", ["Lógica"], "Estudar regras.", Notes: "Nota insubstituível",
            Favorite: true, Curated: true);
        Directory.CreateDirectory(_root);
        var saved = System.Text.Json.JsonSerializer.Serialize(new
        {
            Version = 1,
            Entries = new[] { retired }
        });
        File.WriteAllText(CatalogPath, saved);

        var library = new InspirationLibrary(CatalogPath);
        var preserved = Assert.Single(library.All, r => r.Id == "ref.retired");
        Assert.Equal("Nota insubstituível", preserved.Notes);
        Assert.True(preserved.Favorite);
        Assert.True(preserved.Curated);

        library.SetPersonalDetails("ref.retired", true, "Nota atualizada", "JogoNovo");
        var reopened = new InspirationLibrary(CatalogPath);
        Assert.Equal("Nota atualizada", reopened.All.Single(r => r.Id == "ref.retired").Notes);
        Assert.Equal("JogoNovo", reopened.All.Single(r => r.Id == "ref.retired").ProjectName);
        Assert.Contains(reopened.All, r => r.Id == "ref.celeste");
    }

    [Fact]
    public void SaveRejectsInvalidDetailsWithoutChangingExistingState()
    {
        var catalog = new InspirationLibrary(CatalogPath);
        var added = catalog.Add("Teste", "Puzzle", "Top-down", "Minimalista",
            ["Lógica"], "Estudar movimentos.");
        var previous = File.ReadAllText(CatalogPath);
        Assert.Throws<ArgumentException>(() => catalog.SetPersonalDetails(added.Id, true, new string('x', 6001), ""));
        Assert.Equal(previous, File.ReadAllText(CatalogPath));
        Assert.False(catalog.All.Single(r => r.Id == added.Id).Favorite);
    }
}
