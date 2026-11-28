using Lunet.Compiler;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class ProjectImportsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PresetIsRealCSharpWithSiblingBinding(bool broad)
    {
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("imports_test", [
            new SourceFile(ProjectImports.Path, ProjectImports.Source(broad)),
            new SourceFile("Game.cs", "public sealed class TestGame : Game { }"),
            new SourceFile("Player.cs", "public sealed class Player { public Vector2 Position; public List<int> Values = new(); }")
        ]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        var removed = compiler.Compile("imports_test", [
            new SourceFile("Game.cs", "public sealed class TestGame : Game { }"),
            new SourceFile("Player.cs", "public sealed class Player { public Vector2 Position; }")
        ]);
        Assert.False(removed.Success);
    }

    [Fact]
    public void DefaultPresetIncludesEveryApprovedNamespaceAndContext()
    {
        string[] expected = [
            "System", "System.Numerics", "System.Collections.Generic", "System.Linq",
            "System.Text", "System.Text.Json", "System.IO", "System.Threading", "System.Threading.Tasks",
            "System.Diagnostics", "System.Reflection", "System.Collections.Concurrent", "System.Text.RegularExpressions",
            "Lunet", "Lunet.Graphics", "Lunet.Input", "Lunet.Content", "Lunet.Audio", "Lunet.Physics",
            "Lunet.Scenes", "Lunet.UI", "Lunet.Pathfinding", "Lunet.Storage", "Lunet.Networking"
        ];
        var lines = ProjectImports.Source().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected.Select(n => "global using " + n + ";").Append("global using static Lunet.Context;"), lines);
        Assert.Equal(8, ProjectImports.Source(false).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(ProjectImports.Source(true), ProjectImports.Source());
    }

    [Fact]
    public void DefaultContextBindsInIndependentClassAndCanBeRemoved()
    {
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        const string player = "public sealed class Player { public object[] Current() => new object[] { Graphics, Content, Input, Audio, Save, Services, Timers, Log, Dispatcher }; }";
        var source = ProjectImports.Source();
        var result = compiler.Compile("default_context", [
            new SourceFile(ProjectImports.Path, source),
            new SourceFile("Game.cs", "public sealed class TestGame : Game { }"),
            new SourceFile("Player.cs", player)
        ]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        var removed = compiler.Compile("default_context", [
            new SourceFile(ProjectImports.Path, source.Replace("global using static Lunet.Context;\n", "")),
            new SourceFile("Game.cs", "public sealed class TestGame : Game { }"),
            new SourceFile("Player.cs", player)
        ]);
        Assert.False(removed.Success);
    }

    [Fact]
    public void ExistingProjectImportsAreNeverRewrittenOnOpen()
    {
        string root = Path.Combine(Path.GetTempPath(), "lunet-imports-preserve-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProjectStore(root);
            var project = store.Create("Existing");
            const string custom = "global using Lunet;\n// custom\n";
            project.WriteText(ProjectImports.Path, custom);
            Assert.Equal(custom, store.Open(project.Name).ReadText(ProjectImports.Path));
            project.Delete(ProjectImports.Path);
            Assert.DoesNotContain(ProjectImports.Path, store.Open(project.Name).ListFiles());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void LegacyPolicyRemainsLegacy()
    {
        var manifest = ProjectManifest.Parse("""{"name":"Legacy"}""");
        Assert.Equal(0, manifest.SdkImportsVersion);
        Assert.DoesNotContain("sdkImportsVersion", manifest.ToJson());
    }

    [Fact]
    public void AllNewTemplatesCompileWithBothPresets()
    {
        string root = Path.Combine(Path.GetTempPath(), "lunet-imports-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProjectStore(root);
            var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            foreach (var template in Enum.GetValues<ProjectTemplate>())
            {
                var project = store.Create("Preset" + template, template);
                Assert.Equal(ProjectImports.Source(true), project.ReadText(ProjectImports.Path));
                var initial = compiler.Compile(project.Name, project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList());
                Assert.True(initial.Success, template + " / default: " + string.Join("\n", initial.Diagnostics));
                foreach (bool broad in new[] { false, true })
                {
                    project.WriteText(ProjectImports.Path, ProjectImports.Source(broad));
                    var result = compiler.Compile(project.Name, project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList());
                    Assert.True(result.Success, template + " / " + broad + ": " + string.Join("\n", result.Diagnostics));
                }
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
