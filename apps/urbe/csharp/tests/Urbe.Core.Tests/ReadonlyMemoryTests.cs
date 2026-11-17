using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>#963 case 04: read-only protects the notes, not the agent's memory; learning still asks.</summary>
public sealed class ReadonlyMemoryTests
{
    [Fact]
    public void ReadOnlyKeepsNotesClosedAndMemoryLearningAskedFor()
    {
        var agent = AssistantAgent.Agents[0] with { Tools = "all" };
        Assert.False(AssistantAgent.AllowTool(agent, "readonly", "write"));
        Assert.False(AssistantAgent.AllowTool(agent, "readonly", "destructive"));
        Assert.True(AssistantAgent.AllowTool(agent, "readonly", "memory-write"));
        Assert.True(AssistantAgent.NeedsApproval("memory-write", "readonly", runAllow: false));
    }

    [Fact]
    public void TheGuidanceNamesEachSourceAndWhatAnEmptyResultDoesNotProve()
    {
        Assert.Contains("list_conversations", AssistantAgent.SourcesGuidance);
        Assert.Contains("recall_memory", AssistantAgent.SourcesGuidance);
        Assert.Contains("review_memory sem candidatos não prova", AssistantAgent.SourcesGuidance);
        Assert.Contains("responda direto, sem buscar nas notas", AssistantAgent.SourcesGuidance);
        Assert.Contains("vale o que ele diz agora", AssistantAgent.SourcesGuidance);
        Assert.Contains("learn_memory continua disponível", AssistantAgent.ReadOnlyMemoryGuidance);
    }
}
