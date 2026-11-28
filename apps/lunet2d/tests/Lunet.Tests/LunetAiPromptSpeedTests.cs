using Lunet.Ai;

namespace Lunet.Tests;

/// <summary>Latency regression: conceptual prompts must not scan the entire editor and project.</summary>
public sealed class LunetAiPromptSpeedTests
{
    [Theory]
    [InlineData("O que é o SpriteBatch?")]
    [InlineData("Como funciona GameTime?")]
    [InlineData("Explique a diferença entre Update e Draw")]
    public void GeneralTeachingQuestion_UsesCompactContext(string prompt)
    {
        var editor = LunetAiEditorContext.Empty;
        Assert.True(LunetAiSession.IsGeneralExplanation(prompt, LunetAiMode.Teach, editor, null));
    }

    [Theory]
    [InlineData("Analise este arquivo e explique o que faz")]
    [InlineData("Corrija o erro neste arquivo")]
    [InlineData("Explique meu código")]
    [InlineData("O que há de errado no projeto?")]
    public void ProjectSpecificQuestion_KeepsRealEditorContext(string prompt)
    {
        var editor = new LunetAiEditorContext("Game.cs", null, 1, [], []);
        Assert.False(LunetAiSession.IsGeneralExplanation(prompt, LunetAiMode.Teach, editor, null));
    }

    [Fact]
    public void ProjectMap_ReusesUnchangedSymbolsAndInvalidatesEditsMovesAndDeletes()
    {
        var root = Directory.CreateTempSubdirectory("lunet-map-cache-").FullName;
        try
        {
            var project = new Lunet.Core.ProjectStore(root).Create("Map");
            project.WriteText("Coin.cs", "class Coin {}");
            var map = new LunetAiProjectMap(project);
            var before = Assert.Single(map.Symbols("Coin.cs"));
            Assert.Same(before, Assert.Single(map.Symbols("Coin.cs")));

            var file = Path.Combine(project.Directory, "Coin.cs");
            var timestamp = File.GetLastWriteTimeUtc(file);
            File.WriteAllText(file, "class Hero {}");
            File.SetLastWriteTimeUtc(file, timestamp);
            var after = Assert.Single(map.Symbols("Coin.cs"));
            Assert.Equal("Hero", after.Name);
            Assert.NotSame(before, after);

            project.Rename("Coin.cs", "Hero.cs");
            Assert.Empty(map.Symbols("Coin.cs"));
            Assert.Equal("Hero.cs", Assert.Single(map.Symbols("Hero.cs")).File);
            project.Delete("Hero.cs");
            Assert.Empty(map.Symbols("Hero.cs"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CodeChanges_NeverUseCompactContext()
    {
        var editor = new LunetAiEditorContext("Game.cs", null, 1, [], []);
        Assert.False(LunetAiSession.IsGeneralExplanation("O que é o SpriteBatch?",
            LunetAiMode.Teach, editor, null));
        Assert.False(LunetAiSession.IsGeneralExplanation("Crie NPC e um diálogo",
            LunetAiMode.Do, editor, null));
        Assert.False(LunetAiSession.IsGeneralExplanation("Explique este método",
            LunetAiMode.Teach, editor with { Selection = "void Update() {}" }, null));
        Assert.False(LunetAiSession.IsGeneralExplanation("Como usar áudio?",
            LunetAiMode.Teach, editor, ["Content/Audio/example.wav"]));
    }
}
