using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class OfflineAiRemovalTests
{
    [Fact]
    public void ASavedOfflineAiProviderLeavesAndItsAgentsWaitForAChoice()
    {
        var json = """{"version":1,"agentWorkspaceVersion":1,"providerId":"device","model":"gemma4-e2b","providers":[{"id":"device","preset":"device","kind":"local"},{"id":"or","preset":"openrouter","apiKey":"k"}],"agentModels":{"geral":{"providerId":"device","model":"gemma4-e2b"},"escritor":{"providerId":"or","model":"x/y"}},"modelCache":{"device":{"at":1,"list":[]}}}""";
        var c = AssistantStore.ConfigFromJson((JsonObject)JsonNode.Parse(json)!);
        Assert.Equal(["or"], c.Providers.Select(p => p.Id));
        Assert.False(c.AgentModels.ContainsKey("geral"));
        Assert.Equal("x/y", c.AgentModels["escritor"].Model);   // the other choices stay
        Assert.Null(c.ProviderId);
        Assert.Equal("", c.Model);
        Assert.False(c.ModelCache.ContainsKey("device"));
    }
}
