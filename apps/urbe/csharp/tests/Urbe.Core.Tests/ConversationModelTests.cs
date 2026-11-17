using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class ConversationModelTests
{
    [Fact]
    public void TheConversationRecordsTheModelTheTurnActuallyUses()
    {
        var cfg = new AiConfig { ProviderId = "openrouter", Model = "base" };
        var conv = new AiConversation { AgentId = "alice", ProviderId = "openrouter", Model = "openai/gpt-6-luna-pro" };
        cfg.AgentModels["alice"] = new AiModelChoice("openrouter", "deepseek/deepseek-v4.1-flash");
        var used = cfg.UseModelFor(conv);
        Assert.Equal("deepseek/deepseek-v4.1-flash", used.Model);
        Assert.Equal("deepseek/deepseek-v4.1-flash", conv.Model);
        Assert.Equal(cfg.ModelFor(conv), used);
    }
}
