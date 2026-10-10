using Lunet.Core;

namespace Lunet.Tests;

public class AdvancedTemplateTests
{
    [Fact]
    public void ExistingTemplateValuesRemainCompatible()
    {
        Assert.Equal(0, (int)ProjectTemplate.Blank);
        Assert.Equal(1, (int)ProjectTemplate.CoinCatcher);
        Assert.Equal(2, (int)ProjectTemplate.Lab);
        Assert.Equal(3, (int)ProjectTemplate.Animation);
        Assert.Equal(4, (int)ProjectTemplate.Particles);
    }

    [Theory]
    [InlineData(ProjectTemplate.Animation, "AnimationDemo", "animacao")]
    [InlineData(ProjectTemplate.Particles, "ParticleDemo", "particulas")]
    public void CreateWritesTheExecutableGuideAndPreservesExistingProjects(ProjectTemplate template, string name, string guide)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/guides/" + guide + ".md"))) root = root.Parent;
        Assert.NotNull(root);
        var code = File.ReadAllText(Path.Combine(root!.FullName, "docs/guides/" + guide + ".md")).Split("```csharp\n")[1].Split("```")[0];
        var directory = Path.Combine(Path.GetTempPath(), "lunet-advanced-templates-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProjectStore(directory);
            var existing = store.Create("Existing");
            existing.WriteText("Game.cs", "// conteúdo do usuário");
            existing.WriteText("Content/custom.txt", "asset do usuário");
            var manifestBytes = File.ReadAllBytes(Path.Combine(existing.Directory, "lunet.json"));
            var project = store.Create(name, template);
            Assert.Equal(code.Trim(), project.ReadText("Game.cs").Trim());
            Assert.True(Directory.Exists(Path.Combine(project.Directory, "Content")));
            Assert.Equal("// conteúdo do usuário", store.Open("Existing").ReadText("Game.cs"));
            Assert.Equal("asset do usuário", store.Open("Existing").ReadText("Content/custom.txt"));
            Assert.Equal(manifestBytes, File.ReadAllBytes(Path.Combine(existing.Directory, "lunet.json")));
            Assert.Throws<ProjectException>(() => store.Create(name, template));
            Assert.Equal(code.Trim(), store.Open(name).ReadText("Game.cs").Trim());
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var compiled = compiler.Compile(name, [new Lunet.Compiler.SourceFile("Game.cs", project.ReadText("Game.cs"))]);
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
