using AgentRuntime;
using Lunet.Ai;

namespace Lunet.Tests;

public sealed class LunetAiRunProgressTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void StepBudget_AcceptsUserSelectableValues(int steps)
    {
        Assert.Equal(steps, LunetAiRunOptions.ValidateMaxSteps(steps));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(129)]
    [InlineData(-1)]
    public void StepBudget_RejectsInvalidValues(int steps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LunetAiRunOptions.ValidateMaxSteps(steps));
    }

    [Fact]
    public void DefaultBudget_AllowsMoreThanTwelveModelResponses()
    {
        Assert.True(LunetAiRunOptions.DefaultMaxSteps > 12);
        Assert.Equal(32, LunetAiRunOptions.DefaultMaxSteps);
    }

    [Fact]
    public void Diagnostic_ShowsToolsAndBlockReasonWithoutArgumentsOrPayload()
    {
        var agent = new AgentIdentity("lunet.code", "Lunet", "dev");
        var at = DateTimeOffset.UtcNow;
        var events = new []
        {
            new RuntimeEvent("run", 1, at, EventKind.ModelResponded, agent, "task", "scope",
                Payload: "PRIVATE_CONTENT"),
            new RuntimeEvent("run", 2, at, EventKind.ToolCalled, agent, "task", "scope",
                Tool: "lunet.files.search", Payload: "{secret: PRIVATE_CONTENT}"),
            new RuntimeEvent("run", 3, at, EventKind.ToolResult, agent, "task", "scope",
                Tool: "lunet.files.search", Result: "ok", Payload: "PRIVATE_CONTENT"),
            new RuntimeEvent("run", 4, at, EventKind.RunBlocked, agent, "task", "scope",
                Result: "StepLimit: limite de 12 passos")
        };
        var activity = LunetAiRunActivity.Summarize(events);
        Assert.Contains(activity, s => s.Contains("lunet.files.search", StringComparison.Ordinal));
        Assert.Contains(activity, s => s.Contains("StepLimit", StringComparison.Ordinal));
        Assert.DoesNotContain(activity, s => s.Contains("PRIVATE_CONTENT", StringComparison.Ordinal));
        Assert.DoesNotContain(activity, s => s.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Diagnostic_LimitsEventCount()
    {
        var agent = new AgentIdentity("lunet.code", "Lunet", "dev");
        var events = Enumerable.Range(1, 100).Select(i =>
            new RuntimeEvent("run", i, DateTimeOffset.UtcNow, EventKind.ModelResponded,
                agent, "task", "scope")).ToArray();
        Assert.Equal(20, LunetAiRunActivity.Summarize(events, 20).Count);
    }
}
