using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>Memory P1 (owner, PR #952): the agent learns from what the person says across conversations, not only logs runs.</summary>
public sealed class MemoryLearningTests
{
    private static IReadOnlyList<AiMemoryRecord> Say(AiConfig cfg, string text, string conversation) =>
        AssistantMemoryEngine.CaptureUserStatements(cfg, "v", "alice", text, "conversation:" + conversation);

    private static List<AiMemoryRecord> Active(AiConfig cfg) => cfg.MemoryRecords.Where(m => m.State == "active").ToList();

    [Fact]
    public void PreferencesDecisionsAndProceduresAreLearned()
    {
        var cfg = new AiConfig();
        Say(cfg, "Gosto de resumos curtos. Vou usar ABNT nas referências. Sempre que eu pedir um resumo, coloque os termos em negrito.", "c1");
        Assert.Equal(["preference", "decision", "procedure"], Active(cfg).Select(m => m.Kind));
        Assert.All(Active(cfg), m => Assert.Equal("conversation:c1", m.SourceReference));
    }

    [Fact]
    public void ANewerOppositeStatementSupersedesTheOlderAndKeepsIt()
    {
        var cfg = new AiConfig();
        var old = Say(cfg, "Gosto de strogonoff.", "c1").Single();
        var now = Say(cfg, "Não gosto de strogonoff.", "c2").Single();
        Assert.Equal("superseded", old.State);
        Assert.NotNull(old.ValidUntil);
        Assert.Contains(old.Id, now.RelatedMemoryIds);
        Assert.Equal(["Não gosto de strogonoff."], Active(cfg).Select(m => m.Text));
        Assert.Empty(AssistantMemoryEngine.PotentialConflicts(cfg, "v", "alice"));
    }

    [Fact]
    public void AMoreSpecificStatementRefinesAndBothStay()
    {
        var cfg = new AiConfig();
        var general = Say(cfg, "Gosto de strogonoff.", "c1").Single();
        var specific = Say(cfg, "Gosto de strogonoff de frango, mas não de porco.", "c2").Single();
        Assert.Equal(2, Active(cfg).Count);
        Assert.Contains(general.Id, specific.RelatedMemoryIds);
    }

    [Fact]
    public void SayingItAgainInAnotherConversationReinforces()
    {
        var cfg = new AiConfig();
        var first = Say(cfg, "Prefiro respostas em tópicos.", "c1").Single();
        Assert.Same(first, Say(cfg, "Prefiro respostas em tópicos.", "c2").Single());
        Assert.Single(Active(cfg));
        Assert.Contains("conversation:c2", first.SourceReference);
        Assert.True(first.Confidence > 85);
    }

    [Fact]
    public void LearnedKnowledgeOutranksRunEpisodes()
    {
        var cfg = new AiConfig();
        AssistantMemoryEngine.ObserveFinishedRun(cfg, "v", "alice", "r1", ["Resumos.md"], "faça resumos curtos de Resumos");
        Say(cfg, "Gosto de resumos curtos.", "c1");
        var top = AssistantMemoryEngine.Search(cfg, "v", "alice", "resumos curtos").First();
        Assert.Equal("preference", top.Memory.Kind);
    }

    [Fact]
    public void QuestionsAndOtherAgentsAreNotLearned()
    {
        var cfg = new AiConfig();
        Assert.Empty(Say(cfg, "Gosto de strogonoff?", "c1"));
        Say(cfg, "Gosto de chá.", "c1");
        Assert.Empty(AssistantMemoryEngine.Visible(cfg, "v", "bruno"));
    }
}
