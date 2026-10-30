using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-092: the asking agent learns what the consulted specialist changed in the vault.</summary>
public sealed class DelegationChangesTests
{
    [Fact]
    public void EachKindOfChangeIsSaidOnce()
    {
        var lines = AssistantDelegateTools.DescribeChanges(
        [
            new AiChange("a", null, new AiNoteSnap("Notas/Novo.md", "x")),
            new AiChange("b", new AiNoteSnap("Plano.md", "1"), new AiNoteSnap("Plano.md", "2")),
            new AiChange("b", new AiNoteSnap("Plano.md", "2"), new AiNoteSnap("Plano.md", "3")),
            new AiChange("c", new AiNoteSnap("Velho.md", "v"), new AiNoteSnap("Arquivo/Velho.md", "v")),
            new AiChange("d", new AiNoteSnap("Lixo.md", "l"), null, Trashed: true)
        ]);
        Assert.Equal(["criou Notas/Novo.md", "alterou Plano.md", "moveu Velho.md → Arquivo/Velho.md", "moveu para a lixeira Lixo.md"], lines);
        Assert.Empty(AssistantDelegateTools.DescribeChanges([]));
    }
}
