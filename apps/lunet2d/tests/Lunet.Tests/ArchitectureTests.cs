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
    public void Editor_DependsOnlyOnCompiler() => Assert.Equal(["Lunet.Compiler"], Graph()["Lunet.Editor"]);

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

/// <summary>Garante que a documentação de planejamento existe e o ROADMAP cobre todas as fases do spec.</summary>
public class PlanningDocsTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ROADMAP.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void SpecRoutineAndClaudeInstructionsExist()
    {
        foreach (var file in new[] { "docs/SPEC.md", "docs/DEVELOPMENT.md", "docs/audits/TEMPLATE.md", "CLAUDE.md", "tools/roadmap-status.sh" })
            Assert.True(File.Exists(Path.Combine(Root(), file)), file);
    }

    [Fact]
    public void RoadmapHasEveryPhaseFrom0To15AndAnAuditItemPerPhase()
    {
        var roadmap = File.ReadAllText(Path.Combine(Root(), "ROADMAP.md"));
        for (var phase = 0; phase <= 15; phase++)
            Assert.Matches($@"(?m)^## Fase {phase} — ", roadmap);
        for (var phase = 1; phase <= 14; phase++)
            Assert.Contains($"Auditoria de fechamento da Fase {phase} registrada", roadmap);
    }

    [Fact]
    public void SpecPhasesMatchRoadmapPhases()
    {
        var spec = File.ReadAllText(Path.Combine(Root(), "docs/SPEC.md"));
        var roadmap = File.ReadAllText(Path.Combine(Root(), "ROADMAP.md"));
        var specPhases = System.Text.RegularExpressions.Regex.Matches(spec, @"(?m)^### Fase (\d+) ").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(16, specPhases.Length);
        foreach (var phase in specPhases)
            Assert.Matches($@"(?m)^## Fase {phase} — ", roadmap);
    }
}

/// <summary>O projeto Android só compila no CI; este teste pega erros de sintaxe antes, em qualquer máquina.</summary>
public class AndroidSourceSyntaxTests
{
    [Fact]
    public void AllAndroidSourcesParseWithoutSyntaxErrors()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Lunet.Android"))) dir = dir.Parent;
        var files = Directory.GetFiles(Path.Combine(dir!.FullName, "src", "Lunet.Android"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(files);

        var problems = new List<string>();
        foreach (var file in files)
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file, cancellationToken: TestContext.Current.CancellationToken);
            foreach (var d in tree.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                problems.Add($"{Path.GetFileName(file)}({d.Location.GetLineSpan().StartLinePosition.Line + 1}): {d.GetMessage()}");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
