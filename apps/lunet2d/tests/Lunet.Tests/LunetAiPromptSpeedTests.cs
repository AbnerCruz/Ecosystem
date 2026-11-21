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
        var editor = new LunetAiEditorContext("Game.cs", null, 42, [], []);
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
    public void CodeChanges_NeverUseCompactContext()
    {
        var editor = new LunetAiEditorContext("Game.cs", null, 1, [], []);
        Assert.False(LunetAiSession.IsGeneralExplanation("Crie NPC e um diálogo",
            LunetAiMode.Do, editor, null));
        Assert.False(LunetAiSession.IsGeneralExplanation("Explique este método",
            LunetAiMode.Teach, editor with { Selection = "void Update() {}" }, null));
        Assert.False(LunetAiSession.IsGeneralExplanation("Como usar áudio?",
            LunetAiMode.Teach, editor, ["Content/Audio/example.wav"]));
    }
}
