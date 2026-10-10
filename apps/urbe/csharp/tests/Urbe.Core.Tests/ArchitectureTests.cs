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
    [InlineData("Urbe.UI", "Urbe.Core")]
    [InlineData("Urbe.App", "Urbe.UI")]
    public void ReferencesRespectApprovedBoundaries(string name, string dependency)
    {
        var directory = Path.Combine(Root, "src", name);
        var project = XDocument.Load(Path.Combine(directory, name + ".csproj"));
        var references = project.Descendants("ProjectReference").Select(reference =>
            Path.GetFullPath(Path.Combine(directory, reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))).ToArray();
        var expected = dependency.Length == 0 ? [] : new[] { Path.Combine(Root, "src", dependency, dependency + ".csproj") };
        Assert.Equal(expected, references);
        Assert.All(references, reference => Assert.True(File.Exists(reference)));
        // Dependencies added via native/framework packages must not leak to the RCL.
        if (name == "Urbe.UI") Assert.DoesNotContain(project.Descendants("PackageReference"), reference =>
            reference.Attribute("Include")!.Value.Contains("Maui", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NativeBuildCannotReplaceExistingAndroidInstallation()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/Urbe.App/Urbe.App.csproj"));
        Assert.Equal("app.urbe.csharp", project.Descendants("ApplicationId").Single().Value);
        Assert.Equal("2.0.0-beta.1", project.Descendants("ApplicationDisplayVersion").Single().Value);
        Assert.Equal("2000001", project.Descendants("ApplicationVersion").Single().Value);
        Assert.NotEqual("app.urbe", project.Descendants("ApplicationId").Single().Value);
        var manifest = XDocument.Load(Path.Combine(Root, "src/Urbe.App/Platforms/Android/AndroidManifest.xml"));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        Assert.Equal("false", manifest.Descendants("application").Single().Attribute(android + "allowBackup")?.Value);
        Assert.DoesNotContain(manifest.Descendants("uses-permission"), permission =>
            permission.Attribute(android + "name")!.Value.Contains("STORAGE", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeAppComposesCanonicalRazorRoot()
    {
        var native = XDocument.Load(Path.Combine(Root, "src/Urbe.App/MainPage.xaml"));
        var root = native.Descendants().Single(element => element.Name.LocalName == "RootComponent");
        Assert.Equal("{x:Type ui:UrbeRoutes}", root.Attribute("ComponentType")?.Value);
        Assert.Equal("clr-namespace:Urbe.UI;assembly=Urbe.UI", native.Root!.GetNamespaceOfPrefix("ui")!.NamespaceName);
    }
}
