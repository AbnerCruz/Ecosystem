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
}
