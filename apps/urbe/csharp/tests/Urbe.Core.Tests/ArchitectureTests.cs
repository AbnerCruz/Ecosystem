using System.Xml.Linq;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class ArchitectureTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Urbe.Portable.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Execute os testes dentro do checkout do Urbe.");
    }

    [Fact]
    public void ProductIdentityMatchesCanonicalManifest()
    {
        var manifest = Path.GetFullPath(Path.Combine(Root, "../../..", "ecosystem.json"));
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
        Assert.Equal("Urbe", json.RootElement.GetProperty("components").GetProperty(ProductIdentity.Id).GetProperty("name").GetString());
        Assert.Equal("Urbe", ProductIdentity.DisplayName);
    }

    [Fact]
    public void CoreRunsWithoutUiNativeOrOtherProducts()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/Urbe.Core/Urbe.Core.csproj"));
        Assert.Empty(project.Descendants("ProjectReference"));
        var packages = project.Descendants("PackageReference")
            .Select(reference => new
            {
                Name = reference.Attribute("Include")?.Value
                    ?? throw new InvalidDataException("PackageReference sem Include."),
                Version = reference.Attribute("Version")?.Value
                    ?? throw new InvalidDataException("PackageReference sem Version.")
            })
            .ToArray();
        Assert.Equal(
            new[]
            {
                new { Name = "CSharpMath", Version = "[1.0.0-pre.2]" },
                new { Name = "CSharpMath.Rendering", Version = "[1.0.0-pre.2]" }
            },
            packages);
        Assert.Empty(project.Descendants("FrameworkReference"));
        Assert.DoesNotContain(typeof(ProductIdentity).Assembly.GetReferencedAssemblies(),
            reference => reference.Name!.StartsWith("Microsoft.Maui", StringComparison.Ordinal) ||
                         reference.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Urbe.Core", "")]
    [InlineData("Urbe.Client", "Urbe.Core")]
    [InlineData("Urbe.Desktop", "Urbe.Client")]
    [InlineData("Urbe.Android", "Urbe.Client")]
    public void ReferencesRespectApprovedBoundaries(string name, string dependency)
    {
        var directory = Path.Combine(Root, "src", name);
        var project = XDocument.Load(Path.Combine(directory, name + ".csproj"));
        var references = project.Descendants("ProjectReference").Select(reference =>
            Path.GetFullPath(Path.Combine(directory, reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))).ToArray();
        var expected = dependency.Length == 0 ? [] : new[] { Path.Combine(Root, "src", dependency, dependency + ".csproj") };
        Assert.Equal(expected, references);
        Assert.All(references, reference => Assert.True(File.Exists(reference)));
    }

    /// <summary>
    /// ADD-0028 (DEC-0051): the visual simulation (world, city, life, decorations, building looks) left the product and
    /// the frozen Blazor shell with it. It stays only in the branch urbe-simulacao-congelada; nothing of it comes back
    /// here without a new order of the owner.
    /// </summary>
    [Fact]
    public void TheSimulationIsNotInTheProduct()
    {
        foreach (var gone in new[] { "Urbe.UI", "Urbe.App", "Urbe.Web" })
            Assert.False(Directory.Exists(Path.Combine(Root, "src", gone)), "src/" + gone + " voltou");
        string[] simulation = ["LegacyCity", "LegacyWorld", "LegacyTile", "LegacyChunk", "LegacyVillager", "LegacyFauna", "LegacyVegetation",
            "LegacyTerrain", "LegacyTexturePack", "LegacyGroundStyle", "TownKids", "TownTrades", "WorldPrefs", "WorldDecoration", "WorldProjection", "LinkFocus"];
        var types = typeof(ProductIdentity).Assembly.GetTypes().Select(t => t.Name);
        Assert.DoesNotContain(types, t => simulation.Any(s => t.StartsWith(s, StringComparison.Ordinal)));
        Assert.False(Directory.Exists(Path.Combine(Root, "src", "Urbe.Client", "World")), "src/Urbe.Client/World voltou");
    }
}
