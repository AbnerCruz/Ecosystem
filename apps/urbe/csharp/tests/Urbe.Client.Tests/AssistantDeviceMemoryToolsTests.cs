using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>UC-87: local inference must be able to learn, subject to write permissions.</summary>
public sealed class AssistantDeviceMemoryToolsTests
{
    [Fact]
    public void OnDeviceToolsExposeStructuredMemoryLearningAlongsideRecall()
    {
        var tools = AssistantPanel.ToolsFor(new AiProviderConfig("device", "device"));
        Assert.Contains(tools, t => t.Name == "recall_memory" && t.Access == "read");
        Assert.Contains(tools, t => t.Name == "learn_memory" && t.Access == "memory-write");
        Assert.DoesNotContain(tools, t => t.Name == "forget_memory");
        Assert.DoesNotContain(tools, t => t.Name == "supersede_memory");
    }
}
