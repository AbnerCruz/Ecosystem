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
