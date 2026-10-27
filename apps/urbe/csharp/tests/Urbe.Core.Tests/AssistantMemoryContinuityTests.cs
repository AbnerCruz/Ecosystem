using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>UC-87 regression-first: durable user learning and precise, scoped recall.</summary>
public sealed class AssistantMemoryContinuityTests
{
    [Fact]
    public void StableUserStatementsAreLearnedWithoutAnExtraModelCall()
    {
        var config = new AiConfig();
        var learned = AssistantMemoryEngine.CaptureUserStatements(config, "Livros", "escritor",
            "Prefiro respostas curtas. Meu objetivo é concluir a apostila.", "conversation:turn-1");

        Assert.Equal(2, learned.Count);
        Assert.Contains(learned, x => x.Kind == "preference" && x.Text.Contains("respostas curtas"));
        Assert.Contains(learned, x => x.Kind == "goal" && x.Text.Contains("concluir a apostila"));
        Assert.All(learned, x =>
        {
            Assert.Equal("agent", x.Scope);
            Assert.Equal("escritor", x.AgentId);
            Assert.Equal("Livros", x.Vault);
            Assert.Equal("user", x.Origin);
            Assert.Equal("conversation:turn-1", x.SourceReference);
        });

        var store = new AssistantStore(new MemoryAssistantStorage());
        store.SetConfigVerified(config);
        var reopened = store.GetConfig();
        Assert.Equal(2, AssistantMemoryEngine.Visible(reopened, "Livros", "escritor").Count);
        Assert.Empty(AssistantMemoryEngine.Visible(reopened, "Livros", "pesquisador"));
        Assert.Empty(AssistantMemoryEngine.Visible(reopened, "Outro", "escritor"));
        Assert.Empty(AssistantMemoryEngine.CaptureUserStatements(reopened, "Livros", "escritor",
            "Prefiro respostas curtas. Meu objetivo é concluir a apostila.", "conversation:turn-1"));
    }

    [Theory]
    [InlineData("Você pode escrever uma apostila?")]
    [InlineData("Prefiro respostas curtas?")]
    [InlineData("Não prefiro respostas curtas.")]
    [InlineData("Faça uma nota sobre meu objetivo.")]
    [InlineData("A senha é supersecreta.")]
    [InlineData("Prefiro senha: credencial123")]
    [InlineData("```\nPrefiro texto curto\n```")]
    public void AmbiguousOrSensitiveTurnsNeverCreateMemories(string request)
    {
        var config = new AiConfig();
        Assert.Empty(AssistantMemoryEngine.CaptureUserStatements(config, "Vault", "agente", request, "conversation:c1"));
        Assert.Empty(config.MemoryRecords);
    }

    [Fact]
    public void QueryVocabularyVariationRetrievesRelevantMemoryButNotOtherAgent()
    {
        var config = new AiConfig();
        var dog = AssistantMemoryEngine.Remember(config, "Meu cachorro se chama Rex", "Vault", "a", scope: "agent");
        AssistantMemoryEngine.Remember(config, "O jogo tem personagens", "Vault", "a", scope: "agent");
        AssistantMemoryEngine.Remember(config, "Meu cachorro se chama Fido", "Vault", "b", scope: "agent");

        var matches = AssistantMemoryEngine.Search(config, "Vault", "a", "Qual é o nome do meu pet?", 6);
        Assert.Contains(matches, m => m.Memory.Id == dog.Id);
        Assert.DoesNotContain(matches, m => m.Memory.AgentId == "b");
        Assert.DoesNotContain(matches, m => m.Memory.Text.Contains("jogo"));
    }

    [Fact]
    public void MemoryWritesAreNotReadOnlyOperations()
    {
        var reader = AssistantAgent.Agents.Single(x => x.Id == "pesquisador");
        var writer = AssistantAgent.Agents.Single(x => x.Id == "escritor");
        Assert.False(AssistantAgent.AllowTool(reader, "readonly", "memory-write"));
        Assert.False(AssistantAgent.AllowTool(writer, "readonly", "memory-write"));
        Assert.True(AssistantAgent.AllowTool(writer, "ask", "memory-write"));
        Assert.False(AssistantAgent.NeedsApproval("memory-write", "ask", runAllow: false) == false);
    }
}
