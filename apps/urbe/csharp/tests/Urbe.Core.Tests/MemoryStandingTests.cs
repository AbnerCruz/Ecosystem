using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-088: a task the app recorded, what an agent declared, what the person said and what was checked are told apart.</summary>
public sealed class MemoryStandingTests
{
    [Fact]
    public void EachMemorySaysWhatItIsWorth()
    {
        var cfg = new AiConfig();
        var episode = AssistantMemoryEngine.ObserveFinishedRun(cfg, "V", "geral", "r1", ["Notas/Plano.md"], "Reescreva o plano")!;
        Assert.Equal("registrada pelo app; o resultado não foi conferido", AssistantMemoryEngine.Standing(episode));
        var declared = AssistantMemoryEngine.Remember(cfg, "O plano tem cinco etapas", "V", "geral", "fact", "agent", "agent", "conversation:c1");
        Assert.Equal("declarada pelo agente; não conferida", AssistantMemoryEngine.Standing(declared));
        var said = AssistantMemoryEngine.Remember(cfg, "Prefiro respostas curtas", "V", "geral", "preference", "vault", "user", "conversation:c1");
        Assert.Equal("dita pelo usuário", AssistantMemoryEngine.Standing(said));
        // the person corrects the agent's memory: it is now checked
        Assert.True(AssistantMemoryEngine.Edit(cfg, declared.Id, "O plano tem seis etapas"));
        Assert.Equal("verificada", AssistantMemoryEngine.Standing(declared));
    }
}
