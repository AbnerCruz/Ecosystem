using System.Xml.Linq;

namespace Lunet.Tests;

/// <summary>Valida as regras de dependência entre projetos (ADR 0002).</summary>
public class ArchitectureTests
{
    private static readonly string Src = FindSrc();

    private static string FindSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src");
    }

    private static Dictionary<string, HashSet<string>> Graph() =>
        Directory.GetFiles(Src, "*.csproj", SearchOption.AllDirectories).ToDictionary(
            p => Path.GetFileNameWithoutExtension(p),
            p => XDocument.Load(p).Descendants("ProjectReference")
                .Select(e => Path.GetFileNameWithoutExtension(((string)e.Attribute("Include")!).Replace('\\', '/')))
                .ToHashSet());

    [Fact]
    public void Framework_DependsOnNothingInLunet() => Assert.Empty(Graph()["Lunet.Framework"]);

    [Fact]
    public void Core_DoesNotDependOnCompilerRuntimeOrAndroid() => Assert.Empty(Graph()["Lunet.Core"]);

    [Fact]
    public void Compiler_DoesNotDependOnAnyLunetProject() => Assert.Empty(Graph()["Lunet.Compiler"]);

    [Fact]
    public void Runtime_DependsOnlyOnFramework() =>
        Assert.Equal(["Lunet.Framework"], Graph()["Lunet.Runtime"]);

    [Fact]
    public void FrameworkHasNoPackageReferences()
    {
        var doc = XDocument.Load(Path.Combine(Src, "Lunet.Framework", "Lunet.Framework.csproj"));
        Assert.Empty(doc.Descendants("PackageReference"));
    }

    [Fact]
    public void NoCircularDependencies()
    {
        var graph = Graph();
        var state = new Dictionary<string, int>();
        void Visit(string node)
        {
            if (state.TryGetValue(node, out var s)) { Assert.NotEqual(1, s); return; }
            state[node] = 1;
            foreach (var next in graph.GetValueOrDefault(node) ?? []) Visit(next);
            state[node] = 2;
        }
        foreach (var node in graph.Keys) Visit(node);
    }
}
