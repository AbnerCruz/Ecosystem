using Hub.Core;

namespace Hub.Tests;

public class EcosystemReaderTests
{
    // Fixtures com nomes neutros: o Hub descobre os Products nos dados e o código não nomeia nenhum deles (NN-002).
    const string Json = """
    {
      "components": {
        "alpha": { "name": "Alpha", "type": "product", "status": "active",
                   "version": { "authority": "version-file", "file": "apps/alpha/VERSION" },
                   "source": { "repository": "https://example.invalid/alpha" } },
        "beta":  { "name": "Beta", "type": "product", "status": "active", "publicUrl": "https://example.invalid/beta/",
                   "version": { "authority": "version-file", "file": "apps/beta/package.json" } },
        "hub":   { "name": "Hub", "type": "product", "status": "active" },
        "tool1": { "name": "Tool", "type": "tool", "status": "active" }
      }
    }
    """;

    static string? Files(string path) => path switch
    {
        "apps/alpha/VERSION" => "1.2.3\n",
        "apps/beta/package.json" => """{ "name": "beta", "version": "4.5.6-beta" }""",
        _ => null,
    };

    [Fact]
    public void ListsOnlyProductsExceptTheHubItself()
    {
        var ids = EcosystemReader.ReadProducts(Json, Files).Select(p => p.Id).ToArray();
        Assert.Equal(["alpha", "beta"], ids);
    }

    [Fact]
    public void ReadsFieldsWithTheirSource()
    {
        var alpha = EcosystemReader.ReadProducts(Json, Files).Single(p => p.Id == "alpha");
        Assert.Equal("Alpha", alpha.Name.Value);
        Assert.Equal("active", alpha.Status.Value);
        Assert.Equal("https://example.invalid/alpha", alpha.Repository.Value);
        Assert.Equal("ecosystem.json#components.alpha.name", alpha.Name.Source);
        Assert.All(new[] { alpha.Name, alpha.Type, alpha.Status, alpha.Version }, d => Assert.Equal(Availability.Derived, d.Availability));
    }

    [Fact]
    public void VersionComesFromTheDeclaredFile()
    {
        var all = EcosystemReader.ReadProducts(Json, Files);
        Assert.Equal("1.2.3", all.Single(p => p.Id == "alpha").Version.Value);
        var beta = all.Single(p => p.Id == "beta");
        Assert.Equal("4.5.6-beta", beta.Version.Value);
        Assert.Equal("apps/beta/package.json", beta.Version.Source);
    }

    [Fact]
    public void MissingFieldsAreNotAvailableNeverInvented()
    {
        var alpha = EcosystemReader.ReadProducts(Json, Files).Single(p => p.Id == "alpha");
        Assert.Equal(Availability.NotAvailable, alpha.PublicUrl.Availability);
        Assert.Null(alpha.PublicUrl.Value);

        var noFiles = EcosystemReader.ReadProducts(Json, _ => null).Single(p => p.Id == "alpha");
        Assert.Equal(Availability.NotAvailable, noFiles.Version.Availability);
        Assert.Null(noFiles.Version.Value);
    }

    [Fact]
    public void VersionFileThatThrowsOrIsMalformedIsNotAvailable()
    {
        var throwing = EcosystemReader.ReadProducts(Json, _ => throw new IOException("sem acesso")).Single(p => p.Id == "beta");
        Assert.Equal(Availability.NotAvailable, throwing.Version.Availability);
        var malformed = EcosystemReader.ReadProducts(Json, _ => "{ não é json").Single(p => p.Id == "beta");
        Assert.Equal(Availability.NotAvailable, malformed.Version.Availability);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("[]")]
    [InlineData("""{ "components": [] }""")]
    public void InvalidSourceYieldsNoProductsAndNoException(string json) =>
        Assert.Empty(EcosystemReader.ReadProducts(json, Files));

    [Fact]
    public void CacheRoundTripComesBackAsStale()
    {
        var fresh = EcosystemReader.ReadProducts(Json, Files);
        var cached = ProductCache.Load(ProductCache.Serialize(fresh));
        Assert.Equal(fresh.Select(p => p.Id), cached.Select(p => p.Id));
        var alpha = cached.Single(p => p.Id == "alpha");
        Assert.Equal(Availability.Stale, alpha.Name.Availability);
        Assert.Equal("Alpha", alpha.Name.Value);
        Assert.Equal(Availability.NotAvailable, alpha.PublicUrl.Availability); // o que não existia continua não disponível
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ quebrado")]
    public void BrokenCacheIsEmptyNotAnException(string? cached) => Assert.Empty(ProductCache.Load(cached));

    [Fact]
    public void RealEcosystemFileIsReadableAndEveryProductHasANameAndVersion()
    {
        string? root = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "MANIFEST.md")) && File.Exists(Path.Combine(d.FullName, "ecosystem.json"))) { root = d.FullName; break; }
        Assert.NotNull(root);

        var products = EcosystemReader.ReadProducts(
            File.ReadAllText(Path.Combine(root!, "ecosystem.json")),
            rel => { var p = Path.Combine(root!, rel); return File.Exists(p) ? File.ReadAllText(p) : null; });

        Assert.NotEmpty(products);
        Assert.DoesNotContain(products, p => p.Id == "hub");
        Assert.All(products, p =>
        {
            Assert.Equal(Availability.Derived, p.Name.Availability);
            Assert.Equal(Availability.Derived, p.Version.Availability);
        });
    }
}
